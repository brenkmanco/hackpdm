using System.Text.Json;
using System.Text.Json.Serialization;

using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo.Models;

namespace HackPDM.Core.Pdm;

/// <summary>Download workflow kinds eligible for checkpointed restart resume.</summary>
public static class DownloadResumeKind
{
	public const string GetLatest = nameof( GetLatest );
	public const string CheckoutLatest = nameof( CheckoutLatest );
	public const string DownloadVersions = nameof( DownloadVersions );
}

/// <summary>Persisted download progress snapshot allowing interrupted downloads to resume across restarts.</summary>
public sealed class DownloadResumeState
{
	public int SchemaVersion { get; set; } = 1;
	public string AccountKey { get; set; } = string.Empty;
	public string VaultPath { get; set; } = string.Empty;
	public string OperationKind { get; set; } = DownloadResumeKind.GetLatest;
	public string Label { get; set; } = string.Empty;
	public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
	public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
	public int TotalCount { get; set; }
	public int CompletedCount { get; set; }
	public int SkippedCount { get; set; }
	public long DownloadedBytes { get; set; }
	public int[] RemainingVersionIds { get; set; } = [];
	public int[] CheckoutEntryIds { get; set; } = [];
	public int[] PendingEntryIds { get; set; } = [];

	[JsonIgnore]
	public bool HasRemainingWork => RemainingVersionIds.Length > 0 || PendingEntryIds.Length > 0;

	[JsonIgnore]
	public int RemainingCount => RemainingVersionIds.Length > 0 ? RemainingVersionIds.Length : PendingEntryIds.Length;

	public string FormatSummary()
	{
		string title = string.IsNullOrWhiteSpace( Label ) ? "Download" : Label;
		if( RemainingVersionIds.Length > 0 && TotalCount > 0 )
		{
			string bytesPart = DownloadedBytes > 0 ? $", {ByteSize.Format( DownloadedBytes )} downloaded" : string.Empty;
			return $"{title}: {CompletedCount}/{TotalCount} files ({RemainingVersionIds.Length} remaining{bytesPart})";
		}
		if( RemainingVersionIds.Length > 0 )
			return $"{title}: {RemainingVersionIds.Length} file(s) remaining";
		return $"{title}: {PendingEntryIds.Length} entry(s) pending resolution";
	}
}

[JsonSourceGenerationOptions( WriteIndented = false, PropertyNameCaseInsensitive = true )]
[JsonSerializable( typeof( DownloadResumeState ) )]
internal sealed partial class DownloadResumeJsonContext : JsonSerializerContext;

/// <summary>Atomically stores and clears the single active download resume checkpoint under local app data.</summary>
public sealed class DownloadResumeStore( string? filePath = null )
{
	private readonly Lock _gate = new();
	private readonly string? _customFilePath = filePath;

	public static DownloadResumeStore Default { get; } = new();

	public string FilePath => _customFilePath ?? Path.Combine( AppDataPaths.Data, "download-resume.json" );

	public static string BuildAccountKey( string? odooUrl, string? odooDb, string? odooUser, string? pwaPath )
		=> $"{odooUrl ?? ""}|{odooDb ?? ""}|{odooUser ?? ""}|{pwaPath ?? ""}";

	public DownloadResumeState? Load()
	{
		lock( _gate )
		{
			string path = FilePath;
			if( !File.Exists( path ) )
				return null;
			try
			{
				string json = File.ReadAllText( path );
				if( string.IsNullOrWhiteSpace( json ) )
					return null;
				return JsonSerializer.Deserialize( json, DownloadResumeJsonContext.Default.DownloadResumeState );
			}
			catch( Exception ex ) when( ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException )
			{
				HackLogger.Warn( nameof( DownloadResumeStore ), $"Could not read saved download resume state '{path}': {ex.Message}", ex );
				return null;
			}
		}
	}

	public DownloadResumeState? LoadForAccount( string accountKey )
	{
		if( string.IsNullOrEmpty( accountKey ) )
			return null;
		DownloadResumeState? state = Load();
		if( state is null || !state.HasRemainingWork )
			return null;
		return string.Equals( state.AccountKey, accountKey, StringComparison.OrdinalIgnoreCase ) ? state : null;
	}

	public void Save( DownloadResumeState state )
	{
		ArgumentNullException.ThrowIfNull( state );
		string json = JsonSerializer.Serialize( state, DownloadResumeJsonContext.Default.DownloadResumeState );
		lock( _gate )
		{
			string path = FilePath;
			string? dir = Path.GetDirectoryName( path );
			if( !string.IsNullOrEmpty( dir ) )
				Directory.CreateDirectory( dir );
			string temp = path + ".tmp";
			File.WriteAllText( temp, json );
			File.Move( temp, path, overwrite: true );
		}
	}

	public void Clear()
	{
		lock( _gate )
		{
			string path = FilePath;
			FileOps.TryDelete( path );
			FileOps.TryDelete( path + ".tmp" );
		}
	}
}

/// <summary>Maintains thread-safe in-memory progress and throttled atomic disk checkpoints during downloads.</summary>
internal sealed class DownloadCheckpointTracker
{
	private const long FlushIntervalMs = 2000;
	private const int FlushItemThreshold = 25;

	private readonly DownloadResumeStore _store;
	private readonly Lock _gate = new();
	private readonly Lock _writeLock = new();
	private readonly string _accountKey;
	private readonly string _vaultPath;
	private readonly string _operationKind;
	private readonly string _label;
	private readonly int[] _checkoutEntryIds;
	private readonly DateTimeOffset _createdUtc;

	private readonly HashSet<int> _remainingVersionIds = [];
	private int[] _pendingEntryIds;
	private int _totalCount;
	private int _completedCount;
	private int _skippedCount;
	private long _downloadedBytes;
	private int _dirtyItems;
	private long _lastFlushTick = Environment.TickCount64;
	private long _latestStateSeq;
	private int _asyncFlushScheduled;
	private bool _cleared;

	public DownloadCheckpointTracker(
		DownloadResumeStore store,
		bool enabled,
		string accountKey,
		string vaultPath,
		string operationKind,
		string label,
		IEnumerable<int>? pendingEntryIds = null,
		IEnumerable<int>? checkoutEntryIds = null,
		DownloadResumeState? existingState = null )
	{
		_store = store;
		Enabled = enabled;
		_accountKey = accountKey;
		_vaultPath = vaultPath;
		_operationKind = operationKind;
		_label = label;
		_checkoutEntryIds = checkoutEntryIds?.Where( static id => id > 0 ).Distinct().ToArray() ?? [];
		_pendingEntryIds = pendingEntryIds?.Where( static id => id > 0 ).Distinct().ToArray() ?? [];

		if( existingState is not null )
		{
			_createdUtc = existingState.CreatedUtc;
			_totalCount = existingState.TotalCount;
			_completedCount = existingState.CompletedCount;
			_skippedCount = existingState.SkippedCount;
			_downloadedBytes = existingState.DownloadedBytes;
			foreach( int vid in existingState.RemainingVersionIds )
			{
				if( vid > 0 )
					_remainingVersionIds.Add( vid );
			}
			if( existingState.RemainingVersionIds.Length > 0 )
				_pendingEntryIds = [];
		}
		else
		{
			_createdUtc = DateTimeOffset.UtcNow;
		}
	}

	public bool Enabled { get; }

	public void InitializePendingEntries( bool flushNow = true )
	{
		if( !Enabled )
			return;
		if( flushNow )
			FlushSync();
	}

	public void SetResolvedVersions(
		IReadOnlyList<HpVersion> toDownload,
		int totalCount,
		int skippedDelta )
	{
		if( !Enabled )
			return;

		lock( _gate )
		{
			if( _cleared )
				return;
			_remainingVersionIds.Clear();
			for( int i = 0; i < toDownload.Count; i++ )
			{
				if( toDownload[ i ].id is int id and > 0 )
					_remainingVersionIds.Add( id );
			}
			_pendingEntryIds = [];
			if( _totalCount <= 0 )
				_totalCount = totalCount;
			_skippedCount += skippedDelta;
			_completedCount = Math.Max( _completedCount, _totalCount - _remainingVersionIds.Count );
			_dirtyItems++;
		}

		FlushSync();
	}

	public void MarkVersionCompleted( int versionId, long downloadedBytes )
	{
		if( !Enabled || versionId <= 0 )
			return;

		bool shouldFlush;
		lock( _gate )
		{
			if( _cleared )
				return;
			if( _remainingVersionIds.Remove( versionId ) )
				_completedCount++;
			if( downloadedBytes > 0 )
				_downloadedBytes += downloadedBytes;
			_dirtyItems++;
			long now = Environment.TickCount64;
			shouldFlush = _dirtyItems >= FlushItemThreshold || now - _lastFlushTick >= FlushIntervalMs;
			if( shouldFlush )
			{
				_dirtyItems = 0;
				_lastFlushTick = now;
			}
		}

		if( shouldFlush )
			ScheduleAsyncFlush();
	}

	public void SaveFailedOnly( IEnumerable<int> failedVersionIds )
	{
		if( !Enabled )
			return;

		lock( _gate )
		{
			_cleared = false;
			_pendingEntryIds = [];
			_remainingVersionIds.Clear();
			foreach( int vid in failedVersionIds )
			{
				if( vid > 0 )
					_remainingVersionIds.Add( vid );
			}
			if( _totalCount > 0 )
				_completedCount = Math.Max( 0, _totalCount - _remainingVersionIds.Count );
		}

		if( HasWork() )
			FlushSync();
		else
			Clear();
	}

	public bool HasWork()
	{
		lock( _gate )
			return !_cleared && ( _remainingVersionIds.Count > 0 || _pendingEntryIds.Length > 0 );
	}

	public void FlushSync()
	{
		if( !Enabled )
			return;

		DownloadResumeState? snapshot;
		long seq;
		lock( _gate )
		{
			if( _cleared || ( _remainingVersionIds.Count == 0 && _pendingEntryIds.Length == 0 ) )
				return;
			_dirtyItems = 0;
			_lastFlushTick = Environment.TickCount64;
			snapshot = BuildSnapshotLocked();
			seq = ++_latestStateSeq;
		}

		WriteIfCurrent( snapshot, seq );
	}

	public void Clear()
	{
		lock( _gate )
		{
			_cleared = true;
			_remainingVersionIds.Clear();
			_pendingEntryIds = [];
			_dirtyItems = 0;
			_latestStateSeq++;
		}

		lock( _writeLock )
			_store.Clear();
	}

	private DownloadResumeState BuildSnapshotLocked() => new()
	{
		AccountKey = _accountKey,
		VaultPath = _vaultPath,
		OperationKind = _operationKind,
		Label = _label,
		CreatedUtc = _createdUtc,
		UpdatedUtc = DateTimeOffset.UtcNow,
		TotalCount = _totalCount,
		CompletedCount = _completedCount,
		SkippedCount = _skippedCount,
		DownloadedBytes = _downloadedBytes,
		RemainingVersionIds = [ .. _remainingVersionIds ],
		CheckoutEntryIds = _checkoutEntryIds,
		PendingEntryIds = _pendingEntryIds,
	};

	private void ScheduleAsyncFlush()
	{
		if( Interlocked.CompareExchange( ref _asyncFlushScheduled, 1, 0 ) != 0 )
			return;

		_ = Task.Run( () =>
		{
			try
			{
				DownloadResumeState? snapshot;
				long seq;
				lock( _gate )
				{
					Volatile.Write( ref _asyncFlushScheduled, 0 );
					if( _cleared || ( _remainingVersionIds.Count == 0 && _pendingEntryIds.Length == 0 ) )
						return;
					snapshot = BuildSnapshotLocked();
					seq = ++_latestStateSeq;
				}
				WriteIfCurrent( snapshot, seq );
			}
			catch( Exception ex )
			{
				Volatile.Write( ref _asyncFlushScheduled, 0 );
				HackLogger.Warn( nameof( DownloadResumeStore ), $"Async checkpoint save failed: {ex.Message}", ex );
			}
		} );
	}

	private void WriteIfCurrent( DownloadResumeState snapshot, long seq )
	{
		lock( _writeLock )
		{
			lock( _gate )
			{
				if( _cleared || seq < _latestStateSeq )
					return;
			}
			try
			{
				_store.Save( snapshot );
			}
			catch( Exception ex )
			{
				HackLogger.Warn( nameof( DownloadResumeStore ), $"Could not persist download checkpoint: {ex.Message}", ex );
			}
		}
	}
}
