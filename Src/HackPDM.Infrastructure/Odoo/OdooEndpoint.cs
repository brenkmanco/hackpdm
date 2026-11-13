using System;

namespace HackPDM.Infrastructure.Odoo;

/// <summary>Builds the server URL and decides whether a URL is safe to send the login over.</summary>
/// <remarks>
/// Odoo's JSON-RPC protocol sends the password (or API key) with every request, so plain HTTP exposes it to
/// anyone on the network. HTTPS is the default; plain HTTP is accepted only to this computer (local development
/// and CI) or when the user has turned HTTPS off in the Odoo settings. Certificates are checked by the operating
/// system's trust store; a server with a self-signed certificate needs its certificate (or its CA) trusted there.
/// </remarks>
public static class OdooEndpoint
{
	/// <summary>Builds the server's base URL.</summary>
	/// <param name="address">The host name or IP address; a full URL (<c>https://pdm.example.com</c>) is used as it is.</param>
	/// <param name="port">The port, or empty for the scheme's default port.</param>
	/// <param name="useHttps">Whether to use <c>https</c> when <paramref name="address"/> names no scheme.</param>
	/// <returns>The URL without a trailing slash, such as <c>https://pdm.example.com:8443</c>; empty when <paramref name="address"/> is empty.</returns>
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

	/// <summary>Tells why a URL must not be used for the login, or null when it may.</summary>
	/// <param name="url">The server's base URL.</param>
	/// <param name="allowPlainHttp">Whether the user turned HTTPS off, accepting an unencrypted connection to another computer.</param>
	/// <returns>
	/// <see langword="null"/> for an <c>https</c> URL, for <c>http</c> to this computer (localhost, 127.0.0.1, ::1), and for
	/// <c>http</c> when <paramref name="allowPlainHttp"/> is set; otherwise the reason, worded for the user.
	/// </returns>
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
