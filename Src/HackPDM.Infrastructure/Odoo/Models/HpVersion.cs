using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using HackPDM.Core;
using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Infrastructure.SldWrks;
using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

using SolidWorks.Interop.swdocumentmgr;

using DateTime = System.DateTime;

//using static System.Net.Mime.MediaTypeNames;


// Resharper disable InconsistentNaming

namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.HP_VERSION_NAME, OdooDefaultsConstants.HP_VERSION)]
public partial class HpVersion : HpBaseModelTransport<HpVersion>, IHpVersionModel
{
	[OdooProp(OdooFieldType.Char, "name")] public string? name { get; set; }
	[OdooProp(OdooFieldType.Char, "file_ext")] public string? file_ext { get; set; }
	[OdooProp(OdooFieldType.Char, "checksum")] public string? checksum { get; set; }
    [OdooProp(OdooFieldType.Char, "windows_complete_name")] public string? windows_complete_name { get; set; }
    
	[OdooProp(OdooFieldType.Many2One, "entry_id")] public Many2One? entry_id { get; set; }
	IMany2One? IHpVersionModel.entry_id { get =>(IMany2One?)entry_id; set => entry_id = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2One, "node_id")] public Many2One? node_id { get; set; }
	IMany2One? IHpVersionModel.node_id { get =>(IMany2One?)node_id; set => node_id = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2One, "dir_id")] public Many2One? dir_id { get; set; }
	IMany2One? IHpVersionModel.dir_id { get =>(IMany2One?)dir_id; set => dir_id = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2One, "checkout_user")] public Many2One? checkout_user { get; set; }
	IMany2One? IHpVersionModel.checkout_user { get => (IMany2One?)checkout_user; set => checkout_user = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2One, "attachment_id")] public Many2One? attachment_id { get; set; }
	IMany2One? IHpVersionModel.attachment_id { get =>(IMany2One?)attachment_id; set => attachment_id = (Many2One?)value; }

	[OdooProp(OdooFieldType.Many2Many, "parent_ids")] public Many2Many? parent_ids { get; set; }
	[OdooProp(OdooFieldType.Many2Many, "child_ids")] public Many2Many? child_ids { get; set; }
	IMany2Many? IHpVersionModel.parent_ids { get => (IMany2Many?)parent_ids; set => parent_ids = (Many2Many?)value; }
	IMany2Many? IHpVersionModel.child_ids { get => (IMany2Many?)child_ids; set => child_ids = (Many2Many?)value; }


	[OdooProp(OdooFieldType.DateTime, "file_modify_stamp")] public DateTime? file_modify_stamp { get; set; }

	[OdooProp(OdooFieldType.Integer, "file_size")] public int? file_size { get; set; }

	[OdooProp(OdooFieldType.Binary, "preview_image")] public byte[]? preview_image { get; set; }
	[OdooProp(OdooFieldType.Binary, "file_contents")] public byte[]? file_contents { get; set; }
	public string? FileContentsBase64 { get; private set; }
    /// <summary>
    /// The version's folder relative to the working folder, with this OS's separators (set from
    /// dir_id when it was read). Falls back to windows_complete_name without "root\" and without
    /// the file name; it used to keep both the file name and the backslashes.
    /// </summary>
    public string? WinPathway { get => field ??= windows_complete_name is null ? null : PwaPaths.FolderOf(windows_complete_name); set; }
    
        
    static HpVersion()
    {
        UsualExcludedFields = ["preview_image", "file_contents"];
    }
    
    /// <summary>
    /// Finishes a record that was just populated from a server row by setting <see cref="WinPathway"/>
    /// from the folder the server reported.
    /// </summary>
    /// <remarks>
    /// Uses <c>dir_id</c>'s display name when it was read, otherwise <c>directory_complete_name</c>, each made
    /// relative with <see cref="HackPDM.Core.PwaPaths.ToRelative(string)"/>. When neither was read,
    /// <see cref="WinPathway"/> keeps falling back to <c>windows_complete_name</c>. The base
    /// <see cref="HackPDM.Domain.OdooModels.Models.HpBaseModel.CompleteConstruction"/> always runs afterwards.
    /// </remarks>
    public override void CompleteConstruction()
    {
        try
        {
            // the folder from dir_id ([id, "root / a / b"]) or directory_complete_name, when they were read
            if (OdooValues.Many2OneName(HashedValues.GetValueOrDefault("dir_id")) is string directory)
                WinPathway = PwaPaths.ToRelative(directory);
			else if (HashedValues.GetValueOrDefault("directory_complete_name") is string completeName)
				WinPathway = PwaPaths.ToRelative(completeName);
		}
        finally 
        {
            base.CompleteConstruction();
        }
    }
}
public partial class HpVersion : HpBaseModelTransport<HpVersion>
{
    /// <summary>
    /// Reads the <c>version_property_ids</c> of the given versions and then fetches those
    /// <see cref="HpVersionProperty"/> records, one request per version.
    /// </summary>
    /// <param name="ids">The hp.version ids whose properties to read.</param>
    /// <returns>
    /// One array per version row the server returned, in the server's order (not necessarily the order of
    /// <paramref name="ids"/>); an entry is <see langword="null"/> for a version without properties. The list is
    /// empty when the read of the versions fails.
    /// </returns>
    /// <remarks>Makes one <c>read</c> call on hp.version plus one read of hp.version.property per version.</remarks>
    public static async Task<List<HpVersionProperty[]?>> GetAllVersionPropertiesAsync(IReadOnlyList<int> ids)
    {
    	const string versionPropField = "version_property_ids";
    	var rows = await OdooRpc.ReadAsync(GetHpModel()!, ids, [versionPropField]);
    	List<HpVersionProperty[]?> versionProperties = [];
    	foreach (OdooRow row in rows.ValueOr([]))
    		versionProperties.Add(await HpVersionProperty.GetRecordsByIdsAsync(row.RefIds(versionPropField)));
    	return versionProperties;
    }

	public override string ToString()
    {
        return name;
    }
}