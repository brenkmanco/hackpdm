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
		string raw = $"Login failed: {{\"password\": \"SuperSecret!123\", \"api_key\": \"ak_live_999\"}} Authorization: Bearer eyJhbGciOiJIUzI1NiJ9 cookie=sess_abc123 body={fakeBase64}";

		string redacted = LogRedactor.Redact( raw );

		Assert.DoesNotContain( "SuperSecret!123", redacted );
		Assert.DoesNotContain( "ak_live_999", redacted );
		Assert.DoesNotContain( "eyJhbGciOiJIUzI1NiJ9", redacted );
		Assert.DoesNotContain( "sess_abc123", redacted );
		Assert.DoesNotContain( fakeBase64, redacted );
		Assert.Contains( "[REDACTED]", redacted );
		Assert.Contains( "[REDACTED_BASE64:300chars]", redacted );
	}

	[Fact]
	public void HackLogger_WritesRedactedRollingLogsAndPrunesOldFiles()
	{
		// Write enough lines to force multiple rotations under the 4 KiB test cap
		for( int i = 0; i < 120; i++ )
			HackLogger.Info( "TestCat", $"Entry {i}: password=MySecretPassword{i} payload={new string( 'B', 260 )}" );

		string[] logFiles = Directory.GetFiles( _tempDir, "hackpdm-*.log" );
		Assert.NotEmpty( logFiles );
		Assert.True( logFiles.Length <= 4, $"Expected bounded rotated log count, got {logFiles.Length}" );

		string combined = string.Concat( logFiles.Select( File.ReadAllText ) );
		Assert.DoesNotContain( "MySecretPassword", combined );
		Assert.Contains( "[REDACTED]", combined );
		Assert.Contains( "[REDACTED_BASE64:260chars]", combined );
	}

	[Fact]
	public void WriteCrashDump_CreatesRedactedCrashFileWithDiagnostics()
	{
		Exception ex = new InvalidOperationException( "RPC failed with password=TopSecretPass42" );
		string? dumpPath = HackLogger.WriteCrashDump( "TestCrash", ex, appVersion: "1.1.0", context: "Page=FileManager" );

		Assert.NotNull( dumpPath );
		Assert.True( File.Exists( dumpPath ) );
		string text = File.ReadAllText( dumpPath! );
		Assert.Contains( "ClientVersion: 1.1.0", text );
		Assert.Contains( "Page=FileManager", text );
		Assert.DoesNotContain( "TopSecretPass42", text );
		Assert.Contains( "[REDACTED]", text );
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
