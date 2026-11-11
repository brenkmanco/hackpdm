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

namespace HackPDM.Services;

/// <summary>Builds the directory tree shown in the file manager: Odoo folders plus local-only ones.</summary>
public interface IDirectoryTreeService
{
	/// <summary>Returns the merged tree, or null when the remote tree couldn't be read.</summary>
	Task<DirectoryNode?> LoadAsync();
}

public sealed class DirectoryTreeService : IDirectoryTreeService
{
	private const string RootName = "root";

	public async Task<DirectoryNode?> LoadAsync()
	{
		try
		{
			if( OdooDefaults.Instance.HpDirectoryRoot is not HpDirectory remoteRoot
				|| await remoteRoot.GetSubdirectoriesAsync( false ) is not Hashtable remote )
				return null;

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

	/// <summary>Converts Odoo's nested { name, id, directories } tables into nodes.</summary>
	private static DirectoryNode FromRemote( Hashtable table, string? parentPath )
	{
		string name = table[ "name" ] as string ?? RootName;
		string treePath = parentPath is null ? name : $"{parentPath}\\{name}";

		DirectoryNode node = new( name, table[ "id" ] as int? ?? 0 )
		{
			ExistsLocally = Directory.Exists( HackDefaults.DefaultPath( treePath, true ) ),
		};

		if( table[ "directories" ] is Hashtable { Count: > 0 } children )
		{
			foreach( DictionaryEntry pair in children )
			{
				if( pair.Value is Hashtable child )
					node.Children.Add( FromRemote( child, treePath ) );
			}
		}
		return node;
	}

	/// <summary>Every folder under the PWA as a tree path ("root\a\b").</summary>
	private static IEnumerable<string> LocalTreePaths( string pwa )
		=> Directory.Exists( pwa )
			? Directory.EnumerateDirectories( pwa, "*", SearchOption.AllDirectories ).Select( dir => RootName + dir[ pwa.TrimEnd( '\\', '/' ).Length.. ] )
			: [];
}
