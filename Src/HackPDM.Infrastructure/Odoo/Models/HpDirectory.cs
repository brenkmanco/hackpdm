using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

	public static (int, int) LastAvailableDirectory( ArrayList paths )
    {
        Hashtable last = OClient.Command<Hashtable>(GetHpModel(), "last_available_directory", [paths]);

        return ( (int)last [ "index" ], (int)last [ "dir_id" ]);
    }
    public async static Task<bool> CreateNew( HpDirectory[] directories )
    {
        for (int i = 0; i < directories.Count(); i++ )
        {
            if ( directories [ i ].id == 0 )
            {
                await directories [ i ].CreateAsync( false );
                if ( directories [ i ].id == 0 )
                    return false;
            }
        }
        return true;
    }
    public async static Task<HpDirectory[]?> CreateNew( ArrayList paths )
    {
        Hashtable last = await OClient.CommandAsync<Hashtable>(GetHpModel(), "last_available_directory", [paths]);

        // this means that all directories in paths were found 
        int nextIndex = (int)last["index"] + 1;
        int lastDirId = (int)last["dir_id"];

        if (nextIndex >= paths.Count)
            return [await GetRecordByIdAsync( lastDirId ) ?? null];

        HpDirectory[] directories = new HpDirectory[paths.Count - nextIndex];
        int lastParentId = lastDirId;
        for (int i = nextIndex; i < paths.Count; i++)
        {
            HpDirectory newDirectory = new()
            {
                name = (string)paths[i],
                parent_id = lastParentId,
                sandboxed = false,
                deleted = false,
                default_cat = 1,
            };
            await newDirectory.CreateAsync(false);

            if (newDirectory.id == 0) throw new Exception("HpDirectory not created");
                    
            directories[nextIndex] = newDirectory;
            // for next iteration
            lastParentId = newDirectory.id ?? 0;
        }
        return directories;
    }
    public int GetId()
    {
        string linuxPath = parent_path.Replace(@"\", @" / ").Replace(@"\\", @" / ");
        return OClient.Command<int>(this.HpModel, "get_dir_id_for_parentpath", new ArrayList(new string[] { linuxPath }));
    }
    public Hashtable GetSubdirectories(bool withEntries = true)
    {
        if (this.IsRecord)
        {
            return OClient.Command<Hashtable>(HpModel, "get_children_directories_by_id", new ArrayList(new ArrayList { this.id, withEntries }));
        }
        return null;
    }
    public static Dictionary<string, object>? GetSubdirectories(int id)
    {
        return id != 0
            ? OClient.Command<Dictionary<string, object>>(GetHpModel(), "get_children_directories_by_id", new ArrayList(new ArrayList { id, false }))
            : null;
    }

    public Hashtable GetSubdirectories(string pathway)
    {
        string linuxPath = pathway.Replace(@"\", @" / ").Replace(@"\\", @" / ");
        return OClient.Command<Hashtable>(HpModel, "get_children_directories", new ArrayList(new string[] { linuxPath }));
    }
    public Hashtable GetEntries()
    {
        if (this.IsRecord || this.id != 0)
        {
            return GetEntries(this.id);
        }
        return null;
    }
    public static Hashtable GetEntries(int? directoryId, bool showInActive = false)
        => OClient.Command<Hashtable>(
            GetHpModel(), 
            "get_entries", 
            new ArrayList(new ArrayList { new ArrayList {directoryId, showInActive} })
        );
            
        
    public ArrayList? GetDirectoryEntryIDs(bool withSubEntries = false, bool withDeleted = true)
        => GetDirectoryEntryIDs( this.id ?? 0, withSubEntries, withDeleted );
	public async Task<ArrayList?> GetDirectoryEntryIDsAsync(bool withSubEntries = false, bool withDeleted = true)
		=> await GetDirectoryEntryIDsAsync(this.id ?? 0, withSubEntries, withDeleted);
	public static async Task<ArrayList?> GetDirectoryEntryIDsAsync( int directoryId, bool withSubEntries = false, bool withDeleted = false)
		=> directoryId != 0 
			?  await OClient.CommandAsync<ArrayList>(GetHpModel()!, "get_all_entry_ids", [directoryId, withDeleted, withSubEntries], 10000 ) 
            :   null;
    public static ArrayList? GetDirectoryEntryIDs( int directoryId, bool withSubEntries = false, bool withDeleted = false )
    {
        return  directoryId != 0 
            ?  OClient.Command<ArrayList>( GetHpModel()!, "get_all_entry_ids", [ directoryId, withDeleted, withSubEntries ], 10000 ) 
            :   null;
    }
}
public class ExplorerItem
{
    public string Name { get; set; }
    public string IconPath { get; set; } 
    public bool IsFolder { get; set; }
    public ObservableCollection<ExplorerItem> Children { get; set; }
}