using System.Runtime.InteropServices;

namespace HackPDM.Core.SolidWorks;

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
/// <param name="vaultRoot">The local working folder (vault root); null or empty disables the vault-based steps 1 and 4.</param>
public sealed class SwReferenceResolver( string? vaultRoot )
{
	private static readonly char[] PathSeparators = [ '\\', '/' ];
	private static readonly EnumerationOptions CaseInsensitiveSimple = new()
	{
		MatchCasing = MatchCasing.CaseInsensitive,
		IgnoreInaccessible = true,
		MatchType = MatchType.Simple,
	};
	private static readonly EnumerationOptions Recursive = new()
	{
		RecurseSubdirectories = true,
		IgnoreInaccessible = true,
		MatchCasing = MatchCasing.CaseInsensitive,
		AttributesToSkip = FileAttributes.ReparsePoint,
	};

	private readonly string? _vaultRoot = string.IsNullOrEmpty( vaultRoot ) ? null : Path.TrimEndingDirectorySeparator( vaultRoot );
	private readonly string? _vaultName = string.IsNullOrEmpty( vaultRoot ) ? null : Path.GetFileName( Path.TrimEndingDirectorySeparator( vaultRoot ) );
	private readonly Lock _indexGate = new();
	private Dictionary<string, List<string>>? _byName;

	/// <summary>Splits a stored path on both separators: stored paths are Windows paths whatever the OS.</summary>
	/// <param name="storedPath">The path as stored in a SolidWorks document.</param>
	/// <returns>The non-empty path segments, drive (e.g. <c>C:</c>) included.</returns>
	public static string[] Segments( string storedPath )
		=> storedPath.Split( PathSeparators, StringSplitOptions.RemoveEmptyEntries );

	private static string[] NormalizeSegments( string[] rawSegments )
	{
		if( rawSegments.Length == 0 )
			return rawSegments;
		bool hasDrivePrefix = rawSegments[ 0 ].Length == 2 && rawSegments[ 0 ][ 1 ] == ':';
		int minPopDepth = hasDrivePrefix ? 1 : 0;
		List<string> normalized = new( rawSegments.Length );
		foreach( string seg in rawSegments )
		{
			if( seg == "." )
				continue;
			if( seg == ".." )
			{
				if( normalized.Count > minPopDepth && normalized[ ^1 ] != ".." )
					normalized.RemoveAt( normalized.Count - 1 );
				else if( !hasDrivePrefix )
					normalized.Add( seg );
				continue;
			}
			normalized.Add( seg );
		}
		return [ .. normalized ];
	}

	private static bool IsInvalidLookupSegment( string segment )
		=> string.IsNullOrEmpty( segment )
			|| segment is "." or ".."
			|| segment.IndexOfAny( [ '*', '?', '\0' ] ) >= 0;

	/// <summary>Gets the file name of a stored Windows path on any OS (where <see cref="Path.GetFileName(string)"/> wouldn't split on backslashes).</summary>
	/// <param name="storedPath">The path as stored in a SolidWorks document.</param>
	/// <returns>The last segment, or "" for an empty path.</returns>
	public static string FileName( string storedPath )
	{
		ReadOnlySpan<char> trimmed = storedPath.AsSpan().TrimEnd( PathSeparators );
		if( trimmed.IsEmpty )
			return "";
		int sep = trimmed.LastIndexOfAny( '\\', '/' );
		return sep < 0 ? trimmed.ToString() : trimmed[ ( sep + 1 ).. ].ToString();
	}

	/// <summary>Finds the local file for a stored reference, trying the steps listed on <see cref="SwReferenceResolver"/> in order.</summary>
	/// <param name="storedPath">The path as stored in the referencing document.</param>
	/// <param name="referencingFile">The local path of the document that holds the reference; enables the "next to the referencing document" step.</param>
	/// <returns>The local path of the referenced file, or null when it can't be found.</returns>
	/// <remarks>The last step scans every <c>*.sld*</c> file under the vault once (skipping inaccessible folders and reparse points) and caches the index; files added later aren't seen by this instance.</remarks>
	public string? Resolve( string storedPath, string? referencingFile = null )
	{
		if( string.IsNullOrWhiteSpace( storedPath ) )
			return null;
		string[] segments = NormalizeSegments( Segments( storedPath ) );
		if( segments.Length == 0 )
			return null;
		string name = segments[ ^1 ];
		if( IsInvalidLookupSegment( name ) )
			return null;

		if( _vaultRoot is not null && _vaultName is not null )
		{
			for( int i = 0; i < segments.Length - 1; i++ )
			{
				if( segments[ i ].Equals( _vaultName, StringComparison.OrdinalIgnoreCase )
					&& Find( _vaultRoot, segments.AsSpan( i + 1 ) ) is { } inVault )
					return inVault;
			}
		}

		if( Path.GetDirectoryName( referencingFile ) is { Length: > 0 } siblingDir
			&& Find( siblingDir, MemoryMarshal.CreateReadOnlySpan( ref name, 1 ) ) is { } sibling )
			return sibling;

		try
		{
			if( Path.IsPathFullyQualified( storedPath ) && File.Exists( storedPath ) )
				return Path.GetFullPath( storedPath );
		}
		catch( Exception e ) when( e is ArgumentException or NotSupportedException or UnauthorizedAccessException or IOException )
		{
		}

		return SearchVault( name, segments );
	}

	/// <summary>Follows <paramref name="segments"/> under <paramref name="root"/>, matching each name case-insensitively.</summary>
	private static string? Find( string root, ReadOnlySpan<string> segments )
	{
		if( segments.IsEmpty )
			return null;
		for( int i = 0; i < segments.Length; i++ )
		{
			if( IsInvalidLookupSegment( segments[ i ] ) )
				return null;
		}
		if( OperatingSystem.IsWindows() )
		{
			string direct = root;
			for( int i = 0; i < segments.Length; i++ )
				direct = Path.Combine( direct, segments[ i ] );
			if( !File.Exists( direct ) )
				return null;
		}
		try
		{
			string current = root;
			for( int i = 0; i < segments.Length; i++ )
			{
				string segment = segments[ i ];
				bool last = i == segments.Length - 1;
				if( !Directory.Exists( current ) )
					return null;
				string? match = null;
				foreach( string entry in last
					? Directory.EnumerateFiles( current, segment, CaseInsensitiveSimple )
					: Directory.EnumerateDirectories( current, segment, CaseInsensitiveSimple ) )
				{
					if( Path.GetFileName( entry ).Equals( segment, StringComparison.OrdinalIgnoreCase ) )
					{
						match = entry;
						break;
					}
				}
				if( match is null )
					return null;
				current = match;
			}
			return current;
		}
		catch( Exception e ) when( e is UnauthorizedAccessException or IOException or ArgumentException )
		{
			return null;
		}
	}

	private string? SearchVault( string name, string[] storedSegments )
	{
		if( _vaultRoot is null || !Directory.Exists( _vaultRoot ) )
			return null;
		Dictionary<string, List<string>> index = Volatile.Read( ref _byName ) ?? GetOrBuildIndex( _vaultRoot );
		if( !index.TryGetValue( name, out var candidates ) || candidates.Count == 0 )
			return null;
		if( candidates.Count == 1 )
			return candidates[ 0 ];

		string best = candidates[ 0 ];
		int bestScore = SharedParentFolders( Segments( best ), storedSegments );
		for( int i = 1; i < candidates.Count; i++ )
		{
			string candidate = candidates[ i ];
			int score = SharedParentFolders( Segments( candidate ), storedSegments );
			if( score > bestScore || ( score == bestScore && StringComparer.Ordinal.Compare( candidate, best ) < 0 ) )
			{
				best = candidate;
				bestScore = score;
			}
		}
		return best;
	}

	private Dictionary<string, List<string>> GetOrBuildIndex( string root )
	{
		lock( _indexGate )
		{
			if( _byName is { } existing )
				return existing;
			var built = IndexVault( root );
			Volatile.Write( ref _byName, built );
			return built;
		}
	}

	private static Dictionary<string, List<string>> IndexVault( string root )
	{
		var index = new Dictionary<string, List<string>>( StringComparer.OrdinalIgnoreCase );
		try
		{
			foreach( string file in Directory.EnumerateFiles( root, "*.sld*", Recursive ) )
			{
				string name = Path.GetFileName( file );
				if( !index.TryGetValue( name, out var list ) )
					index[ name ] = list = [];
				list.Add( file );
			}
		}
		catch( Exception e ) when( e is UnauthorizedAccessException or IOException or ArgumentException )
		{
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
