using System;
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

using OClient = HackPDM.Infrastructure.Odoo.OdooClient;

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
	public static Task<Hashtable> LastAvailableDirectory( ArrayList paths )
	{
		var last = OClient.CommandAsync<Hashtable>(GetHpModel(), "last_available_directory", [new ArrayList { paths }]);
		return last;
	}
	public async static Task<HpDirectory[]?> CreateNew( ArrayList paths )
	{
		paths.Insert( 0, "root" );

		Hashtable last = await LastAvailableDirectory( paths );
		// this means that all directories in paths were found 
		int nextIndex = 1;

		if( !last.TryGetValue( "index", out int index ) )
		{ index = 0; }
		if( !last.TryGetValue( "dir_id", out int lastDirId ) )
		{ lastDirId = 1; }
		nextIndex = index + 1;

		if( nextIndex >= paths.Count )
			return [ await GetRecordByIdAsync( lastDirId ) ?? null ];

		HpDirectory[] directories = new HpDirectory[paths.Count - nextIndex];
		int lastParentId = lastDirId;
		for( int i = nextIndex; i < paths.Count; i++ )
		{
			HpDirectory newDirectory = new()
			{
				name = (string)paths[i],
				parent_id = lastParentId,
				sandboxed = false,
				deleted = false,
				default_cat = 1,
			};
			await newDirectory.CreateAsync( false );

			if( newDirectory.id == 0 )
			{
				throw new Exception( "HpDirectory not created" );
			}

			directories[ nextIndex ] = newDirectory;
			// for next iteration
			lastParentId = newDirectory.id ?? 0;
		}
		return directories;
	}
	public async Task<Hashtable?> GetSubdirectoriesAsync( bool withEntries = true )
	{
		return this.IsRecord
			? await OClient.CommandAsync<Hashtable>( HpModel, "get_children_directories_by_id", new ArrayList( new ArrayList { this.id, withEntries } ) )
			: null;
	}
	public static async Task<Hashtable?> GetEntriesAsync( int? directoryId, bool showInActive = false )
		=> await OClient.CommandAsync<Hashtable>(
			GetHpModel(),
			"get_entries",
			[ with( new ArrayList { new ArrayList { directoryId, showInActive } } ) ]
		);

	public async Task<ArrayList?> GetDirectoryEntryIDsAsync( bool withSubEntries = false, bool withDeleted = true )
		=> await GetDirectoryEntryIDsAsync( this.id ?? 0, withSubEntries, withDeleted );
	public static async Task<ArrayList?> GetDirectoryEntryIDsAsync( int directoryId, bool withSubEntries = false, bool withDeleted = false )
		=> directoryId != 0
			? await OClient.CommandAsync<ArrayList>( GetHpModel()!, "get_all_entry_ids", [ directoryId, withDeleted, withSubEntries ], 10000 )
			: null;
}
public class ExplorerItem
{
    public string Name { get; set; }
    public string IconPath { get; set; } 
    public bool IsFolder { get; set; }
    public ObservableCollection<ExplorerItem> Children { get; set; }
}