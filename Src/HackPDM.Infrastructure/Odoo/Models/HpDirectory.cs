using System;
using HackPDM.Core;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;


// Resharper disable InconsistentNaming

namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.HP_DIRECTORY_NAME, OdooDefaultsConstants.HP_DIRECTORY)]
public partial class HpDirectory : HpBaseModelTransport<HpDirectory>, IHpDirectoryModel
{
	[OdooProp(OdooFieldType.Char, "name")] public string? name { get; set; }
	[OdooProp(OdooFieldType.Char, "parent_path")] public string? parent_path { get; set; }
	[OdooProp(OdooFieldType.Many2One, "parent_id")] public Many2One? parent_id { get; set; }
	IMany2One? IHpDirectoryModel.parent_id { get =>(IMany2One?)parent_id; set => parent_id = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2One, "default_cat")] public Many2One? default_cat { get; set; }
	IMany2One? IHpDirectoryModel.default_cat { get =>(IMany2One?)default_cat; set => default_cat = (Many2One?)value; }
	[OdooProp(OdooFieldType.Boolean, "deleted")] public bool? deleted { get; set; }
	[OdooProp(OdooFieldType.Boolean, "sandboxed")] public bool? sandboxed { get; set; }
}
public partial class HpDirectory : HpBaseModelTransport<HpDirectory>
{
	/// <summary>Asks the server how much of a folder chain already exists.</summary>
	/// <param name="paths">The folder names from the root down, starting with the root's name (for example <c>["root", "a", "b"]</c>).</param>
	/// <returns>
	/// On success, the index in <paramref name="paths"/> of the deepest existing folder and that folder's
	/// hp.directory id (0 and 1, the root, when the server's row lacks them); otherwise the RPC error.
	/// </returns>
	/// <remarks>Calls the addon method <c>hp.directory.last_available_directory</c>.</remarks>
	public static async Task<Result<LastAvailableDirectory>> LastAvailableDirectory( IReadOnlyList<string> paths )
	{
		Result<OdooRow> last = await OdooRpc.ExecuteAsync<OdooRow>( GetHpModel()!, "last_available_directory", [ paths ] );
		return last.Map( row => new LastAvailableDirectory( row?.Int( "index" ) ?? 0, row?.Int( "dir_id" ) ?? 1 ) );
	}

	/// <summary>
	/// Creates on the server the folders of a path below the root that don't exist yet, one record at a time,
	/// each parented to the previous one.
	/// </summary>
	/// <param name="paths">The folder names below the root, outermost first; <see cref="HackPDM.Core.PwaPaths.Root"/> is prepended.</param>
	/// <returns>
	/// The newly created directories, outermost first; or, when the whole path already exists, a one-element
	/// array holding the deepest existing directory (that element is <see langword="null"/> if it could not be read).
	/// </returns>
	/// <remarks>
	/// New directories get <c>sandboxed</c> and <c>deleted</c> set to false and category 1 as <c>default_cat</c>
	/// only when that category exists locally. Creation stops at the first failure, leaving any already
	/// created folders on the server.
	/// </remarks>
	/// <exception cref="HackPDM.Infrastructure.Odoo.OdooException">The <see cref="LastAvailableDirectory"/> call failed.</exception>
	/// <exception cref="System.InvalidOperationException">The server did not create one of the directories.</exception>
	public async static Task<HpDirectory[]?> CreateNew( IEnumerable<string> paths )
	{
		List<string> path = [ PwaPaths.Root, .. paths ];

		LastAvailableDirectory last = ( await LastAvailableDirectory( path ) ).Unwrap();
		int nextIndex = last.Index + 1;
		if( nextIndex >= path.Count )
			return [ await GetRecordByIdAsync( last.DirectoryId ) ?? null ];

		// category 1 is the usual default, but it may not exist (a fresh database has no categories),
		// and referencing a missing record makes the server reject the directory
		Many2One? defaultCategory = OdooDefaults.Instance.HpCategories?.Any( c => c.id == 1 ) is true ? 1 : null;

		HpDirectory[] directories = new HpDirectory[ path.Count - nextIndex ];
		int lastParentId = last.DirectoryId;
		for( int i = nextIndex; i < path.Count; i++ )
		{
			HpDirectory newDirectory = new()
			{
				name = path[ i ],
				parent_id = lastParentId,
				sandboxed = false,
				deleted = false,
				default_cat = defaultCategory,
			};
			if( await newDirectory.CreateAsync( false ) == 0 )
				throw new InvalidOperationException( $"directory '{path[ i ]}' not created: {newDirectory.LastError}" );

			directories[ i - nextIndex ] = newDirectory;
			lastParentId = newDirectory.id ?? 0;
		}
		return directories;
	}

	/// <summary>Fetches the tree below this directory in one server call.</summary>
	/// <param name="withEntries">Whether to include each directory's entries as well as its subdirectories.</param>
	/// <returns>
	/// This directory as a <see cref="HackPDM.Infrastructure.Odoo.SubdirectoryDto"/> with every directory (and, with
	/// <paramref name="withEntries"/>, entry) below it; a failed result when this object was not read from the
	/// server or the call fails.
	/// </returns>
	/// <remarks>Calls <c>hp.directory.get_children_directories_by_id</c> with the long RPC timeout.</remarks>
	public async Task<Result<SubdirectoryDto>> GetSubdirectoriesAsync( bool withEntries = true )
	{
		if( !IsRecord || id is not int directoryId )
			return Result<SubdirectoryDto>.Fail( "not a directory read from the server" );
		Result<OdooRow> tree = await OdooRpc.ExecuteAsync<OdooRow>( HpModel!, "get_children_directories_by_id", [ directoryId, withEntries ], OdooRpc.LongTimeout );
		return tree.Map( row => SubdirectoryDto.FromRow( row ?? [] ) );
	}

	/// <summary>Lists the entries directly in a directory (not in its subdirectories).</summary>
	/// <param name="directoryId">The hp.directory id.</param>
	/// <param name="showInActive">Whether to include deleted (inactive) entries.</param>
	/// <returns>The entries as <see cref="HackPDM.Infrastructure.Odoo.RemoteEntryDto"/> values, or the RPC error.</returns>
	/// <remarks>Calls <c>hp.directory.get_entries</c> with the long RPC timeout.</remarks>
	public static async Task<Result<IReadOnlyList<RemoteEntryDto>>> GetEntriesAsync( int directoryId, bool showInActive = false )
	{
		// hp.directory.get_entries(value) expects value = [directory_id, show_inactive]
		Result<OdooRow> entries = await OdooRpc.ExecuteAsync<OdooRow>( GetHpModel()!, "get_entries", [ new object?[] { directoryId, showInActive } ], OdooRpc.LongTimeout );
		return entries.Map( map => RemoteEntryDto.ListFrom( map ) );
	}

	/// <summary>Lists the ids of the entries in this directory.</summary>
	/// <param name="withSubEntries">Whether to include entries in subdirectories.</param>
	/// <param name="withDeleted">Whether to include deleted entries. Note this overload defaults to <see langword="true"/>, unlike the static one.</param>
	/// <returns>The entry ids, or a failed result when this directory has no id.</returns>
	/// <remarks>Forwards to <see cref="GetDirectoryEntryIDsAsync(int, bool, bool)"/> with this record's id.</remarks>
	public Task<Result<IReadOnlyList<int>>> GetDirectoryEntryIDsAsync( bool withSubEntries = false, bool withDeleted = true )
		=> GetDirectoryEntryIDsAsync( this.id ?? 0, withSubEntries, withDeleted );
	/// <summary>Lists the ids of the entries in a directory and, optionally, below it.</summary>
	/// <param name="directoryId">The hp.directory id; 0 returns a failed result without calling the server.</param>
	/// <param name="withSubEntries">Whether to include entries in subdirectories.</param>
	/// <param name="withDeleted">Whether to include deleted entries.</param>
	/// <returns>The entry ids, or the error.</returns>
	/// <remarks>Calls <c>hp.directory.get_all_entry_ids</c> with the long RPC timeout.</remarks>
	public static Task<Result<IReadOnlyList<int>>> GetDirectoryEntryIDsAsync( int directoryId, bool withSubEntries = false, bool withDeleted = false )
		=> directoryId != 0
			? OdooRpc.ExecuteAsync<IReadOnlyList<int>>( GetHpModel()!, "get_all_entry_ids", [ directoryId, withDeleted, withSubEntries ], OdooRpc.LongTimeout )
			: Task.FromResult( Result<IReadOnlyList<int>>.Fail( "no directory" ) );
}
