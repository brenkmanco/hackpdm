using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HackPDM.Extensions.General;
using HackPDM.Hack;
// ReSharper disable InconsistentNaming


//using static System.Net.Mime.MediaTypeNames;



namespace HackPDM.Odoo.OdooModels.Models;

public partial class HpVersionProperty : HpBaseModel<HpVersionProperty>
{
	public string sw_config_name;
        public string text_value;
        public float number_value;
        public bool yesno_value;
        public string date_value;
        public int version_id;
        public int prop_id;
        public string prop_name;
    
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
public partial class HpVersionProperty : HpBaseModel<HpVersionProperty>
{
    public PropertyType GetValueType()
    {
        if (text_value != null && text_value != "" && text_value != "False") return PropertyType.Text;
        if (date_value != null && date_value != "" && date_value != "False") return PropertyType.Date;
        if (number_value != default) return PropertyType.Number;
        if (yesno_value != default) return PropertyType.Yesno;
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
            number = number_value;
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
            yesNo = yesno_value;
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
                if (!OdooDefaults.DependentExt.Contains($".{version.file_ext.ToUpper()}")) continue;
                string pathway = version.WinPathway;
                List<string> paths = [];
				
                List<Tuple<string, string, string, object>> props = HackDefaults.DocMgr.GetProperties(pathway);
                HpVersionProperty[] properties = [.. props.Select(prop =>
                {
                    bool isSuccessful = false;
                    if (!OdooDefaults.RestrictProperties | OdooDefaults.ExtToProp.TryGetValue(prop.Item2, out HpProperty hpProperty))
                    {
                        HpVersionProperty vProp = new()
                        {
                            sw_config_name = prop.Item1 == "" ? null : prop.Item1,
                            version_id = version.Id != 0 ? version.Id : throw new Exception("version id not defined"),
                        };
                        if (hpProperty is not null) vProp.prop_id = hpProperty.Id; 
                        switch (prop.Item3)
                        {
                            case "text": vProp.text_value        = (string)prop.Item4; break;
                            case "date": vProp.date_value        = (string)prop.Item4; break;
                            case "yesno": vProp.yesno_value      = (bool)prop.Item4; break;
                            case "number": vProp.number_value    = (float)prop.Item4; break;
                        }
                        isSuccessful = true;
                        Debug.WriteLine($"prop: {prop.Item2} | {isSuccessful}");
                        return vProp;
                    }
                    Debug.WriteLine($"prop: {prop.Item2} | {isSuccessful}");
                    return null;
                })];
                versionProperties = [.. versionProperties, .. properties];
            }
            catch (Exception e)
            {
                Debug.WriteLine($"unable to create properties for {version.Id}\n{e}");
                return;
            }
        }
        if (versionProperties.Length > 0)
        {
            await MultiCreateAsync<HpVersionProperty>(versionProperties.ToArrayList());
        }
    }
    public enum PropertyType
    {
        Text,
        Number,
        Yesno,
        Date,
        None,
    }
}