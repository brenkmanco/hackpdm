using System.Buffers;
using System.ComponentModel;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;

namespace HackPDM.Core.Pdm;

public readonly record struct VersionCheckResult( bool IsAllowed, string? Reason );

[Bindable( true )]
public sealed record UpdateInfo(
	string Version,
	string DownloadUrl,
	string Sha256,
	string? ReleaseNotesUrl,
	string Source,
	bool IsMandatory );

/// <summary>Checks client version against Odoo's client_version rule and downloads SHA-256 verified installers.</summary>
public static class ClientUpdate
{
	private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes( 30 ) };
	private static readonly char[] ClauseSeparators = [ ',', ';' ];
	private static readonly SearchValues<char> WhitespaceChars = SearchValues.Create( " \t\r\n" );

	public static Version Current { get; } = typeof( ClientUpdate ).Assembly.GetName().Version ?? new Version( 0, 0 );

	public static string CurrentText { get; } =
		typeof( ClientUpdate ).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
		?? Current.ToString();

	public static bool IsOutdated( Version current, string? required )
		=> Version.TryParse( required?.Trim(), out Version? minimum ) && Normalize( current ) < Normalize( minimum );

	public static VersionCheckResult CheckVersion( Version current, string? requirement )
	{
		if( string.IsNullOrWhiteSpace( requirement ) )
			return new( true, null );

		string spec = requirement.Trim();
		Version normalizedCurrent = Normalize( current );

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

		string[] rawTokens = spec.Split( ClauseSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
		List<string> clauses = [];
		foreach( string token in rawTokens )
		{
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

		foreach( string clause in clauses )
		{
			( string op, string verText ) = clause switch
			{
				_ when clause.StartsWith( ">=", StringComparison.Ordinal ) || clause.StartsWith( "<=", StringComparison.Ordinal ) || clause.StartsWith( "==", StringComparison.Ordinal )
					=> ( clause[ ..2 ], clause[ 2.. ].Trim() ),
				_ when clause.StartsWith( ">", StringComparison.Ordinal ) || clause.StartsWith( "<", StringComparison.Ordinal ) || clause.StartsWith( "=", StringComparison.Ordinal )
					=> ( clause[ ..1 ], clause[ 1.. ].Trim() ),
				_ => ( "==", clause ),
			};

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

	public static bool IsVersionMismatch( Version current, string? requirement )
		=> !CheckVersion( current, requirement ).IsAllowed;

	private static Version Normalize( Version v )
		=> new( v.Major, Math.Max( 0, v.Minor ), Math.Max( 0, v.Build ), Math.Max( 0, v.Revision ) );

	private static string FormatVersion( Version v )
		=> v.Revision > 0
			? $"{v.Major}.{v.Minor}.{Math.Max( 0, v.Build )}.{v.Revision}"
			: $"{v.Major}.{v.Minor}.{Math.Max( 0, v.Build )}";

	public static async Task<Result<string>> DownloadInstallerAsync(
		string? source,
		string? sha256,
		string? targetDirectory = null,
		CancellationToken token = default )
	{
		if( string.IsNullOrWhiteSpace( source ) )
			return Result<string>.Fail( "the server does not say where to download the client (hp.settings client_download_url)" );
		string expected = ( sha256 ?? "" ).Trim().ToLowerInvariant();
		if( !IsValidSha256Hex( expected.AsSpan() ) )
			return Result<string>.Fail( "the server gives no valid SHA-256 for the installer (hp.settings client_download_sha256), so it cannot be checked" );

		source = source.Trim();
		bool web = Uri.TryCreate( source, UriKind.Absolute, out Uri? uri ) && uri.Scheme is "http" or "https";
		string fileName = Path.GetFileName( web ? uri!.AbsolutePath : uri is { IsFile: true } ? uri.LocalPath : source );
		if( string.IsNullOrEmpty( fileName ) )
			fileName = OperatingSystem.IsWindows() ? "HackPDM-setup.exe" : "HackPDM-setup";
		bool createdTempDir = targetDirectory is null;
		targetDirectory ??= Path.Combine( Path.GetTempPath(), $"hackpdm-update-{Guid.NewGuid():N}" );
		string target = Path.Combine( targetDirectory, fileName );
		string partial = target + ".download";

		try
		{
			Directory.CreateDirectory( targetDirectory );
			string actual;
			using( IncrementalHash hasher = IncrementalHash.CreateHash( HashAlgorithmName.SHA256 ) )
			{
				FileStream output = new( partial, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true );
				await using( output.ConfigureAwait( false ) )
				{
					if( web )
					{
						using HttpResponseMessage response = await Http.GetAsync( uri, HttpCompletionOption.ResponseHeadersRead, token ).ConfigureAwait( false );
						response.EnsureSuccessStatusCode();
						Stream input = await response.Content.ReadAsStreamAsync( token ).ConfigureAwait( false );
						await using( input.ConfigureAwait( false ) )
							await CopyAndHashAsync( input, output, hasher, token ).ConfigureAwait( false );
					}
					else
					{
						FileStream input = new( uri is { IsFile: true } ? uri.LocalPath : source, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, useAsync: true );
						await using( input.ConfigureAwait( false ) )
							await CopyAndHashAsync( input, output, hasher, token ).ConfigureAwait( false );
					}
					Span<byte> digest = stackalloc byte[ SHA256.HashSizeInBytes ];
					hasher.GetHashAndReset( digest );
					actual = Convert.ToHexStringLower( digest );
				}
			}

			if( actual != expected )
			{
				CleanupFailedDownload( partial, createdTempDir ? targetDirectory : null );
				HackLogger.Error( "Updater", $"SHA-256 mismatch for installer from '{source}': expected={expected}, actual={actual}; file deleted." );
				return Result<string>.Fail( $"the downloaded installer does not match the server's SHA-256 (got {actual}); it was deleted" );
			}

			File.Move( partial, target, overwrite: true );
			HackLogger.Info( "Updater", $"Downloaded and verified installer '{fileName}' (sha256={actual})." );
			return Result<string>.Success( target );
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException or HttpRequestException or TaskCanceledException && !token.IsCancellationRequested )
		{
			CleanupFailedDownload( partial, createdTempDir ? targetDirectory : null );
			HackLogger.Error( "Updater", $"Installer download from '{source}' failed: {e.Message}", e );
			return Result<string>.Fail( $"could not download the installer from {source}: {e.Message}" );
		}
		catch
		{
			CleanupFailedDownload( partial, createdTempDir ? targetDirectory : null );
			throw;
		}
	}

	private static void CleanupFailedDownload( string partialPath, string? autoCreatedDirectory )
	{
		FileOps.TryDelete( partialPath );
		if( autoCreatedDirectory is not null )
		{
			try
			{
				if( Directory.Exists( autoCreatedDirectory ) && !Directory.EnumerateFileSystemEntries( autoCreatedDirectory ).Any() )
					Directory.Delete( autoCreatedDirectory, recursive: false );
			}
			catch( Exception ex ) when( ex is IOException or UnauthorizedAccessException )
			{
			}
		}
	}

	private static async Task CopyAndHashAsync( Stream input, Stream output, IncrementalHash hasher, CancellationToken token )
	{
		byte[] buffer = ArrayPool<byte>.Shared.Rent( 64 * 1024 );
		try
		{
			int read;
			while( ( read = await input.ReadAsync( buffer.AsMemory( 0, buffer.Length ), token ).ConfigureAwait( false ) ) > 0 )
			{
				hasher.AppendData( buffer.AsSpan( 0, read ) );
				await output.WriteAsync( buffer.AsMemory( 0, read ), token ).ConfigureAwait( false );
			}
		}
		finally
		{
			ArrayPool<byte>.Shared.Return( buffer );
		}
	}

	private static bool IsValidSha256Hex( ReadOnlySpan<char> hex )
	{
		if( hex.Length != 64 )
			return false;
		for( int i = 0; i < hex.Length; i++ )
		{
			if( !Uri.IsHexDigit( hex[ i ] ) )
				return false;
		}
		return true;
	}

	public static string? ParseSha256File( string? content )
	{
		if( string.IsNullOrWhiteSpace( content ) )
			return null;
		ReadOnlySpan<char> span = content.AsSpan().TrimStart();
		int end = span.IndexOfAny( WhitespaceChars );
		ReadOnlySpan<char> token = end >= 0 ? span[ ..end ] : span;
		return IsValidSha256Hex( token ) ? token.ToString().ToLowerInvariant() : null;
	}

	public static string GetPlatformAssetSuffix()
	{
		if( OperatingSystem.IsWindows() )
			return "-win-x64-setup.exe";
		if( OperatingSystem.IsMacOS() )
			return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "-osx-arm64.tar.gz" : "-osx-x64.tar.gz";
		return "-linux-x64.tar.gz";
	}

	public static async Task<Result<UpdateInfo?>> CheckGitHubLatestReleaseAsync(
		string ownerRepo = "moonxbears/wyrmpdm",
		CancellationToken token = default )
	{
		try
		{
			using HttpRequestMessage req = new( HttpMethod.Get, $"https://api.github.com/repos/{ownerRepo}/releases/latest" );
			req.Headers.UserAgent.Add( new ProductInfoHeaderValue( "HackPDM", CurrentText ) );
			req.Headers.Accept.Add( new MediaTypeWithQualityHeaderValue( "application/vnd.github+json" ) );

			using HttpResponseMessage resp = await Http.SendAsync( req, HttpCompletionOption.ResponseHeadersRead, token ).ConfigureAwait( false );
			if( !resp.IsSuccessStatusCode )
				return Result<UpdateInfo?>.Fail( $"GitHub release check returned HTTP {(int)resp.StatusCode}." );

			Stream stream = await resp.Content.ReadAsStreamAsync( token ).ConfigureAwait( false );
			await using( stream.ConfigureAwait( false ) )
			{
				using JsonDocument doc = await JsonDocument.ParseAsync( stream, cancellationToken: token ).ConfigureAwait( false );
				JsonElement root = doc.RootElement;

				string tag = root.TryGetProperty( "tag_name", out JsonElement tagEl ) ? tagEl.GetString() ?? "" : "";
				string versionText = tag.TrimStart( 'v', 'V' ).Split( '-', 2 )[ 0 ];
				if( !IsOutdated( Current, versionText ) )
					return Result<UpdateInfo?>.Success( null );

				string? htmlUrl = root.TryGetProperty( "html_url", out JsonElement urlEl ) ? urlEl.GetString() : null;
				string suffix = GetPlatformAssetSuffix();
				string shaSuffix = suffix + ".sha256";
				string? downloadUrl = null;
				string? shaUrl = null;

				if( root.TryGetProperty( "assets", out JsonElement assetsEl ) && assetsEl.ValueKind == JsonValueKind.Array )
				{
					foreach( JsonElement asset in assetsEl.EnumerateArray() )
					{
						string name = asset.TryGetProperty( "name", out JsonElement nameEl ) ? nameEl.GetString() ?? "" : "";
						string url = asset.TryGetProperty( "browser_download_url", out JsonElement dlEl ) ? dlEl.GetString() ?? "" : "";
						if( string.IsNullOrEmpty( url ) )
							continue;
						if( name.EndsWith( suffix, StringComparison.OrdinalIgnoreCase ) )
							downloadUrl = url;
						else if( name.EndsWith( shaSuffix, StringComparison.OrdinalIgnoreCase ) )
							shaUrl = url;
					}
				}

				if( string.IsNullOrWhiteSpace( downloadUrl ) || string.IsNullOrWhiteSpace( shaUrl ) )
					return Result<UpdateInfo?>.Fail( $"Release {tag} does not include a verified package for this platform ({suffix})." );

				using HttpRequestMessage shaReq = new( HttpMethod.Get, shaUrl );
				shaReq.Headers.UserAgent.Add( new ProductInfoHeaderValue( "HackPDM", CurrentText ) );
				using HttpResponseMessage shaResp = await Http.SendAsync( shaReq, token ).ConfigureAwait( false );
				shaResp.EnsureSuccessStatusCode();
				string? sha256 = ParseSha256File( await shaResp.Content.ReadAsStringAsync( token ).ConfigureAwait( false ) );
				if( sha256 is null )
					return Result<UpdateInfo?>.Fail( $"Release {tag} checksum file could not be parsed." );

				return Result<UpdateInfo?>.Success( new UpdateInfo(
					Version: versionText,
					DownloadUrl: downloadUrl,
					Sha256: sha256,
					ReleaseNotesUrl: htmlUrl,
					Source: "GitHub Releases",
					IsMandatory: false ) );
			}
		}
		catch( Exception e ) when( e is HttpRequestException or IOException or TaskCanceledException or JsonException && !token.IsCancellationRequested )
		{
			return Result<UpdateInfo?>.Fail( $"Could not check GitHub Releases: {e.Message}" );
		}
	}
}
