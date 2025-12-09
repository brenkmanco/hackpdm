using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Documents;

using HackPDM.Extensions.General;
using HackPDM.Forms.Hack;
using HackPDM.Hack;
using HackPDM.Odoo.Methods;
using HackPDM.Src.ClientUtils.Types;


//using static System.Net.Mime.MediaTypeNames;


using OClient = HackPDM.Odoo.OdooClient;
// ReSharper disable InconsistentNaming

namespace HackPDM.Odoo.OdooModels.Models;

public partial class HpEntry : HpBaseModel<HpEntry>
{
	public string name;
    public string checkout_date;
    public bool deleted;
    public int latest_version_id;
    public int dir_id;
    public int type_id;
    public int cat_id;
    public int? checkout_user;
    public int? checkout_node;
    internal bool IsLatest { get; set; } = false;
    
    public HpEntry() {  }
    public HpEntry(
        string name,
        string checkoutDate = null,
        bool active = true,
        int latestVersionId = 0,
        int dirId = 0,
        int typeId = 0,
        int catId = 0,
        int checkoutUser = 0,
        int checkoutNode = 0)
    {
        this.name = name;
        this.deleted = !active;
        this.latest_version_id = latestVersionId;
        this.dir_id = dirId;
        this.type_id = typeId;
        this.cat_id = catId;
        this.checkout_node = checkoutNode;
    
        this.checkout_user = checkoutUser == 0 ? OdooDefaults.OdooId : checkoutUser;
		this.checkout_date = checkoutDate == null ? OdooDefaults.OdooDateFormat(DateTime.Now) : checkoutDate;
	}
}
public partial class HpEntry : HpBaseModel<HpEntry>
{
	public HackFile? LocalFile
	{
		get => field = GetLocalFile();
		set => field = value;
	}
	public HackFile? GetLocalFile()
	{

		if (HashedValues.TryGetValue("windows_complete_name", out string? path))
		{
			path = HpDirectory.NodePathToWindowsPath(path, true);
			return new HackFile(path);
		}
		if (HashedValues.TryGetValue(nameof(dir_id), out ArrayList? arr2))
		{
			string? path2 = arr2?[1] as string;
			path2 = HpDirectory.ConvertToWindowsPath(path2, true);
			return new HackFile(path2);
		}
		return null;
	}
	public HashSet<string>? GetDependencyPaths()
	{
		HackFile? hack = null;
		string? thisPath = null;
		HashSet<string>? dependentPaths = [];
		if (HashedValues.TryGetValue("windows_complete_name", out string? path))
		{
			thisPath = HpDirectory.NodePathToWindowsPath(path, true);
			hack = new HackFile(thisPath);
		} 
		else if (HashedValues.TryGetValue(nameof(dir_id), out ArrayList? arr2))
		{
			string? path2 = arr2?[1] as string;
			thisPath = HpDirectory.ConvertToWindowsPath(path2, true);
			hack = new HackFile(thisPath);
		}
		else return null;

		if (OdooDefaults.DependentExt.Contains(hack.Info.Extension))
		{
			var dependencies = HackDefaults.DocMgr.GetDependencies(thisPath);
			if (dependencies is not null && dependencies.Count > 0)
			{
				foreach (string[] deps in dependencies)
				{
					string dpath = deps[1];
					int index = dpath.IndexOf($"\\{HackDefaults.PwaPathRelative}\\");
					if (index == -1) return null;
					var splitPath = dpath[index..];
					
					dependentPaths.Add(Path.Combine([HackDefaults.PwaPathAbsolute, splitPath]));
				}
			}
		}

		return dependentPaths is not null and { Count: > 0 } ? dependentPaths : null;
	}
	public IEnumerable<EntryLocalPath> GetDependentPathways()
	{
		HackFile? hack = null;
		string? thisPath = null;
		HashSet<string>? dependentPaths = [];
		if (HashedValues.TryGetValue("windows_complete_name", out string? path))
		{
			thisPath = HpDirectory.NodePathToWindowsPath(path, true);
			hack = new HackFile(thisPath);
		}
		else if (HashedValues.TryGetValue(nameof(dir_id), out ArrayList? arr2))
		{
			string? path2 = arr2?[1] as string;
			thisPath = HpDirectory.ConvertToWindowsPath(path2, true);
			hack = new HackFile(thisPath);
		}
		else goto EndEmpty;

		if (OdooDefaults.DependentExt.Contains(hack.Info.Extension))
		{
			var dependencies = HackDefaults.DocMgr.GetDependencies(thisPath);
			if (dependencies is not null && dependencies.Count > 0)
			{
				foreach (string[] deps in dependencies)
				{
					string dpath = deps[1];
					bool insidePwa = dpath.StartsWith(HackDefaults.PwaPathAbsolute);

					yield return insidePwa ? new(dpath, this) : new(dpath, this, true);
				}
			}
		}
		EndEmpty:
		DoNothing();
	}
	private static void DoNothing() { }
    public static ArrayList GetLatestIDs(ArrayList ids)
    {
        const string latest = "latest_version_id";
           
        ArrayList list = OClient.Read(GetHpModel(), ids, [latest], 10000);

        return list;
    }
    public static int GetLatestID(int id)
    {
        ArrayList list = OClient.Read(GetHpModel(), [id], ["latest_version_id"], 10000);
        return list is not null and {Count: > 0 } ? ((list[0] as Hashtable)?["latest_version_id"] as ArrayList)?[0] is int latestId ? latestId : 0 : 0;
	}
	public static async Task<int> GetLatestIDAsync(int id)
	{
		ArrayList list = await OClient.ReadAsync(GetHpModel(), [id], ["latest_version_id"], 10000);
		return list is not null and {Count: > 0 } ?  ((list[0] as Hashtable)?["latest_version_id"] as ArrayList)?[0] is int latestId ? latestId : 0 : 0;
	}
	public int GetLatestID()
    {
        if (HashedValues.TryGetValue("latest_version_id", out int latestId)) return latestId;
		ArrayList list = OClient.Read(GetHpModel(), [this.Id], ["latest_version_id"], 10000);

		return list is not null and {Count: > 0 } ? ((list[0] as Hashtable)?["latest_version_id"] as ArrayList)?[0] is int id ? id : 0 : 0;
	}
	public bool CanCheckOut() => (checkout_user is null or 0) && !deleted;
    public bool CanUnCheckOut() => (checkout_user is not null) && checkout_user == OdooDefaults.OdooId;
        
    public async Task CheckOut()
    {
        if (!CanCheckOut()) return;
        checkout_user = OdooDefaults.OdooId;
        checkout_date = OdooDefaults.OdooDateFormat( DateTime.Now );
        checkout_node = OdooDefaults.MyNode.Id;

        await WriteChangedValuesAsync("checkout_user", "checkout_date", "checkout_node");
        HpVersion version = new(nodeId: checkout_node);
        version.Id = latest_version_id;
        await version.WriteChangedValuesAsync("node_id");
        if (HashedValues.TryGetValue("windows_complete_name", out object objpath) && objpath is string winpath)
        {
            string absPath = Path.Combine(HackDefaults.PwaPathAbsolute, winpath[5..]);
            FileInfo file = new(absPath);
            if (file.Exists)
            {
                file.Attributes &= ~FileAttributes.ReadOnly;
            }
        }

    }
    public async Task UnCheckOut()
    {
        if (!CanUnCheckOut()) return;
        checkout_user = null;
        checkout_date = null;
        checkout_node = null;

        await WriteChangedValuesAsync( "checkout_user", "checkout_date", "checkout_node" );
        if (HashedValues.TryGetValue("windows_complete_name", out object objpath) && objpath is string winpath)
        {
            string absPath = Path.Combine(HackDefaults.PwaPathAbsolute, winpath[5..]);
            FileInfo file = new(absPath);
            if (file.Exists)
            {
                file.Attributes |= FileAttributes.ReadOnly;
            }
        }
    }
    internal static async Task<HpEntry?> CreateNew( HackFile hackFile, int dirId )
    {
        if (OdooDefaults.RestrictTypes & !OdooDefaults.ExtToType.TryGetValue( hackFile.TypeExt.ToLower(), out HpType type ) )
            return null;

        HpEntry newEntry = new()
        {
            name = hackFile.Name,
            deleted = false,
            dir_id = dirId,
        };
        if (type is not null)
        {
            newEntry.cat_id = type.cat_id;
            newEntry.type_id = type.Id;
        }
	
        await newEntry.CreateAsync( false );

        return newEntry.Id == 0 ? null : newEntry;
    }
	internal static async Task<(EntryReturnType, HpEntry?)> GetFallbackCreateEntryAsync( HackFile hackFile, int dirId )
	{
		HpEntry? entry = null;

		if (OdooDefaults.RestrictTypes & !OdooDefaults.ExtToType.TryGetValue( hackFile.TypeExt.ToLower(), out HpType type ) )
			return (EntryReturnType.InvalidType, null);

		entry = (await GetRecordsBySearchAsync( [("name", "=", hackFile.Name), ("dir_id", "=", dirId), ("deleted", "=", false)] ))?.FirstOrDefault();
		if (entry is not null)
			return (EntryReturnType.GotExisting, entry);


		HpEntry newEntry = new()
		{
			name = hackFile.Name,
			deleted = false,
			dir_id = dirId,
		};
		if (type is not null)
		{
			newEntry.cat_id = type.cat_id;
			newEntry.type_id = type.Id;
		}
		await newEntry.CreateAsync( false );
		return entry?.Id == 0 ? (EntryReturnType.Failed, null) : (EntryReturnType.Created, entry);
	}
	
    internal static async Task<ArrayList> GetEntryList(int[] entryIds, bool update = false)
    {
        ArrayList arr = await OClient.CommandAsync<ArrayList>(HpVersion.GetHpModel(), "get_recursive_dependency_entries", [entryIds.ToArrayList()], 1000000);
        return arr;
    }
    internal async Task LogicalDelete() 
    {
        deleted = true;
        await WriteChangedValuesAsync( "deleted" );
    }
    internal async Task LogicalUnDelete() 
    {
        deleted = false;
        await WriteChangedValuesAsync( "deleted" );
    }
    public override string ToString()
    {
        return name;
    }

}