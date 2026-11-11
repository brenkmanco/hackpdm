using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Operations;
using HackPDM.Infrastructure.SldWrks;

namespace HackPDM.Infrastructure.Hack;

/// <summary>Which local files a file or folder brings with it: SolidWorks files pull in everything they reference.</summary>
public static class DependencyScanner
{
	/// <summary>
	/// <paramref name="file"/> and everything it references. False with the unresolved nodes when
	/// any reference can't be found.
	/// </summary>
	public static async Task<(bool success, List<HackFile> hackFiles, List<HackResultTree.ResultNode> hackResults)> GetHackFileWithDependencies( this HackFile file )
	{
		HackResultTree hackTree = new( file );
		await Task.Run( () => hackTree.Root?.ProcessDependencies() );
		if( hackTree.Root?.IsBrokenPropagated is true )
			return (false, [], hackTree.Root.BrokenNodes as List<HackResultTree.ResultNode> ?? []);
		List<HackFile> hackFiles = hackTree.Root?.GetAllNodes().Select( r => r.Value?.Hack ).OfType<HackFile>().ToList() ?? [];
		return (true, hackFiles, []);
	}

	public static Task<(bool success, List<HackFile> hackFiles, List<HackResultTree.ResultNode> hackResults)> GetHackFileWithDependencies( EntryRow entry )
		=> new HackFile( entry ).GetHackFileWithDependencies();

	/// <summary>
	/// The files directly in <paramref name="folderPath"/> plus everything its SolidWorks files
	/// reference, each once. Names the server's entry filters reject (SolidWorks "~$" lock files)
	/// are left out. A SolidWorks file whose references can't all be found is left out with its
	/// tree and reported in <paramref name="broken"/>; returns false when there were any.
	/// </summary>
	public static bool GetHackFolderWithDependencies( string folderPath, out List<HackFile> hackFiles, out List<HackResultTree.ResultNode> broken )
	{
		HashSet<HackFile> files = [];
		broken = [];
		foreach( string path in Directory.EnumerateFiles( folderPath, "*", SearchOption.TopDirectoryOnly ) )
		{
			if( PdmCommitOperations.IsFilteredName( Path.GetFileName( path ) ) )
				continue;
			HackFile? hack = HackFile.GetFromPath( path );
			if( hack is not { Exists: true } )
				continue;
			if( !SwDocumentTypes.IsSolidWorks( path ) )
			{
				files.Add( hack );
				continue;
			}
			// the tree used to be kept only when it was broken, so no SolidWorks file was ever committed
			var (success, tree, results) = GetHackFileWithDependencies( hack ).GetAwaiter().GetResult();
			if( success )
				files.AddAll( tree );
			else
				broken.AddRange( results );
		}
		hackFiles = [ .. files ];
		return broken.Count == 0;
	}
}
