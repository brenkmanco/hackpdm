using System.Collections;
//using static System.Net.Mime.MediaTypeNames;


using OClient = HackPDM.Odoo.OdooClient;
// ReSharper disable InconsistentNaming

namespace HackPDM.Odoo.OdooModels.Models;

[OdooModel(OdooDefaults.IR_ATTACHMENT_NAME, OdooDefaults.IR_ATTACHMENT)]
public partial class IrAttachment : HpBaseModel<IrAttachment>
{
    [OdooField(OdooFieldType.Char)] public string name;
    [OdooField(OdooFieldType.Char)] public string res_model;
    [OdooField(OdooFieldType.Char)] public string checksum;
    [OdooField(OdooFieldType.Char)] public string mimetype;
	[OdooField(OdooFieldType.Char)] public string type;
    
    [OdooField(OdooFieldType.Integer)] public int file_size;
	
    [OdooField(OdooFieldType.Many2one)] public int res_id;
    
    private string _fileContentsBase64;
    public IrAttachment() { }
    public IrAttachment(
        string name,
        int resId = 0,
        int fileSize = 0,
        string resModel = null,
        string checksum = null,
        string mimetype = null,
        string type = "binary",
        string fileContentsBase64 = null)
    {
        this.name = name;
        this.res_id = resId;
        this.file_size = fileSize;
        this.res_model = resModel;
        this.checksum = checksum;
        this.mimetype = mimetype;
        this.type = type;
        this._fileContentsBase64 = fileContentsBase64;
    }
}
public partial class IrAttachment : HpBaseModel<IrAttachment>
{
    public string DownloadContents()
    {
        const string datas = "datas";
        if (this.IsRecord || this.id != 0)
        {
            // reads the datas field in ir.attachment and returns an ArrayList with one record because of one ID
            // which contains a hashtable with keys: datas and id. datas has a value of string which is the base 64 file contents
            this._fileContentsBase64 = (string)((Hashtable)OClient.Read(HpModel, [this.id], [datas])[0])[datas];
            return this._fileContentsBase64;
        }
        return null;
    }
        
    public string GetFileContentsB64() => _fileContentsBase64;
}