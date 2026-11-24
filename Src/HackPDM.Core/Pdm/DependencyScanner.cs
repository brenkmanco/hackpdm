using HackPDM.Core.Configuration;
using HackPDM.Core.Services;
using HackPDM.Core.SolidWorks;

namespace HackPDM.Core.Pdm;

/// <summary>A dependency that prevented a SolidWorks file from being committed.</summary>
public sealed record BrokenDependency( string? FullPath, HackResult Reason );

/// <summary>Recursively collects local files and all SolidWorks references needed for a commit.</summary>
public static class DependencyScanner
{
	public static Task<(bool Success, List<HackFile> Files, List<BrokenDependency> Broken)> GetFileWithDependenciesAsync( EntryRow entry )
		=> GetFileWithDependenciesAsync( new HackFile( entry ) );

	public static Task<(bool Success, List<HackFile> Files, List<BrokenDependency> Broken)> GetFileWithDependenciesAsync( HackFile file )
		=> Task.Run( () => ScanSingle( file ) );

	public static Task<(List<HackFile> Files, List<BrokenDependency> Broken)> GetFolderWithDependenciesAsync( string folderPath )
		=> Task.Run( () => ScanFolder( folderPath ) );

	private static (List<HackFile> Files, List<BrokenDependency> Broken) ScanFolder( string folderPath )
	{
		if( string.IsNullOrWhiteSpace( folderPath ) || !Directory.Exists( folderPath ) )
			return ( [], [] );

		HashSet<HackFile> files = [];
		List<BrokenDependency> broken = [];
		HashSet<(string? Path, HackResult Reason)> seenBroken = new( BrokenKeyComparer.Instance );
		SwReferenceResolver resolver = new( HackDefaults.Instance.PwaPathAbsolute );
		Dictionary<string, (List<SwDependencies.Reference>? Refs, Exception? Error)> depCache = new( PwaPaths.PathComparer );
		Dictionary<string, HackFile> fileCache = new( PwaPaths.PathComparer );
		HashSet<string> activeBranch = new( PwaPaths.PathComparer );
		HashSet<string> visited = new( PwaPaths.PathComparer );

		IEnumerable<string> paths;
		try
		{
			paths = Directory.EnumerateFiles( folderPath, "*", SearchOption.TopDirectoryOnly );
		}
		catch( Exception ex ) when( ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException )
		{
			HackLogger.Warn( nameof( DependencyScanner ), $"Could not enumerate '{folderPath}': {ex.Message}", ex );
			return ( [], [] );
		}

		foreach( string path in paths )
		{
			if( CommitOperations.IsFilteredName( Path.GetFileName( path ) ) )
				continue;
			string canonical = TryFullPath( path );
			if( !fileCache.TryGetValue( canonical, out HackFile? hack ) )
			{
				hack = HackFile.GetFromPath( path );
				if( hack is not { Exists: true } )
					continue;
				fileCache[ canonical ] = hack;
			}
			if( !SwDocumentTypes.IsSolidWorks( path ) )
			{
				HackResult check = ValidateFile( hack );
				if( check == HackResult.Clean )
					files.Add( hack );
				else
					AddBroken( broken, seenBroken, hack.FullPath, check );
				continue;
			}

			List<HackFile> tree = [];
			List<BrokenDependency> results = [];
			HashSet<(string? Path, HackResult Reason)> treeBroken = new( BrokenKeyComparer.Instance );
			activeBranch.Clear();
			visited.Clear();
			Walk( hack, resolver, tree, results, treeBroken, activeBranch, visited, depCache, fileCache );
			if( results.Count == 0 )
			{
				for( int i = 0; i < tree.Count; i++ )
					files.Add( tree[ i ] );
			}
			else
			{
				for( int i = 0; i < results.Count; i++ )
					AddBroken( broken, seenBroken, results[ i ].FullPath, results[ i ].Reason );
			}
		}
		return ( [ .. files ], broken );
	}

	private static (bool Success, List<HackFile> Files, List<BrokenDependency> Broken) ScanSingle( HackFile root )
	{
		SwReferenceResolver resolver = new( HackDefaults.Instance.PwaPathAbsolute );
		List<HackFile> collected = [];
		List<BrokenDependency> broken = [];
		HashSet<(string? Path, HackResult Reason)> seenBroken = new( BrokenKeyComparer.Instance );
		HashSet<string> activeBranch = new( PwaPaths.PathComparer );
		HashSet<string> visited = new( PwaPaths.PathComparer );

		Walk( root, resolver, collected, broken, seenBroken, activeBranch, visited, depCache: null, fileCache: null );
		return broken.Count > 0 ? ( false, [], broken ) : ( true, collected, [] );
	}

	private static void Walk(
		HackFile file,
		SwReferenceResolver resolver,
		List<HackFile> collected,
		List<BrokenDependency> broken,
		HashSet<(string? Path, HackResult Reason)> seenBroken,
		HashSet<string> activeBranch,
		HashSet<string> visited,
		Dictionary<string, (List<SwDependencies.Reference>? Refs, Exception? Error)>? depCache,
		Dictionary<string, HackFile>? fileCache )
	{
		HackResult check = ValidateFile( file );
		if( check != HackResult.Clean )
		{
			AddBroken( broken, seenBroken, file.FullPath, check );
			return;
		}

		string canonical = Path.GetFullPath( file.FullPath! );
		if( activeBranch.Contains( canonical ) || !visited.Add( canonical ) )
			return;

		collected.Add( file );
		if( file.TypeExt is not { } ext || !SwDocumentTypes.IsSolidWorksExtension( ext ) )
			return;

		List<SwDependencies.Reference> references;
		if( depCache is not null && depCache.TryGetValue( canonical, out var cached ) )
		{
			if( cached.Error is not null || cached.Refs is null )
			{
				AddBroken( broken, seenBroken, canonical, HackResult.CorruptDepFile );
				return;
			}
			references = cached.Refs;
		}
		else
		{
			try
			{
				references = SwDependencies.Of( canonical, resolver );
				if( depCache is not null )
					depCache[ canonical ] = ( references, null );
			}
			catch( Exception e )
			{
				HackLogger.Warn( "Dependencies", $"References of '{canonical}' could not be read: {e.Message}", e );
				if( depCache is not null )
					depCache[ canonical ] = ( null, e );
				AddBroken( broken, seenBroken, canonical, HackResult.CorruptDepFile );
				return;
			}
		}

		activeBranch.Add( canonical );
		try
		{
			foreach( var (stored, local) in references )
			{
				string refPath = local ?? UnresolvedPath( stored, canonical );
				string refKey = TryFullPath( refPath );
				if( activeBranch.Contains( refKey ) || visited.Contains( refKey ) )
					continue;
				HackFile child;
				if( fileCache is not null )
				{
					if( !fileCache.TryGetValue( refKey, out HackFile? cachedChild ) )
						fileCache[ refKey ] = cachedChild = HackFile.GetFromPath( refPath ) ?? new HackFile { FullPath = refPath };
					child = cachedChild;
				}
				else
				{
					child = HackFile.GetFromPath( refPath ) ?? new HackFile { FullPath = refPath };
				}
				Walk( child, resolver, collected, broken, seenBroken, activeBranch, visited, depCache, fileCache );
			}
		}
		finally
		{
			activeBranch.Remove( canonical );
		}
	}

	private static void AddBroken(
		List<BrokenDependency> broken,
		HashSet<(string? Path, HackResult Reason)> seen,
		string? path,
		HackResult reason )
	{
		if( seen.Add( ( path, reason ) ) )
			broken.Add( new BrokenDependency( path, reason ) );
	}

	private sealed class BrokenKeyComparer : IEqualityComparer<(string? Path, HackResult Reason)>
	{
		public static readonly BrokenKeyComparer Instance = new();
		public bool Equals( (string? Path, HackResult Reason) x, (string? Path, HackResult Reason) y )
			=> x.Reason == y.Reason && PwaPaths.PathComparer.Equals( x.Path, y.Path );
		public int GetHashCode( (string? Path, HackResult Reason) obj )
			=> HashCode.Combine( obj.Path is null ? 0 : PwaPaths.PathComparer.GetHashCode( obj.Path ), obj.Reason );
	}

	private static HackResult ValidateFile( HackFile file )
	{
		if( string.IsNullOrEmpty( file.FullPath ) )
			return HackResult.MissingFile;
		HackResult result = HackResult.NotTested;
		if( !PwaPaths.IsInside( file.FullPath ) )
			result |= HackResult.OutOfPWA;
		if( file.Exists is not true && !File.Exists( file.FullPath ) )
			result |= HackResult.MissingFile;
		return result == HackResult.NotTested ? HackResult.Clean : result;
	}

	private static string UnresolvedPath( string stored, string referencing )
		=> OperatingSystem.IsWindows() || Path.GetDirectoryName( referencing ) is not { Length: > 0 } dir
			? stored
			: Path.Combine( dir, SwReferenceResolver.FileName( stored ) );

	private static string TryFullPath( string path )
	{
		try { return Path.GetFullPath( path ); }
		catch { return path; }
	}
}
