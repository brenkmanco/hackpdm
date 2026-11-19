using HackPDM.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Core.Hack;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;

namespace HackPDM.Mvvm.Models.Hack;

/// <summary>Builds the directory tree shown in the file manager: Odoo folders plus local-only ones.</summary>
public interface IDirectoryTreeService
{
	/// <summary>Loads the directory tree from the server and merges in the folders that exist only in the local vault.</summary>
	/// <returns>The merged tree with each level sorted, or null when the remote tree couldn't be read.</returns>
	/// <remarks>Failures (including exceptions) are written to the debug output and return null rather than throwing.
	/// The local vault is scanned recursively on a thread-pool thread.</remarks>
	Task<DirectoryNode?> LoadAsync();

	/// <summary>Loads the directory tree while reporting progress messages and honoring cancellation.</summary>
	Task<DirectoryNode?> LoadAsync( IProgress<string>? progress, CancellationToken token = default )
		=> LoadAsync();
}

/// <inheritdoc cref="IDirectoryTreeService"/>
public sealed class DirectoryTreeService : IDirectoryTreeService
{
	private const string RootName = PwaPaths.Root;
	private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
		? StringComparer.OrdinalIgnoreCase
		: StringComparer.Ordinal;

	/// <inheritdoc/>
	public Task<DirectoryNode?> LoadAsync() => LoadAsync( progress: null, token: CancellationToken.None );

	/// <inheritdoc/>
	public async Task<DirectoryNode?> LoadAsync( IProgress<string>? progress, CancellationToken token = default )
	{
		try
		{
			if( OdooDefaults.Instance.HpDirectoryRoot is not HpDirectory remoteRoot )
				return null;

			string pwa = HackDefaults.Instance.PwaPathAbsolute;
			// Overlap local folder enumeration with the Odoo RPC call instead of scanning sequentially.
			Task<HashSet<string>> localScanTask = Task.Run( () => LocalTreePathSet( pwa ), token );

			progress?.Report( "Fetching folder structure from Odoo…" );
			Result<SubdirectoryDto> tree = await remoteRoot.GetSubdirectoriesAsync( false );
			token.ThrowIfCancellationRequested();
			if( tree.Value is not { } remote )
			{
				HackPDM.Core.Logging.HackLogger.Error( "DirectoryTree", $"Directory tree could not be read: {tree.Error}" );
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
			HackPDM.Core.Logging.HackLogger.Error( "DirectoryTree", $"Directory tree failed to load: {exception.Message}", exception );
			return null;
		}
	}

	/// <summary>Converts the server's directory tree into nodes using pre-scanned local folder existence.</summary>
	private static DirectoryNode FromRemote( SubdirectoryDto directory, string? parentPath, HashSet<string> localPaths, HashSet<string> remotePaths )
	{
		string name = directory.Name is { Length: > 0 } n ? n : RootName;
		string treePath = parentPath is null ? name : $"{parentPath}\\{name}";
		remotePaths.Add( treePath );

		DirectoryNode node = new( name, directory.Id )
		{
			ExistsLocally = parentPath is null ? Directory.Exists( HackDefaults.Instance.PwaPathAbsolute ) : localPaths.Contains( treePath ),
		};
		foreach( SubdirectoryDto child in directory.Directories )
			node.Children.Add( FromRemote( child, treePath, localPaths, remotePaths ) );
		return node;
	}

	/// <summary>Every folder under the PWA as a tree path ("root\a\b").</summary>
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
