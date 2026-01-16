using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;
// Resharper disable InconsistentNaming

namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.IR_ATTACHMENT_NAME, OdooDefaultsConstants.IR_ATTACHMENT)]
public interface IIrAttachment : IHpOdooRecord
{
    [OdooProp(OdooFieldType.Char, "name")] public string? name {get;set;}
    [OdooProp(OdooFieldType.Char, "res_model")] public string? res_model {get;set;}
    [OdooProp(OdooFieldType.Char, "checksum")] public string? checksum {get;set;}
    [OdooProp(OdooFieldType.Char, "mimetype")] public string? mimetype {get;set;}
	[OdooProp(OdooFieldType.Char, "type")] public string? type {get;set;}
    
    [OdooProp(OdooFieldType.Integer, "file_size")] public int? file_size {get;set;}
	
    [OdooProp(OdooFieldType.Many2one, "res_id")] public IMany2One? res_id {get;set;}
    
    public string _fileContentsBase64 {get;set;}
}