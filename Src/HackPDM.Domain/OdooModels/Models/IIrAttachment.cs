using HackPDM.Shared.GlobalData;
// Resharper disable InconsistentNaming

namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.IR_ATTACHMENT_NAME, OdooDefaultsConstants.IR_ATTACHMENT)]
public interface IIrAttachment : IHpOdooRecord
{
    [OdooProp(OdooFieldType.Char)] public string name {get;set;}
    [OdooProp(OdooFieldType.Char)] public string res_model {get;set;}
    [OdooProp(OdooFieldType.Char)] public string checksum {get;set;}
    [OdooProp(OdooFieldType.Char)] public string mimetype {get;set;}
	[OdooProp(OdooFieldType.Char)] public string type {get;set;}
    
    [OdooProp(OdooFieldType.Integer)] public int file_size {get;set;}
	
    [OdooProp(OdooFieldType.Many2one)] public int res_id {get;set;}
    
    public string _fileContentsBase64 {get;set;}
}