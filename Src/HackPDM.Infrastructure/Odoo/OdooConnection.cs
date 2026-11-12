using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace HackPDM.Infrastructure.Odoo;

/// <summary>Reachability checks the profile manager runs to explain a failed login.</summary>
public static class OdooConnection
{
	/// <summary>Reports whether a user id is set in <see cref="OdooDefaults.Instance"/>, as a number for the profile manager's status display.</summary>
	/// <returns>1 when <see cref="OdooDefaults.OdooId"/> is non-zero (logged in), 0 when not.</returns>
	public static int CorrectUserId() => OdooDefaults.Instance.OdooId is 0 ? 0 : 1;

	/// <summary>Checks whether the configured server host answers an ICMP ping.</summary>
	/// <returns>True when the ping succeeds; false when no address is set, the ping fails, or it can't be sent.</returns>
	/// <remarks>Uses <see cref="Ping"/> with its default timeout (5 seconds). Hosts that block ICMP report false even when the server is reachable.</remarks>
	public static async Task<bool> CorrectOdooAddress()
	{
		try
		{
			using Ping pinger = new();
			return OdooDefaults.Instance.OdooAddress is { } address
				&& await pinger.SendPingAsync( address ) is { Status: IPStatus.Success };
		}
		catch( Exception e ) when( e is PingException or InvalidOperationException or ArgumentException )
		{
			return false;
		}
	}

	/// <summary>Checks whether the configured server port accepts a TCP connection.</summary>
	/// <returns>True when a connection opens within 5 seconds; false when the address or port is unset or invalid, the connection is refused, or it times out.</returns>
	/// <remarks>Opens and immediately closes a plain TCP connection; no HTTP request is sent.</remarks>
	public static async Task<bool> CorrectOdooPort()
	{
		if( OdooDefaults.Instance.OdooAddress is not { } address || !int.TryParse( OdooDefaults.Instance.OdooPort, out int port ) )
			return false;
		try
		{
			using TcpClient client = new();
			using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );
			await client.ConnectAsync( address, port, timeout.Token );
			return true;
		}
		catch( Exception e ) when( e is SocketException or OperationCanceledException )
		{
			return false;
		}
	}
}
