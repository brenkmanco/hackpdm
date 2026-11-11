using System.Collections;
using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;
//using static System.Net.Mime.MediaTypeNames;

// Resharper disable InconsistentNaming

namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.IR_ATTACHMENT_NAME, OdooDefaultsConstants.IR_ATTACHMENT)]
public partial class IrAttachment : HpBaseModelTransport<IrAttachment>, IIrAttachment
{
    [OdooProp(OdooFieldType.Char, "name")] public string? name { get; set; }
    [OdooProp(OdooFieldType.Char, "res_model")] public string? res_model { get; set; }
	[OdooProp(OdooFieldType.Char, "checksum")] public string? checksum { get; set; }
	[OdooProp(OdooFieldType.Char, "mimetype")] public string? mimetype { get; set; }
	[OdooProp(OdooFieldType.Char, "type")] public string? type { get; set; }

	[OdooProp(OdooFieldType.Integer, "file_size")] public int? file_size { get; set; }

	[OdooProp(OdooFieldType.Many2One, "res_id")] public Many2One? res_id { get; set; }
	IMany2One? IIrAttachment.res_id { get =>(IMany2One?)res_id; set => res_id = (Many2One?)value; }
	string IIrAttachment._fileContentsBase64 { get; set; }

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
public partial class IrAttachment : HpBaseModelTransport<IrAttachment>
{
    public async Task<string> DownloadContents()
    {
        const string datas = "datas";
        if (this.IsRecord || this.id != 0)
        {
            // datas: the contents, base64
            var rows = await OdooRpc.ReadAsync(GetHpModel()!, [this.id ?? 0], [datas]);
            this._fileContentsBase64 = rows.Value is [var row, ..] ? row.Text(datas) : null;
            return this._fileContentsBase64;
        }
        return null;
    }
        
}