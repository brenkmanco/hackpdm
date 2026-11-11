namespace HackPDM.Infrastructure.SldWrks;

/// <summary>
/// Finds the local file for a reference stored in a SolidWorks document. Stored references are
/// absolute Windows paths from the machine that saved the file (e.g.
/// <c>C:\Users\someone\Desktop\Robot\shaft.SLDPRT</c>), so they rarely exist as-is, and never on
/// Linux or macOS. Tried in order, matching names case-insensitively:
/// <list type="number">
/// <item>the part of the stored path after the vault folder's name, under the local vault;</item>
/// <item>the same file name next to the referencing document (where SolidWorks also looks);</item>
/// <item>the stored path itself, when it exists here;</item>
/// <item>a file of that name anywhere in the vault, preferring the one whose parent folders
/// best match the stored path.</item>
/// </list>
/// The vault index for the last step is built on first use and kept for this instance's lifetime.
/// </summary>
public sealed class SwReferenceResolver( string? vaultRoot )
{
	private readonly string? _vaultRoot = string.IsNullOrEmpty( vaultRoot ) ? null : Path.TrimEndingDirectorySeparator( vaultRoot );
	private Dictionary<string, List<string>>? _byName;

	private static readonly EnumerationOptions Recursive = new()
	{
		RecurseSubdirectories = true,
		IgnoreInaccessible = true,
		MatchCasing = MatchCasing.CaseInsensitive,
		AttributesToSkip = FileAttributes.ReparsePoint,
	};

	/// <summary>Splits a stored path on both separators: stored paths are Windows paths whatever the OS.</summary>
	public static string[] Segments( string storedPath )
		=> storedPath.Split( [ '\\', '/' ], StringSplitOptions.RemoveEmptyEntries );

	public static string FileName( string storedPath ) => Segments( storedPath ) is [ .., var last ] ? last : "";

	/// <summary>The local path of the referenced file, or null when it can't be found.</summary>
	public string? Resolve( string storedPath, string? referencingFile = null )
	{
		string[] segments = Segments( storedPath );
		if( segments.Length == 0 )
			return null;
		string name = segments[ ^1 ];

		if( _vaultRoot is not null )
		{
			string vaultName = Path.GetFileName( _vaultRoot );
			for( int i = 0; i < segments.Length - 1; i++ )
			{
				if( segments[ i ].Equals( vaultName, StringComparison.OrdinalIgnoreCase )
					&& Find( _vaultRoot, segments[ ( i + 1 ).. ] ) is { } inVault )
					return inVault;
			}
		}

		if( Path.GetDirectoryName( referencingFile ) is { Length: > 0 } siblingDir && Find( siblingDir, [ name ] ) is { } sibling )
			return sibling;

		if( Path.IsPathFullyQualified( storedPath ) && File.Exists( storedPath ) )
			return Path.GetFullPath( storedPath );

		return SearchVault( name, segments );
	}

	/// <summary>Follows <paramref name="segments"/> under <paramref name="root"/>, matching each name case-insensitively.</summary>
	private static string? Find( string root, ReadOnlySpan<string> segments )
	{
		string current = root;
		for( int i = 0; i < segments.Length; i++ )
		{
			bool last = i == segments.Length - 1;
			string exact = Path.Combine( current, segments[ i ] );
			if( last ? File.Exists( exact ) : Directory.Exists( exact ) )
			{
				current = exact;
				continue;
			}
			if( !Directory.Exists( current ) )
				return null;
			var options = new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive, IgnoreInaccessible = true, MatchType = MatchType.Simple };
			string? match = last
				? Directory.EnumerateFiles( current, segments[ i ], options ).FirstOrDefault()
				: Directory.EnumerateDirectories( current, segments[ i ], options ).FirstOrDefault();
			if( match is null )
				return null;
			current = match;
		}
		return current;
	}

	private string? SearchVault( string name, string[] storedSegments )
	{
		if( _vaultRoot is null || !Directory.Exists( _vaultRoot ) )
			return null;
		_byName ??= IndexVault( _vaultRoot );
		if( !_byName.TryGetValue( name, out var candidates ) )
			return null;
		return candidates
			.OrderByDescending( c => SharedParentFolders( Segments( c ), storedSegments ) )
			.ThenBy( c => c, StringComparer.Ordinal )
			.First();
	}

	private static Dictionary<string, List<string>> IndexVault( string root )
	{
		var index = new Dictionary<string, List<string>>( StringComparer.OrdinalIgnoreCase );
		foreach( string file in Directory.EnumerateFiles( root, "*.sld*", Recursive ) )
		{
			string name = Path.GetFileName( file );
			if( !index.TryGetValue( name, out var list ) )
				index[ name ] = list = [];
			list.Add( file );
		}
		return index;
	}

	/// <summary>How many folders directly above the file name the two paths have in common.</summary>
	private static int SharedParentFolders( string[] a, string[] b )
	{
		int count = 0;
		for( int i = 2; i <= Math.Min( a.Length, b.Length ); i++ )
		{
			if( !a[ ^i ].Equals( b[ ^i ], StringComparison.OrdinalIgnoreCase ) )
				break;
			count++;
		}
		return count;
	}
}
