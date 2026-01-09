//using static System.Net.Mime.MediaTypeNames;

// Resharper disable InconsistentNaming
using HackPDM.Shared.GlobalData;

namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_CATEGORY_PROPERTY_NAME, OdooDefaultsConstants.HP_CATEGORY_PROPERTY)]
public interface IHpCategoryPropertyModel : IHpOdooRecord
{
	[OdooProp(OdooFieldType.Many2one)]
	public int cat_id { get; set; }
	[OdooProp(OdooFieldType.Many2one)]
	public int prop_id { get; set; }
}