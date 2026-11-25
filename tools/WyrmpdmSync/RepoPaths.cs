namespace WyrmpdmSync;

/// <summary>Where things live in the repository.</summary>
internal sealed class RepoPaths( string root )
{
	public string Root { get; } = root;
	public string DesignDir => Path.Combine( Root, "design" );
	public string Manifest => Path.Combine( DesignDir, "penpot-sync.json" );
	public string TokenFile => Path.Combine( DesignDir, ".penpot-token" );
	public string CacheDir => Path.Combine( DesignDir, ".cache" );
	public string CachedFile => Path.Combine( CacheDir, "penpot-file.json" );

	/// <summary>The repository holding <paramref name="start"/> (the folder with HackPDM.sln).</summary>
	public static RepoPaths Find( string? start = null )
	{
		for( DirectoryInfo? dir = new( start ?? Directory.GetCurrentDirectory() ); dir is not null; dir = dir.Parent )
			if( File.Exists( Path.Combine( dir.FullName, "HackPDM.sln" ) ) )
				return new RepoPaths( dir.FullName );
		throw new SyncException( "Run this inside the WyrmPDM repository (no HackPDM.sln found above the current folder)." );
	}

	/// <summary>A repository-relative path with forward slashes, as stored in the manifest.</summary>
	public string Relative( string path ) => Path.GetRelativePath( Root, path ).Replace( '\\', '/' );
}

/// <summary>A problem the user can fix; printed without a stack trace.</summary>
internal sealed class SyncException( string message ) : Exception( message );
