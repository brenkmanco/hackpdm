using HackPDM.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using HackPDM.Core.Hack;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;

namespace HackPDM.Mvvm.Models.Hack;

/// <summary>Builds the directory tree shown in the file manager: Odoo folders plus local-only ones.</summary>
public interface IDirectoryTreeService
{
	/// <summary>Returns the merged tree, or null when the remote tree couldn't be read.</summary>
	Task<DirectoryNode?> LoadAsync();
}

public sealed class DirectoryTreeService : IDirectoryTreeService
{
	private const string RootName = PwaPaths.Root;

	public async Task<DirectoryNode?> LoadAsync()
	{
		try
		{
			if( OdooDefaults.Instance.HpDirectoryRoot is not HpDirectory remoteRoot )
				return null;
			Result<SubdirectoryDto> tree = await remoteRoot.GetSubdirectoriesAsync( false );
			if( tree.Value is not { } remote )
			{
				Debug.WriteLine( $"directory tree could not be read: {tree.Error}" );
				return null;
			}

			string pwa = HackDefaults.Instance.PwaPathAbsolute;
			return await Task.Run( () =>
			{
				DirectoryNode root = FromRemote( remote, parentPath: null );
				DirectoryTreeMerger.AddLocalDirectories( root, LocalTreePaths( pwa ) );
				root.SortRecursive();
				return root;
			} );
		}
		catch( Exception exception )
		{
			Debug.WriteLine( $"directory tree failed to load: {exception}" );
			return null;
		}
	}

	/// <summary>Converts the server's directory tree into nodes.</summary>
	private static DirectoryNode FromRemote( SubdirectoryDto directory, string? parentPath )
	{
		string name = directory.Name is { Length: > 0 } n ? n : RootName;
		string treePath = parentPath is null ? name : $"{parentPath}\\{name}";

		DirectoryNode node = new( name, directory.Id )
		{
			ExistsLocally = Directory.Exists( PwaPaths.ToAbsolute( treePath ) ),
		};
		foreach( SubdirectoryDto child in directory.Directories )
			node.Children.Add( FromRemote( child, treePath ) );
		return node;
	}

	/// <summary>Every folder under the PWA as a tree path ("root\a\b").</summary>
	private static IEnumerable<string> LocalTreePaths( string pwa )
		=> Directory.Exists( pwa )
			? Directory.EnumerateDirectories( pwa, "*", SearchOption.AllDirectories ).Select( dir => PwaPaths.ToServerName( dir ) ).OfType<string>()
			: [];
}
