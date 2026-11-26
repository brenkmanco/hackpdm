using System.Text;


namespace HackPDM.Tests.Integration;

/// <summary>
/// "Get latest" downloads (DownloadOperations). Large batches used to be read in one RPC
/// call with a fixed 90 s timeout; when that call failed the files in it were skipped without
/// any error. Batches are now limited by size, failed files are retried one by one, and
/// anything that still cannot be downloaded is reported.
/// </summary>
public class DownloadBatchingTests
{
	private const int MB = 1024 * 1024;

	[Fact]
	public void ContentsTimeout_ScalesWithPayloadAndAttempt()
	{
		Assert.Equal( 90_000, DownloadOperations.ContentsTimeoutMs( 1024 ) );           // never below the old 90 s
		int forty = DownloadOperations.ContentsTimeoutMs( 40L * MB );
		Assert.True( forty > 200_000, $"40 MB should allow well over 90 s, got {forty} ms" );
		Assert.True( DownloadOperations.ContentsTimeoutMs( 40L * MB, attempt: 2 ) > forty );
		Assert.Equal( 30 * 60_000, DownloadOperations.ContentsTimeoutMs( 10_000L * MB ) ); // capped
	}
}

/// <summary>Downloads against the live server (seed data: root / Projects / Parts, 3 small files).</summary>
[Collection( OdooCollection.Name )]
public class GetLatestDownloadTests : IDisposable
{
	private static readonly string[] Seeded = [ "bracket.txt", "plate.txt", "readme.txt" ];
	private static readonly string[] LatestExcluded = [ "preview_image", "entry_id", "node_id", "file_modify_stamp", "attachment_id", "file_contents" ];

	private readonly string _pwa = Path.Combine( Path.GetTempPath(), $"hackpdm-getlatest-{Guid.NewGuid():N}" );
	private readonly HackPDM.Core.Configuration.ISettingsProvider? _previousSettings = HackDefaults.Instance.SettingsProvider;

	public GetLatestDownloadTests()
	{
		OdooTestServer.ConfigureClient();
		Directory.CreateDirectory( _pwa );
		HackDefaults.Instance.SettingsProvider = new InMemorySettingsProvider();
		HackDefaults.Instance.PwaPathAbsolute = _pwa;
	}

	public void Dispose()
	{
		HackDefaults.Instance.SettingsProvider = _previousSettings;
		try { Directory.Delete( _pwa, true ); } catch { }
	}

	private async Task<HpVersion[]> SeededLatestVersionsAsync()
	{
		await OdooTestServer.LoginAsync();
		var ids = await OdooRpc.SearchAsync( "hp.entry", OdooDomain.Where( "name", "in", Seeded ) );
		Assert.True( ids.Value?.Count == Seeded.Length, $"seed data missing (Odoo/ci/seed_smoke_data.py): {ids}" );
		var versions = await DownloadOperations.GetLatestVersionsAsync( ids.Value!, LatestExcluded );
		Assert.Equal( Seeded.Length, versions?.Length );
		return versions!;
	}

	/// <summary>Contents of every file written under the working folder (location-agnostic).</summary>
	private HashSet<string> WrittenContents()
		=> Directory.EnumerateFiles( _pwa, "*", SearchOption.AllDirectories ).Select( f => File.ReadAllText( f ) ).ToHashSet();

	[OdooFact]
	public async Task GetLatest_DownloadsEveryFile()
	{
		var versions = await SeededLatestVersionsAsync();

		var log = new List<string>();
		DownloadOperations.DownloadSummary summary = new();
		await DownloadOperations.DownloadAdaptiveAsync(
			versions,
			CancellationToken.None,
			summary,
			maxBatchCount: 1,
			maxConcurrency: 2,
			log: ( s, m ) => { lock( log ) log.Add( $"{s}: {m}" ); } );

		Assert.True( summary.Failed.IsEmpty, string.Join( "; ", summary.Failed ) );
		Assert.Equal( Seeded.Length, summary.Downloaded );
		var written = WrittenContents();
		Assert.All( Seeded, name => Assert.Contains( $"hackpdm smoke test file {name}\n", written ) );
		// each file lands at its own path under the working folder: root / Projects / Parts / <name>
		Assert.All( Seeded, name =>
		{
			string expected = Path.Combine( _pwa, "Projects", "Parts", name );
			Assert.True( File.Exists( expected ),
				$"{expected} missing; written: {string.Join( ", ", Directory.EnumerateFileSystemEntries( _pwa, "*", SearchOption.AllDirectories ) )}" );
			Assert.Equal( $"hackpdm smoke test file {name}\n", File.ReadAllText( expected ) );
		} );
	}

	[OdooFact]
	public async Task GetLatest_FileThatCannotBeDownloaded_IsReportedNotSkipped()
	{
		var versions = await SeededLatestVersionsAsync();
		var ghost = new HpVersion
		{
			id = 987_654_321,
			name = "ghost.txt",
			checksum = "0000000000000000000000000000000000000000",
			file_size = 10,
			windows_complete_name = @"root\Projects\Parts\ghost.txt",
		};

		var log = new List<string>();
		DownloadOperations.DownloadSummary summary = new();
		await DownloadOperations.DownloadAdaptiveAsync(
			[ .. versions, ghost ],
			CancellationToken.None,
			summary,
			maxConcurrency: 1,
			log: ( s, m ) => { lock( log ) log.Add( $"{s}: {m}" ); } );

		Assert.Equal( Seeded.Length, summary.Downloaded );
		var failure = Assert.Single( summary.Failed );
		Assert.Contains( "ghost.txt", failure );
		Assert.Contains( log, l => l.StartsWith( $"{StatusMessage.ERROR}" ) && l.Contains( "ghost.txt" ) );
		Assert.Contains( log, l => l.Contains( "Retrying" ) );

		var written = WrittenContents();
		Assert.All( Seeded, name => Assert.Contains( $"hackpdm smoke test file {name}\n", written ) );
	}
}
