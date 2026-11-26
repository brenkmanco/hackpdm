using System.Net;
using System.Text;

using WyrmpdmSync.Penpot;
using WyrmpdmSync.Sync;

using Xunit;

namespace WyrmpdmSync.Tests;

public class BasicsTests
{
	[Theory]
	[InlineData( 0xFF485E92u, "#485E92" )]
	[InlineData( 0x80000000u, "#00000080" )]
	public void Hex_WritesPenpotsForm( uint argb, string hex ) => Assert.Equal( hex, Hex.FromArgb( argb ) );

	[Theory]
	[InlineData( "#485e92", "#485E92" )]
	[InlineData( "485E92", "#485E92" )]
	[InlineData( "#abc", "#AABBCC" )]
	[InlineData( "#00000080", "#00000080" )]
	[InlineData( "#485E92FF", "#485E92" )]
	[InlineData( "{Light.Primary}", null )]
	[InlineData( "", null )]
	public void Hex_Normalizes( string text, string? expected ) => Assert.Equal( expected, Hex.Normalize( text ) );

	[Theory]
	[InlineData( "#111111", "#111111", null, (int)SyncState.InSync )]
	[InlineData( "#222222", "#111111", "#111111", (int)SyncState.ChangedInPenpot )]
	[InlineData( "#111111", "#222222", "#111111", (int)SyncState.ChangedInCode )]
	[InlineData( "#222222", "#333333", "#111111", (int)SyncState.Conflict )]
	[InlineData( "#222222", "#333333", null, (int)SyncState.Unlinked )]
	[InlineData( null, "#111111", "#111111", (int)SyncState.MissingInPenpot )]
	[InlineData( "#111111", null, "#111111", (int)SyncState.MissingInCode )]
	public void ThreeWay_Classifies( string? penpot, string? code, string? synced, int expected )
		=> Assert.Equal( (SyncState)expected, ThreeWay.Compare( penpot, code, synced ) );

	[Fact]
	public async Task Client_PostsJson_WithTheToken_AndNeverEchoesItInErrors()
	{
		const string secret = "test-token-not-real";
		Recorder handler = new( HttpStatusCode.Unauthorized, """{"type":"authentication"}""" );
		using PenpotClient client = new( "https://penpot.example", secret, handler );

		SyncException ex = await Assert.ThrowsAsync<SyncException>( () => client.GetFileAsync( Guid.Empty ) );

		Assert.Equal( HttpMethod.Post, handler.Request!.Method );
		Assert.Equal( "https://penpot.example/api/main/methods/get-file", handler.Request.RequestUri!.ToString() );
		Assert.Equal( $"Token {secret}", handler.Request.Headers.Authorization!.ToString() );
		Assert.Contains( "\"id\":\"00000000-0000-0000-0000-000000000000\"", handler.Body );
		Assert.DoesNotContain( secret, ex.Message );
		Assert.Contains( "access token", ex.Message );
	}

	[Fact]
	public void RepoPaths_FindsTheRepository()
	{
		RepoPaths paths = RepoPaths.Find( AppContext.BaseDirectory );
		Assert.True( File.Exists( Path.Combine( paths.Root, "HackPDM.sln" ) ) );
		Assert.Equal( "design/penpot-sync.json", paths.Relative( paths.Manifest ) );
	}

	private sealed class Recorder( HttpStatusCode status, string body ) : HttpMessageHandler
	{
		public HttpRequestMessage? Request { get; private set; }
		public string Body { get; private set; } = "";

		protected override async Task<HttpResponseMessage> SendAsync( HttpRequestMessage request, CancellationToken ct )
		{
			Request = request;
			Body = request.Content is null ? "" : await request.Content.ReadAsStringAsync( ct );
			return new HttpResponseMessage( status ) { Content = new StringContent( body, Encoding.UTF8, "application/json" ) };
		}
	}
}
