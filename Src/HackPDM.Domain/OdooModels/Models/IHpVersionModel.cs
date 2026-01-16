using System;
using System.Collections;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

// Resharper disable InconsistentNaming

namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_VERSION_NAME, OdooDefaultsConstants.HP_VERSION)]
public interface IHpVersionModel : IHpOdooRecord
{
    [OdooProp(OdooFieldType.Char, "name")] public string? name {get;set;}
    [OdooProp(OdooFieldType.Char, "file_ext")] public string? file_ext {get;set;}
    [OdooProp(OdooFieldType.Char, "checksum")] public string? checksum {get;set;}
    [OdooProp(OdooFieldType.Char, "windows_complete_name")] public string? windows_complete_name { get; set; }
    
    [OdooProp(OdooFieldType.Many2one, "entry_id")] public IMany2One? entry_id {get;set;}
    [OdooProp(OdooFieldType.Many2one, "node_id")] public IMany2One? node_id {get;set;}
	[OdooProp(OdooFieldType.Many2one, "dir_id")] public IMany2One? dir_id {get;set;}
    [OdooProp(OdooFieldType.Many2one, "attachment_id")] public IMany2One? attachment_id {get;set;}

    [OdooProp(OdooFieldType.DateTime, "file_modify_stamp")] public DateTime? file_modify_stamp {get;set;}
    
    [OdooProp(OdooFieldType.Integer, "file_size")] public int? file_size {get;set;}

    [OdooProp(OdooFieldType.Binary, "preview_image")] public string? preview_image {get;set;}
	[OdooProp(OdooFieldType.Binary, "file_contents")] public string? file_contents {get;set;}
}