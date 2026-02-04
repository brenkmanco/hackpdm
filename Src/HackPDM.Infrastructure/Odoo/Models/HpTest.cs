using System.Collections;

using HackPDM.Core;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;
// Resharper disable InconsistentNaming

namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.HP_TEST_NAME, OdooDefaultsConstants.HP_TEST)]
public partial class HpTest : HpBaseModelTransport<HpTest>, IHpTestModel
{
	[OdooProp(OdooFieldType.Binary		, "binary"	 )] public byte[]? binary		{ get; set; }
	[OdooProp(OdooFieldType.Boolean		, "boolean"	 )]	public bool? boolean		{ get; set; }
	[OdooProp(OdooFieldType.Char		, "character")]	public string? character	{ get; set; }
	[OdooProp(OdooFieldType.Date		, "dates"	 )]	public DateTime? dates		{ get; set; }
	[OdooProp(OdooFieldType.DateTime	, "datetimes")]	public DateTime? datetimes	{ get; set; }
	[OdooProp(OdooFieldType.Float		, "floats"	 )]	public float? floats		{ get; set; }
	[OdooProp(OdooFieldType.Html		, "html"	 )]	public string? html			{ get; set; }
	[OdooProp(OdooFieldType.Image		, "image"	 )]	public byte[]? image		{ get; set; }
	[OdooProp(OdooFieldType.Integer		, "integer"	 )]	public int? integer			{ get; set; }
	[OdooProp(OdooFieldType.Json		, "json"	 )]	public Hashtable? json		{ get; set; }
	[OdooProp(OdooFieldType.Monetary	, "monetary" )]	public decimal? monetary	{ get; set; }
	[OdooProp(OdooFieldType.Selection	, "selection")]	public string? selection	{ get; set; }
	[OdooProp(OdooFieldType.Text		, "text"	 )]	public string? text			{ get; set; }
	[OdooProp(OdooFieldType.Many2One	, "manytoone" )] public Many2One? many2one	{ get; set; }
	[OdooProp(OdooFieldType.One2Many	, "onetomany" )] public One2Many? one2many	{ get; set; }
    [OdooProp(OdooFieldType.Many2Many	, "manytomany")] public Many2Many? many2many { get; set; }
	
	IMany2One? IHpTestModel.many2one { get => (IMany2One?)many2one; set => many2one = (Many2One?)value; }
	IOne2Many? IHpTestModel.one2many { get => (IOne2Many?)one2many; set => one2many = (One2Many?)value; }
	IMany2Many? IHpTestModel.many2many { get => (IMany2Many?)many2many; set => many2many = (Many2Many?)value; }
}
