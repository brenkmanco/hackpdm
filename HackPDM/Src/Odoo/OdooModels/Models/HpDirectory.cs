using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HackPDM.Hack;
//using static System.Net.Mime.MediaTypeNames;


using OClient = HackPDM.Odoo.OdooClient;
// ReSharper disable InconsistentNaming

namespace HackPDM.Odoo.OdooModels.Models;

public partial class HpDirectory : HpBaseModel<HpDirectory>
{
    internal readonly string[] UsualExcludedFields = [];
    public string name;
    public string parent_path;
    public int? parent_id;
    public int? default_cat;
    public bool? deleted;
    public bool? sandboxed;

    public HpDirectory() { }
    public HpDirectory(
        string name, 
        string parentPath = null, 
        int? parentId = 0, 
        int? defaultCat = 0, 
        bool? deleted = false, 
        bool? sandboxed = false) : this()
    {
        this.name = name;
        this.parent_path = parentPath;
        this.parent_id = parentId;
        this.default_cat = defaultCat;
        this.deleted = deleted;
        this.sandboxed = sandboxed;
    }
}
public partial class HpDirectory : HpBaseModel<HpDirectory>
{
    public static (int, int) LastAvailableDirectory( ArrayList paths )
    {
        Hashtable last = OClient.Command<Hashtable>(GetHpModel(), "last_available_directory", [paths]);

        return ( (int)last [ "index" ], (int)last [ "dir_id" ]);
    }

    public async static Task<bool> CreateNew( HpDirectory[] directories )
    {
        for (int i = 0; i < directories.Count(); i++ )
        {
            if ( directories [ i ].Id == 0 )
            {
                await directories [ i ].CreateAsync( false );
                if ( directories [ i ].Id == 0 )
                    return false;
            }
        }
        return true;
    }
    public async static Task<HpDirectory[]> CreateNew( ArrayList paths )
    {
        Hashtable last = await OClient.CommandAsync<Hashtable>(GetHpModel(), "last_available_directory", [paths]);

        // this means that all directories in paths were found 
        int nextIndex = (int)last["index"] + 1;
        int lastDirId = (int)last["dir_id"];

        if (nextIndex >= paths.Count)
            return [GetRecordById( lastDirId )];

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

            if (newDirectory.Id == 0) throw new Exception("HpDirectory not created");
                    
            directories[nextIndex] = newDirectory;
            // for next iteration
            lastParentId = newDirectory.Id;
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
            return OClient.Command<Hashtable>(HpModel, "get_children_directories_by_id", new ArrayList(new ArrayList { this.Id, withEntries }));
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
        if (this.IsRecord || this.Id != 0)
        {
            return GetEntries(this.Id);
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
        => GetDirectoryEntryIDs( this.Id, withSubEntries, withDeleted );
	public async Task<ArrayList?> GetDirectoryEntryIDsAsync(bool withSubEntries = false, bool withDeleted = true)
		=> await GetDirectoryEntryIDsAsync(this.Id, withSubEntries, withDeleted);
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
    public static string? ConvertToWindowsPath(string? pathway, bool withAbsolutePath)
    {
	    if (pathway is null) return null;
        string[] pathwaySegmented = pathway.Split([" / "], StringSplitOptions.RemoveEmptyEntries);
        if (pathwaySegmented[0] == "root" || pathwaySegmented[0] == HackDefaults.PwaPathRelative)
        {
			pathwaySegmented = pathwaySegmented[1..];
		}
        string relativePath = string.Join(@"\", pathwaySegmented);

        return withAbsolutePath ? Path.Combine(HackDefaults.PwaPathAbsolute, relativePath) : relativePath;
    }
	public static string? NodePathToWindowsPath(string? pathway, bool withAbsolutePath = true)
	{
		if (pathway is null) return null;
		string[] pathwaySegmented = pathway.Split(["\\"], StringSplitOptions.RemoveEmptyEntries);
		if (pathwaySegmented[0] == "root" || pathwaySegmented[0] == HackDefaults.PwaPathRelative)
		{
			pathwaySegmented = pathwaySegmented[1..];
		}
		string relativePath = string.Join(@"\", pathwaySegmented);

		return withAbsolutePath ? Path.Combine(HackDefaults.PwaPathAbsolute, relativePath) : relativePath;
	}
    public static string WindowsToOdooPath(string pathway, bool fromFullPath = false)
    {
        if (fromFullPath)
        {
            pathway = pathway[(HackDefaults.PwaPathAbsolute.Length - HackDefaults.PwaPathRelative.Length)..];
        }
        string[] pathwaySegmented = pathway.Split('\\');
        if (pathwaySegmented[0] == HackDefaults.PwaPathRelative)
        {
            pathwaySegmented[0] = "root";
        }
        if (pathwaySegmented[0] != "root")
        {
            pathwaySegmented = [.. pathwaySegmented.Prepend("root")];
        }
        string relativePath = string.Join(@" / ", pathwaySegmented);
        return relativePath;
    }
    public override string ToString()
    {
        return name;
    }
}
public class ExplorerItem
{
    public string Name { get; set; }
    public string IconPath { get; set; } 
    public bool IsFolder { get; set; }
    public ObservableCollection<ExplorerItem> Children { get; set; }
}