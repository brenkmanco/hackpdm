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

	private static Version Normalize( Version v ) => new( v.Major, Math.Max( 0, v.Minor ), Math.Max( 0, v.Build ), Math.Max( 0, v.Revision ) );

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
				return Result<string>.Fail( $"the downloaded installer does not match the server's SHA-256 (got {actual}); it was deleted" );
			}
			File.Move( partial, target, overwrite: true );
			return Result<string>.Success( target );
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException or HttpRequestException or TaskCanceledException && !token.IsCancellationRequested )
		{
			TryDelete( partial );
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
