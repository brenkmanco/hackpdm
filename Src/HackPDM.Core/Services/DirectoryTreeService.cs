using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;
using HackPDM.Core.Pdm;

namespace HackPDM.Core.Services;

/// <summary>Builds the directory tree shown in the file manager: Odoo folders merged with local-only folders.</summary>
public interface IDirectoryTreeService
{
	Task<DirectoryNode?> LoadAsync();
	Task<DirectoryNode?> LoadAsync( IProgress<string>? progress, CancellationToken token = default ) => LoadAsync();
}

public sealed class DirectoryTreeService : IDirectoryTreeService
{
	private static readonly EnumerationOptions RecursiveDirOptions = new()
	{
		RecurseSubdirectories = true,
		IgnoreInaccessible = true,
		ReturnSpecialDirectories = false,
		AttributesToSkip = FileAttributes.None,
	};

	public Task<DirectoryNode?> LoadAsync() => LoadAsync( progress: null, token: CancellationToken.None );

	public async Task<DirectoryNode?> LoadAsync( IProgress<string>? progress, CancellationToken token = default )
	{
		try
		{
			if( OdooDefaults.Instance.HpDirectoryRoot is not HpDirectory remoteRoot )
				return null;

			string pwa = HackDefaults.Instance.PwaPathAbsolute;
			string rootName = remoteRoot.name is { Length: > 0 } rn ? rn : PwaPaths.Root;
			Task<(HashSet<string> Paths, bool RootExists)> localScanTask = Task.Run( () => LocalTreePathSet( pwa, rootName ), token );

			progress?.Report( "Fetching folder structure from Odoo…" );
			Result<SubdirectoryDto> tree = await remoteRoot.GetSubdirectoriesAsync( false );
			token.ThrowIfCancellationRequested();
			if( tree.Value is not { } remote )
			{
				HackLogger.Error( "DirectoryTree", $"Directory tree could not be read: {tree.Error}" );
				return null;
			}

			progress?.Report( "Merging local workspace folders…" );
			var (localPaths, rootExists) = await localScanTask;
			return await Task.Run( () =>
			{
				HashSet<string> remotePaths = new( PwaPaths.PathComparer );
				DirectoryNode root = FromRemote( remote, parentPath: null, rootExists, localPaths, remotePaths );
				DirectoryTreeMerger.AddLocalDirectories( root, localPaths.Where( p => !remotePaths.Contains( p ) ) );
				root.SortRecursive();
				return root;
			}, token );
		}
		catch( OperationCanceledException )
		{
			return null;
		}
		catch( Exception exception )
		{
			HackLogger.Error( "DirectoryTree", $"Directory tree failed to load: {exception.Message}", exception );
			return null;
		}
	}

	private static DirectoryNode FromRemote(
		SubdirectoryDto directory,
		string? parentPath,
		bool rootExistsLocally,
		HashSet<string> localPaths,
		HashSet<string> remotePaths )
	{
		string name = directory.Name is { Length: > 0 } n ? n : PwaPaths.Root;
		string treePath = parentPath is null ? name : $"{parentPath}\\{name}";
		remotePaths.Add( treePath );

		DirectoryNode node = new( name, directory.Id )
		{
			ExistsLocally = parentPath is null ? rootExistsLocally : localPaths.Contains( treePath ),
		};
		for( int i = 0; i < directory.Directories.Count; i++ )
			node.Children.Add( FromRemote( directory.Directories[ i ], treePath, rootExistsLocally, localPaths, remotePaths ) );
		return node;
	}

	private static (HashSet<string> Paths, bool RootExists) LocalTreePathSet( string pwa, string rootName )
	{
		HashSet<string> set = new( PwaPaths.PathComparer );
		if( string.IsNullOrEmpty( pwa ) || !Directory.Exists( pwa ) )
			return ( set, false );

		string normalizedPwa = Path.TrimEndingDirectorySeparator( Path.GetFullPath( pwa ) );
		int prefixLen = normalizedPwa.Length;
		foreach( string dir in Directory.EnumerateDirectories( normalizedPwa, "*", RecursiveDirOptions ) )
		{
			if( dir.Length <= prefixLen )
				continue;
			ReadOnlySpan<char> tail = dir.AsSpan( prefixLen ).Trim( "\\/" );
			if( tail.IsEmpty )
				continue;
			string treePath = tail.Contains( '/' )
				? string.Concat( rootName, "\\", tail ).Replace( '/', '\\' )
				: string.Concat( rootName, "\\", tail );
			set.Add( treePath );
		}
		return ( set, true );
	}
}
