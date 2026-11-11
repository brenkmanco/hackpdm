using System.Net;
using System.Net.Sockets;
using System.Text;

using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.RPC;

namespace HackPDM.Tests.Operations;

/// <summary>
/// Odoo's built-in Werkzeug server answers HTTP/1.0 and closes the socket after each response.
/// SocketsHttpHandler still pooled those connections, so a call that reused one before the
/// server's FIN arrived failed with "The response ended prematurely" (the intermittent
/// "An error occurred while sending the request" seen in the live tests).
/// </summary>
public class RpcConnectionReuseTests
{
	/// <summary>
	/// Fake HTTP/1.0 server: reads one request per connection, answers it, and closes the socket
	/// only after a delay, without reading anything more. That delay makes the race certain:
	/// a request sent on a reused connection never gets an answer.
	/// </summary>
	private sealed class Http10Server : IAsyncDisposable
	{
		private readonly TcpListener _listener = new( IPAddress.Loopback, 0 );
		private readonly CancellationTokenSource _stop = new();
		private readonly Task _loop;
		public int Connections;

		public Http10Server()
		{
			_listener.Start();
			_loop = AcceptLoop();
		}

		public string Url => $"http://127.0.0.1:{( (IPEndPoint)_listener.LocalEndpoint ).Port}/jsonrpc";

		private async Task AcceptLoop()
		{
			try
			{
				while( true )
				{
					var client = await _listener.AcceptTcpClientAsync( _stop.Token );
					Interlocked.Increment( ref Connections );
					_ = Serve( client );
				}
			}
			catch( OperationCanceledException ) { }
			catch( SocketException ) { }
		}

		private static async Task Serve( TcpClient client )
		{
			using var _ = client;
			var stream = client.GetStream();
			var buffer = new byte[ 8192 ];
			var request = new StringBuilder();
			int contentLength = -1, headerEnd = -1;
			while( headerEnd < 0 || request.Length < headerEnd + contentLength )
			{
				int n = await stream.ReadAsync( buffer );
				if( n == 0 )
					return;
				request.Append( Encoding.ASCII.GetString( buffer, 0, n ) );
				string text = request.ToString();
				if( headerEnd < 0 && text.IndexOf( "\r\n\r\n", StringComparison.Ordinal ) is int end and >= 0 )
				{
					headerEnd = end + 4;
					var match = System.Text.RegularExpressions.Regex.Match( text[ ..end ], @"(?im)^content-length:\s*(\d+)" );
					contentLength = match.Success ? int.Parse( match.Groups[ 1 ].Value ) : 0;
				}
			}

			byte[] body = Encoding.UTF8.GetBytes( """{"jsonrpc":"2.0","id":null,"result":42}""" );
			byte[] head = Encoding.ASCII.GetBytes(
				$"HTTP/1.0 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\n\r\n" );
			await stream.WriteAsync( head );
			await stream.WriteAsync( body );
			await stream.FlushAsync();
			await Task.Delay( 150 ); // late FIN, as under load
		}

		public async ValueTask DisposeAsync()
		{
			_stop.Cancel();
			_listener.Stop();
			await _loop;
		}
	}

	[Fact]
	public async Task SequentialCalls_ToAnHttp10Server_AllSucceed_OnFreshConnections()
	{
		await using var server = new Http10Server();
		const int calls = 6;

		for( int i = 0; i < calls; i++ )
		{
			var request = new JsonRpcRequest { Params = new() { Service = "common", Method = "version", Args = [] } };
			var response = await request.SendJsonAsync( server.Url, timeout: 5_000 );
			Assert.NotNull( response );
			Assert.Equal( 42, response!.Result?.GetInt32() );
		}

		Assert.True( ExtensionOdoo.ServerClosesConnections( server.Url ) );
		Assert.Equal( calls, server.Connections );
	}
}
