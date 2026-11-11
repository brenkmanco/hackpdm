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
	[OdooProp(OdooFieldType.Char, "name")] public partial string? name {get;set;}
	[OdooProp(OdooFieldType.Char, "windows_complete_name")] public partial string? windows_complete_name { get; set; }
	[OdooProp(OdooFieldType.DateTime, "checkout_date")] public DateTime? checkout_date {get;set;}
	[OdooProp(OdooFieldType.Boolean, "deleted")] public bool? deleted {get;set;}
	[OdooProp(OdooFieldType.Boolean, "is_checked_out")] public bool? is_checked_out {get;set;}
	[OdooProp(OdooFieldType.Many2One, "latest_version_id")] public Many2One? latest_version_id {get;set;}
	IMany2One? IHpEntryModel.latest_version_id { get =>(IMany2One?)latest_version_id; set => latest_version_id = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2One, "dir_id")] public Many2One? dir_id {get;set;}
	IMany2One? IHpEntryModel.dir_id { get =>(IMany2One?)dir_id; set => dir_id = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2One, "type_id")] public partial Many2One? type_id {get;set;}
	IMany2One? IHpEntryModel.type_id { get =>(IMany2One?)type_id; set => type_id = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2One, "checkout_user")] public Many2One? checkout_user {get;set;}
	IMany2One? IHpEntryModel.checkout_user { get =>(IMany2One?)checkout_user; set => checkout_user = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2One, "checkout_node")] public Many2One? checkout_node {get;set;}
	IMany2One? IHpEntryModel.checkout_node { get =>(IMany2One?)checkout_node; set => checkout_node = (Many2One?)value; }
	
	[OdooProp(OdooFieldType.Many2One, "cat_id")] public partial Many2One? cat_id {get;set;}
	IMany2One? IHpEntryModel.cat_id { get =>(IMany2One?)cat_id; set => cat_id = (Many2One?)value; }

	public bool IsLatest { get; }

    //public int id { get; set; }
}

public partial class HpEntry
{
	public EntryReturnType returnType { get; set; }
	public HackFile? LocalFile
	{
		get => field ??= GetLocalFile();
		set;
	}
	private IHpTypeModel? TypeStore
	{
		get => field ??= LocalFile is not null 
			? OdooDefaults.Instance!.ExtToType.TryGetValue( LocalFile?.TypeExt.ToLower(), out IHpTypeModel? itype ) 
				? itype 
				: null 
			: null;
		set;
	}
	public partial Many2One? type_id
	{
		get => field ??= TypeStore?.id;
		set;
	}
	public partial Many2One? cat_id 
	{ 
		get => field ??= TypeStore?.id;
		set;
	}
	public partial string? windows_complete_name 
	{ 
		// the server's form, "root\dir\file"; HackFile.RelativePath is only the directory
		get => field ??= LocalFile is { } local ? Operations.PdmCommitOperations.ServerWindowsName( local ) : null;
		set;
	}
	public partial string? name 
	{ 
		get => field ??= LocalFile?.Name;
		set;
	}
	public HackFile? GetLocalFile()
	{

		if( HashedValues.TryGetValue( "windows_complete_name", out string? path ) )
		{
			path = PwaPaths.ToAbsolute( path );
			return new HackFile( path );
		}
		if( HashedValues.TryGetValue( nameof( dir_id ), out ArrayList? arr2 ) )
		{
			string? path2 = arr2?[1] as string;
			path2 = PwaPaths.ToAbsolute( path2 );
			return new HackFile( path2 );
		}
		return null;
	}

	public bool CanCheckOut() => (checkout_user?.id is null or 0) && deleted is false;
	public bool CanUnCheckOut() => (checkout_user is not null) && checkout_user == OdooDefaults.Instance.OdooId;

	/// <summary>Checks the entry out to this user and node; false when the server refused or failed.</summary>
	public async Task<bool> CheckOut()
	{
		if (!CanCheckOut()) return false;
		if (latest_version_id?.id is not int versionId || versionId == 0 || OdooDefaults.Instance.MyNode?.id is not int nodeId)
			return false;

		// hp.version.checkout_entry(version_id, node): the arguments used to be sent as one list,
		// which the server ignored, so nothing was ever checked out
		bool success = await OdooClient<HpVersion>.CommandAsync<bool>( "checkout_entry", [ versionId, nodeId ] );
		if (success)
		{
			checkout_user = OdooDefaults.Instance.OdooId;
			checkout_date = DateTime.Now;
			checkout_node = nodeId;
			is_checked_out = true;
		}
		//await WriteChangedValuesAsync("checkout_user", "checkout_date", "checkout_node");
		//HpVersion version = new()
		//{
		//	node_id = checkout_node,
		//	id = latest_version_id
		//};
		//await version.WriteChangedValuesAsync("node_id");
		if (success)
			SetLocalReadOnly(false);
		return success;
	}
	public async Task<bool> UnCheckOut()
	{
		if (!CanUnCheckOut()) return false;
		checkout_user = null;
		checkout_date = null;
		checkout_node = null;
		is_checked_out = false;

		bool success = await WriteChangedValuesAsync("checkout_user", "checkout_date", "checkout_node", "is_checked_out");
		if (success)
			SetLocalReadOnly(true);
		return success;
	}
	/// <summary>Local copies are writable only while checked out.</summary>
	private void SetLocalReadOnly(bool readOnly)
	{
		string? winpath = HashedValues.TryGetValue("windows_complete_name", out object? objpath) && objpath is string stored
			? stored
			: windows_complete_name;
		if (string.IsNullOrEmpty(winpath))
			return;
		// stored as "root\dir\file" whatever the client OS
		FileInfo file = new(PwaPaths.ToAbsolute( winpath )!);
		if (!file.Exists)
			return;
		file.Attributes = readOnly ? file.Attributes | FileAttributes.ReadOnly : file.Attributes & ~FileAttributes.ReadOnly;
	}
	public static async Task<HpEntry?> ConvertToEntry(HackFile? hack, int dirId, HpPDMCommit? commit = null)
	{
		// if the hack file is a restricted type return
		if (OdooDefaults.Instance?.RestrictTypes is true
			& OdooDefaults.Instance!.ExtToType.TryGetValue(hack.TypeExt.ToLower(), out IHpTypeModel? itype) is false)
			return null;

		HpEntry entry = new()
		{
			commit_id = new Many2One
			{
				id = commit?.id
			},
			name = hack?.Name,
			deleted = false,
			dir_id = dirId,
			LocalFile = hack,
		};

		if (itype is not null)
		{
			entry.cat_id = itype.cat_id?.id ?? 0;
			entry.type_id = itype.id;
		}
		entry.returnType = EntryReturnType.Staged;
		return entry;
	}
	public async Task<HpEntry?> FindRemoteEntry()
	{
		HpEntry? entry = (await GetRecordsBySearchAsync(
			[
				new ArrayList { "name", "=", name },
				new ArrayList { "dir_id", "=", dir_id?.id },
				new ArrayList { "deleted", "=", false }
			]))?.FirstOrDefault();
		if (entry is not null and { id: not 0 })
		{
			entry.returnType = EntryReturnType.GotExisting;
		}
		return entry;
	}
	public async Task<HpRecordStaged?> StageEntry()
	{
		//HpEntry? entry = await FindRemoteEntry();
		HpRecordStaged? stagedRecord = null;

		stagedRecord = await this.StageRecAsync(false);
		returnType = stagedRecord?.id is null or 0 ? EntryReturnType.Failed : EntryReturnType.Staged;

		return stagedRecord;
	}

	//public static async Task<(EntryReturnType, HpEntry?, HpRecordStaged?)> GetFallbackCreateEntryAsync( HackFile hackFile, int dirId, HpPDMCommit? commit = null)
	//{
	//}
	public async Task LogicalDelete()
	{
		deleted = true;
		await WriteChangedValuesAsync("deleted");
	}
	public async Task LogicalUnDelete()
	{
		deleted = false;
		await WriteChangedValuesAsync("deleted");
	}
	public override string ToString()
	{
		return name;
	}
}