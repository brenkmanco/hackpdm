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
	/// <summary>How far <paramref name="paths"/> (root first) already exists on the server.</summary>
	public static async Task<Result<LastAvailableDirectory>> LastAvailableDirectory( IReadOnlyList<string> paths )
	{
		Result<OdooRow> last = await OdooRpc.ExecuteAsync<OdooRow>( GetHpModel()!, "last_available_directory", [ paths ] );
		return last.Map( row => new LastAvailableDirectory( row?.Int( "index" ) ?? 0, row?.Int( "dir_id" ) ?? 1 ) );
	}

	/// <summary>
	/// Creates the folders of <paramref name="paths"/> (below root) that don't exist yet; the created
	/// directories, or the existing deepest one when they all exist.
	/// </summary>
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

	/// <summary>This directory with every directory (and, with <paramref name="withEntries"/>, entry) below it.</summary>
	public async Task<Result<SubdirectoryDto>> GetSubdirectoriesAsync( bool withEntries = true )
	{
		if( !IsRecord || id is not int directoryId )
			return Result<SubdirectoryDto>.Fail( "not a directory read from the server" );
		Result<OdooRow> tree = await OdooRpc.ExecuteAsync<OdooRow>( HpModel!, "get_children_directories_by_id", [ directoryId, withEntries ], OdooRpc.LongTimeout );
		return tree.Map( row => SubdirectoryDto.FromRow( row ?? [] ) );
	}

	/// <summary>The entries directly in a directory (deleted ones too with <paramref name="showInActive"/>).</summary>
	public static async Task<Result<IReadOnlyList<RemoteEntryDto>>> GetEntriesAsync( int directoryId, bool showInActive = false )
	{
		// hp.directory.get_entries(value) expects value = [directory_id, show_inactive]
		Result<OdooRow> entries = await OdooRpc.ExecuteAsync<OdooRow>( GetHpModel()!, "get_entries", [ new object?[] { directoryId, showInActive } ], OdooRpc.LongTimeout );
		return entries.Map( map => RemoteEntryDto.ListFrom( map ) );
	}

	public Task<Result<IReadOnlyList<int>>> GetDirectoryEntryIDsAsync( bool withSubEntries = false, bool withDeleted = true )
		=> GetDirectoryEntryIDsAsync( this.id ?? 0, withSubEntries, withDeleted );
	/// <summary>The ids of the entries in a directory (and, with <paramref name="withSubEntries"/>, below it).</summary>
	public static Task<Result<IReadOnlyList<int>>> GetDirectoryEntryIDsAsync( int directoryId, bool withSubEntries = false, bool withDeleted = false )
		=> directoryId != 0
			? OdooRpc.ExecuteAsync<IReadOnlyList<int>>( GetHpModel()!, "get_all_entry_ids", [ directoryId, withDeleted, withSubEntries ], OdooRpc.LongTimeout )
			: Task.FromResult( Result<IReadOnlyList<int>>.Fail( "no directory" ) );
}
