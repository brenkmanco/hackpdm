using System.Diagnostics.CodeAnalysis;

using HackPDM.Core.Configuration;

namespace HackPDM.Core.Pdm;

/// <summary>Converts between server names (root\a\b\file), server directories (root / a / b), and local paths.</summary>
public static class PwaPaths
{
	public const string Root = "root";
	public const char NameSeparator = '\\';
	public const string DirectorySeparator = " / ";

	private static readonly char[] AnySeparator = [ '\\', '/' ];

	public static string Vault => HackDefaults.Instance.PwaPathAbsolute;

	public static string[] Segments( string? serverPath )
	{
		if( string.IsNullOrEmpty( serverPath ) )
			return [];
		string[] segments = serverPath.Contains( DirectorySeparator )
			? serverPath.Split( DirectorySeparator, StringSplitOptions.RemoveEmptyEntries )
			: serverPath.Split( AnySeparator, StringSplitOptions.RemoveEmptyEntries );
		return segments is [ var first, .. ] && IsRootName( first ) ? segments[ 1.. ] : segments;
	}

	private static bool IsRootName( string segment )
		=> string.Equals( segment, Root, StringComparison.OrdinalIgnoreCase )
			|| ( HackDefaults.Instance?.PwaPathRelative is { Length: > 0 } rel
				&& string.Equals( segment, rel, StringComparison.OrdinalIgnoreCase ) );

	public static bool SameTreePath( string? a, string? b )
	{
		if( a is null || b is null )
			return false;
		string[] sa = Segments( a );
		string[] sb = Segments( b );
		if( sa.Length != sb.Length )
			return false;
		for( int i = 0; i < sa.Length; i++ )
		{
			if( !string.Equals( sa[ i ], sb[ i ], StringComparison.OrdinalIgnoreCase ) )
				return false;
		}
		return true;
	}

	public static bool IsSameOrAncestorTreePath( string? ancestor, string? descendant )
	{
		if( ancestor is null || descendant is null )
			return false;
		string[] sa = Segments( ancestor );
		string[] sd = Segments( descendant );
		if( sa.Length > sd.Length )
			return false;
		for( int i = 0; i < sa.Length; i++ )
		{
			if( !string.Equals( sa[ i ], sd[ i ], StringComparison.OrdinalIgnoreCase ) )
				return false;
		}
		return true;
	}

	public static string ToRelative( string? serverPath )
		=> string.Join( Path.DirectorySeparatorChar, Segments( serverPath ) );

	public static string? ToAbsolute( string? serverPath )
		=> serverPath is null ? null : Path.Combine( Vault, ToRelative( serverPath ) );

	public static string FolderOf( string? serverName )
		=> Segments( serverName ) is { Length: > 1 } segments
			? string.Join( Path.DirectorySeparatorChar, segments[ ..^1 ] )
			: "";

	public static string[]? LocalSegments( string? localPath )
	{
		string vault = Vault;
		if( string.IsNullOrWhiteSpace( localPath ) || string.IsNullOrWhiteSpace( vault )
			|| !Path.IsPathFullyQualified( localPath ) || !Path.IsPathFullyQualified( vault ) )
			return null;

		string normalizedVault;
		string normalizedPath;
		try
		{
			normalizedVault = Path.GetFullPath( vault );
			normalizedPath = Path.GetFullPath( localPath );
		}
		catch
		{
			return null;
		}

		string relative = Path.GetRelativePath( normalizedVault, normalizedPath );
		if( relative == "." )
			return [];
		if( relative == ".." || relative.StartsWith( ".." + Path.DirectorySeparatorChar ) || Path.IsPathRooted( relative ) )
			return null;
		return relative.Split( AnySeparator, StringSplitOptions.RemoveEmptyEntries );
	}

	public static bool IsInside( [NotNullWhen( true )] string? localPath )
		=> LocalSegments( localPath ) is not null;

	public static string ToServerName( IEnumerable<string> segments )
		=> string.Join( NameSeparator, [ Root, .. segments ] );

	public static string? ToServerName( string? localPath )
		=> LocalSegments( localPath ) is { } segments ? ToServerName( segments ) : null;

	public static string? ToServerDirectory( string? localPath )
		=> LocalSegments( localPath ) is { } segments ? string.Join( DirectorySeparator, [ Root, .. segments ] ) : null;
}
