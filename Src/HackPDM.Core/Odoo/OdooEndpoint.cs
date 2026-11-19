using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace HackPDM.Core.Odoo;

/// <summary>Builds Odoo server URLs and enforces HTTPS policy for credentials.</summary>
public static class OdooEndpoint
{
	public static string BuildUrl( string? address, string? port, bool useHttps )
	{
		address = address?.Trim().TrimEnd( '/' ) ?? "";
		if( address.Length == 0 )
			return "";
		if( address.Contains( "://", StringComparison.Ordinal ) )
			return address;
		string scheme = useHttps ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
		return string.IsNullOrWhiteSpace( port ) ? $"{scheme}://{address}" : $"{scheme}://{address}:{port.Trim()}";
	}

	public static string? InsecureReason( string? url, bool allowPlainHttp )
	{
		if( string.IsNullOrWhiteSpace( url ) )
			return "the Odoo server address is not set";
		if( !Uri.TryCreate( url, UriKind.Absolute, out Uri? uri ) )
			return $"'{url}' is not a valid server address";
		if( uri.Scheme == Uri.UriSchemeHttps )
			return null;
		if( uri.Scheme != Uri.UriSchemeHttp )
			return $"'{url}' must start with https://";
		if( uri.IsLoopback || allowPlainHttp )
			return null;
		return $"refusing to send the password unencrypted to {uri.Host}: connect with HTTPS, "
			+ "or turn off \"Use HTTPS\" in the Odoo settings if this network is trusted";
	}
}

/// <summary>Network reachability checks for Odoo login diagnostics.</summary>
public static class OdooConnection
{
	public static int CorrectUserId() => OdooDefaults.Instance.OdooId == 0 ? 0 : 1;

	public static (string Host, int Port)? ResolveEndpoint( string? address, string? portText, bool useHttps )
	{
		string url = OdooEndpoint.BuildUrl( address, portText, useHttps );
		if( string.IsNullOrEmpty( url )
			|| !Uri.TryCreate( url, UriKind.Absolute, out Uri? uri )
			|| string.IsNullOrEmpty( uri.Host )
			|| uri.Port <= 0 )
			return null;
		return ( uri.Host, uri.Port );
	}

	public static async Task<bool> CorrectOdooAddress()
	{
		var settings = OdooDefaults.Instance;
		if( ResolveEndpoint( settings.OdooAddress, settings.OdooPort, settings.OdooUseHttps ) is not { Host: var host } )
			return false;
		try
		{
			using Ping pinger = new();
			if( await pinger.SendPingAsync( host ) is { Status: IPStatus.Success } )
				return true;
		}
		catch( Exception e ) when( e is PingException or InvalidOperationException or ArgumentException )
		{
		}
		try
		{
			return ( await Dns.GetHostAddressesAsync( host ) ).Length > 0;
		}
		catch
		{
			return false;
		}
	}

	public static async Task<bool> CorrectOdooPort()
	{
		var settings = OdooDefaults.Instance;
		if( ResolveEndpoint( settings.OdooAddress, settings.OdooPort, settings.OdooUseHttps ) is not var (host, port) )
			return false;
		try
		{
			using TcpClient client = new();
			using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );
			await client.ConnectAsync( host, port, timeout.Token );
			return true;
		}
		catch( Exception e ) when( e is SocketException or OperationCanceledException )
		{
			return false;
		}
	}
}
