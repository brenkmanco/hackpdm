using System.Collections;
using HackPDM.Shared.GlobalData;

// Resharper disable InconsistentNaming

namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_VERSION_NAME, OdooDefaultsConstants.HP_VERSION)]
public interface IHpVersionModel : IHpOdooRecord
{
    [OdooProp(OdooFieldType.Char)] public string? name {get;set;}
    [OdooProp(OdooFieldType.Char)] public string? file_ext {get;set;}
    [OdooProp(OdooFieldType.Char)] public string? checksum {get;set;}
    [OdooProp(OdooFieldType.Char)] public string? windows_complete_name { get; set; }
    
    [OdooProp(OdooFieldType.Many2one)] public int? entry_id {get;set;}
    [OdooProp(OdooFieldType.Many2one)] public int? node_id {get;set;}
	[OdooProp(OdooFieldType.Many2one)] public int? dir_id {get;set;}
    [OdooProp(OdooFieldType.Many2one)] public int? attachment_id {get;set;}

    [OdooProp(OdooFieldType.DateTime)] public DateTime? file_modify_stamp {get;set;}
    
    [OdooProp(OdooFieldType.Integer)] public int? file_size {get;set;}

    [OdooProp(OdooFieldType.Binary)] public string? preview_image {get;set;}
	[OdooProp(OdooFieldType.Binary)] public string? file_contents {get;set;}
}