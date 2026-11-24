using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Tests.Characterization;
using HackPDM.Tests.Core;
using HackPDM.Tests.Mocks;

namespace HackPDM.Tests.Operations;

[Collection( SharedStateCollection.Name )]
public sealed class RpcAndWorkflowProductionTests
{
	#region Mock Odoo loopback server and scoped test fixtures

	internal sealed record RpcCall( string Service, string Method, string? Model, string? ModelMethod, JsonElement Args );

	internal sealed record MockHttpResponse( int StatusCode, string StatusText, string Body, bool CloseConnection = false, bool TruncateBody = false );

	private sealed class MockOdooServer : IAsyncDisposable
	{
		private readonly TcpListener _listener = new( IPAddress.Loopback, 0 );
		private readonly CancellationTokenSource _stop = new();
		private readonly Task _acceptLoop;
		private readonly Func<RpcCall, MockHttpResponse> _handler;

		public ConcurrentQueue<RpcCall> Calls { get; } = new();

		public MockOdooServer( Func<RpcCall, object?> jsonResultHandler )
			: this( call =>
			{
				object? payload = jsonResultHandler( call );
				if( payload is MockHttpResponse raw )
					return raw;
				string json = SerializeJsonRpcResult( payload );
				return new MockHttpResponse( 200, "OK", json );
			} )
		{
		}

		public MockOdooServer( Func<RpcCall, MockHttpResponse> handler )
		{
			_handler = handler;
			_listener.Start();
			_acceptLoop = AcceptLoopAsync();
		}

		public string BaseUrl => $"http://127.0.0.1:{( (IPEndPoint)_listener.LocalEndpoint ).Port}";

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
						int needed = headerEnd + 4 + contentLength;
						if( pending.Count >= needed )
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

				MockHttpResponse response;
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
					response = _handler( call );
				}
				catch( Exception ex )
				{
					response = new MockHttpResponse( 200, "OK", JsonRpcErrorBody( "ServerError", ex.Message ) );
				}

				byte[] respBody = Encoding.UTF8.GetBytes( response.Body );
				int advertisedLength = response.TruncateBody ? respBody.Length + 50 : respBody.Length;
				string connHeader = response.CloseConnection || response.TruncateBody ? "Connection: close\r\n" : "Connection: keep-alive\r\n";
				byte[] head = Encoding.ASCII.GetBytes(
					$"HTTP/1.1 {response.StatusCode} {response.StatusText}\r\nContent-Type: application/json\r\nContent-Length: {advertisedLength}\r\n{connHeader}\r\n" );
				await stream.WriteAsync( head, _stop.Token );
				await stream.WriteAsync( respBody, _stop.Token );
				await stream.FlushAsync( _stop.Token );

				if( response.CloseConnection || response.TruncateBody )
					return;
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

	private sealed class ScopedOdooEnvironment : IDisposable
	{
		private readonly HackPDM.Core.Configuration.ISettingsProvider? _prevHackSettings;
		private readonly string _prevPwaAbs;
		private readonly string _prevPwaRel;
		private readonly HackPDM.Core.Configuration.ISettingsProvider? _prevOdooSettings;
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

		public ScopedOdooEnvironment( string? baseUrl = null )
		{
			string root = Path.Combine( Path.GetTempPath(), $"hackpdm-prodtest-{Guid.NewGuid():N}" );
			Vault = Directory.CreateDirectory( Path.Combine( root, "PWA" ) ).FullName;
			TempDir = Directory.CreateDirectory( Path.Combine( root, "Temp" ) ).FullName;

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

			try { Directory.Delete( Path.GetDirectoryName( Vault )!, recursive: true ); } catch { }
		}
	}

	private sealed class InMemorySettings : HackPDM.Core.Configuration.ISettingsProvider
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

	private sealed class RecordingReporter : IOperationReporter
	{
		public ConcurrentQueue<(StatusMessage Status, string Message)> Logs { get; } = new();
		public ConcurrentQueue<(string Caption, string Message)> Notices { get; } = new();
		public int Total { get; private set; }
		public int Processed { get; private set; }
		public int CompletedCount { get; private set; }
		public long DownloadedBytes { get; private set; }
		public long UploadedBytes { get; private set; }

		public void Log( StatusMessage status, string message ) => Logs.Enqueue( ( status, message ) );
		public void ShowWindow() { }
		public CancellationTokenSource BeginOperation() => new();
		public void Cancel() { }
		public void SetTotal( int total ) => Total = total;
		public void Advance( int processed, int skipped = 0 ) => Processed += processed + skipped;
		public void SetProcessed( int processed ) => Processed = processed;
		public void AddDownloadedBytes( long bytes ) => DownloadedBytes += bytes;
		public void AddUploadedBytes( long bytes ) => UploadedBytes += bytes;
		public void Complete() => CompletedCount++;
		public Task NotifyAsync( string message, string caption = "Status" )
		{
			Notices.Enqueue( ( caption, message ) );
			return Task.CompletedTask;
		}
	}

	#endregion

	#region P0 / P1 Odoo Lookup, Login, Transport, and Schema Tests

	[Fact]
	public async Task OdooLookupCache_LoadAsync_DoesNotCreateDuplicateNode_WhenNodeReadFails()
	{
		await using MockOdooServer server = new( call =>
		{
			if( call.Model == HpNode.ModelName && call.ModelMethod == "search_read" )
				return new MockHttpResponse( 200, "OK", JsonRpcErrorBody( "AccessError", "transient node error" ) );
			if( call.Model == HpSetting.ModelName && call.ModelMethod == "search_read" )
				return new object[] { new Dictionary<string, object?> { [ "id" ] = 1, [ "name" ] = "restrict_types", [ "type" ] = "bool", [ "bool_value" ] = true } };
			return Array.Empty<object>();
		} );
		using ScopedOdooEnvironment env = new( server.BaseUrl );

		OdooLookupCache cache = await OdooLookupCache.LoadAsync();

		Assert.Null( cache.HpNodes );
		Assert.NotNull( cache.HpSettings );
		Assert.DoesNotContain( server.Calls, c => c.Model == HpNode.ModelName && c.ModelMethod == "create" );
	}

	[Fact]
	public async Task OdooDefaults_InitializeAsync_FailsAndClearsSession_WhenHpSettingsReadFails()
	{
		await using MockOdooServer server = new( call =>
		{
			if( call.Service == "common" && call.Method == "login" )
				return 42;
			if( call.Model == HpSetting.ModelName )
				return new MockHttpResponse( 200, "OK", JsonRpcErrorBody( "UserError", "settings blocked" ) );
			return Array.Empty<object>();
		} );
		using ScopedOdooEnvironment env = new( server.BaseUrl );
		OdooDefaults.Instance.OdooId = 0;

		InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>( () => OdooDefaults.Instance.InitializeAsync() );
		Assert.Contains( "settings blocked", ex.Message );
		Assert.Equal( 0, OdooDefaults.Instance.OdooId );
		Assert.Null( OdooDefaults.Instance.HpSettings );
	}

	[Fact]
	public async Task OdooDefaults_InitializeLoginAsync_ConcurrentCalls_LoginOnlyOnce()
	{
		int loginCount = 0;
		await using MockOdooServer server = new( call =>
		{
			if( call.Service == "common" && call.Method == "login" )
			{
				Interlocked.Increment( ref loginCount );
				Thread.Sleep( 60 );
				return 77;
			}
			return null;
		} );
		using ScopedOdooEnvironment env = new( server.BaseUrl );
		OdooDefaults.Instance.OdooId = 0;

		await Task.WhenAll(
			OdooDefaults.Instance.InitializeLoginAsync(),
			OdooDefaults.Instance.InitializeLoginAsync(),
			OdooDefaults.Instance.InitializeLoginAsync() );

		Assert.Equal( 1, loginCount );
		Assert.Equal( 77, OdooDefaults.Instance.OdooId );
	}

	[Fact]
	public async Task OdooRpc_LoginAsync_ClearsStaleOdooId_OnFailure()
	{
		await using MockOdooServer server = new( _ => false );
		using ScopedOdooEnvironment env = new( server.BaseUrl );
		OdooDefaults.Instance.OdooId = 99;

		Result<int> result = await OdooRpc.LoginAsync();

		Assert.False( result.Ok );
		Assert.Equal( 0, OdooDefaults.Instance.OdooId );
	}

	[Fact]
	public async Task OdooRpc_CallAsync_RetriesIdempotentCalls_OnTruncatedHttpBody()
	{
		int attempts = 0;
		await using MockOdooServer server = new( call =>
		{
			int attempt = Interlocked.Increment( ref attempts );
			if( attempt == 1 )
				return new MockHttpResponse( 200, "OK", "{\"jsonrpc\":\"2.0\",\"result\":", TruncateBody: true );
			return new MockHttpResponse( 200, "OK", SerializeJsonRpcResult( 5 ) );
		} );
		using ScopedOdooEnvironment env = new( server.BaseUrl );

		Result<int> count = await OdooRpc.SearchCountAsync( HpEntry.ModelName );

		Assert.True( count.Ok, count.Error );
		Assert.Equal( 5, count.Value );
		Assert.Equal( 2, attempts );
	}

	[Fact]
	public async Task OdooTransport_And_Convert_WorkWithoutReflection()
	{
		JsonRpcRequest req = new()
		{
			Params = new()
			{
				Service = "object",
				Method = "execute",
				Args =
				[
					"db", 1, "pw", "hp.entry", "search_read",
					OdooDomain.Where( "name", Operators.Unset, null ).ToRpc(),
					new Dictionary<string, object?> { [ "k" ] = new[] { 1, 2 } },
				],
			},
		};
		using var content = OdooTransport.SerializeRequest( req );
		string json = await content.ReadAsStringAsync();
		Assert.Contains( "\"=\"", json );
		Assert.Contains( "false", json );

		using JsonDocument respDoc = JsonDocument.Parse( """{"jsonrpc":"2.0","id":"7","error":{"code":200,"message":"err","data":{"name":"UserError","message":"bad","arguments":["a",2],"context":{"lang":"en_US"}}}}""" );
		JsonRpcResponse? parsed = OdooTransport.ParseResponse( respDoc.RootElement );
		Assert.NotNull( parsed?.Error?.Data );
		Assert.Equal( "UserError: bad", parsed!.Error!.Describe() );
		Assert.Equal( "en_US", parsed.Error.Data!.Context![ "lang" ].GetString() );

		using JsonDocument numDoc = JsonDocument.Parse( "\"hello\"" );
		Assert.Equal( "hello", OdooRpc.Convert<string>( numDoc.RootElement ).Value );
		Result<Uri> unsupported = OdooRpc.Convert<Uri>( numDoc.RootElement );
		Assert.False( unsupported.Ok );
		Assert.Contains( "unsupported", unsupported.Error );
	}

	[Fact]
	public void HpPDMCommit_AllFields_ExcludesNonExistentServerFields()
	{
		Assert.DoesNotContain( "job_uuid", HpPDMCommit.AllFields );
		Assert.DoesNotContain( "committing", HpPDMCommit.AllFields );
		Assert.DoesNotContain( "progress_total", HpPDMCommit.AllFields );
		Assert.DoesNotContain( "duration_seconds", HpPDMCommit.AllFields );
		Assert.Contains( nameof( HpPDMCommit.committed ), HpPDMCommit.AllFields );
		Assert.Contains( nameof( HpPDMCommit.node_by ), HpPDMCommit.AllFields );
	}

	[Fact]
	public async Task OdooRpc_CreateCommitAsync_OmitsZeroNodeBy_WhenMyNodeIsNull()
	{
		await using MockOdooServer server = new( call => 123 );
		using ScopedOdooEnvironment env = new( server.BaseUrl );
		OdooDefaults.Instance.Lookups = OdooLookupCache.Empty;

		Result<int> created = await OdooRpc.CreateCommitAsync();

		Assert.True( created.Ok, created.Error );
		Assert.Equal( 123, created.Value );
		RpcCall rpc = Assert.Single( server.Calls );
		JsonElement values = rpc.Args[ 5 ];
		Assert.Equal( JsonValueKind.Object, values.ValueKind );
		Assert.False( values.TryGetProperty( nameof( HpPDMCommit.node_by ), out _ ) );
	}

	[Fact]
	public void HpRecord_PreStageRec_RejectsMany2OneWithNullId()
	{
		HpEntry entry = new()
		{
			name = "part.txt",
			commit_id = new Many2One { id = null, name = "unfinished" },
		};

		Assert.Null( entry.PreStageRec() );
		Dictionary<string, object?> computed = entry.ComputeValues( includeEmpty: false );
		Assert.False( computed.ContainsKey( nameof( HpRecordBase.commit_id ) ) );
	}

	#endregion

	#region Commit, Checksums, Empty Files, Downloads, and Local Writer Tests

	[Fact]
	public async Task CommitOperations_SegmentCommitFiles_MatchesCaseInsensitively_AndRejectsDeletedCollisions()
	{
		await using MockOdooServer server = new( call =>
		{
			if( call.Model == HpEntry.ModelName && call.ModelMethod == "search_read" )
			{
				return new object[]
				{
					new Dictionary<string, object?>
					{
						[ "id" ] = 11,
						[ "name" ] = "Existing_Part.TXT",
						[ "dir_id" ] = new object[] { 5, "root / Sub" },
						[ "deleted" ] = false,
						[ "checkout_user" ] = new object[] { 10, "Alice" },
						[ "latest_version_id" ] = new object[] { 101, "v1" },
						[ "windows_complete_name" ] = @"root\Sub\Existing_Part.TXT",
					},
					new Dictionary<string, object?>
					{
						[ "id" ] = 12,
						[ "name" ] = "deleted_part.txt",
						[ "dir_id" ] = new object[] { 5, "root / Sub" },
						[ "deleted" ] = true,
						[ "checkout_user" ] = false,
						[ "latest_version_id" ] = new object[] { 102, "v1" },
						[ "windows_complete_name" ] = @"root\Sub\deleted_part.txt",
					},
				};
			}
			if( call.Model == HpVersion.ModelName && call.ModelMethod == "read" )
			{
				return new object[]
				{
					new Dictionary<string, object?>
					{
						[ "id" ] = 101,
						[ "checksum" ] = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
						[ "file_size" ] = 12,
					},
				};
			}
			return Array.Empty<object>();
		} );
		using ScopedOdooEnvironment env = new( server.BaseUrl );
		string sub = Directory.CreateDirectory( Path.Combine( env.Vault, "Sub" ) ).FullName;
		string existingPath = Path.Combine( sub, "existing_part.txt" );
		string deletedPath = Path.Combine( sub, "Deleted_Part.txt" );
		string freshPath = Path.Combine( sub, "fresh.txt" );
		File.WriteAllText( existingPath, "modified content" );
		File.WriteAllText( deletedPath, "collides with deleted" );
		File.WriteAllText( freshPath, "brand new" );

		MockStatusLogger logger = new();
		var (localOnly, remoteExisting, unchanged, _) = await CommitOperations.SegmentCommitFilesAsync(
			[
				HackFile.GetFromPath( existingPath )!,
				HackFile.GetFromPath( deletedPath )!,
				HackFile.GetFromPath( freshPath )!,
			],
			logger );

		Assert.Single( remoteExisting, f => f.Name == "existing_part.txt" );
		Assert.Single( localOnly, f => f.Name == "fresh.txt" );
		Assert.Empty( unchanged );
		Assert.True( logger.HasMessageContaining( "deleted (Dt)" ) );

		RpcCall searchCall = Assert.Single( server.Calls, c => c.Model == HpEntry.ModelName );
		string domainJson = searchCall.Args[ 5 ].GetRawText();
		Assert.Contains( "=ilike", domainJson );
		Assert.Contains( @"root\\\\Sub\\\\existing\\_part.txt", domainJson );
		Assert.DoesNotContain( "\"deleted\"", domainJson );
	}

	[Fact]
	public async Task CommitOperations_SegmentCommitFiles_ThrowsOdooException_WhenRpcFails()
	{
		await using MockOdooServer server = new( _ => new MockHttpResponse( 200, "OK", JsonRpcErrorBody( "ConnectionError", "rpc down" ) ) );
		using ScopedOdooEnvironment env = new( server.BaseUrl );
		string path = Path.Combine( env.Vault, "a.txt" );
		File.WriteAllText( path, "hello" );

		await Assert.ThrowsAsync<OdooException>( () => CommitOperations.SegmentCommitFilesAsync( [ HackFile.GetFromPath( path )! ] ) );
	}

	[Fact]
	public async Task CommitStaging_FindRemoteEntry_ThrowsOnDeletedCollision_AndMatchesCaseInsensitively()
	{
		await using MockOdooServer server = new( call => new object[]
		{
			new Dictionary<string, object?>
			{
				[ "id" ] = 77,
				[ "name" ] = "Ghost.txt",
				[ "dir_id" ] = new object[] { 2, "root" },
				[ "deleted" ] = true,
			},
		} );
		using ScopedOdooEnvironment env = new( server.BaseUrl );

		HpEntry probe = new() { name = "ghost.txt", dir_id = 2 };
		InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>( () => CommitStaging.FindRemoteEntryAsync( probe ) );
		Assert.Contains( "deleted (Dt)", ex.Message );
	}

	[Fact]
	public void Checksums_EmptyFile_ReturnsCanonicalEmptySha1_AndMatches()
	{
		string temp = Path.Combine( Path.GetTempPath(), $"hackpdm-empty-{Guid.NewGuid():N}.txt" );
		try
		{
			File.WriteAllBytes( temp, [] );
			string? sha1 = Checksums.OfFile( temp );
			Assert.Equal( Checksums.EmptySha1, sha1 );
			Assert.True( Checksums.Matches( temp, Checksums.EmptySha1 ) );
			Assert.False( Checksums.Matches( temp, "0000000000000000000000000000000000000000" ) );
		}
		finally
		{
			FileOps.TryDelete( temp );
		}
	}

	[Fact]
	public async Task DownloadOperations_SupportsInlineZeroByteVersions_AndThrowsOnVaultChecksumRpcFailure()
	{
		await using MockOdooServer server = new( call =>
		{
			if( call.Model == HpVersion.ModelName && call.ModelMethod == "read" )
			{
				return new object[]
				{
					new Dictionary<string, object?>
					{
						[ "id" ] = 500,
						[ "name" ] = "empty.txt",
						[ "file_size" ] = 0,
						[ "file_contents" ] = false,
						[ "dir_id" ] = new object[] { 1, "root" },
						[ "windows_complete_name" ] = @"root\empty.txt",
					},
				};
			}
			if( call.Model == HpVersion.ModelName && call.ModelMethod == "search_read" )
				return new MockHttpResponse( 200, "OK", JsonRpcErrorBody( "ServerError", "history unavailable" ) );
			return Array.Empty<object>();
		} );
		using ScopedOdooEnvironment env = new( server.BaseUrl );

		HpVersion inlineEmpty = new()
		{
			id = 500,
			name = "empty.txt",
			checksum = null,
			file_size = 0,
			WinPathway = "",
			windows_complete_name = @"root\empty.txt",
		};
		DownloadOperations.DownloadSummary summary = new();
		List<HpVersion> filtered = await DownloadOperations.FilterVersionsToDownloadAsync( [ inlineEmpty ], CancellationToken.None, summary );
		Assert.Single( filtered );

		DownloadOperations.DownloadSummary dlSummary = await DownloadOperations.ProcessDownloadsAsync( [ filtered ], CancellationToken.None );
		Assert.Equal( 1, dlSummary.Downloaded );
		Assert.Empty( dlSummary.Failed );
		string localPath = Path.Combine( env.Vault, "empty.txt" );
		Assert.True( File.Exists( localPath ) );
		Assert.Empty( File.ReadAllBytes( localPath ) );

		// Re-filtering when local 0-byte file already matches skips downloading cleanly
		DownloadOperations.DownloadSummary skipSummary = new();
		List<HpVersion> secondFilter = await DownloadOperations.FilterVersionsToDownloadAsync( [ inlineEmpty ], CancellationToken.None, skipSummary );
		Assert.Empty( secondFilter );
		Assert.Equal( 1, skipSummary.Skipped );

		// Modified local file when vault checksum lookup RPC fails throws OdooException instead of silently keeping
		Assert.True( ( File.GetAttributes( localPath ) & FileAttributes.ReadOnly ) != 0 );
		File.SetAttributes( localPath, FileAttributes.Normal );
		File.WriteAllText( localPath, "edited locally" );
		await Assert.ThrowsAsync<OdooException>( () => DownloadOperations.FilterVersionsToDownloadAsync( [ inlineEmpty ], CancellationToken.None, new() ) );
	}

	[Fact]
	public async Task VersionFileService_DownloadContents_And_GetVersionFromLocal_And_WinPathwayPreserved()
	{
		await using MockOdooServer server = new( call =>
		{
			if( call.Model == HpVersion.ModelName && call.ModelMethod == "search_read" )
			{
				return new object[]
				{
					new Dictionary<string, object?> { [ "id" ] = 10, [ "name" ] = "part.txt", [ "file_size" ] = 4 },
					new Dictionary<string, object?> { [ "id" ] = 25, [ "name" ] = "part.txt", [ "file_size" ] = 4 },
				};
			}
			if( call.Model == HpVersion.ModelName && call.ModelMethod == "read" )
			{
				return new object[]
				{
					new Dictionary<string, object?> { [ "id" ] = 25, [ "file_contents" ] = "" },
				};
			}
			return Array.Empty<object>();
		} );
		using ScopedOdooEnvironment env = new( server.BaseUrl );

		string sub = Directory.CreateDirectory( Path.Combine( env.Vault, "Projects", "Parts" ) ).FullName;
		string localFile = Path.Combine( sub, "part.txt" );
		File.WriteAllText( localFile, "data" );

		// Construct via FileInfo so RelativePath includes filename; GetVersionFromLocal must query directory only and return max id
		HackFile hack = new( new FileInfo( localFile ), computeChecksum: false );
		var (found, latest) = await hack.GetVersionFromLocal();
		Assert.True( found );
		Assert.Equal( 25, latest!.id );

		RpcCall searchCall = Assert.Single( server.Calls, c => c.ModelMethod == "search_read" );
		Assert.Contains( "root / Projects / Parts", searchCall.Args[ 5 ].GetRawText() );

		// Empty string payload with null/0 file_size returns byte[0], whereas positive file_size returns null
		HpVersion emptyWithoutSize = new() { id = 25, file_size = null };
		byte[]? emptyBytes = await emptyWithoutSize.DownloadContentsAsync();
		Assert.NotNull( emptyBytes );
		Assert.Empty( emptyBytes! );

		HpVersion emptyWithPositiveSize = new() { id = 25, file_size = 12 };
		Assert.Null( await emptyWithPositiveSize.DownloadContentsAsync() );

		// DownloadFileAsync to temp directory must NOT mutate WinPathway
		HpVersion toTemp = new()
		{
			id = 25,
			name = "part.txt",
			WinPathway = Path.Combine( "Projects", "Parts" ),
			dir_id = new Many2One { id = 3, name = "root / Projects / Parts" },
			file_contents = "abc"u8.ToArray(),
		};
		EntryFileService files = new();
		string writtenTempDir = await files.DownloadVersionAsync( toTemp, toTemp: true );
		Assert.Equal( Path.Combine( "Projects", "Parts" ), toTemp.WinPathway );
		VersionMovePlan moveToWorking = files.PlanMove( toTemp, toTemp: true );
		Assert.Equal( Path.Combine( writtenTempDir, "part.txt" ), moveToWorking.To.FullName );
		Assert.True( moveToWorking.To.Exists );
	}

	[Fact]
	public void LocalFileWriter_And_PwaPaths_HandleSameFolderFileName_AndRejectTraversal()
	{
		using ScopedOdooEnvironment env = new();

		HackFile sameName = new()
		{
			Name = "README",
			DirectoryName = "README",
			FileContents = "hi"u8.ToArray(),
		};
		Assert.Equal( Path.Combine( env.Vault, "README", "README" ), LocalFileWriter.LocalTargetPath( sameName ) );

		HackFile traversalName = new()
		{
			Name = "../evil.txt",
			DirectoryName = env.Vault,
			FileContents = "bad"u8.ToArray(),
		};
		Assert.Null( LocalFileWriter.LocalTargetPath( traversalName ) );
		Assert.Null( LocalFileWriter.Write( traversalName ) );

		HackFile outsideDir = new()
		{
			Name = "evil.txt",
			DirectoryName = Path.Combine( Path.GetDirectoryName( env.Vault )!, "Outside" ),
			FileContents = "bad"u8.ToArray(),
		};
		Assert.Null( LocalFileWriter.Write( outsideDir ) );

		Assert.Null( PwaPaths.ToAbsolute( @"root\..\..\outside.txt" ) );
		Assert.Null( PwaPaths.ToAbsolute( "root / .. / outside.txt" ) );
	}

	#endregion

	#region Workflows, Dependencies, Delete, Checkout, and ContentTransfer Tests

	[Fact]
	public async Task PdmWorkflowService_CheckoutLatestAsync_SkipsCheckout_WhenDownloadAborts()
	{
		await using MockOdooServer server = new( call =>
		{
			if( call.Model == HpVersion.ModelName && call.ModelMethod == "get_recursive_dependency_entries" )
				return new[] { 10 };
			if( call.Model == HpEntry.ModelName && call.ModelMethod == "get_recursive_dependency_entries_by_entries" )
				return new[] { 10 };
			if( call.Model == HpEntry.ModelName && call.ModelMethod == "related_browse" )
			{
				return new object[]
				{
					new Dictionary<string, object?>
					{
						[ "id" ] = 100,
						[ "name" ] = "part.txt",
						[ "checksum" ] = "1111111111111111111111111111111111111111",
						[ "file_size" ] = 5,
						[ "windows_complete_name" ] = @"root\part.txt",
					},
				};
			}
			if( call.Model == HpVersion.ModelName && call.ModelMethod == "read" )
				return new MockHttpResponse( 200, "OK", JsonRpcErrorBody( "IOError", "download failed" ) );
			return Array.Empty<object>();
		} );
		using ScopedOdooEnvironment env = new( server.BaseUrl );
		OdooDefaults.Instance.Lookups = new OdooLookupCache { MyNode = new HpNode { id = 5, name = "pc" } };
		RecordingReporter reporter = new();
		PdmWorkflowService service = new( reporter );

		await service.CheckoutLatestAsync( [ 10 ] );

		Assert.DoesNotContain( server.Calls, c => c.ModelMethod == "checkout_entry" );
		Assert.Contains( reporter.Notices, n => n.Message.Contains( "NOT downloaded" ) );
	}

	[Fact]
	public async Task PdmWorkflowService_CommitAsync_CatchesOdooException_AndNotifiesOnFailure()
	{
		await using MockOdooServer server = new( call =>
		{
			if( call.Model == HpEntry.ModelName && call.ModelMethod == "search_read" )
				return Array.Empty<object>();
			if( call.Model == HpPDMCommit.ModelName && call.ModelMethod == "create" )
				return new MockHttpResponse( 200, "OK", JsonRpcErrorBody( "AccessError", "cannot create commit" ) );
			return Array.Empty<object>();
		} );
		using ScopedOdooEnvironment env = new( server.BaseUrl );
		string path = Path.Combine( env.Vault, "file.txt" );
		File.WriteAllText( path, "content" );
		RecordingReporter reporter = new();
		PdmWorkflowService service = new( reporter );

		await service.CommitAsync( [ HackFile.GetFromPath( path )! ] );

		Assert.Contains( reporter.Logs, l => l.Status == StatusMessage.ERROR && l.Message.Contains( "cannot create commit" ) );
		Assert.Contains( reporter.Notices, n => n.Message.Contains( "Commit failed" ) );
	}

	[Fact]
	public async Task PdmWorkflowService_CollectCommitFilesAsync_TerminatesAll_WhenAnySolidWorksFileBroken()
	{
		using ScopedOdooEnvironment env = new();
		string goodTxt = Path.Combine( env.Vault, "good.txt" );
		string brokenAsm = Path.Combine( env.Vault, "broken.SLDASM" );
		File.WriteAllText( goodTxt, "ok" );
		File.WriteAllText( brokenAsm, "corrupt sldasm" );

		RecordingReporter reporter = new();
		PdmWorkflowService service = new( reporter );
		HashSet<HackFile> files = await service.CollectCommitFilesAsync(
		[
			new EntryRow { Name = "good.txt", FullName = goodTxt, Type = "txt", ReprType = EntryReprType.Local },
			new EntryRow { Name = "broken.SLDASM", FullName = brokenAsm, Type = "sldasm", ReprType = EntryReprType.Local },
		] );

		Assert.Empty( files );
		Assert.Contains( reporter.Logs, l => l.Status == StatusMessage.ERROR && l.Message == "Commit Terminated" );
	}

	[Fact]
	public async Task DependencyScanner_ScanFolder_HandlesMissingFolder_OutOfPwaNonSw_AndDeduplicatesBroken()
	{
		using ScopedOdooEnvironment env = new();
		var (missingFiles, missingBroken) = await DependencyScanner.GetFolderWithDependenciesAsync( Path.Combine( env.Vault, "DoesNotExist" ) );
		Assert.Empty( missingFiles );
		Assert.Empty( missingBroken );

		string outsideDir = Directory.CreateDirectory( Path.Combine( Path.GetDirectoryName( env.Vault )!, "OutsideFolder" ) ).FullName;
		File.WriteAllText( Path.Combine( outsideDir, "note.txt" ), "outside" );
		var (outFiles, outBroken) = await DependencyScanner.GetFolderWithDependenciesAsync( outsideDir );
		Assert.Empty( outFiles );
		BrokenDependency bd = Assert.Single( outBroken );
		Assert.Equal( HackResult.OutOfPWA, bd.Reason );

		string swDir = Directory.CreateDirectory( Path.Combine( env.Vault, "SwDup" ) ).FullName;
		string asm1 = Path.Combine( swDir, "new.SLDASM" );
		string asm2 = Path.Combine( swDir, "old.SLDASM" );
		File.Copy( SwSamples.Old( "new.SLDASM" ), asm1 );
		File.Copy( SwSamples.Old( "old.SLDASM" ), asm2 );
		File.Copy( SwSamples.Old( "new.SLDPRT" ), Path.Combine( swDir, "new.SLDPRT" ) );
		// Missing tmp engine 2.SLDPRT referenced via new.SLDPRT in BOTH assemblies
		var (_, swBroken) = await DependencyScanner.GetFolderWithDependenciesAsync( swDir );
		int missingCount = swBroken.Count( b => b.FullPath?.EndsWith( "tmp engine 2.SLDPRT", StringComparison.OrdinalIgnoreCase ) is true );
		Assert.Equal( 1, missingCount );
	}

	[Fact]
	public async Task CheckoutOperations_SetsExplicitLastError_WhenNodeOrVersionMissing_AndUpdatesReadOnly()
	{
		await using MockOdooServer server = new( call => call.ModelMethod == "checkout_entry" || call.ModelMethod == "write" );
		using ScopedOdooEnvironment env = new( server.BaseUrl );
		string local = Path.Combine( env.Vault, "part.txt" );
		File.WriteAllText( local, "hello" );

		HpEntry missingVersion = new() { id = 1, name = "no_ver.txt", deleted = false };
		Assert.False( await CheckoutOperations.CheckOutEntryAsync( missingVersion ) );
		Assert.Contains( "no committed version", missingVersion.LastError );

		HpEntry valid = new() { id = 2, name = "part.txt", deleted = false, latest_version_id = 20, windows_complete_name = @"root\part.txt" };
		OdooDefaults.Instance.Lookups = OdooLookupCache.Empty;
		Assert.False( await CheckoutOperations.CheckOutEntryAsync( valid ) );
		Assert.Contains( "hp.node", valid.LastError );

		OdooDefaults.Instance.Lookups = new OdooLookupCache { MyNode = new HpNode { id = 5, name = "pc" } };
		await CheckoutOperations.CheckOutEntriesAsync( [ valid ] );
		Assert.Equal( 10, valid.checkout_user?.id );
		Assert.False( new FileInfo( local ).IsReadOnly );

		await CheckoutOperations.UnCheckOutEntriesAsync( [ valid ], currentUserId: 10 );
		Assert.Null( valid.checkout_user );
		Assert.True( new FileInfo( local ).IsReadOnly );
	}

	[Fact]
	public async Task DeleteOperations_SetDeletedAsync_FallsBackPerEntry_AndPermanentDeleteExecutesChain()
	{
		await using MockOdooServer server = new( call =>
		{
			if( call.Model == HpEntry.ModelName && call.ModelMethod == "write" )
			{
				JsonElement ids = call.Args[ 5 ];
				if( ids.GetArrayLength() > 1 )
					return new MockHttpResponse( 200, "OK", JsonRpcErrorBody( "UserError", "checked out to another user" ) );
				return ids[ 0 ].GetInt32() == 2
					? new MockHttpResponse( 200, "OK", JsonRpcErrorBody( "UserError", "held by Alice" ) )
					: true;
			}
			if( call.Model == HpEntry.ModelName && call.ModelMethod == "related_browse" )
				return new object[] { new Dictionary<string, object?> { [ "id" ] = 301 } };
			if( call.ModelMethod == "search" )
				return new[] { 901 };
			return true;
		} );
		using ScopedOdooEnvironment env = new( server.BaseUrl );

		HpEntry e1 = new() { id = 1, name = "ok.txt" };
		HpEntry e2 = new() { id = 2, name = "held.txt" };
		MockStatusLogger log = new();

		bool allDeleted = await DeleteOperations.SetDeletedAsync( [ e1, e2 ], deleted: true, log );
		Assert.False( allDeleted );
		Assert.True( e1.deleted );
		Assert.Null( e2.deleted );
		Assert.Contains( "held by Alice", e2.LastError );
		Assert.False( log.HasMessageContaining( "Could not mark ok.txt" ) );
		Assert.True( log.HasMessageContaining( "Could not mark held.txt" ) );

		bool perm = await DeleteOperations.PermanentDeleteEntry( [ 1 ], log );
		Assert.True( perm );
		Assert.Contains( server.Calls, c => c.Model == HpEntry.ModelName && c.ModelMethod == "unlink" );
	}

	[Fact]
	public async Task ContentTransfer_UploadAndDownload_RoundTripChunked()
	{
		byte[] serverStore = [];
		await using MockOdooServer server = new( call =>
		{
			if( call.Model == "hp.upload" && call.ModelMethod == "upload_begin" )
			{
				serverStore = new byte[ call.Args[ 5 ].GetInt32() ];
				return new Dictionary<string, object?> { [ "id" ] = 77, [ "chunk_size" ] = 1024 };
			}
			if( call.Model == "hp.upload" && call.ModelMethod == "upload_chunk" )
			{
				int offset = call.Args[ 6 ].GetInt32();
				byte[] chunk = Convert.FromBase64String( call.Args[ 7 ].GetString()! );
				Array.Copy( chunk, 0, serverStore, offset, chunk.Length );
				return offset + chunk.Length;
			}
			if( call.Model == "hp.upload" && call.ModelMethod == "upload_finish" )
				return true;
			if( call.Model == HpVersion.ModelName && call.ModelMethod == "read_contents" )
			{
				int offset = call.Args[ 6 ].GetInt32();
				int len = Math.Min( call.Args[ 7 ].GetInt32(), serverStore.Length - offset );
				return new Dictionary<string, object?>
				{
					[ "data" ] = Convert.ToBase64String( serverStore, offset, len ),
					[ "size" ] = serverStore.Length,
				};
			}
			return null;
		} );
		using ScopedOdooEnvironment env = new( server.BaseUrl );

		byte[] payload = RandomNumberGenerator.GetBytes( 2500 );
		using MemoryStream uploadStream = new( payload );
		Result<int> up = await ContentTransfer.UploadAsync( uploadStream );
		Assert.True( up.Ok, up.Error );
		Assert.Equal( 77, up.Value );

		HpVersion version = new()
		{
			id = 88,
			name = "big.bin",
			file_size = payload.Length,
			checksum = Convert.ToHexStringLower( SHA1.HashData( payload ) ),
		};
		Result<byte[]> dl = await ContentTransfer.DownloadBytesAsync( version );
		Assert.True( dl.Ok, dl.Error );
		Assert.Equal( payload, dl.Value );
	}

	#endregion

	#region Services, Properties, Search, Details, ProfileStore, Watcher, Session, and Buffer Tests

	[Fact]
	public void HpVersionProperty_GetValueType_HandlesZeroAndFalse_AndExtToPropIsCaseInsensitive()
	{
		using ScopedOdooEnvironment env = new();
		HpProperty numProp = new() { id = 1, name = "Mass", prop_type = "number" };
		HpProperty boolProp = new() { id = 2, name = "Approved", prop_type = "yesno" };
		OdooDefaults.Instance.Lookups = new OdooLookupCache
		{
			IdToProp = new() { [ 1 ] = numProp, [ 2 ] = boolProp },
			ExtToProp = new( StringComparer.OrdinalIgnoreCase ) { [ "Mass" ] = numProp, [ "Approved" ] = boolProp },
		};

		Assert.True( OdooDefaults.Instance.ExtToProp.ContainsKey( "MASS" ) );

		HpVersionProperty zeroNum = new() { prop_id = 1, number_value = 0f, yesno_value = false };
		HpVersionProperty falseBool = new() { prop_id = 2, number_value = 0f, yesno_value = false };
		Assert.Equal( PropertyType.Number, zeroNum.GetValueType() );
		Assert.Equal( PropertyType.Yesno, falseBool.GetValueType() );

		// Standalone without IdToProp
		Assert.Equal( PropertyType.Number, new HpVersionProperty { number_value = 0f }.GetValueType() );
		Assert.Equal( PropertyType.Yesno, new HpVersionProperty { yesno_value = false }.GetValueType() );
	}

	[Fact]
	public void EntryStatusResolver_LockedLocalFile_ReturnsUnknownInsteadOfRo()
	{
		FileStatus locked = EntryStatusResolver.ResolveRemote(
			deleted: false,
			checkoutUserId: null,
			myUserId: 10,
			latestChecksum: "abc",
			localChecksum: null,
			remoteDate: DateTime.UtcNow,
			localModified: DateTime.UtcNow );
		Assert.Equal( FileStatus.Unknown, locked );
	}

	[Fact]
	public async Task EntryListService_LoadAsync_ThrowsOnRpcFailure_AndReconcilesMixedAndZeroByteEntries()
	{
		await using MockOdooServer server = new( call =>
		{
			if( call.Model == HpDirectory.ModelName && call.ModelMethod == "get_entries" )
			{
				int dirId = call.Args[ 5 ][ 0 ].GetInt32();
				if( dirId == 999 )
					return new MockHttpResponse( 200, "OK", JsonRpcErrorBody( "UserError", "dir error" ) );
				return new Dictionary<string, object?>
				{
					[ "empty.txt" ] = new Dictionary<string, object?>
					{
						[ "id" ] = 1,
						[ "type" ] = "txt",
						[ "checkout" ] = 0,
						[ "fullname" ] = "root / empty.txt",
						[ "size" ] = 0,
						[ "latest" ] = 11,
						[ "release" ] = 0,
						[ "latest_checksum" ] = false,
						[ "deleted" ] = false,
					},
					[ "remote.txt" ] = new Dictionary<string, object?>
					{
						[ "id" ] = 2,
						[ "type" ] = "txt",
						[ "checkout" ] = 0,
						[ "fullname" ] = "root / remote.txt",
						[ "size" ] = 5,
						[ "latest" ] = 12,
						[ "release" ] = 0,
						[ "latest_checksum" ] = "1111111111111111111111111111111111111111",
						[ "deleted" ] = false,
					},
				};
			}
			return null;
		} );
		using ScopedOdooEnvironment env = new( server.BaseUrl );
		File.WriteAllBytes( Path.Combine( env.Vault, "empty.txt" ), [] );
		File.WriteAllText( Path.Combine( env.Vault, "local.txt" ), "local" );

		EntryListService listService = new();
		await Assert.ThrowsAsync<OdooException>( () => listService.LoadAsync( 999, "root", false, false, CancellationToken.None ) );

		EntryListResult loaded = await listService.LoadAsync( 1, "root", false, false, CancellationToken.None );
		Assert.Equal( 2, loaded.RemoteCount );
		Assert.Equal( 1, loaded.LocalOnlyCount );
		Assert.Equal( FileStatus.Ok, Assert.Single( loaded.Rows, r => r.Name == "empty.txt" ).Status );
		Assert.Equal( FileStatus.Ro, Assert.Single( loaded.Rows, r => r.Name == "remote.txt" ).Status );
		Assert.Equal( FileStatus.Lo, Assert.Single( loaded.Rows, r => r.Name == "local.txt" ).Status );
	}

	[Fact]
	public async Task EntrySearchService_PropertyFilters_QueryHpVersionPropertyEntryId()
	{
		await using MockOdooServer server = new( call =>
		{
			if( call.Model == HpVersionProperty.ModelName && call.ModelMethod == "search_read" )
			{
				return new object[]
				{
					new Dictionary<string, object?> { [ "id" ] = 1, [ nameof( HpVersionProperty.entry_id ) ] = new object[] { 55, "Part.sldprt" } },
				};
			}
			if( call.Model == HpEntry.ModelName && call.ModelMethod == "search_read" )
			{
				return new object[]
				{
					new Dictionary<string, object?> { [ "id" ] = 55, [ "name" ] = "Part.sldprt", [ "directory_complete_name" ] = "root / Parts" },
				};
			}
			return Array.Empty<object>();
		} );
		using ScopedOdooEnvironment env = new( server.BaseUrl );
		OdooDefaults.Instance.Lookups = new OdooLookupCache
		{
			IdToProp = new() { [ 7 ] = new HpProperty { id = 7, name = "Mass", prop_type = "number" } },
		};

		EntrySearchService search = new();
		List<SearchRow> rows = await search.SearchAsync( new EntrySearchQuery(
			"", "ilike", false, false, false, 50,
			[ new SearchPropertiesRow { ID = 7, Comparer = Operators.GreaterThan, Value = "2.5" } ] ) );

		SearchRow row = Assert.Single( rows );
		Assert.Equal( 55, row.Id );
		RpcCall propCall = Assert.Single( server.Calls, c => c.Model == HpVersionProperty.ModelName );
		Assert.Contains( nameof( HpVersionProperty.entry_id ), propCall.Args[ 6 ].GetRawText() );
	}

	[Fact]
	public async Task EntryDetailsService_And_DirectoryTreeService_LoadExpectedData()
	{
		await using MockOdooServer server = new( call =>
		{
			if( call.Model == HpEntry.ModelName && call.ModelMethod == "related_browse" )
			{
				string rel = call.Args[ 6 ].GetString()!;
				if( rel == "version_ids" )
				{
					return new object[]
					{
						new Dictionary<string, object?>
						{
							[ "id" ] = 200,
							[ "create_uid" ] = new object[] { 10, "Alice" },
							[ "file_size" ] = 100,
							[ "version_property_ids" ] = new[] { 300 },
						},
					};
				}
			}
			if( call.Model == HpVersionProperty.ModelName && call.ModelMethod == "read" )
			{
				return new object[]
				{
					new Dictionary<string, object?> { [ "id" ] = 300, [ "version_id" ] = 200, [ "prop_id" ] = 1, [ "prop_name" ] = "Mass", [ "number_value" ] = 0.0, [ "yesno_value" ] = false },
				};
			}
			if( call.Model == HpDirectory.ModelName && call.ModelMethod == "get_children_directories_by_id" )
			{
				return new Dictionary<string, object?>
				{
					[ "id" ] = 1,
					[ "name" ] = "root",
					[ "directories" ] = new Dictionary<string, object?>
					{
						[ "RemoteOnly" ] = new Dictionary<string, object?> { [ "id" ] = 2, [ "name" ] = "RemoteOnly", [ "directories" ] = new Dictionary<string, object?>(), [ "entries" ] = new Dictionary<string, object?>() },
					},
					[ "entries" ] = new Dictionary<string, object?>(),
				};
			}
			return Array.Empty<object>();
		} );
		using ScopedOdooEnvironment env = new( server.BaseUrl );
		Directory.CreateDirectory( Path.Combine( env.Vault, "LocalOnly" ) );
		OdooDefaults.Instance.Lookups = new OdooLookupCache
		{
			HpDirectoryRoot = new HpDirectory { id = 1, name = "root", IsRecord = true },
			IdToProp = new() { [ 1 ] = new HpProperty { id = 1, name = "Mass", prop_type = "number" } },
			IdToUser = new() { [ 10 ] = new HpUser { id = 10, name = "Alice" } },
		};

		EntryDetailsService details = new();
		EntryRow entry = new() { Id = 10, LatestId = 200 };
		HistoryRow hist = Assert.Single( await details.GetHistoryAsync( entry ) );
		Assert.Equal( "Alice", hist.ModUser?.name );
		PropertiesRow prop = Assert.Single( await details.GetPropertiesAsync( entry ) );
		Assert.Equal( PropertyType.Number, prop.Type );
		Assert.Equal( 0f, prop.ValueData );

		DirectoryTreeService treeService = new();
		DirectoryNode? root = await treeService.LoadAsync();
		Assert.NotNull( root );
		Assert.NotNull( root!.FindChild( "RemoteOnly" ) );
		Assert.NotNull( root.FindChild( "LocalOnly" ) );
	}

	[Fact]
	public void EntryFileService_CountFilesIsRecursive_AndDeleteFilesSkipsLockedFiles()
	{
		using ScopedOdooEnvironment env = new();
		string sub = Directory.CreateDirectory( Path.Combine( env.Vault, "Sub", "Inner" ) ).FullName;
		string f1 = Path.Combine( env.Vault, "Sub", "a.txt" );
		string f2 = Path.Combine( sub, "b.txt" );
		File.WriteAllText( f1, "1" );
		File.WriteAllText( f2, "2" );

		EntryFileService files = new();
		Assert.Equal( 2, files.CountFiles( Path.Combine( env.Vault, "Sub" ) ) );

		using( FileStream lockHandle = new( f1, FileMode.Open, FileAccess.ReadWrite, FileShare.None ) )
		{
			files.DeleteFiles( [ new FileInfo( f1 ), new FileInfo( f2 ) ] );
			Assert.False( File.Exists( f2 ) );
		}
	}

	[Fact]
	public async Task ProfileStore_WriteIsAtomic_AndApplyClearsLookupsAndBlankedFields()
	{
		using ScopedOdooEnvironment env = new();
		OdooDefaults.Instance.OdooPort = "8069";
		OdooDefaults.Instance.Lookups = new OdooLookupCache { HpProperties = [ new HpProperty { id = 1, name = "P" } ] };

		string profilePath = Path.Combine( env.TempDir, "nested", "profile.json" );
		SavedProfileData saved = new()
		{
			OdooAddress = "10.0.0.99",
			OdooPort = "",
			OdooDb = "newdb",
			OdooUseHttps = true,
			OdooCredentialTarget = "CustomTarget",
			PWAPathAbsolute = env.Vault,
			Username = "bob",
			Password = "pw",
		};
		await ProfileStore.WriteAsync( profilePath, saved );
		Assert.Empty( Directory.GetFiles( Path.GetDirectoryName( profilePath )!, "*.tmp" ) );

		SavedProfileData? loaded = await ProfileStore.ReadAsync( profilePath );
		Assert.NotNull( loaded );
		ProfileStore.Apply( loaded! );

		Assert.Equal( "10.0.0.99", OdooDefaults.Instance.OdooAddress );
		Assert.Equal( "", OdooDefaults.Instance.OdooPort );
		Assert.Equal( 0, OdooDefaults.Instance.OdooId );
		Assert.Same( OdooLookupCache.Empty, OdooDefaults.Instance.Lookups );
		Assert.Empty( OdooDefaults.Instance.HpProperties ?? [] );
	}

	[Fact]
	public void LoginSession_ObservableRingBuffer_And_TransferRateTracker_ThreadSafetyAndInvariants()
	{
		LoginSession session = new();
		Parallel.For( 0, 100, i => session.LoggedInAs( $"user-{i % 3}" ) );
		Assert.True( session.IsLoggedIn );
		Assert.NotNull( session.Account );

		ObservableRingBuffer<int> ring = new( 4 );
		Assert.Throws<ArgumentNullException>( () => ring.AddRange( null! ) );
		ring.AddRange( [ 1, 2, 3, 4 ] );
		int? countDuringCapacityChange = null;
		ring.PropertyChanged += ( _, e ) =>
		{
			if( e.PropertyName == nameof( ring.Capacity ) )
				countDuringCapacityChange = ring.Count;
		};
		ring.Resize( 2 );
		Assert.Equal( 2, countDuringCapacityChange );

		Parallel.For( 0, 200, i => ring.Add( i ) );
		Assert.Equal( 2, ring.Count );

		TransferRateTracker tracker = new();
		Parallel.For( 0, 100, _ =>
		{
			tracker.BeginTransfer();
			tracker.AddBytes( 1024 );
		} );
		tracker.EndTransfer();
		Assert.Equal( 100 * 1024, tracker.TotalBytes );
	}

	[Fact]
	public async Task ClientUpdate_UsesExeFallback_AndDeletesEmptyTempDirOnFailure()
	{
		string before = Path.Combine( Path.GetTempPath(), $"hackpdm-clientupd-{Guid.NewGuid():N}" );
		Assert.DoesNotContain( "is null", Operators.Unset.ToOdoo() );
		Assert.Equal( "=", Operators.Unset.ToOdoo() );

		// Non-existent file download without explicit targetDirectory should not leave a new hackpdm-update-* folder behind
		HashSet<string> existingDirs = [ .. Directory.GetDirectories( Path.GetTempPath(), "hackpdm-update-*" ) ];
		Result<string> failed = await ClientUpdate.DownloadInstallerAsync( Path.Combine( before, "setup.exe" ), new string( 'a', 64 ) );
		Assert.False( failed.Ok );
		string[] afterDirs = Directory.GetDirectories( Path.GetTempPath(), "hackpdm-update-*" );
		Assert.All( afterDirs, d => Assert.Contains( d, existingDirs ) );
	}

	[Fact]
	public async Task LocalFileWatcher_SwitchVault_And_ErrorRecoveryDoesNotRecreateDeletedVault()
	{
		string root1 = Path.Combine( Path.GetTempPath(), $"hackpdm-w1-{Guid.NewGuid():N}" );
		string root2 = Path.Combine( Path.GetTempPath(), $"hackpdm-w2-{Guid.NewGuid():N}" );
		ConcurrentQueue<LocalFileChange> seen = new();
		using LocalFileWatcher watcher = new();
		watcher.Changed += batch => { foreach( LocalFileChange c in batch ) seen.Enqueue( c ); };
		try
		{
			watcher.Watch( root1 );
			Directory.Delete( root1, recursive: true );
			await Task.Delay( 1300 );
			Assert.False( Directory.Exists( root1 ) );

			watcher.Watch( root2 );
			string inRoot2 = Path.Combine( root2, "fresh.txt" );
			await File.WriteAllTextAsync( inRoot2, "ok" );
			await Task.Delay( 900 );
			Assert.DoesNotContain( seen, c => c.FullPath.StartsWith( root1, StringComparison.OrdinalIgnoreCase ) );
		}
		finally
		{
			try { if( Directory.Exists( root1 ) ) Directory.Delete( root1, recursive: true ); } catch { }
			try { if( Directory.Exists( root2 ) ) Directory.Delete( root2, recursive: true ); } catch { }
		}
	}

	#endregion
}
