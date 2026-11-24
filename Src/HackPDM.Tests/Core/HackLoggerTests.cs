using HackPDM.Tests.Integration;

namespace HackPDM.Tests.Core;

public sealed class HackLoggerTests : IDisposable
{
	private readonly string _tempDir = Path.Combine( Path.GetTempPath(), $"hackpdm-log-test-{Guid.NewGuid():N}" );

	public HackLoggerTests()
	{
		Directory.CreateDirectory( _tempDir );
		HackLogger.Configure( _tempDir, HackLogLevel.Debug, maxFileBytes: 4096, maxRetainedFiles: 3 );
	}

	public void Dispose()
	{
		HackLogger.ResetToDefaults();
		try { Directory.Delete( _tempDir, recursive: true ); } catch { }
	}

	[Fact]
	public void LogRedactor_RedactsPasswordsTokensCookiesAndBase64Payloads()
	{
		string fakeBase64 = new( 'A', 300 );
		string urlSafeBase64 = new( '-', 260 );
		string multilineBase64 = string.Join( "\n", Enumerable.Repeat( new string( 'C', 76 ), 4 ) ) + "==";
		string raw = $"Login failed: {{\"password\": \"SuperSecret!123\", \"api_key\": \"ak_live_999\", \"swdocmgr_key\": \"SWK_999\", \"SwLicenseKey\": \"LIC_1\", \"pwd\": \"P4ss\"}} url=https://alice:urlSecret99@odoo.example.com:8069/xmlrpc Authorization: Bearer eyJhbGciOiJIUzI1NiJ9 cookie=sess_abc123 body={fakeBase64} urlb64={urlSafeBase64} pem={multilineBase64}";

		string redacted = LogRedactor.Redact( raw );

		Assert.DoesNotContain( "SuperSecret!123", redacted );
		Assert.DoesNotContain( "ak_live_999", redacted );
		Assert.DoesNotContain( "SWK_999", redacted );
		Assert.DoesNotContain( "LIC_1", redacted );
		Assert.DoesNotContain( "P4ss", redacted );
		Assert.DoesNotContain( "urlSecret99", redacted );
		Assert.DoesNotContain( "alice:urlSecret99", redacted );
		Assert.DoesNotContain( "eyJhbGciOiJIUzI1NiJ9", redacted );
		Assert.DoesNotContain( "sess_abc123", redacted );
		Assert.DoesNotContain( fakeBase64, redacted );
		Assert.DoesNotContain( urlSafeBase64, redacted );
		Assert.DoesNotContain( new string( 'C', 76 ), redacted );
		Assert.Contains( "https://[REDACTED]@odoo.example.com:8069/xmlrpc", redacted );
		Assert.Contains( "[REDACTED]", redacted );
		Assert.Contains( "[REDACTED_BASE64:300chars]", redacted );
		Assert.Contains( "[REDACTED_BASE64:260chars]", redacted );
	}

	[Fact]
	public void BuildDiagnosticSummary_RedactsUrlCredentials()
	{
		string summary = HackLogger.BuildDiagnosticSummary(
			clientVersion: "1.2.3",
			odooUrl: "https://svc:MyTopSecret@pdm.company.org:8069",
			odooDb: "prod",
			credentialStore: "Windows Credential Manager" );

		Assert.DoesNotContain( "MyTopSecret", summary );
		Assert.Contains( "https://[REDACTED]@pdm.company.org:8069", summary );
	}

	[Fact]
	public void HackLogger_WritesRedactedRollingLogsAndPrunesOldFiles()
	{
		// Write enough lines to force multiple rotations under the 4 KiB test cap
		for( int i = 0; i < 120; i++ )
			HackLogger.Info( "TestCat", $"Entry {i}: password=MySecretPassword{i} payload={new string( 'B', 260 )}" );

		string[] logFiles = Directory.GetFiles( _tempDir, "hackpdm-*.log" );
		Assert.NotEmpty( logFiles );
		Assert.True( logFiles.Length <= 3, $"Expected <= maxRetainedFiles (3), got {logFiles.Length}" );

		string combined = string.Concat( logFiles.Select( File.ReadAllText ) );
		Assert.DoesNotContain( "MySecretPassword", combined );
		Assert.Contains( "[REDACTED]", combined );
		Assert.Contains( "[REDACTED_BASE64:260chars]", combined );
	}

	[Fact]
	public void PruneOldLogs_BreaksEqualTimestampTiesNumerically_AndKeepsNewestTenPlusActive()
	{
		string day = DateTimeOffset.UtcNow.ToString( "yyyyMMdd" );
		DateTime stamp = new( 2026, 11, 24, 12, 0, 0, DateTimeKind.Utc );
		for( int n = 1; n <= 11; n++ )
		{
			string p = Path.Combine( _tempDir, $"hackpdm-{day}.{n}.log" );
			File.WriteAllText( p, $"rot-{n}" );
			File.SetLastWriteTimeUtc( p, stamp );
		}

		HackLogger.Configure( _tempDir, HackLogLevel.Information, maxFileBytes: 1024 * 1024, maxRetainedFiles: 3 );
		HackLogger.Info( "Tie", "active-line" );

		string[] remaining = Directory.GetFiles( _tempDir, "hackpdm-*.log" )
			.Select( Path.GetFileName )
			.OrderBy( n => n, StringComparer.Ordinal )
			.ToArray()!;
		Assert.Equal( 3, remaining.Length );
		Assert.Contains( $"hackpdm-{day}.log", remaining );
		Assert.Contains( $"hackpdm-{day}.11.log", remaining );
		Assert.Contains( $"hackpdm-{day}.10.log", remaining );
	}

	[Fact]
	public void WriteCrashDump_CreatesRedactedCrashFileWithDiagnostics_AndDoesNotOverwriteBackToBack()
	{
		Exception ex1 = new InvalidOperationException( "RPC failed with password=TopSecretPass42" );
		Exception ex2 = new InvalidOperationException( "Second crash with pwd=SecondSecret99" );
		string? dump1 = HackLogger.WriteCrashDump( "TestCrash1", ex1, appVersion: "1.1.0", context: "Page=FileManager" );
		string? dump2 = HackLogger.WriteCrashDump( "TestCrash2", ex2, appVersion: "1.1.0", context: "Page=Graph" );

		Assert.NotNull( dump1 );
		Assert.NotNull( dump2 );
		Assert.NotEqual( dump1, dump2 );
		Assert.True( File.Exists( dump1 ) );
		Assert.True( File.Exists( dump2 ) );

		string text1 = File.ReadAllText( dump1! );
		Assert.Contains( "ClientVersion: 1.1.0", text1 );
		Assert.Contains( "Page=FileManager", text1 );
		Assert.DoesNotContain( "TopSecretPass42", text1 );
		Assert.Contains( "[REDACTED]", text1 );

		// Pruning also keeps crash dumps bounded to maxRetainedFiles (3)
		for( int i = 0; i < 4; i++ )
			_ = HackLogger.WriteCrashDump( $"Crash{i}", ex1 );
		Assert.True( Directory.GetFiles( _tempDir, "crash-*.log" ).Length <= 3 );
	}

	[Fact]
	public async Task LocalFileWriter_AtomicWriteLeavesNoTempFilesBehind()
	{
		var prevSettings = HackDefaults.Instance.SettingsProvider;
		string prevPwa = HackDefaults.Instance.PwaPathAbsolute;
		try
		{
			HackDefaults.Instance.SettingsProvider = new InMemorySettingsProvider();
			HackDefaults.Instance.PwaPathAbsolute = _tempDir;
			HackFile file = new()
			{
				Name = "bracket.txt",
				DirectoryName = "Sub",
				FileContents = "initial content"u8.ToArray(),
				Owner = true,
			};

			bool ok = await LocalFileWriter.WriteAsync( file );
			Assert.True( ok );
			string target = Path.Combine( _tempDir, "Sub", "bracket.txt" );
			Assert.Equal( "initial content", File.ReadAllText( target ) );
			Assert.Empty( Directory.GetFiles( Path.Combine( _tempDir, "Sub" ), "*.tmp" ) );

			// Overwrite atomically
			file.FileContents = "updated content"u8.ToArray();
			Assert.True( await LocalFileWriter.WriteAsync( file ) );
			Assert.Equal( "updated content", File.ReadAllText( target ) );
			Assert.Empty( Directory.GetFiles( Path.Combine( _tempDir, "Sub" ), "*.tmp" ) );
		}
		finally
		{
			HackDefaults.Instance.SettingsProvider = prevSettings;
			HackDefaults.Instance.PwaPathAbsolute = prevPwa;
		}
	}
}
