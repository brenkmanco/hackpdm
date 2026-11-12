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
	/// Collects <paramref name="file"/> and every local file it references, recursively, by building and
	/// resolving a <see cref="HackResultTree"/>. Only SolidWorks files have references; any other file yields just itself.
	/// </summary>
	/// <param name="file">The local file to start from.</param>
	/// <returns>
	/// <c>success</c> true with <c>hackFiles</c> holding the file and all its (transitive) references, each tree node once;
	/// or <c>success</c> false with an empty file list and <c>hackResults</c> holding the broken nodes (missing, outside
	/// the working folder, or unreadable) when any part of the tree is broken.
	/// </returns>
	/// <remarks>
	/// Reference resolution reads the SolidWorks files on a thread-pool thread (<see cref="Task.Run(Func{Task})"/>).
	/// Circular references are skipped. See <see cref="HackResultTree.ResultNode.ProcessDependencies"/>.
	/// </remarks>
	public static async Task<(bool success, List<HackFile> hackFiles, List<HackResultTree.ResultNode> hackResults)> GetHackFileWithDependencies( this HackFile file )
	{
		HackResultTree hackTree = new( file );
		await Task.Run( () => hackTree.Root?.ProcessDependencies() );
		if( hackTree.Root?.IsBrokenPropagated is true )
			return (false, [], hackTree.Root.BrokenNodes as List<HackResultTree.ResultNode> ?? []);
		List<HackFile> hackFiles = hackTree.Root?.GetAllNodes().Select( r => r.Value?.Hack ).OfType<HackFile>().ToList() ?? [];
		return (true, hackFiles, []);
	}

	/// <summary>Collects the local file of a browser entry and everything it references; see <see cref="GetHackFileWithDependencies(HackFile)"/>.</summary>
	/// <param name="entry">The entry; its local file is used.</param>
	/// <returns>The same tuple as <see cref="GetHackFileWithDependencies(HackFile)"/>.</returns>
	public static Task<(bool success, List<HackFile> hackFiles, List<HackResultTree.ResultNode> hackResults)> GetHackFileWithDependencies( EntryRow entry )
		=> new HackFile( entry ).GetHackFileWithDependencies();

	/// <summary>
	/// Collects the files directly in <paramref name="folderPath"/> (not subfolders) plus everything its SolidWorks files
	/// reference, each once. Names the server's entry filters reject (SolidWorks "~$" lock files) are left out.
	/// </summary>
	/// <param name="folderPath">The local folder to scan.</param>
	/// <returns>
	/// <c>Files</c>: the folder's files and their references, without duplicates. <c>Broken</c>: the broken nodes of every
	/// SolidWorks file whose references can't all be found; such a file is left out of <c>Files</c> together with its whole tree.
	/// </returns>
	/// <remarks>Files are checked one after another; filtering uses <see cref="PdmCommitOperations.IsFilteredName"/>.</remarks>
	/// <exception cref="DirectoryNotFoundException"><paramref name="folderPath"/> does not exist.</exception>
	/// <exception cref="UnauthorizedAccessException">The folder can't be listed.</exception>
	public static async Task<(List<HackFile> Files, List<HackResultTree.ResultNode> Broken)> GetHackFolderWithDependenciesAsync( string folderPath )
	{
		HashSet<HackFile> files = [];
		List<HackResultTree.ResultNode> broken = [];
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
			var (success, tree, results) = await GetHackFileWithDependencies( hack );
			if( success )
				files.AddAll( tree );
			else
				broken.AddRange( results );
		}
		return ( [ .. files ], broken );
	}
}
