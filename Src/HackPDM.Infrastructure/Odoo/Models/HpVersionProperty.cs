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
    /// <summary>Works out which of the value fields holds this property's value.</summary>
    /// <returns>
    /// The first match in this order: <see cref="HackPDM.Shared.GlobalData.PropertyType.Text"/> for a non-empty
    /// <c>text_value</c> other than "False" (Odoo's empty marker); <see cref="HackPDM.Shared.GlobalData.PropertyType.Date"/>
    /// when <c>date_value</c> is set; <see cref="HackPDM.Shared.GlobalData.PropertyType.Number"/> when <c>number_value</c>
    /// is not 0; <see cref="HackPDM.Shared.GlobalData.PropertyType.Yesno"/> when <c>yesno_value</c> is true; otherwise
    /// <see cref="HackPDM.Shared.GlobalData.PropertyType.None"/>.
    /// </returns>
    /// <remarks>
    /// A <see langword="null"/> <c>number_value</c> also counts as "not 0", so a property with no value set
    /// reports Number; a yes/no property that is false reports None.
    /// </remarks>
    public PropertyType GetValueType()
    {
        if (!string.IsNullOrEmpty(text_value) && text_value != "False") return PropertyType.Text;
        if (date_value is not null) return PropertyType.Date;
        if (number_value != 0) return PropertyType.Number;
        if (yesno_value is true) return PropertyType.Yesno;
        return PropertyType.None;
    }
        
    /// <summary>Gets the text value when <see cref="GetValueType"/> says the property is text.</summary>
    /// <param name="text">The <c>text_value</c> when the property is text; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the property's value type is Text.</returns>
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
    /// <summary>Gets the number value when <see cref="GetValueType"/> says the property is a number.</summary>
    /// <param name="number">The <c>number_value</c> (0 when null) when the property is a number; otherwise 0.</param>
    /// <returns><see langword="true"/> when the property's value type is Number.</returns>
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
    /// <summary>Gets the yes/no value when <see cref="GetValueType"/> says the property is yes/no.</summary>
    /// <param name="yesNo">Set to <see langword="true"/> when the property is yes/no; otherwise <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when the property's value type is Yesno (which only happens when <c>yesno_value</c> is true).</returns>
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
    /// <summary>Checks whether the property is a date.</summary>
    /// <param name="date">
    /// When the property is a date, the <c>text_value</c> field (not <c>date_value</c>), which is usually empty for
    /// date properties; otherwise <see langword="null"/>.
    /// </param>
    /// <returns><see langword="true"/> when the property's value type is Date.</returns>
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
	/// Turns a custom property read from a SolidWorks file into an unsaved property record of a version.
	/// </summary>
	/// <param name="config">The SolidWorks configuration name; "" (file-level property) is stored as <see langword="null"/>.</param>
	/// <param name="name">The property name; looked up in <c>OdooDefaults.Instance.ExtToProp</c> to fill <c>prop_id</c>.</param>
	/// <param name="type">The SolidWorks property type: "text", "date", "yesno" or "number". Any other type leaves every value field empty.</param>
	/// <param name="value">The value, boxed as the type implies (string, <see cref="System.DateTime"/>, bool, or a number).</param>
	/// <param name="versionId">The hp.version id the property belongs to.</param>
	/// <param name="commitId">The commit the property is staged in.</param>
	/// <returns>
	/// The new record (not yet sent to the server), or <see langword="null"/> when the property name is unknown and
	/// the server's <c>RestrictProperties</c> setting is not explicitly false.
	/// </returns>
	/// <exception cref="System.InvalidCastException"><paramref name="value"/> is not of the type that <paramref name="type"/> implies.</exception>
	/// <exception cref="System.FormatException">A "number" <paramref name="value"/> is a string that is not a number.</exception>
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
