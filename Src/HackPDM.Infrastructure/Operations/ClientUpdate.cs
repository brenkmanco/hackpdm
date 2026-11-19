using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Infrastructure.Odoo;

namespace HackPDM.Infrastructure.Operations;

/// <summary>Result of checking a client version against the server's <c>client_version</c> rule.</summary>
public readonly record struct VersionCheckResult( bool IsAllowed, string? Reason );

/// <summary>
/// The client's version against the one the server requires, and fetching the published installer. The
/// installer's location and SHA-256 come from the server's settings (over the logged-in, encrypted
/// connection), and a file that does not match that checksum is never kept or run.
/// </summary>
public static class ClientUpdate
{
	private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes( 30 ) };

	/// <summary>This client's version (the HackPDM version every project is built with).</summary>
	public static Version Current { get; } = typeof( ClientUpdate ).Assembly.GetName().Version ?? new Version( 0, 0 );

	/// <summary>This client's version as people read it ("1.2.0").</summary>
	public static string CurrentText { get; } =
		typeof( ClientUpdate ).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? Current.ToString();

	/// <summary>Whether <paramref name="current"/> is older than the version the server requires.</summary>
	/// <param name="current">The client's version.</param>
	/// <param name="required">The server's <c>client_version</c> setting ("1.2", "1.2.0", "1.0.0.27"); empty or unreadable means no requirement.</param>
	/// <returns><see langword="true"/> only when <paramref name="required"/> is a version newer than <paramref name="current"/>; missing parts count as 0.</returns>
	public static bool IsOutdated( Version current, string? required )
		=> Version.TryParse( required?.Trim(), out Version? minimum ) && Normalize( current ) < Normalize( minimum );

	/// <summary>
	/// Evaluates <paramref name="current"/> against the server's <c>client_version</c> requirement.
	/// Supports exact versions (<c>1.1.0</c>), single bounds (<c>&gt;=1.1.0</c>, <c>&gt;1.0.0</c>, <c>&lt;=1.3.0</c>, <c>&lt;2.0.0</c>),
	/// combined ranges (<c>&gt;=1.1.0, &lt;2.0.0</c> or <c>&gt;=1.1.0 &lt;=1.3.0</c>), and inclusive hyphen ranges (<c>1.1.0 - 1.3.0</c>).
	/// Empty/whitespace allows access; malformed non-empty rules fail closed.
	/// </summary>
	public static VersionCheckResult CheckVersion( Version current, string? requirement )
	{
		if( string.IsNullOrWhiteSpace( requirement ) )
			return new( true, null );

		string spec = requirement.Trim();
		Version normalizedCurrent = Normalize( current );

		// Hyphen inclusive range: "1.1.0 - 1.3.0"
		int dashIdx = spec.IndexOf( " - ", StringComparison.Ordinal );
		if( dashIdx >= 0 )
		{
			string left = spec[ ..dashIdx ].Trim();
			string right = spec[ ( dashIdx + 3 ).. ].Trim();
			if( !Version.TryParse( left, out Version? minVer ) || !Version.TryParse( right, out Version? maxVer ) )
				return new( false, $"Invalid server client_version range '{spec}'." );
			if( Normalize( minVer ) > Normalize( maxVer ) )
				return new( false, $"Invalid server client_version range '{spec}' (minimum exceeds maximum)." );
			bool inRange = normalizedCurrent >= Normalize( minVer ) && normalizedCurrent <= Normalize( maxVer );
			return inRange
				? new( true, null )
				: new( false, $"Client version {FormatVersion( current )} is outside the allowed server version range '{spec}'." );
		}

		// Split comma-separated (and/or space-separated) clauses, preserving operators separated from numbers like ">= 1.1.0"
		string[] rawTokens = spec.Split( [ ',', ';' ], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
		List<string> clauses = [];
		foreach( string token in rawTokens )
		{
			// Also allow space-separated operator clauses like ">=1.1.0 <2.0.0"
			string[] parts = token.Split( ' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
			for( int i = 0; i < parts.Length; i++ )
			{
				if( parts[ i ] is ">=" or "<=" or "==" or ">" or "<" or "=" && i + 1 < parts.Length )
				{
					clauses.Add( parts[ i ] + parts[ i + 1 ] );
					i++;
				}
				else
				{
					clauses.Add( parts[ i ] );
				}
			}
		}

		if( clauses.Count == 0 )
			return new( true, null );

		foreach( string clause in clauses )
		{
			string op;
			string verText;
			if( clause.StartsWith( ">=", StringComparison.Ordinal ) || clause.StartsWith( "<=", StringComparison.Ordinal ) || clause.StartsWith( "==", StringComparison.Ordinal ) )
			{
				op = clause[ ..2 ];
				verText = clause[ 2.. ].Trim();
			}
			else if( clause.StartsWith( ">", StringComparison.Ordinal ) || clause.StartsWith( "<", StringComparison.Ordinal ) || clause.StartsWith( "=", StringComparison.Ordinal ) )
			{
				op = clause[ ..1 ];
				verText = clause[ 1.. ].Trim();
			}
			else
			{
				op = "==";
				verText = clause;
			}

			if( !Version.TryParse( verText, out Version? bound ) )
				return new( false, $"Invalid server client_version requirement '{spec}'." );

			Version normalizedBound = Normalize( bound );
			bool matches = op switch
			{
				">=" => normalizedCurrent >= normalizedBound,
				">" => normalizedCurrent > normalizedBound,
				"<=" => normalizedCurrent <= normalizedBound,
				"<" => normalizedCurrent < normalizedBound,
				"=" or "==" => normalizedCurrent == normalizedBound,
				_ => false,
			};

			if( !matches )
				return new( false, $"Client version {FormatVersion( current )} does not match the server's required version '{spec}'." );
		}

		return new( true, null );
	}

	/// <summary>Returns <see langword="true"/> when <paramref name="current"/> fails the server's <c>client_version</c> requirement.</summary>
	public static bool IsVersionMismatch( Version current, string? requirement )
		=> !CheckVersion( current, requirement ).IsAllowed;

	private static Version Normalize( Version v ) => new( v.Major, Math.Max( 0, v.Minor ), Math.Max( 0, v.Build ), Math.Max( 0, v.Revision ) );

	private static string FormatVersion( Version v )
		=> v.Revision > 0 ? $"{v.Major}.{v.Minor}.{Math.Max( 0, v.Build )}.{v.Revision}" : $"{v.Major}.{v.Minor}.{Math.Max( 0, v.Build )}";

	/// <summary>Fetches the installer and checks it against <paramref name="sha256"/>.</summary>
	/// <param name="source">An http(s) URL, a <c>file://</c> URI, or a local or network-share path.</param>
	/// <param name="sha256">The expected SHA-256 in hex; required.</param>
	/// <param name="targetDirectory">Where to save the installer; a new folder under the temp folder when null.</param>
	/// <param name="token">Cancels the download.</param>
	/// <returns>The verified installer's path; or why it could not be fetched (nothing is left behind then).</returns>
	/// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
	public static async Task<Result<string>> DownloadInstallerAsync( string? source, string? sha256, string? targetDirectory = null, CancellationToken token = default )
	{
		if( string.IsNullOrWhiteSpace( source ) )
			return Result<string>.Fail( "the server does not say where to download the client (hp.settings client_download_url)" );
		string expected = ( sha256 ?? "" ).Trim().ToLowerInvariant();
		if( expected.Length != 64 || expected.Any( c => !Uri.IsHexDigit( c ) ) )
			return Result<string>.Fail( "the server gives no valid SHA-256 for the installer (hp.settings client_download_sha256), so it cannot be checked" );

		source = source.Trim();
		bool web = Uri.TryCreate( source, UriKind.Absolute, out Uri? uri ) && uri.Scheme is "http" or "https";
		string fileName = Path.GetFileName( web ? uri!.AbsolutePath : uri is { IsFile: true } ? uri.LocalPath : source );
		if( string.IsNullOrEmpty( fileName ) )
			fileName = "HackPDM-setup";
		targetDirectory ??= Path.Combine( Path.GetTempPath(), $"hackpdm-update-{Guid.NewGuid():N}" );
		string target = Path.Combine( targetDirectory, fileName );
		string partial = target + ".download";
		try
		{
			Directory.CreateDirectory( targetDirectory );
			await using( FileStream output = new( partial, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true ) )
			{
				if( web )
				{
					using HttpResponseMessage response = await Http.GetAsync( uri, HttpCompletionOption.ResponseHeadersRead, token );
					response.EnsureSuccessStatusCode();
					await using Stream input = await response.Content.ReadAsStreamAsync( token );
					await input.CopyToAsync( output, token );
				}
				else
				{
					await using FileStream input = new( uri is { IsFile: true } ? uri.LocalPath : source, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, useAsync: true );
					await input.CopyToAsync( output, token );
				}
			}
			string actual;
			await using( FileStream check = File.OpenRead( partial ) )
				actual = Convert.ToHexStringLower( await SHA256.HashDataAsync( check, token ) );
			if( actual != expected )
			{
				File.Delete( partial );
				HackPDM.Core.Logging.HackLogger.Error( "Updater", $"SHA-256 mismatch for installer from '{source}': expected={expected}, actual={actual}; file deleted." );
				return Result<string>.Fail( $"the downloaded installer does not match the server's SHA-256 (got {actual}); it was deleted" );
			}
			File.Move( partial, target, overwrite: true );
			HackPDM.Core.Logging.HackLogger.Info( "Updater", $"Downloaded and verified installer '{fileName}' (sha256={actual})." );
			return Result<string>.Success( target );
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException or HttpRequestException or TaskCanceledException && !token.IsCancellationRequested )
		{
			TryDelete( partial );
			HackPDM.Core.Logging.HackLogger.Error( "Updater", $"Installer download from '{source}' failed: {e.Message}", e );
			return Result<string>.Fail( $"could not download the installer from {source}: {e.Message}" );
		}
		catch
		{
			TryDelete( partial );
			throw;
		}
	}

	private static void TryDelete( string path )
	{
		try
		{
			File.Delete( path );
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException )
		{
		}
	}
}
