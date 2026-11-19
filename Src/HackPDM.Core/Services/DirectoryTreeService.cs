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
	private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
		? StringComparer.OrdinalIgnoreCase
		: StringComparer.Ordinal;

	public Task<DirectoryNode?> LoadAsync() => LoadAsync( progress: null, token: CancellationToken.None );

	public async Task<DirectoryNode?> LoadAsync( IProgress<string>? progress, CancellationToken token = default )
	{
		try
		{
			if( OdooDefaults.Instance.HpDirectoryRoot is not HpDirectory remoteRoot )
				return null;

			string pwa = HackDefaults.Instance.PwaPathAbsolute;
			Task<HashSet<string>> localScanTask = Task.Run( () => LocalTreePathSet( pwa ), token );

			progress?.Report( "Fetching folder structure from Odoo…" );
			Result<SubdirectoryDto> tree = await remoteRoot.GetSubdirectoriesAsync( false );
			token.ThrowIfCancellationRequested();
			if( tree.Value is not { } remote )
			{
				HackLogger.Error( "DirectoryTree", $"Directory tree could not be read: {tree.Error}" );
				return null;
			}

			progress?.Report( "Merging local workspace folders…" );
			HashSet<string> localPaths = await localScanTask;
			return await Task.Run( () =>
			{
				HashSet<string> remotePaths = new( PathComparer );
				DirectoryNode root = FromRemote( remote, parentPath: null, localPaths, remotePaths );
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
		HashSet<string> localPaths,
		HashSet<string> remotePaths )
	{
		string name = directory.Name is { Length: > 0 } n ? n : PwaPaths.Root;
		string treePath = parentPath is null ? name : $"{parentPath}\\{name}";
		remotePaths.Add( treePath );

		DirectoryNode node = new( name, directory.Id )
		{
			ExistsLocally = parentPath is null
				? Directory.Exists( HackDefaults.Instance.PwaPathAbsolute )
				: localPaths.Contains( treePath ),
		};
		foreach( SubdirectoryDto child in directory.Directories )
			node.Children.Add( FromRemote( child, treePath, localPaths, remotePaths ) );
		return node;
	}

	private static HashSet<string> LocalTreePathSet( string pwa )
	{
		HashSet<string> set = new( PathComparer );
		if( string.IsNullOrEmpty( pwa ) || !Directory.Exists( pwa ) )
			return set;

		EnumerationOptions opts = new()
		{
			RecurseSubdirectories = true,
			IgnoreInaccessible = true,
			ReturnSpecialDirectories = false,
			AttributesToSkip = FileAttributes.None,
		};
		foreach( string dir in Directory.EnumerateDirectories( pwa, "*", opts ) )
		{
			if( PwaPaths.ToServerName( dir ) is { } treePath )
				set.Add( treePath );
		}
		return set;
	}
}
