using System.Drawing;
using HackPDM.Extensions.General;
// ReSharper disable InconsistentNaming


//using static System.Net.Mime.MediaTypeNames;



namespace HackPDM.Odoo.OdooModels.Models;

public partial class HpType : HpBaseModel<HpType>
{
	public string description;
	public string file_ext;
	public string icon;
	public string type_regex;
	public int cat_id;
	public Image ImageSave {get;set;}

	public HpType()
	{ 
	}
	public HpType(
		string description = null,
		string fileExt = null,
		string iconBase64 = null,
		string typeRegex = null,
		int catId = 0)
	{
		this.description = description;
		this.file_ext = fileExt; 
		this.icon = iconBase64;
		this.type_regex = typeRegex;
		this.cat_id = catId;
		this.ImageSave = null;
	}
	public HpType(
		string description = null,
		string fileExt = null,
		Image icon = null,
		string typeRegex = null,
		int catId = 0,
		bool saveImageType = false)
	{
		this.description = description;
		this.file_ext = fileExt;
		this.type_regex = typeRegex;
		this.cat_id = catId;

		if (saveImageType) this.ImageSave = icon;
		else this.ImageSave = null;

		this.icon = icon?.ToBase64String();
	}
}
public partial class HpType : HpBaseModel<HpType> {}