using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HackPDM.Core;
using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Infrastructure.SldWrks;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

// Resharper disable InconsistentNaming


//using static System.Net.Mime.MediaTypeNames;



namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.HP_VERSION_PROPERTY_NAME, OdooDefaultsConstants.HP_VERSION_PROPERTY)]
public partial class HpVersionProperty : HpBaseModelTransport<HpVersionProperty>, IHpVersionPropertyModel
{
	[OdooProp(OdooFieldType.Char, "prop_name")] public string? prop_name { get; set; }
	[OdooProp(OdooFieldType.Char, "sw_config_name")] public string? sw_config_name { get; set; }
	[OdooProp(OdooFieldType.Char, "text_value")] public string? text_value { get; set; }
	[OdooProp(OdooFieldType.Float, "number_value")] public float? number_value { get; set; }
	[OdooProp(OdooFieldType.Boolean, "yesno_value")] public bool? yesno_value { get; set; }
	[OdooProp(OdooFieldType.DateTime, "date_value")] public DateTime? date_value { get; set; }
	[OdooProp(OdooFieldType.Many2One, "version_id")] public Many2One? version_id { get; set; }
	[OdooProp(OdooFieldType.Many2One, "prop_id")] public Many2One? prop_id { get; set; }

	IMany2One? IHpVersionPropertyModel.version_id { get => (IMany2One?)version_id; set => version_id = (Many2One?)value; }
	IMany2One? IHpVersionPropertyModel.prop_id { get => (IMany2One?)prop_id; set => prop_id = (Many2One?)value; }

	public HpVersionProperty() { }
}
public partial class HpVersionProperty : HpBaseModelTransport<HpVersionProperty>
{
    public PropertyType GetValueType()
    {
        if (!string.IsNullOrEmpty(text_value) && text_value != "False") return PropertyType.Text;
        if (date_value is not null) return PropertyType.Date;
        if (number_value != 0) return PropertyType.Number;
        if (yesno_value is true) return PropertyType.Yesno;
        return PropertyType.None;
    }
        
    public bool IsText( out string? text )
    {
        PropertyType pType = GetValueType();
        text = null;
        if (pType == PropertyType.Text)
        {
            text = text_value;
            return true;
        }
        return false;
    }
    public bool IsNumber( out float number )
    {
        PropertyType pType = GetValueType();
        number = default;
        if (pType == PropertyType.Number)
        {
            number = number_value ?? 0;
            return true;
        }
        return false;
    }
    public bool IsYesNo(out bool yesNo)
    {
        PropertyType pType = GetValueType();
        yesNo = default;
        if (pType == PropertyType.Yesno)
        {
            yesNo = yesno_value is true;
            return true;
        }
        return false;
    }
    public bool IsDate(out string? date)
    {
        PropertyType pType = GetValueType();
        date = null;
        if (pType == PropertyType.Date)
        {
            date = text_value;
            return true;
        }
        return false;
    }
	/// <summary>
	/// A custom property read from a SolidWorks file (configuration, name, type, value) as a property
	/// of version <paramref name="versionId"/>; null when the server doesn't know the property and
	/// only allows known ones.
	/// </summary>
	public static HpVersionProperty? FromSwProperty( string config, string name, string type, object? value, int versionId, IMany2One? commitId )
	{
		IHpPropertyModel? known = null;
		bool isKnown = OdooDefaults.Instance.ExtToProp?.TryGetValue( name, out known ) ?? false;
		if( !isKnown && OdooDefaults.Instance.RestrictProperties is not false )
			return null;
		HpVersionProperty property = new()
		{
			sw_config_name = config == "" ? null : config,
			version_id = versionId,
			commit_id = commitId,
		};
		if( known is not null )
			property.prop_id = known.id ?? 0;
		switch( type )
		{
			case "text": property.text_value = (string?)value; break;
			case "date": property.date_value = (DateTime?)value; break;
			case "yesno": property.yesno_value = (bool?)value; break;
			// SWDocMgr boxes numbers as decimal; unboxing that as float? throws
			case "number": property.number_value = value is null ? null : Convert.ToSingle( value, System.Globalization.CultureInfo.InvariantCulture ); break;
		}
		return property;
	}
}
