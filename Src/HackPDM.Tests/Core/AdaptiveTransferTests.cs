using HackPDM.Core.Odoo.Models;
using HackPDM.Core.Pdm;

namespace HackPDM.Tests.Core;

public sealed class AdaptiveTransferTests
{
	private const long KiB = 1024;
	private const long MiB = 1024 * 1024;

	private static HpVersion Version( int id, long size ) => new()
	{
		id = id,
		name = $"file_{id}.sldprt",
		file_size = (int)size,
	};

	[Fact]
	public void Controller_StartsAtAtMost50FilesAnd5MiB()
	{
		AdaptiveTransferController controller = new(
			maxBatchCount: 100,
			effectiveRamBatchBytes: () => 16 * MiB );

		Assert.Equal( 50, controller.CurrentBatchCount );
		Assert.Equal( 5 * MiB, controller.CurrentBatchBytes );
		Assert.Equal( 5 * MiB, controller.GetChunkBytes() );
	}

	[Fact]
	public void Controller_ScalesUpOnFastRpcs_AndCapsChunksAtServerLimit()
	{
		AdaptiveTransferController controller = new(
			maxBatchCount: 50,
			effectiveRamBatchBytes: () => 16 * MiB );

		// 5 MiB completing in 0.25s (~20 MiB/s) should ramp both batch bytes and chunk bytes up toward 2s target.
		for( int i = 0; i < 4; i++ )
			controller.RecordRpcSample( 5 * MiB, fileCount: 3, TimeSpan.FromMilliseconds( 250 ) );

		Assert.Equal( 16 * MiB, controller.CurrentBatchBytes );
		Assert.Equal( AdaptiveTransferController.MaxServerChunkBytes, controller.GetChunkBytes() );
		// Explicit lower chunk limit (e.g. test override) is always respected.
		Assert.Equal( 100 * 1024, controller.GetChunkBytes( explicitChunkLimit: 100 * 1024 ) );
	}

	[Fact]
	public void Controller_ScalesDownBothBatchesAndChunksOnSlowRpcs()
	{
		AdaptiveTransferController controller = new(
			maxBatchCount: 50,
			effectiveRamBatchBytes: () => 16 * MiB );

		// 5 MiB taking 10s (~512 KiB/s -> 2s target = 1 MiB) drives both batch and chunk size down together.
		for( int i = 0; i < 4; i++ )
			controller.RecordRpcSample( 5 * MiB, fileCount: 2, TimeSpan.FromSeconds( 10 ) );

		Assert.InRange( controller.CurrentBatchBytes, 800 * KiB, 1400 * KiB );
		Assert.Equal( controller.CurrentBatchBytes, controller.GetChunkBytes() );
	}

	[Fact]
	public void Controller_SmallFileCountBoundSlowdown_ReducesBatchCountWithoutPoisoningChunkSize()
	{
		AdaptiveTransferController controller = new(
			maxBatchCount: 50,
			effectiveRamBatchBytes: () => 16 * MiB );

		// Seed healthy bandwidth with one byte-bound RPC (~4 MiB/s -> 8 MiB target).
		controller.RecordRpcSample( 5 * MiB, fileCount: 2, TimeSpan.FromSeconds( 1.25 ) );
		long bytesBefore = controller.CurrentBatchBytes;
		Assert.True( bytesBefore > 5 * MiB );

		// Now 50 tiny 10 KB files take 6s due to ORM overhead -> should lower count cap, not chunk byte budget.
		controller.RecordRpcSample( 50 * 10 * KiB, fileCount: 50, TimeSpan.FromSeconds( 6 ) );

		Assert.True( controller.CurrentBatchCount < 50 );
		Assert.Equal( bytesBefore, controller.CurrentBatchBytes );
	}

	[Fact]
	public void Controller_RespectsLiveRamPressureCeiling()
	{
		long simulatedRamCap = 16 * MiB;
		AdaptiveTransferController controller = new(
			maxBatchCount: 50,
			effectiveRamBatchBytes: () => simulatedRamCap );

		for( int i = 0; i < 4; i++ )
			controller.RecordRpcSample( 5 * MiB, fileCount: 2, TimeSpan.FromMilliseconds( 200 ) );
		Assert.Equal( 16 * MiB, controller.CurrentBatchBytes );

		// RAM tightens mid-download -> immediate cap on both inline batches and chunks.
		simulatedRamCap = 2 * MiB;
		Assert.Equal( 2 * MiB, controller.CurrentBatchBytes );
		Assert.Equal( 2 * MiB, controller.GetChunkBytes() );
	}

	[Fact]
	public void InterleavedQueue_InterleavesLargeMediumAndSmallFiles_AndAdaptsMidRun()
	{
		List<HpVersion> versions =
		[
			Version( 1, 20 * MiB ), // large
			Version( 2, 10 * MiB ), // large
			Version( 3, 3 * MiB ),  // medium inline
			Version( 4, 2 * MiB ),  // medium inline
			Version( 5, 1500 * KiB ), // medium inline
			Version( 6, 50 * KiB ), // small inline
			Version( 7, 50 * KiB ), // small inline
			Version( 8, 50 * KiB ), // small inline
		];

		AdaptiveTransferController controller = new( maxBatchCount: 50, effectiveRamBatchBytes: () => 16 * MiB );
		InterleavedDownloadQueue queue = new( versions );

		// Slot 0: largest >4 MiB file alone
		Assert.True( queue.TryDequeue( controller, out var b1, out bool large1 ) );
		Assert.True( large1 );
		Assert.Equal( [ 1 ], b1.Select( v => v.id!.Value ) );

		// Slot 1: medium inline end packed under initial 5 MiB cap -> 3 MiB + 2 MiB = 5 MiB
		Assert.True( queue.TryDequeue( controller, out var b2, out bool large2 ) );
		Assert.False( large2 );
		Assert.Equal( [ 3, 4 ], b2.Select( v => v.id!.Value ) );

		// Slot 2: small inline end packs remaining small files together under 5 MiB cap
		Assert.True( queue.TryDequeue( controller, out var b3, out bool large3 ) );
		Assert.False( large3 );
		Assert.Contains( 6, b3.Select( v => v.id!.Value ) );
		Assert.Contains( 7, b3.Select( v => v.id!.Value ) );
		Assert.Contains( 8, b3.Select( v => v.id!.Value ) );

		// Next slot 0: second large file
		Assert.True( queue.TryDequeue( controller, out var b4, out bool large4 ) );
		Assert.True( large4 );
		Assert.Equal( [ 2 ], b4.Select( v => v.id!.Value ) );

		// Exhaust remaining and ensure every version was dispatched exactly once.
		HashSet<int> dispatched = [ .. b1.Concat( b2 ).Concat( b3 ).Concat( b4 ).Select( v => v.id!.Value ) ];
		while( queue.TryDequeue( controller, out var extra, out _ ) )
		{
			foreach( HpVersion v in extra )
				Assert.True( dispatched.Add( v.id!.Value ) );
		}
		Assert.Equal( versions.Count, dispatched.Count );
	}

	[Fact]
	public void TransferRateTracker_ExcludesSkipCheckDurationAndLocksFinalMbps()
	{
		const long freq = 1000; // 1 tick = 1 ms
		long clockMs = 0;
		TransferRateTracker tracker = new( () => clockMs, freq );

		// 15 seconds spent finding dependencies & checking local files for skips -> NOT counted!
		clockMs = 15_000;
		Assert.Equal( 0, tracker.CurrentMbps );

		// Download phase begins at t = 15s
		tracker.BeginTransfer();
		clockMs = 17_000; // 2.0s elapsed loading 5,000,000 bytes (= 40,000,000 bits -> 20.0 Mbps)
		tracker.AddBytes( 5_000_000 );
		Assert.Equal( 20.0, tracker.CurrentMbps, precision: 2 );

		// Another 2.0s loading 10,000,000 bytes (cumulative 15,000,000 bytes in 4.0s -> 30.0 Mbps)
		clockMs = 19_000;
		tracker.AddBytes( 10_000_000 );
		Assert.Equal( 30.0, tracker.CurrentMbps, precision: 2 );

		// Transfer ends; waiting 30s afterward must NOT decay the final achieved Mbps.
		tracker.EndTransfer();
		clockMs = 49_000;
		Assert.Equal( 30.0, tracker.CurrentMbps, precision: 2 );
	}
}
