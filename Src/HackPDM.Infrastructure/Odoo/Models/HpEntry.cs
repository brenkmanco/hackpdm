using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HackPDM.Core;
using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Infrastructure.SldWrks;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;




//using static System.Net.Mime.MediaTypeNames;


using OClient = HackPDM.Infrastructure.Odoo.OdooClient;
// Resharper disable InconsistentNaming

namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.HP_ENTRY_NAME, OdooDefaultsConstants.HP_ENTRY)]
public partial class HpEntry : HpBaseModelTransport<HpEntry>, IHpEntryModel
{
	[OdooProp(OdooFieldType.Char, "name")] public string? name {get;set;}
	[OdooProp(OdooFieldType.Char, "windows_complete_name")] public string? windows_complete_name { get; set; }
	[OdooProp(OdooFieldType.DateTime, "checkout_date")] public DateTime? checkout_date {get;set;}
	[OdooProp(OdooFieldType.Boolean, "deleted")] public bool? deleted {get;set;}
	[OdooProp(OdooFieldType.Many2One, "latest_version_id")] public Many2One? latest_version_id {get;set;}
	IMany2One? IHpEntryModel.latest_version_id { get =>(IMany2One?)latest_version_id; set => latest_version_id = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2One, "dir_id")] public Many2One? dir_id {get;set;}
	IMany2One? IHpEntryModel.dir_id { get =>(IMany2One?)dir_id; set => dir_id = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2One, "type_id")] public Many2One? type_id {get;set;}
	IMany2One? IHpEntryModel.type_id { get =>(IMany2One?)type_id; set => type_id = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2One, "checkout_user")] public Many2One? checkout_user {get;set;}
	IMany2One? IHpEntryModel.checkout_user { get =>(IMany2One?)checkout_user; set => checkout_user = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2One, "checkout_node")] public Many2One? checkout_node {get;set;}
	IMany2One? IHpEntryModel.checkout_node { get =>(IMany2One?)checkout_node; set => checkout_node = (Many2One?)value; }
	
	[OdooProp(OdooFieldType.Many2One, "cat_id")] public Many2One? cat_id {get;set;}
	IMany2One? IHpEntryModel.cat_id { get =>(IMany2One?)cat_id; set => cat_id = (Many2One?)value; }

	public bool IsLatest { get; }

	public HackFile? LocalFile
	{
		get => field = GetLocalFile();
		set => field = value;
	}
	public HackFile? GetLocalFile()
	{

		if (HashedValues.TryGetValue("windows_complete_name", out string? path))
		{
			path = FileOperations.NodePathToWindowsPath(path, true);
			return new HackFile(path);
		}
		if (HashedValues.TryGetValue(nameof(dir_id), out ArrayList? arr2))
		{
			string? path2 = arr2?[1] as string;
			path2 = FileOperations.ConvertToWindowsPath(path2, true);
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
			thisPath = FileOperations.NodePathToWindowsPath(path, true);
			hack = new HackFile(thisPath);
		} 
		else if (HashedValues.TryGetValue(nameof(dir_id), out ArrayList? arr2))
		{
			string? path2 = arr2?[1] as string;
			thisPath = FileOperations.ConvertToWindowsPath(path2, true);
			hack = new HackFile(thisPath);
		}
		else return null;

		if (OdooDefaultsConstants.DependentExt.Contains(hack.Info.Extension))
		{
			var dependencies = SolidWorksUtil.DocMgr?.GetDependencies(thisPath);
			if (dependencies is not null && dependencies.Count > 0)
			{
				foreach (string[] deps in dependencies)
				{
					string dpath = deps[1];
					int index = dpath.IndexOf($"\\{HackDefaults.Instance.PwaPathRelative}\\");
					if (index == -1) return null;
					var splitPath = dpath[index..];
					
					dependentPaths.Add(Path.Combine([HackDefaults.Instance.PwaPathAbsolute, splitPath]));
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
			thisPath = FileOperations.NodePathToWindowsPath(path, true);
			hack = new HackFile(thisPath);
		}
		else if (HashedValues.TryGetValue(nameof(dir_id), out ArrayList? arr2))
		{
			string? path2 = arr2?[1] as string;
			thisPath = FileOperations.ConvertToWindowsPath(path2, true);
			hack = new HackFile(thisPath);
		}
		else goto EndEmpty;

		if (OdooDefaultsConstants.DependentExt.Contains(hack.Info.Extension))
		{
			var dependencies = SolidWorksUtil.DocMgr?.GetDependencies(thisPath);
			if (dependencies is not null && dependencies.Count > 0)
			{
				foreach (string[] deps in dependencies)
				{
					string dpath = deps[1];
					bool insidePwa = dpath.StartsWith(HackDefaults.Instance.PwaPathAbsolute);

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
        const string latest = nameof(HpEntry.latest_version_id);
           
        ArrayList list = OClient.Read(GetHpModel(), ids, [latest], 10000);

        return list;
    }
    public static int GetLatestID(int id)
    {
        ArrayList list = OClient.Read(GetHpModel(), [id], [nameof(HpEntry.latest_version_id)], 10000);
        return list is not null and {Count: > 0 } ? ((list[0] as Hashtable)?[nameof(HpEntry.latest_version_id)] as ArrayList)?[0] is int latestId ? latestId : 0 : 0;
	}
	public static async Task<int> GetLatestIDAsync(int id)
	{
		ArrayList list = await OClient.ReadAsync(GetHpModel(), [id], [nameof(HpEntry.latest_version_id)], 10000);
		return list is not null and {Count: > 0 } ?  ((list[0] as Hashtable)?[nameof(HpEntry.latest_version_id)] as ArrayList)?[0] is int latestId ? latestId : 0 : 0;
	}
	public int GetLatestID()
    {
        if (HashedValues.TryGetValue(nameof(HpEntry.latest_version_id), out int latestId)) return latestId;
		ArrayList list = OClient.Read(GetHpModel(), [this.id], [nameof(HpEntry.latest_version_id)], 10000);

		return list is not null and {Count: > 0 } ? ((list[0] as Hashtable)?[nameof(HpEntry.latest_version_id)] as ArrayList)?[0] is int id ? id : 0 : 0;
	}
	public bool CanCheckOut() => (checkout_user?.id is null or 0) && deleted is false;
    public bool CanUnCheckOut() => (checkout_user is not null) && checkout_user == OdooDefaults.Instance.OdooId;
        
    public async Task CheckOut()
    {
        if (!CanCheckOut()) return;
        checkout_user = OdooDefaults.Instance.OdooId;
        checkout_date = DateTime.Now;
        checkout_node = OdooDefaults.Instance.MyNode.id;

        await WriteChangedValuesAsync("checkout_user", "checkout_date", "checkout_node");
		HpVersion version = new()
		{
			node_id = checkout_node,
			id = latest_version_id
		};
		await version.WriteChangedValuesAsync("node_id");
        if (HashedValues.TryGetValue("windows_complete_name", out object objpath) && objpath is string winpath)
        {
            string absPath = Path.Combine(HackDefaults.Instance.PwaPathAbsolute, winpath[5..]);
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
            string absPath = Path.Combine(HackDefaults.Instance.PwaPathAbsolute, winpath[5..]);
            FileInfo file = new(absPath);
            if (file.Exists)
            {
                file.Attributes |= FileAttributes.ReadOnly;
            }
        }
    }
    internal static async Task<HpEntry?> CreateNew( HackFile hackFile, int dirId )
    {
        if (OdooDefaults.Instance.RestrictTypes is true & !OdooDefaults.Instance.ExtToType.TryGetValue( hackFile.TypeExt.ToLower(), out IHpTypeModel itype ) )
            return null;

        HpType? type = itype as HpType;
        HpEntry newEntry = new()
        {
            name = hackFile.Name,
            deleted = false,
            dir_id = dirId,
        };
        if (type is not null)
        {
            newEntry.cat_id = type.cat_id ?? 0;
            newEntry.type_id = type.id;
        }
	
        await newEntry.CreateAsync( false );

        return newEntry.id == 0 ? null : newEntry;
    }
	public static async Task<(EntryReturnType, HpEntry?)> GetFallbackCreateEntryAsync( HackFile hackFile, int dirId )
	{
		HpEntry? entry = null;

		if (OdooDefaults.Instance.RestrictTypes is true & !OdooDefaults.Instance.ExtToType.TryGetValue( hackFile.TypeExt.ToLower(), out IHpTypeModel type ) )
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
			newEntry.cat_id = type.cat_id?.id ?? 0;
			newEntry.type_id = type.id;
		}
		await newEntry.CreateAsync( false );
		return entry?.id == 0 ? (EntryReturnType.Failed, null) : (EntryReturnType.Created, entry);
	}
	
    internal static async Task<ArrayList> GetEntryList(int[] entryIds, bool update = false)
    {
        ArrayList arr = await OClient.CommandAsync<ArrayList>(HpVersion.GetHpModel(), "get_recursive_dependency_entries", [entryIds.ToArrayList()], 1000000);
        return arr;
    }

    public async Task LogicalDelete() 
    {
        deleted = true;
        await WriteChangedValuesAsync( "deleted" );
    }

    public async Task LogicalUnDelete() 
    {
        deleted = false;
        await WriteChangedValuesAsync( "deleted" );
    }
    public override string ToString()
    {
        return name;
    }

    public int id { get; set; }
}