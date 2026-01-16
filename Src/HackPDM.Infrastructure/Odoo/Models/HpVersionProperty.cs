using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
	[OdooProp(OdooFieldType.DateTime, "date_value")] public string? date_value { get; set; }
	[OdooProp(OdooFieldType.Many2one, "version_id")] public Many2One? version_id { get; set; }
	[OdooProp(OdooFieldType.Many2one, "prop_id")] public Many2One? prop_id { get; set; }

	IMany2One? IHpVersionPropertyModel.version_id { get => (IMany2One?)version_id; set => version_id = (Many2One?)value; }
	IMany2One? IHpVersionPropertyModel.prop_id { get => (IMany2One?)prop_id; set => prop_id = (Many2One?)value; }

	public HpVersionProperty() { }
    public HpVersionProperty(
        string swConfigName = null,
        string textValue = null,
        float numberValue = default,
        bool yesnoValue = default,
        string dateValue = null,
        int versionId = 0,
        int propId = 0)
    {
        this.sw_config_name = swConfigName;
        this.text_value = textValue;
        this.number_value = numberValue;
        this.yesno_value = yesnoValue;
        this.date_value = dateValue;
        this.version_id = versionId;
        this.prop_id = propId;
    }
}
public partial class HpVersionProperty : HpBaseModelTransport<HpVersionProperty>
{
    public PropertyType GetValueType()
    {
        if (!string.IsNullOrEmpty(text_value) && text_value != "False") return PropertyType.Text;
        if (!string.IsNullOrEmpty(date_value) && date_value != "False") return PropertyType.Date;
        if (number_value != 0) return PropertyType.Number;
        if (yesno_value is true) return PropertyType.Yesno;
        return PropertyType.None;
    }
        
    public bool IsText( out string text )
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
    public bool IsDate(out string date)
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
    public bool IsNone()
    {
        bool isValue = true;
            
        isValue = IsText(out _);
        if (isValue) return false;
            
        isValue = IsNumber(out _);
        if (isValue) return false;

        isValue = IsDate(out _);
        if (isValue) return false;
            
        isValue = IsYesNo(out _);
        if (isValue) return false;

        return true;
    }
    public static async void Create(params HpVersion[] versions)
    {
        HpVersionProperty[] versionProperties = [];
        foreach (HpVersion version in versions)
        {
            try
            {
                if (!OdooDefaultsConstants.DependentExt.Contains($".{version.file_ext.ToUpper()}")) continue;
                string pathway = version.WinPathway;
                List<string> paths = [];
				
                List<Tuple<string, string, string, object>> props = SolidWorksUtil.DocMgr.GetProperties(pathway);
                HpVersionProperty[] properties = [.. props.SkipSelect( prop =>
                {
                    bool isSuccessful = false;
                    IHpPropertyModel? hpProperty = null;
                    
                    bool isFound = OdooDefaults.Instance.ExtToProp?.TryGetValue(prop.Item2, out hpProperty) ?? false;

                    if (isFound || OdooDefaults.Instance.RestrictProperties is false)
                    {
                        HpVersionProperty vProp = new()
                        {
                            sw_config_name = prop.Item1 == "" ? null : prop.Item1,
                            version_id = version.id != 0 ? version.id : throw new Exception("version id not defined"),
                        };
                        if (hpProperty is not null) vProp.prop_id = hpProperty.id ?? 0; 
                        switch (prop.Item3)
                        {
                            case "text": vProp.text_value        = (string)prop.Item4; break;
                            case "date": vProp.date_value        = (string)prop.Item4; break;
                            case "yesno": vProp.yesno_value      = (bool)prop.Item4; break;
                            case "number": vProp.number_value    = (float)prop.Item4; break;
                        }
                        isSuccessful = true;
                        Debug.WriteLine($"prop: {prop.Item2} | {isSuccessful}");
                        return (false, vProp);
                    }
                    Debug.WriteLine($"prop: {prop.Item2} | {isSuccessful}");
                    return (true, null);
                }) ?? []];
                versionProperties = [.. versionProperties, .. properties];
            }
            catch (Exception e)
            {
                Debug.WriteLine($"unable to create properties for {version.id}\n{e}");
                return;
            }
        }
        if (versionProperties.Length > 0)
        {
            await MultiCreateAsync<HpVersionProperty>(versionProperties.ToArrayList());
        }
    }
}