using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace HackPDM.Infrastructure.Odoo;

/// <summary>Reachability checks the profile manager runs to explain a failed login.</summary>
public static class OdooConnection
{
	/// <summary>1 when logged in, 0 when not.</summary>
	public static int CorrectUserId() => OdooDefaults.Instance.OdooId is 0 ? 0 : 1;

	/// <summary>Whether the server's host answers a ping.</summary>
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

	/// <summary>Whether the server's port accepts a TCP connection.</summary>
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
