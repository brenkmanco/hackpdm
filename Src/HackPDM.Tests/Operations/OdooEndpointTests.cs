using HackPDM.Infrastructure.Odoo;
using Xunit;

namespace HackPDM.Tests.Operations;

/// <summary>The server URL is built with HTTPS by default, and the password is never sent unencrypted to another computer unless the user chose to.</summary>
public class OdooEndpointTests
{
	[Theory]
	[InlineData( "pdm.example.com", "", true, "https://pdm.example.com" )]
	[InlineData( "pdm.example.com", "8443", true, "https://pdm.example.com:8443" )]
	[InlineData( "10.0.0.68", "8069", false, "http://10.0.0.68:8069" )]
	[InlineData( " https://pdm.example.com/ ", "9999", false, "https://pdm.example.com" )]
	[InlineData( "", "8069", true, "" )]
	public void BuildUrl( string address, string port, bool useHttps, string expected )
		=> Assert.Equal( expected, OdooEndpoint.BuildUrl( address, port, useHttps ) );

	[Theory]
	[InlineData( "https://pdm.example.com" )]
	[InlineData( "http://localhost:8069" )]
	[InlineData( "http://127.0.0.1:8069" )]
	[InlineData( "http://[::1]:8069" )]
	public void SafeUrls_AreAccepted( string url )
		=> Assert.Null( OdooEndpoint.InsecureReason( url, allowPlainHttp: false ) );

	[Theory]
	[InlineData( "http://10.0.0.68:8069" )]
	[InlineData( "http://pdm.example.com" )]
	public void PlainHttpToAnotherComputer_IsRefused( string url )
		=> Assert.Contains( "unencrypted", OdooEndpoint.InsecureReason( url, allowPlainHttp: false ) );

	[Fact]
	public void PlainHttp_IsAccepted_WhenTheUserTurnedHttpsOff()
		=> Assert.Null( OdooEndpoint.InsecureReason( "http://10.0.0.68:8069", allowPlainHttp: true ) );

	[Theory]
	[InlineData( null )]
	[InlineData( "" )]
	[InlineData( "not a url" )]
	[InlineData( "ftp://pdm.example.com" )]
	public void MissingOrOtherSchemes_AreRefused( string? url )
		=> Assert.NotNull( OdooEndpoint.InsecureReason( url, allowPlainHttp: true ) );
}
