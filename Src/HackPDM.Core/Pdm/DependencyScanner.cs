using HackPDM.Core.Configuration;
using HackPDM.Core.Services;
using HackPDM.Core.SolidWorks;

namespace HackPDM.Core.Pdm;

/// <summary>A dependency that prevented a SolidWorks file from being committed.</summary>
public sealed record BrokenDependency( string? FullPath, HackResult Reason );

/// <summary>Recursively collects local files and all SolidWorks references needed for a commit.</summary>
public static class DependencyScanner
{
	private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
		? StringComparer.OrdinalIgnoreCase
		: StringComparer.Ordinal;

	public static Task<(bool Success, List<HackFile> Files, List<BrokenDependency> Broken)> GetFileWithDependenciesAsync( EntryRow entry )
		=> GetFileWithDependenciesAsync( new HackFile( entry ) );

	public static Task<(bool Success, List<HackFile> Files, List<BrokenDependency> Broken)> GetFileWithDependenciesAsync( HackFile file )
		=> Task.Run( () => ScanSingle( file ) );

	public static async Task<(List<HackFile> Files, List<BrokenDependency> Broken)> GetFolderWithDependenciesAsync( string folderPath )
	{
		HashSet<HackFile> files = [];
		List<BrokenDependency> broken = [];
		foreach( string path in Directory.EnumerateFiles( folderPath, "*", SearchOption.TopDirectoryOnly ) )
		{
			if( CommitOperations.IsFilteredName( Path.GetFileName( path ) ) )
				continue;
			HackFile? hack = HackFile.GetFromPath( path );
			if( hack is not { Exists: true } )
				continue;
			if( !SwDocumentTypes.IsSolidWorks( path ) )
			{
				files.Add( hack );
				continue;
			}
			var (success, tree, results) = await GetFileWithDependenciesAsync( hack );
			if( success )
			{
				foreach( HackFile item in tree )
					files.Add( item );
			}
			else
			{
				broken.AddRange( results );
			}
		}
		return ( [ .. files ], broken );
	}

	private static (bool Success, List<HackFile> Files, List<BrokenDependency> Broken) ScanSingle( HackFile root )
	{
		SwReferenceResolver resolver = new( HackDefaults.Instance.PwaPathAbsolute );
		List<HackFile> collected = [];
		List<BrokenDependency> broken = [];
		HashSet<string> activeBranch = new( PathComparer );
		HashSet<string> visited = new( PathComparer );

		Walk( root, resolver, collected, broken, activeBranch, visited );
		return broken.Count > 0 ? ( false, [], broken ) : ( true, collected, [] );
	}

	private static void Walk(
		HackFile file,
		SwReferenceResolver resolver,
		List<HackFile> collected,
		List<BrokenDependency> broken,
		HashSet<string> activeBranch,
		HashSet<string> visited )
	{
		HackResult check = ValidateFile( file );
		if( check != HackResult.Clean )
		{
			broken.Add( new BrokenDependency( file.FullPath, check ) );
			return;
		}

		string canonical = Path.GetFullPath( file.FullPath! );
		if( activeBranch.Contains( canonical ) || !visited.Add( canonical ) )
			return;

		collected.Add( file );
		if( file.TypeExt is not { } ext || !SwDocumentTypes.IsSolidWorksExtension( ext ) )
			return;

		List<SwDependencies.Reference> references;
		try
		{
			references = SwDependencies.Of( canonical, resolver );
		}
		catch( Exception e )
		{
			HackLogger.Warn( "Dependencies", $"References of '{canonical}' could not be read: {e.Message}", e );
			broken.Add( new BrokenDependency( canonical, HackResult.CorruptDepFile ) );
			return;
		}

		activeBranch.Add( canonical );
		try
		{
			foreach( var (stored, local) in references )
			{
				string refPath = local ?? UnresolvedPath( stored, canonical );
				string refKey = TryFullPath( refPath );
				if( activeBranch.Contains( refKey ) )
					continue;
				HackFile child = HackFile.GetFromPath( refPath ) ?? new HackFile { FullPath = refPath };
				Walk( child, resolver, collected, broken, activeBranch, visited );
			}
		}
		finally
		{
			activeBranch.Remove( canonical );
		}
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
