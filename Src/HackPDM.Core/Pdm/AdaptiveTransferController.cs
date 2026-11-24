using System.Diagnostics;

using HackPDM.Core.Odoo.Models;

namespace HackPDM.Core.Pdm;

/// <summary>
/// Measures actual transfer throughput in Mbps strictly over active transfer windows,
/// excluding skip/checksum checks, metadata fetches, and idle gaps.
/// </summary>
public sealed class TransferRateTracker
{
	private const double RollingWindowSeconds = 8.0;
	private const double MinActiveDisplaySeconds = 0.05;

	private readonly Lock _gate = new();
	private readonly Func<long> _nowTicks;
	private readonly long _tickFrequency;
	private readonly Queue<(long ActiveTick, long Bytes)> _samples = [];

	private long _activeStartTick;
	private long _accumulatedActiveTicks;
	private long _windowBytes;
	private long _totalBytes;
	private bool _active;

	public TransferRateTracker() : this( Stopwatch.GetTimestamp, Stopwatch.Frequency ) { }

	internal TransferRateTracker( Func<long> nowTicks, long tickFrequency )
	{
		_nowTicks = nowTicks;
		_tickFrequency = Math.Max( 1, tickFrequency );
	}

	public bool IsActive
	{
		get
		{
			lock( _gate )
				return _active;
		}
	}

	public long TotalBytes
	{
		get
		{
			lock( _gate )
				return _totalBytes;
		}
	}

	public void Reset()
	{
		lock( _gate )
		{
			_samples.Clear();
			_activeStartTick = 0;
			_accumulatedActiveTicks = 0;
			_windowBytes = 0;
			_totalBytes = 0;
			_active = false;
		}
	}

	public void BeginTransfer()
	{
		lock( _gate )
		{
			if( _active )
				return;
			_activeStartTick = _nowTicks();
			_active = true;
		}
	}

	public void EndTransfer()
	{
		lock( _gate )
		{
			if( !_active )
				return;
			long now = _nowTicks();
			if( now > _activeStartTick )
				_accumulatedActiveTicks += now - _activeStartTick;
			_active = false;
		}
	}

	public void AddBytes( long bytes )
	{
		if( bytes <= 0 )
			return;
		lock( _gate )
		{
			long now = _nowTicks();
			if( !_active )
			{
				_activeStartTick = now;
				_active = true;
			}
			long activeTick = ActiveTicksAt( now );
			_totalBytes += bytes;
			_samples.Enqueue( ( activeTick, bytes ) );
			_windowBytes += bytes;
			PruneSamples( activeTick );
		}
	}

	public double CurrentMbps
	{
		get
		{
			lock( _gate )
			{
				if( _totalBytes <= 0 )
					return 0;

				long activeTicks = ActiveTicksAt( _nowTicks() );
				if( _active )
				{
					PruneSamples( activeTicks );
					long windowTicks = (long)( RollingWindowSeconds * _tickFrequency );
					if( activeTicks >= windowTicks )
						return _windowBytes > 0 ? BytesPerSecondToMbps( _windowBytes / RollingWindowSeconds ) : 0;

					double activeSeconds = (double)activeTicks / _tickFrequency;
					return activeSeconds < MinActiveDisplaySeconds ? 0 : BytesPerSecondToMbps( _totalBytes / activeSeconds );
				}

				double totalSeconds = (double)_accumulatedActiveTicks / _tickFrequency;
				return totalSeconds <= 0 ? 0 : BytesPerSecondToMbps( _totalBytes / totalSeconds );
			}
		}
	}

	private long ActiveTicksAt( long now )
		=> _accumulatedActiveTicks + ( _active && now > _activeStartTick ? now - _activeStartTick : 0 );

	private void PruneSamples( long activeTick )
	{
		long cutoff = activeTick - (long)( RollingWindowSeconds * _tickFrequency );
		while( _samples.Count > 0 && _samples.Peek().ActiveTick < cutoff )
			_windowBytes -= _samples.Dequeue().Bytes;
	}

	public static double BytesPerSecondToMbps( double bytesPerSecond )
		=> bytesPerSecond * 8.0 / 1_000_000.0;

	public static string FormatMbps( double mbps ) => mbps switch
	{
		<= 0 => string.Empty,
		< 0.1 => "<0.1 Mbps",
		< 10 => $"{mbps:F2} Mbps",
		< 100 => $"{mbps:F1} Mbps",
		_ => $"{mbps:F0} Mbps",
	};
}

/// <summary>
/// Dynamically tunes batch byte budgets, file count caps, and large-file chunk sizes toward a
/// target RPC duration (~2 seconds) based on live per-RPC throughput and available RAM.
/// </summary>
public sealed class AdaptiveTransferController
{
	public const double TargetRpcSeconds = 2.0;
	public const int MaxAdaptiveBatchCount = 50;
	public const int MinAdaptiveBatchCount = 5;
	public const long InitialBatchBytes = 5L * 1024 * 1024;
	public const long MinBatchBytes = 512L * 1024;
	public const long MaxAdaptiveBatchBytes = 16L * 1024 * 1024;
	public const int MinChunkBytes = 512 * 1024;
	public const int MaxServerChunkBytes = 8 * 1024 * 1024;

	private const long MinBandwidthSampleBytes = 64L * 1024;
	private const double EwmaAlpha = 0.35;
	private const double MaxStepUpFactor = 1.6;
	private const double MaxStepDownFactor = 0.5;

	private readonly Lock _gate = new();
	private readonly int _maxCountCeiling;
	private readonly int _minCountFloor;
	private readonly long _maxBytesCeiling;
	private readonly Func<long> _effectiveRamBatchBytes;

	private long _currentBatchBytes;
	private int _currentBatchCount;
	private double _ewmaBytesPerSecond;
	private double _ewmaFilesPerSecond;

	public AdaptiveTransferController(
		int maxBatchCount = MaxAdaptiveBatchCount,
		long? maxBatchBytesCeiling = null,
		Func<long>? effectiveRamBatchBytes = null )
	{
		_maxCountCeiling = Math.Clamp( maxBatchCount <= 0 ? MaxAdaptiveBatchCount : maxBatchCount, 1, MaxAdaptiveBatchCount );
		_minCountFloor = Math.Min( MinAdaptiveBatchCount, _maxCountCeiling );
		_maxBytesCeiling = maxBatchBytesCeiling is > 0
			? Math.Clamp( maxBatchBytesCeiling.Value, MinBatchBytes, MaxAdaptiveBatchBytes )
			: MaxAdaptiveBatchBytes;
		_effectiveRamBatchBytes = effectiveRamBatchBytes ?? ( () => DownloadOperations.EffectiveBatchBytes( _maxBytesCeiling ) );

		_currentBatchCount = _maxCountCeiling;
		_currentBatchBytes = Math.Clamp( InitialBatchBytes, MinBatchBytes, CurrentMaxBytesCap() );
	}

	public long CurrentBatchBytes
	{
		get
		{
			long cap = CurrentMaxBytesCap();
			lock( _gate )
				return Math.Clamp( _currentBatchBytes, MinBatchBytes, cap );
		}
	}

	public int CurrentBatchCount
	{
		get
		{
			lock( _gate )
				return _currentBatchCount;
		}
	}

	public double EstimatedRpcBytesPerSecond
	{
		get
		{
			lock( _gate )
				return _ewmaBytesPerSecond;
		}
	}

	public int GetChunkBytes( int? explicitChunkLimit = null )
	{
		long ramCap = Math.Min( MaxServerChunkBytes, CurrentMaxBytesCap() );
		int ceiling = (int)Math.Max( MinChunkBytes, ramCap );
		int target;
		lock( _gate )
			target = (int)Math.Clamp( _currentBatchBytes, MinChunkBytes, ceiling );

		if( explicitChunkLimit is int limit and > 0 )
			return Math.Min( target, limit );
		return target;
	}

	public void RecordRpcSample( long bytes, int fileCount, TimeSpan elapsed )
	{
		if( bytes <= 0 && fileCount <= 0 )
			return;
		double seconds = Math.Max( elapsed.TotalSeconds, 0.01 );
		long maxBytesCap = CurrentMaxBytesCap();

		lock( _gate )
		{
			bool countDominated = fileCount >= _minCountFloor
				&& fileCount >= Math.Max( 1, _currentBatchCount - 2 )
				&& bytes < _currentBatchBytes / 2;

			if( countDominated )
			{
				double sampleFps = fileCount / seconds;
				_ewmaFilesPerSecond = _ewmaFilesPerSecond <= 0
					? sampleFps
					: _ewmaFilesPerSecond * ( 1.0 - EwmaAlpha ) + sampleFps * EwmaAlpha;
				int desiredCount = (int)Math.Round( _ewmaFilesPerSecond * TargetRpcSeconds );
				int minStep = Math.Max( _minCountFloor, (int)Math.Floor( _currentBatchCount * MaxStepDownFactor ) );
				int maxStep = Math.Min( _maxCountCeiling, Math.Max( _currentBatchCount + 1, (int)Math.Ceiling( _currentBatchCount * MaxStepUpFactor ) ) );
				_currentBatchCount = Math.Clamp( desiredCount, minStep, maxStep );
				return;
			}

			if( bytes < MinBandwidthSampleBytes )
				return;

			double sampleBps = bytes / seconds;
			_ewmaBytesPerSecond = _ewmaBytesPerSecond <= 0
				? sampleBps
				: _ewmaBytesPerSecond * ( 1.0 - EwmaAlpha ) + sampleBps * EwmaAlpha;

			long desiredBytes = (long)Math.Round( _ewmaBytesPerSecond * TargetRpcSeconds );
			long minStepBytes = Math.Max( MinBatchBytes, (long)( _currentBatchBytes * MaxStepDownFactor ) );
			long maxStepBytes = Math.Min( maxBytesCap, (long)( _currentBatchBytes * MaxStepUpFactor ) );
			_currentBatchBytes = Math.Clamp( desiredBytes, Math.Min( minStepBytes, maxBytesCap ), Math.Max( MinBatchBytes, maxStepBytes ) );
		}
	}

	private long CurrentMaxBytesCap()
		=> Math.Clamp( _effectiveRamBatchBytes(), MinBatchBytes, _maxBytesCeiling );
}

/// <summary>
/// Interleaves large (>4 MiB) files (largest-first) with medium and small inline batches,
/// slicing each inline batch on demand using the controller's live adaptive limits.
/// </summary>
internal sealed class InterleavedDownloadQueue
{
	private readonly Lock _gate = new();
	private readonly List<HpVersion> _largeDescending = [];
	private readonly List<HpVersion> _inlineAscending = [];
	private int _largeIndex;
	private int _inlineSmallIndex;
	private int _inlineLargeIndex;
	private int _turn;

	public InterleavedDownloadQueue( IEnumerable<HpVersion> versions )
	{
		foreach( HpVersion v in versions )
		{
			if( ContentTransfer.IsLarge( v.file_size ) )
				_largeDescending.Add( v );
			else
				_inlineAscending.Add( v );
		}
		_largeDescending.Sort( static ( a, b ) => ( b.file_size ?? 0 ).CompareTo( a.file_size ?? 0 ) );
		_inlineAscending.Sort( static ( a, b ) => ( a.file_size ?? 0 ).CompareTo( b.file_size ?? 0 ) );
		_inlineLargeIndex = _inlineAscending.Count - 1;
	}

	public int LargeCount => _largeDescending.Count;
	public int InlineCount => _inlineAscending.Count;

	public bool TryDequeue( AdaptiveTransferController controller, out List<HpVersion> batch, out bool isLarge )
	{
		int maxCount = controller.CurrentBatchCount;
		long byteBudget = controller.CurrentBatchBytes;

		lock( _gate )
		{
			for( int attempt = 0; attempt < 3; attempt++ )
			{
				int slot = _turn;
				_turn = ( _turn + 1 ) % 3;
				if( slot == 0 && _largeIndex < _largeDescending.Count )
				{
					batch = [ _largeDescending[ _largeIndex++ ] ];
					isLarge = true;
					return true;
				}
				if( slot == 1 && _inlineSmallIndex <= _inlineLargeIndex )
				{
					batch = TakeFromLargeEndLocked( maxCount, byteBudget );
					isLarge = false;
					return true;
				}
				if( slot == 2 && _inlineSmallIndex <= _inlineLargeIndex )
				{
					batch = TakeFromSmallEndLocked( maxCount, byteBudget );
					isLarge = false;
					return true;
				}
			}

			batch = [];
			isLarge = false;
			return false;
		}
	}

	private List<HpVersion> TakeFromLargeEndLocked( int maxCount, long byteBudget )
	{
		List<HpVersion> batch = [];
		long bytes = 0;
		while( _inlineSmallIndex <= _inlineLargeIndex && batch.Count < maxCount )
		{
			HpVersion next = _inlineAscending[ _inlineLargeIndex ];
			long size = Math.Max( 0, (long)( next.file_size ?? 0 ) );
			if( batch.Count > 0 && bytes + size > byteBudget )
				break;
			batch.Add( next );
			bytes += size;
			_inlineLargeIndex--;
		}
		return batch;
	}

	private List<HpVersion> TakeFromSmallEndLocked( int maxCount, long byteBudget )
	{
		List<HpVersion> batch = [];
		long bytes = 0;
		while( _inlineSmallIndex <= _inlineLargeIndex && batch.Count < maxCount )
		{
			HpVersion next = _inlineAscending[ _inlineSmallIndex ];
			long size = Math.Max( 0, (long)( next.file_size ?? 0 ) );
			if( batch.Count > 0 && bytes + size > byteBudget )
				break;
			batch.Add( next );
			bytes += size;
			_inlineSmallIndex++;
		}
		return batch;
	}
}
