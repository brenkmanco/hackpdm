using System.Collections;
using System.Text;

using HackPDM.Core.Hack;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Infrastructure.Operations;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Tests.Integration;

/// <summary>
/// "Get latest" downloads (PdmDownloadOperations). Large batches used to be read in one RPC
/// call with a fixed 90 s timeout; when that call failed the files in it were skipped without
/// any error. Batches are now limited by size, failed files are retried one by one, and
/// anything that still cannot be downloaded is reported.
/// </summary>
public class DownloadBatchingTests
{
	private static HpVersion V( int id, int size ) => new() { id = id, name = $"f{id}", file_size = size };
	private const int MB = 1024 * 1024;

	[Fact]
	public void BatchBySize_RespectsFileCount()
	{
		var batches = PdmDownloadOperations.BatchBySize( Enumerable.Range( 1, 12 ).Select( i => V( i, 10 ) ), maxCount: 5, maxBytes: long.MaxValue );
		Assert.Equal( [ 5, 5, 2 ], batches.Select( b => b.Count ) );
	}

	[Fact]
	public void BatchBySize_RespectsByteBudget_AndLargeFilesGoAlone()
	{
		int[] sizesMb = [ 40, 30, 25, 20, 15, 10, 5, 3, 2, 1, 1, 2 ];
		var versions = sizesMb.Select( ( mb, i ) => V( i + 1, mb * MB ) ).ToList();

		var batches = PdmDownloadOperations.BatchBySize( versions, maxCount: 5, maxBytes: 16L * MB );

		Assert.Equal( versions.Count, batches.Sum( b => b.Count ) );
		Assert.All( batches, b => Assert.InRange( b.Count, 1, 5 ) );
		// a batch only exceeds the budget when it is a single file larger than the budget
		Assert.All( batches.Where( b => b.Count > 1 ), b => Assert.True( b.Sum( v => (long)v.file_size! ) <= 16L * MB ) );
		Assert.All( versions.Where( v => v.file_size > 16 * MB ), big => Assert.Contains( batches, b => b.Count == 1 && b[ 0 ] == big ) );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( -1 )]
	public void BatchBySize_NonPositiveCount_FallsBackToDefault( int maxCount )
	{
		// a max_batch_size setting of 0 used to reach Help.BatchArray and divide by zero
		var batches = PdmDownloadOperations.BatchBySize( Enumerable.Range( 1, 7 ).Select( i => V( i, 1 ) ), maxCount, long.MaxValue );
		Assert.Equal( [ 5, 2 ], batches.Select( b => b.Count ) );
	}

	[Fact]
	public void BatchBySize_Empty()
	{
		Assert.Empty( PdmDownloadOperations.BatchBySize( [], 5 ) );
		Assert.Empty( PdmDownloadOperations.BatchBySize( null, 5 ) );
	}

	[Fact]
	public void ContentsTimeout_ScalesWithPayloadAndAttempt()
	{
		Assert.Equal( 90_000, PdmDownloadOperations.ContentsTimeoutMs( 1024 ) );           // never below the old 90 s
		int forty = PdmDownloadOperations.ContentsTimeoutMs( 40L * MB );
		Assert.True( forty > 200_000, $"40 MB should allow well over 90 s, got {forty} ms" );
		Assert.True( PdmDownloadOperations.ContentsTimeoutMs( 40L * MB, attempt: 2 ) > forty );
		Assert.Equal( 30 * 60_000, PdmDownloadOperations.ContentsTimeoutMs( 10_000L * MB ) ); // capped
	}
}

/// <summary>Downloads against the live server (seed data: root / Projects / Parts, 3 small files).</summary>
[Collection( OdooCollection.Name )]
public class GetLatestDownloadTests : IDisposable
{
	private static readonly string[] Seeded = [ "bracket.txt", "plate.txt", "readme.txt" ];
	private static readonly string[] LatestExcluded = [ "preview_image", "entry_id", "node_id", "file_modify_stamp", "attachment_id", "file_contents" ];

	private readonly string _pwa = Path.Combine( Path.GetTempPath(), $"hackpdm-getlatest-{Guid.NewGuid():N}" );
	private readonly HackPDM.Abstractions.ISettingsProvider? _previousSettings = HackDefaults.Instance.SettingsProvider;

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
		ArrayList ids = await OdooClient.SearchAsync( "hp.entry", [ new ArrayList { new ArrayList { "name", "in", new ArrayList( Seeded ) } } ] );
		Assert.True( ids.Count == Seeded.Length, $"seed data missing (Odoo/ci/seed_smoke_data.py): {OdooClient.LatestException}" );
		var versions = await PdmDownloadOperations.GetLatestVersionsAsync( ids, LatestExcluded );
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
		// a tiny byte budget forces one file per batch, exercising batching end to end
		var batches = PdmDownloadOperations.BatchBySize( versions, maxCount: 5, maxBytes: 1 );
		Assert.Equal( Seeded.Length, batches.Count );

		var log = new List<string>();
		var summary = await PdmDownloadOperations.ProcessDownloadsAsync( batches, CancellationToken.None, 2,
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
		// one batch with real files and one that does not exist: the batch read fails as a whole,
		// the real files must still arrive via the per-file retry, the ghost must be reported
		List<List<HpVersion>> batches = [ [ .. versions, ghost ] ];

		var log = new List<string>();
		var summary = await PdmDownloadOperations.ProcessDownloadsAsync( batches, CancellationToken.None, 1,
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
