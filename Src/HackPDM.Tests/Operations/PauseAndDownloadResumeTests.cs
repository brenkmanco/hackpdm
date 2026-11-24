using System.Collections;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using HackPDM.Tests.Characterization;

namespace HackPDM.Tests.Operations;

[Collection( SharedStateCollection.Name )]
public sealed class PauseAndDownloadResumeTests
{
	#region Loopback Server, Environment, and Pausable Reporter Helpers

	private sealed record RpcCall( string Service, string Method, string? Model, string? ModelMethod, JsonElement Args );

	private sealed class MockOdooServer : IAsyncDisposable
	{
		private readonly TcpListener _listener = new( IPAddress.Loopback, 0 );
		private readonly CancellationTokenSource _stop = new();
		private readonly Task _acceptLoop;
		private readonly Func<RpcCall, object?> _handler;

		public ConcurrentQueue<RpcCall> Calls { get; } = new();

		public MockOdooServer( Func<RpcCall, object?> handler )
		{
			_handler = handler;
			_listener.Start();
			_acceptLoop = AcceptLoopAsync();
		}

		public string BaseUrl => $"http://127.0.0.1:{( (IPEndPoint)_listener.LocalEndpoint ).Port}";

		public void ClearCalls() => Calls.Clear();

		private async Task AcceptLoopAsync()
		{
			try
			{
				while( !_stop.IsCancellationRequested )
				{
					TcpClient client = await _listener.AcceptTcpClientAsync( _stop.Token );
					_ = Task.Run( () => ServeClientAsync( client ), _stop.Token );
				}
			}
			catch( OperationCanceledException ) { }
			catch( SocketException ) { }
		}

		private async Task ServeClientAsync( TcpClient client )
		{
			using TcpClient owned = client;
			NetworkStream stream = client.GetStream();
			byte[] buffer = new byte[ 64 * 1024 ];
			List<byte> pending = [];

			while( !_stop.IsCancellationRequested )
			{
				int headerEnd = -1;
				int contentLength = 0;

				while( true )
				{
					headerEnd = FindHeaderEnd( pending );
					if( headerEnd >= 0 )
					{
						string headers = Encoding.ASCII.GetString( [ .. pending.GetRange( 0, headerEnd ) ] );
						contentLength = ParseContentLength( headers );
						if( pending.Count >= headerEnd + 4 + contentLength )
							break;
					}

					int read;
					try { read = await stream.ReadAsync( buffer, _stop.Token ); }
					catch { return; }
					if( read == 0 )
						return;
					for( int i = 0; i < read; i++ )
						pending.Add( buffer[ i ] );
				}

				int bodyStart = headerEnd + 4;
				byte[] bodyBytes = [ .. pending.GetRange( bodyStart, contentLength ) ];
				pending.RemoveRange( 0, bodyStart + contentLength );

				string json;
				try
				{
					using JsonDocument doc = JsonDocument.Parse( bodyBytes );
					JsonElement p = doc.RootElement.GetProperty( "params" );
					string service = p.GetProperty( "service" ).GetString() ?? "";
					string method = p.GetProperty( "method" ).GetString() ?? "";
					JsonElement args = p.GetProperty( "args" ).Clone();
					string? model = service == "object" && args.GetArrayLength() > 3 ? args[ 3 ].GetString() : null;
					string? modelMethod = service == "object" && args.GetArrayLength() > 4 ? args[ 4 ].GetString() : null;
					RpcCall call = new( service, method, model, modelMethod, args );
					Calls.Enqueue( call );
					json = SerializeJsonRpcResult( _handler( call ) );
				}
				catch( Exception ex )
				{
					json = JsonRpcErrorBody( "ServerError", ex.Message );
				}

				byte[] respBody = Encoding.UTF8.GetBytes( json );
				byte[] head = Encoding.ASCII.GetBytes(
					$"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {respBody.Length}\r\nConnection: keep-alive\r\n\r\n" );
				await stream.WriteAsync( head, _stop.Token );
				await stream.WriteAsync( respBody, _stop.Token );
				await stream.FlushAsync( _stop.Token );
			}
		}

		private static int FindHeaderEnd( List<byte> bytes )
		{
			for( int i = 0; i <= bytes.Count - 4; i++ )
			{
				if( bytes[ i ] == '\r' && bytes[ i + 1 ] == '\n' && bytes[ i + 2 ] == '\r' && bytes[ i + 3 ] == '\n' )
					return i;
			}
			return -1;
		}

		private static int ParseContentLength( string headers )
		{
			foreach( string line in headers.Split( "\r\n", StringSplitOptions.RemoveEmptyEntries ) )
			{
				if( line.StartsWith( "Content-Length:", StringComparison.OrdinalIgnoreCase )
					&& int.TryParse( line[ "Content-Length:".Length.. ].Trim(), out int len ) )
					return len;
			}
			return 0;
		}

		public async ValueTask DisposeAsync()
		{
			_stop.Cancel();
			_listener.Stop();
			try { await _acceptLoop; } catch { }
			_stop.Dispose();
		}
	}

	private static string SerializeJsonRpcResult( object? result )
	{
		using MemoryStream ms = new();
		using( Utf8JsonWriter writer = new( ms ) )
		{
			writer.WriteStartObject();
			writer.WriteString( "jsonrpc", "2.0" );
			writer.WriteNull( "id" );
			writer.WritePropertyName( "result" );
			WriteValue( writer, result );
			writer.WriteEndObject();
		}
		return Encoding.UTF8.GetString( ms.ToArray() );
	}

	private static string JsonRpcErrorBody( string name, string message )
	{
		using MemoryStream ms = new();
		using( Utf8JsonWriter writer = new( ms ) )
		{
			writer.WriteStartObject();
			writer.WriteString( "jsonrpc", "2.0" );
			writer.WriteNull( "id" );
			writer.WriteStartObject( "error" );
			writer.WriteNumber( "code", 200 );
			writer.WriteString( "message", "Odoo Server Error" );
			writer.WriteStartObject( "data" );
			writer.WriteString( "name", name );
			writer.WriteString( "message", message );
			writer.WriteEndObject();
			writer.WriteEndObject();
			writer.WriteEndObject();
		}
		return Encoding.UTF8.GetString( ms.ToArray() );
	}

	private static void WriteValue( Utf8JsonWriter writer, object? value )
	{
		switch( value )
		{
			case null:
				writer.WriteNullValue();
				break;
			case bool b:
				writer.WriteBooleanValue( b );
				break;
			case string s:
				writer.WriteStringValue( s );
				break;
			case int i:
				writer.WriteNumberValue( i );
				break;
			case long l:
				writer.WriteNumberValue( l );
				break;
			case double d:
				writer.WriteNumberValue( d );
				break;
			case JsonElement je:
				je.WriteTo( writer );
				break;
			case IReadOnlyDictionary<string, object?> roDict:
				writer.WriteStartObject();
				foreach( var (k, v) in roDict )
				{
					writer.WritePropertyName( k );
					WriteValue( writer, v );
				}
				writer.WriteEndObject();
				break;
			case IDictionary dict:
				writer.WriteStartObject();
				foreach( DictionaryEntry entry in dict )
				{
					writer.WritePropertyName( entry.Key?.ToString() ?? "" );
					WriteValue( writer, entry.Value );
				}
				writer.WriteEndObject();
				break;
			case IEnumerable items:
				writer.WriteStartArray();
				foreach( object? item in items )
					WriteValue( writer, item );
				writer.WriteEndArray();
				break;
			default:
				writer.WriteStringValue( value.ToString() );
				break;
		}
	}

	private sealed class InMemorySettings : ISettingsProvider
	{
		private readonly ConcurrentDictionary<string, object?> _values = new();
		public T? Get<T>( string key, T? defaultValue = default )
			=> _values.TryGetValue( key, out object? v ) && v is T t ? t : defaultValue;
		public void Set<T>( string key, T value )
		{
			if( value is null )
				_values.TryRemove( key, out _ );
			else
				_values[ key ] = value;
		}
		public bool Contains( string key ) => _values.ContainsKey( key );
		public void Remove( string key ) => _values.TryRemove( key, out _ );
	}

	private sealed class ScopedOdooEnvironment : IDisposable
	{
		private readonly string _root;
		private readonly ISettingsProvider? _prevHackSettings;
		private readonly string _prevPwaAbs;
		private readonly string _prevPwaRel;
		private readonly ISettingsProvider? _prevOdooSettings;
		private readonly string? _prevAddress;
		private readonly string? _prevPort;
		private readonly string? _prevDb;
		private readonly bool _prevHttps;
		private readonly string? _prevUrl;
		private readonly string? _prevUser;
		private readonly string? _prevPass;
		private readonly int _prevId;
		private readonly OdooLookupCache _prevLookups;
		private readonly string _prevTempPath;

		public string Vault { get; }
		public string TempDir { get; }
		public DownloadResumeStore ResumeStore { get; }

		public ScopedOdooEnvironment( string? baseUrl = null )
		{
			_root = Path.Combine( Path.GetTempPath(), $"hackpdm-pausetest-{Guid.NewGuid():N}" );
			Vault = Directory.CreateDirectory( Path.Combine( _root, "PWA" ) ).FullName;
			TempDir = Directory.CreateDirectory( Path.Combine( _root, "Temp" ) ).FullName;
			ResumeStore = new DownloadResumeStore( Path.Combine( _root, "download-resume.json" ) );

			_prevHackSettings = HackDefaults.Instance.SettingsProvider;
			_prevPwaAbs = HackDefaults.Instance.PwaPathAbsolute;
			_prevPwaRel = HackDefaults.Instance.PwaPathRelative;

			OdooDefaults odoo = OdooDefaults.Instance;
			_prevOdooSettings = odoo.Settings;
			_prevAddress = odoo.OdooAddress;
			_prevPort = odoo.OdooPort;
			_prevDb = odoo.OdooDb;
			_prevHttps = odoo.OdooUseHttps;
			_prevUrl = odoo.OdooUrl;
			_prevUser = odoo.OdooUser;
			_prevPass = odoo.OdooPass;
			_prevId = odoo.OdooId;
			_prevLookups = odoo.Lookups;
			_prevTempPath = PdmConstants.TemporaryPath;

			HackDefaults.Instance.SettingsProvider = new InMemorySettings();
			HackDefaults.Instance.PwaPathAbsolute = Vault;
			HackDefaults.Instance.PwaPathRelative = "PWA";
			PdmConstants.TemporaryPath = TempDir;

			odoo.Settings = new InMemorySettings();
			odoo.OdooUseHttps = false;
			odoo.OdooAddress = "127.0.0.1";
			odoo.OdooPort = "";
			odoo.OdooUrl = baseUrl;
			odoo.OdooDb = "testdb";
			odoo.OdooUser = "alice";
			odoo.OdooPass = "secret";
			odoo.OdooId = 10;
			odoo.Lookups = OdooLookupCache.Empty;
		}

		public void Dispose()
		{
			OdooDefaults odoo = OdooDefaults.Instance;
			odoo.Settings = _prevOdooSettings;
			odoo.OdooAddress = _prevAddress;
			odoo.OdooPort = _prevPort;
			odoo.OdooDb = _prevDb;
			odoo.OdooUseHttps = _prevHttps;
			odoo.OdooUrl = _prevUrl;
			odoo.OdooUser = _prevUser;
			odoo.OdooPass = _prevPass;
			odoo.OdooId = _prevId;
			odoo.Lookups = _prevLookups;

			HackDefaults.Instance.SettingsProvider = _prevHackSettings;
			HackDefaults.Instance.PwaPathAbsolute = _prevPwaAbs;
			HackDefaults.Instance.PwaPathRelative = _prevPwaRel;
			PdmConstants.TemporaryPath = _prevTempPath;

			try { Directory.Delete( _root, recursive: true ); } catch { }
		}
	}

	private sealed class PausableRecordingReporter : IOperationReporter
	{
		public ConcurrentQueue<(StatusMessage Status, string Message)> Logs { get; } = new();
		public ConcurrentQueue<(string Caption, string Message)> Notices { get; } = new();
		public OperationPauseController? PauseController { get; private set; }
		public CancellationTokenSource? CurrentCancellation { get; private set; }
		public int Total { get; private set; }
		public int Processed { get; private set; }
		public int Skipped { get; private set; }
		public int CompletedCount { get; private set; }
		public long DownloadedBytes { get; private set; }
		public long UploadedBytes { get; private set; }

		public void Log( StatusMessage status, string message ) => Logs.Enqueue( ( status, message ) );
		public void ShowWindow() { }

		public CancellationTokenSource BeginOperation() => BeginOperation( canPause: false );

		public CancellationTokenSource BeginOperation( bool canPause )
		{
			CurrentCancellation?.Dispose();
			CurrentCancellation = new CancellationTokenSource();
			PauseController = canPause ? new OperationPauseController() : null;
			return CurrentCancellation;
		}

		public void Cancel() => CurrentCancellation?.Cancel();
		public void SetTotal( int total ) => Total = total;

		public void Advance( int processed, int skipped = 0 )
		{
			Processed += processed + skipped;
			Skipped += skipped;
		}

		public void SetProcessed( int processed ) => Processed = processed;

		public void RestoreProgress( int total, int processed, int skipped, long downloadedBytes )
		{
			Total = total;
			Processed = processed;
			Skipped = skipped;
			DownloadedBytes += downloadedBytes;
		}

		public void AddDownloadedBytes( long bytes ) => DownloadedBytes += bytes;
		public void AddUploadedBytes( long bytes ) => UploadedBytes += bytes;

		public void Complete()
		{
			PauseController?.ResetForCompletion();
			CompletedCount++;
		}

		public Task NotifyAsync( string message, string caption = "Status" )
		{
			Notices.Enqueue( ( caption, message ) );
			return Task.CompletedTask;
		}
	}

	private static async Task WaitUntilAsync( Func<bool> condition, int timeoutMs = 5000 )
	{
		bool reached = await AsyncHelper.WaitUntil( condition, pollingMs: 5, msTimeout: timeoutMs );
		Assert.True( reached, $"Timed out after {timeoutMs}ms waiting for expected condition." );
	}

	private static int[] ExtractIntArray( JsonElement arrayElement )
	{
		if( arrayElement.ValueKind != JsonValueKind.Array )
			return [];
		int[] result = new int[ arrayElement.GetArrayLength() ];
		int i = 0;
		foreach( JsonElement item in arrayElement.EnumerateArray() )
			result[ i++ ] = item.GetInt32();
		return result;
	}

	private static string Sha1Hex( byte[] bytes ) => Convert.ToHexStringLower( SHA1.HashData( bytes ) );

	#endregion

	[Fact]
	public async Task OperationPauseController_Transitions_Pausing_Paused_Resume_Cancel_AndFastPath()
	{
		OperationPauseController ctrl = new();

		// Unpaused fast path completes synchronously without heap wait allocation
		ValueTask fastPath = ctrl.WaitIfPausedAsync();
		Assert.True( fastPath.IsCompletedSuccessfully );

		// Holding an active work unit transitions Running -> Pausing, then Paused on scope dispose
		OperationPauseController.WorkScope scope = OperationPauseController.EnterScope( ctrl );
		ctrl.RequestPause();
		Assert.Equal( OperationPauseState.Pausing, ctrl.State );

		Task blockedWaiter = Task.Run( async () => await ctrl.WaitIfPausedAsync() );
		scope.Dispose();
		Assert.Equal( OperationPauseState.Paused, ctrl.State );

		await Task.Delay( 40 );
		Assert.False( blockedWaiter.IsCompleted );

		ctrl.Resume();
		Assert.Equal( OperationPauseState.Running, ctrl.State );
		await blockedWaiter.WaitAsync( TimeSpan.FromSeconds( 2 ) );

		// Cancelling token while paused throws OperationCanceledException promptly
		ctrl.RequestPause();
		Assert.Equal( OperationPauseState.Paused, ctrl.State );
		using CancellationTokenSource cts = new();
		Task cancelWaiter = ctrl.WaitIfPausedAsync( cts.Token ).AsTask();
		cts.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>( () => cancelWaiter.WaitAsync( TimeSpan.FromSeconds( 2 ) ) );

		// ResetForCompletion sets None and unblocks any remaining waiter
		Task resetWaiter = Task.Run( async () => await ctrl.WaitIfPausedAsync() );
		ctrl.ResetForCompletion();
		Assert.Equal( OperationPauseState.None, ctrl.State );
		await resetWaiter.WaitAsync( TimeSpan.FromSeconds( 2 ) );
	}

	[Fact]
	public void DownloadResumeStore_AtomicSaveLoadAccountScopingClearAndCorruptJson_AndHackDefaultsDefault()
	{
		using ScopedOdooEnvironment env = new();

		// PersistDownloadResume defaults to true and round-trips through HackDefaults
		Assert.True( HackDefaults.Instance.PersistDownloadResume );
		HackDefaults.Instance.PersistDownloadResume = false;
		Assert.False( HackDefaults.Instance.PersistDownloadResume );
		HackDefaults.Instance.PersistDownloadResume = true;
		Assert.True( HackDefaults.Instance.PersistDownloadResume );

		DownloadResumeStore store = env.ResumeStore;
		string accountKey = DownloadResumeStore.BuildAccountKey( "http://127.0.0.1:8069", "testdb", "alice", env.Vault );

		DownloadResumeState state = new()
		{
			AccountKey = accountKey,
			VaultPath = env.Vault,
			OperationKind = DownloadResumeKind.GetLatest,
			Label = "Get Latest",
			TotalCount = 4,
			CompletedCount = 1,
			DownloadedBytes = 128,
			RemainingVersionIds = [ 102, 103, 104 ],
		};
		store.Save( state );

		Assert.True( File.Exists( store.FilePath ) );
		Assert.False( File.Exists( store.FilePath + ".tmp" ) );

		DownloadResumeState? loaded = store.LoadForAccount( accountKey.ToUpperInvariant() );
		Assert.NotNull( loaded );
		Assert.Equal( [ 102, 103, 104 ], loaded!.RemainingVersionIds );
		Assert.Equal( 1, loaded.CompletedCount );

		string otherAccount = DownloadResumeStore.BuildAccountKey( "http://other-host:8069", "testdb", "alice", env.Vault );
		Assert.Null( store.LoadForAccount( otherAccount ) );

		// Empty remaining work returns null for account resume lookup
		store.Save( new DownloadResumeState
		{
			AccountKey = accountKey,
			VaultPath = env.Vault,
			RemainingVersionIds = [],
			PendingEntryIds = [],
		} );
		Assert.Null( store.LoadForAccount( accountKey ) );

		// Corrupt JSON fails gracefully with null
		File.WriteAllText( store.FilePath, "{ not valid json" );
		Assert.Null( store.Load() );

		store.Clear();
		Assert.False( File.Exists( store.FilePath ) );
		Assert.False( File.Exists( store.FilePath + ".tmp" ) );
	}

	[Fact]
	public async Task DownloadAdaptiveAsync_AndLargeChunkedDownload_PauseResumeAndCancelCleansPartFiles()
	{
		Dictionary<int, byte[]> payloads = new()
		{
			[ 101 ] = Encoding.UTF8.GetBytes( "adaptive-file-101" ),
			[ 102 ] = Encoding.UTF8.GetBytes( "adaptive-file-102" ),
			[ 103 ] = Encoding.UTF8.GetBytes( "adaptive-file-103" ),
			[ 104 ] = Encoding.UTF8.GetBytes( "adaptive-file-104" ),
		};
		byte[] largeBytes = new byte[ 4096 ];
		for( int i = 0; i < largeBytes.Length; i++ )
			largeBytes[ i ] = (byte)( ( i * 31 ) & 0xFF );
		string largeSha1 = Sha1Hex( largeBytes );

		OperationPauseController adaptivePause = new();
		int readBatchCount = 0;
		OperationPauseController chunkPause = new();
		int chunkCallCount = 0;

		await using MockOdooServer server = new( call =>
		{
			if( call.Model == HpVersion.ModelName && call.ModelMethod == "read" )
			{
				int callIndex = Interlocked.Increment( ref readBatchCount );
				if( callIndex == 1 )
					adaptivePause.RequestPause();

				int[] requested = ExtractIntArray( call.Args[ 5 ] );
				List<Dictionary<string, object?>> rows = new( requested.Length );
				foreach( int vid in requested )
				{
					byte[] data = payloads[ vid ];
					rows.Add( new Dictionary<string, object?>
					{
						[ "id" ] = vid,
						[ "name" ] = $"f{vid}.txt",
						[ "checksum" ] = Sha1Hex( data ),
						[ "file_size" ] = data.Length,
						[ "windows_complete_name" ] = $@"root\f{vid}.txt",
						[ "file_contents" ] = Convert.ToBase64String( data ),
					} );
				}
				return rows;
			}
			if( call.Model == HpVersion.ModelName && call.ModelMethod == "read_contents" )
			{
				int c = Interlocked.Increment( ref chunkCallCount );
				if( c == 1 )
					chunkPause.RequestPause();

				long offset = call.Args[ 5 ][ 1 ].GetInt64();
				int wanted = call.Args[ 5 ][ 2 ].GetInt32();
				int take = (int)Math.Min( wanted, Math.Max( 0, largeBytes.Length - offset ) );
				byte[] slice = new byte[ take ];
				Array.Copy( largeBytes, offset, slice, 0, take );
				return new Dictionary<string, object?>
				{
					[ "data" ] = Convert.ToBase64String( slice ),
				};
			}
			return Array.Empty<object>();
		} );

		using ScopedOdooEnvironment env = new( server.BaseUrl );

		// Part A: multi-batch adaptive download pauses between batches, resumes, and writes all files
		HpVersion[] versions = [ .. payloads.Select( kvp => new HpVersion
		{
			id = kvp.Key,
			name = $"f{kvp.Key}.txt",
			checksum = Sha1Hex( kvp.Value ),
			file_size = kvp.Value.Length,
			WinPathway = "",
			windows_complete_name = $@"root\f{kvp.Key}.txt",
		} ) ];

		DownloadOperations.DownloadSummary summary = new();
		Task adaptiveTask = DownloadOperations.DownloadAdaptiveAsync(
			versions,
			CancellationToken.None,
			summary,
			maxBatchCount: 2,
			maxConcurrency: 1,
			pause: adaptivePause );

		await WaitUntilAsync( () => adaptivePause.State == OperationPauseState.Paused );
		await Task.Delay( 50 );
		Assert.Equal( 1, Volatile.Read( ref readBatchCount ) );

		adaptivePause.Resume();
		await adaptiveTask.WaitAsync( TimeSpan.FromSeconds( 5 ) );

		Assert.Equal( 4, summary.Downloaded );
		Assert.Empty( summary.Failed );
		foreach( var (vid, expected) in payloads )
		{
			string localPath = Path.Combine( env.Vault, $"f{vid}.txt" );
			Assert.True( File.Exists( localPath ) );
			Assert.Equal( expected, File.ReadAllBytes( localPath ) );
		}

		// Part B: chunked large download pauses cleanly between chunks and removes .part file when cancelled
		int prevChunkBytes = ContentTransfer.ChunkBytes;
		ContentTransfer.ChunkBytes = 1024;
		HashSet<string> existingParts = Directory.GetFiles( Path.GetTempPath(), "hackpdm-*.part" ).ToHashSet( StringComparer.OrdinalIgnoreCase );
		try
		{
			HpVersion largeVersion = new()
			{
				id = 555,
				name = "large.bin",
				file_size = largeBytes.Length,
				checksum = largeSha1,
			};
			using CancellationTokenSource chunkCts = new();
			Task<Result<string>> largeTask = ContentTransfer.DownloadToTempFileAsync(
				largeVersion,
				chunkCts.Token,
				pause: chunkPause );

			await WaitUntilAsync( () => chunkPause.State == OperationPauseState.Paused );
			await Task.Delay( 50 );
			Assert.Equal( 1, Volatile.Read( ref chunkCallCount ) );

			chunkCts.Cancel();
			await Assert.ThrowsAnyAsync<OperationCanceledException>( () => largeTask.WaitAsync( TimeSpan.FromSeconds( 5 ) ) );

			string[] remainingNewParts = [ .. Directory.GetFiles( Path.GetTempPath(), "hackpdm-*.part" )
				.Where( p => !existingParts.Contains( p ) ) ];
			Assert.Empty( remainingNewParts );
		}
		finally
		{
			ContentTransfer.ChunkBytes = prevChunkBytes;
		}
	}

	[Fact]
	public async Task PdmWorkflowService_DownloadShutdownAndSimulatedRestart_ResumesRemainingOnly_AndClears()
	{
		Dictionary<int, byte[]> payloads = new()
		{
			[ 101 ] = Encoding.UTF8.GetBytes( "workflow-version-101" ),
			[ 102 ] = Encoding.UTF8.GetBytes( "workflow-version-102" ),
			[ 103 ] = Encoding.UTF8.GetBytes( "workflow-version-103" ),
			[ 104 ] = Encoding.UTF8.GetBytes( "workflow-version-104" ),
		};

		PausableRecordingReporter firstReporter = new();
		bool pauseOnFirstContentBatch = true;
		int phase1ContentReads = 0;

		await using MockOdooServer server = new( call =>
		{
			if( call.Model == HpVersion.ModelName && call.ModelMethod == "get_recursive_dependency_entries_by_entries" )
				return new[] { 1, 2, 3, 4 };

			if( call.Model == HpEntry.ModelName && call.ModelMethod == "related_browse" )
			{
				return new object[]
				{
					BuildVersionRow( 101, payloads[ 101 ], includeContents: false ),
					BuildVersionRow( 102, payloads[ 102 ], includeContents: false ),
					BuildVersionRow( 103, payloads[ 103 ], includeContents: false ),
					BuildVersionRow( 104, payloads[ 104 ], includeContents: false ),
				};
			}

			if( call.Model == HpVersion.ModelName && call.ModelMethod == "read" )
			{
				bool wantsContents = call.Args[ 6 ].GetRawText().Contains( nameof( HpVersion.file_contents ), StringComparison.Ordinal );
				if( wantsContents && pauseOnFirstContentBatch && Interlocked.Increment( ref phase1ContentReads ) == 1 )
					firstReporter.PauseController?.RequestPause();

				int[] requested = ExtractIntArray( call.Args[ 5 ] );
				return requested.Select( vid => BuildVersionRow( vid, payloads[ vid ], wantsContents ) ).ToArray();
			}

			return Array.Empty<object>();
		} );

		using ScopedOdooEnvironment env = new( server.BaseUrl );
		OdooDefaults.Instance.Lookups = new OdooLookupCache { MaxBatchSize = 2, MaxConcurrency = 1 };

		PdmWorkflowService firstWorkflow = new( firstReporter, resumeStore: env.ResumeStore );
		Task firstDownloadTask = firstWorkflow.GetLatestAsync( [ 1, 2, 3, 4 ] );

		await WaitUntilAsync( () => firstReporter.PauseController?.State == OperationPauseState.Paused );
		await Task.Delay( 50 );
		Assert.Equal( 1, Volatile.Read( ref phase1ContentReads ) );

		bool hadActiveCommit = firstWorkflow.PrepareForShutdown();
		Assert.False( hadActiveCommit );
		firstReporter.Cancel();
		await firstDownloadTask.WaitAsync( TimeSpan.FromSeconds( 5 ) );

		DownloadResumeState? saved = env.ResumeStore.Load();
		Assert.NotNull( saved );
		Assert.Equal( DownloadResumeKind.GetLatest, saved!.OperationKind );
		Assert.Equal( 4, saved.TotalCount );
		Assert.Equal( 2, saved.CompletedCount );
		Assert.Equal( [ 103, 104 ], [ .. saved.RemainingVersionIds.OrderBy( static x => x ) ] );

		// Simulate app restart with a fresh PdmWorkflowService reading from the same store
		pauseOnFirstContentBatch = false;
		server.ClearCalls();

		PausableRecordingReporter secondReporter = new();
		PdmWorkflowService secondWorkflow = new( secondReporter, resumeStore: env.ResumeStore );
		DownloadResumeState? discovered = secondWorkflow.GetResumableDownload();
		Assert.NotNull( discovered );
		Assert.Equal( [ 103, 104 ], [ .. discovered!.RemainingVersionIds.OrderBy( static x => x ) ] );

		bool resumed = await secondWorkflow.ResumeDownloadAsync().WaitAsync( TimeSpan.FromSeconds( 5 ) );
		Assert.True( resumed );

		int[] resumedReadIds = [ .. server.Calls
			.Where( c => c.Model == HpVersion.ModelName && c.ModelMethod == "read" )
			.SelectMany( c => ExtractIntArray( c.Args[ 5 ] ) ) ];
		Assert.DoesNotContain( 101, resumedReadIds );
		Assert.DoesNotContain( 102, resumedReadIds );
		Assert.Contains( 103, resumedReadIds );
		Assert.Contains( 104, resumedReadIds );

		foreach( var (vid, expected) in payloads )
		{
			string localPath = Path.Combine( env.Vault, $"f{vid}.txt" );
			Assert.True( File.Exists( localPath ), $"Missing expected file {localPath}" );
			Assert.Equal( expected, File.ReadAllBytes( localPath ) );
		}

		Assert.Null( secondWorkflow.GetResumableDownload() );
		Assert.Null( env.ResumeStore.Load() );
		Assert.False( File.Exists( env.ResumeStore.FilePath ) );
	}

	[Fact]
	public async Task PdmWorkflowService_PersistDownloadResumeDisabled_WritesNoCheckpoint()
	{
		Dictionary<int, byte[]> payloads = new()
		{
			[ 101 ] = Encoding.UTF8.GetBytes( "disabled-101" ),
			[ 102 ] = Encoding.UTF8.GetBytes( "disabled-102" ),
			[ 103 ] = Encoding.UTF8.GetBytes( "disabled-103" ),
			[ 104 ] = Encoding.UTF8.GetBytes( "disabled-104" ),
		};

		PausableRecordingReporter reporter = new();
		int contentReads = 0;

		await using MockOdooServer server = new( call =>
		{
			if( call.Model == HpVersion.ModelName && call.ModelMethod == "get_recursive_dependency_entries_by_entries" )
				return new[] { 1, 2, 3, 4 };

			if( call.Model == HpEntry.ModelName && call.ModelMethod == "related_browse" )
			{
				return new object[]
				{
					BuildVersionRow( 101, payloads[ 101 ], includeContents: false ),
					BuildVersionRow( 102, payloads[ 102 ], includeContents: false ),
					BuildVersionRow( 103, payloads[ 103 ], includeContents: false ),
					BuildVersionRow( 104, payloads[ 104 ], includeContents: false ),
				};
			}

			if( call.Model == HpVersion.ModelName && call.ModelMethod == "read" )
			{
				bool wantsContents = call.Args[ 6 ].GetRawText().Contains( nameof( HpVersion.file_contents ), StringComparison.Ordinal );
				if( wantsContents && Interlocked.Increment( ref contentReads ) == 1 )
					reporter.PauseController?.RequestPause();

				int[] requested = ExtractIntArray( call.Args[ 5 ] );
				return requested.Select( vid => BuildVersionRow( vid, payloads[ vid ], wantsContents ) ).ToArray();
			}

			return Array.Empty<object>();
		} );

		using ScopedOdooEnvironment env = new( server.BaseUrl );
		HackDefaults.Instance.PersistDownloadResume = false;
		OdooDefaults.Instance.Lookups = new OdooLookupCache { MaxBatchSize = 2, MaxConcurrency = 1 };

		PdmWorkflowService service = new( reporter, resumeStore: env.ResumeStore );
		Task downloadTask = service.GetLatestAsync( [ 1, 2, 3, 4 ] );

		await WaitUntilAsync( () => reporter.PauseController?.State == OperationPauseState.Paused );
		service.PrepareForShutdown();
		reporter.Cancel();
		await downloadTask.WaitAsync( TimeSpan.FromSeconds( 5 ) );

		Assert.Null( env.ResumeStore.Load() );
		Assert.Null( service.GetResumableDownload() );
		Assert.False( File.Exists( env.ResumeStore.FilePath ) );
	}

	[Fact]
	public async Task PdmWorkflowService_CommitPauseResume_AndCancelWhilePausedRollsBackWithoutPersisting()
	{
		PausableRecordingReporter? activeReporter = null;
		int nextStagedId = 1000;

		await using MockOdooServer server = new( call =>
		{
			if( call.Model == HpEntry.ModelName && call.ModelMethod == "search_read" )
				return Array.Empty<object>();

			if( call.Model == HpPDMCommit.ModelName && call.ModelMethod == "create" )
				return 900;

			if( call.Model == HpDirectory.ModelName && call.ModelMethod == "last_available_directory" )
				return new Dictionary<string, object?> { [ "index" ] = 0, [ "dir_id" ] = 1 };

			if( call.Model == HpDirectory.ModelName && call.ModelMethod == "read" )
			{
				return new object[]
				{
					new Dictionary<string, object?> { [ "id" ] = 1, [ "name" ] = "root" },
				};
			}

			if( call.Model == HpRecordStaged.ModelName && call.ModelMethod == "create" )
			{
				if( call.Args.GetRawText().Contains( $"\"{HpVersion.ModelName}\"", StringComparison.Ordinal ) )
					activeReporter?.PauseController?.RequestPause();
				return Interlocked.Increment( ref nextStagedId );
			}

			if( call.Model == HpPDMCommit.ModelName && call.ModelMethod == "start_commit" )
			{
				return new object[]
				{
					new Dictionary<string, object?> { [ "id" ] = 900, [ "committed" ] = true, [ "errored" ] = false },
				};
			}

			if( call.Model == HpPDMCommit.ModelName && call.ModelMethod == "clear_commit" )
				return true;

			return Array.Empty<object>();
		} );

		using ScopedOdooEnvironment env = new( server.BaseUrl );
		string filePath = Path.Combine( env.Vault, "commit-part.txt" );
		File.WriteAllText( filePath, "commit content" );
		HackFile commitFile = HackFile.GetFromPath( filePath )!;

		PdmWorkflowService service = new( activeReporter = new PausableRecordingReporter(), resumeStore: env.ResumeStore );

		// Sub-case A: Pause after staging (before start_commit), verify no start_commit, then resume to completion
		Task commitA = service.CommitAsync( [ commitFile ] );
		await WaitUntilAsync( () => activeReporter.PauseController?.State == OperationPauseState.Paused );
		await Task.Delay( 50 );

		Assert.True( service.HasActiveCommit );
		Assert.DoesNotContain( server.Calls, c => c.Model == HpPDMCommit.ModelName && c.ModelMethod == "start_commit" );
		Assert.Null( env.ResumeStore.Load() );

		activeReporter.PauseController!.Resume();
		await commitA.WaitAsync( TimeSpan.FromSeconds( 5 ) );

		Assert.False( service.HasActiveCommit );
		Assert.Single( server.Calls, c => c.Model == HpPDMCommit.ModelName && c.ModelMethod == "start_commit" );
		Assert.DoesNotContain( server.Calls, c => c.Model == HpPDMCommit.ModelName && c.ModelMethod == "clear_commit" );
		Assert.Null( env.ResumeStore.Load() );

		// Sub-case B: Pause after staging, then cancel -> clears commit on server and persists nothing
		server.ClearCalls();
		PausableRecordingReporter reporterB = new();
		service = new PdmWorkflowService( activeReporter = reporterB, resumeStore: env.ResumeStore );

		Task commitB = service.CommitAsync( [ commitFile ] );
		await WaitUntilAsync( () => reporterB.PauseController?.State == OperationPauseState.Paused );

		Assert.True( service.HasActiveCommit );
		Assert.DoesNotContain( server.Calls, c => c.Model == HpPDMCommit.ModelName && c.ModelMethod == "start_commit" );

		reporterB.Cancel();
		await commitB.WaitAsync( TimeSpan.FromSeconds( 5 ) );

		Assert.False( service.HasActiveCommit );
		Assert.Contains( server.Calls, c => c.Model == HpPDMCommit.ModelName && c.ModelMethod == "clear_commit" );
		Assert.DoesNotContain( server.Calls, c => c.Model == HpPDMCommit.ModelName && c.ModelMethod == "start_commit" );
		Assert.Null( env.ResumeStore.Load() );
	}

	private static Dictionary<string, object?> BuildVersionRow( int versionId, byte[] bytes, bool includeContents )
	{
		Dictionary<string, object?> row = new()
		{
			[ "id" ] = versionId,
			[ "name" ] = $"f{versionId}.txt",
			[ "checksum" ] = Sha1Hex( bytes ),
			[ "file_size" ] = bytes.Length,
			[ "windows_complete_name" ] = $@"root\f{versionId}.txt",
		};
		if( includeContents )
			row[ "file_contents" ] = Convert.ToBase64String( bytes );
		return row;
	}
}
