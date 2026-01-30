//using static System.Net.Mime.MediaTypeNames;

// Resharper disable InconsistentNaming
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_CATEGORY_PROPERTY_NAME, OdooDefaultsConstants.HP_CATEGORY_PROPERTY)]
public interface IHpCategoryPropertyModel : IHpOdooRecord
{
	[OdooProp(OdooFieldType.Many2One, "cat_id")] public IMany2One? cat_id { get; set; }
	[OdooProp(OdooFieldType.Many2One, "prop_id")] public IMany2One? prop_id { get; set; }
}