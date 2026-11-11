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

		if( HashedValues.GetValueOrDefault( "windows_complete_name" ) is string path )
			return new HackFile( PwaPaths.ToAbsolute( path ) );
		if( OdooValues.Many2OneName( HashedValues.GetValueOrDefault( nameof( dir_id ) ) ) is string directory )
			return new HackFile( PwaPaths.ToAbsolute( directory ) );
		return null;
	}

	public bool CanCheckOut() => (checkout_user?.id is null or 0) && deleted is false;
	/// <summary>Checked out to <paramref name="userId"/> (this user by default).</summary>
	public bool CanUnCheckOut( int? userId = null ) => checkout_user is not null && checkout_user == ( userId ?? OdooDefaults.Instance.OdooId );
	public override string ToString()
	{
		return name;
	}
}