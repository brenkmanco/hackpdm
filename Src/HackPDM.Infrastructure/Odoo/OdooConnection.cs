using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace HackPDM.Infrastructure.Odoo;

/// <summary>Reachability checks the profile manager runs to explain a failed login.</summary>
public static class OdooConnection
{
	/// <summary>Reports whether a user id is set in <see cref="OdooDefaults.Instance"/>, as a number for the profile manager's status display.</summary>
	/// <returns>1 when <see cref="OdooDefaults.OdooId"/> is non-zero (logged in), 0 when not.</returns>
	public static int CorrectUserId() => OdooDefaults.Instance.OdooId is 0 ? 0 : 1;

	/// <summary>Extracts the target host and port from the configured Odoo settings (including full URLs and default HTTPS/HTTP ports).</summary>
	public static (string Host, int Port)? ResolveEndpoint( string? address, string? portText, bool useHttps )
	{
		string url = OdooEndpoint.BuildUrl( address, portText, useHttps );
		if( string.IsNullOrEmpty( url ) || !Uri.TryCreate( url, UriKind.Absolute, out Uri? uri ) || string.IsNullOrEmpty( uri.Host ) || uri.Port <= 0 )
			return null;
		return (uri.Host, uri.Port);
	}

	/// <summary>Checks whether the configured server host answers an ICMP ping, falling back to DNS resolution when ICMP is blocked.</summary>
	/// <returns>True when the ping succeeds or the host resolves in DNS; false when no address is set or resolution fails.</returns>
	public static async Task<bool> CorrectOdooAddress()
	{
		if( ResolveEndpoint( OdooDefaults.Instance.OdooAddress, OdooDefaults.Instance.OdooPort, OdooDefaults.Instance.OdooUseHttps ) is not { Host: var host } )
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
			return ( await System.Net.Dns.GetHostAddressesAsync( host ) ).Length > 0;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>Checks whether the configured server port accepts a TCP connection.</summary>
	/// <returns>True when a connection opens within 5 seconds; false when the address or port is unset or invalid, the connection is refused, or it times out.</returns>
	/// <remarks>Opens and immediately closes a plain TCP connection; no HTTP request is sent.</remarks>
	public static async Task<bool> CorrectOdooPort()
	{
		if( ResolveEndpoint( OdooDefaults.Instance.OdooAddress, OdooDefaults.Instance.OdooPort, OdooDefaults.Instance.OdooUseHttps ) is not var (host, port) )
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
