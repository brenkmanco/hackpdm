using System;
using System.Collections;
using System.Collections.Generic;
//
using System.IO;
using System.Linq;
using System.Reflection;

using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;
//


namespace HackPDM.Domain.OdooModels.Models;

public abstract partial class HpBaseModel
{
	// (MVVM) VIEW
	public const int ROOT_OFFSET = 5;
	[OdooProp(OdooFieldType.Integer, "id")] public int? Id { get; set; }

	public static string[] UsualExcludedFields { get; set; } = [];
	public static string[] UsualIncludedFields { get; set; } = [];

	protected static readonly Dictionary<Type, string> HpModelDictionary = new()
    {
        {typeof(IHpNodeModel),                OdooDefaultsConstants.HP_NODE},
        {typeof(IHpEntryModel),               OdooDefaultsConstants.HP_ENTRY},
        {typeof(IHpEntryNameFilterModel),     OdooDefaultsConstants.HP_ENTRY_NAME_FILTER},
        {typeof(IHpDirectoryModel),           OdooDefaultsConstants.HP_DIRECTORY},
        {typeof(IHpCategoryModel),            OdooDefaultsConstants.HP_CATEGORY},
        {typeof(IHpCategoryPropertyModel),    OdooDefaultsConstants.HP_CATEGORY_PROPERTY},
        {typeof(IHpVersionModel),             OdooDefaultsConstants.HP_VERSION},
        {typeof(IHpVersionPropertyModel),     OdooDefaultsConstants.HP_VERSION_PROPERTY},
        {typeof(IHpVersionRelationshipModel), OdooDefaultsConstants.HP_VERSION_RELATIONSHIP},
        {typeof(IHpReleaseModel),             OdooDefaultsConstants.HP_RELEASE},
        {typeof(IHpReleaseVersionRelModel),   OdooDefaultsConstants.HP_RELEASE_VERSION_REL},
        {typeof(IHpTypeModel),                OdooDefaultsConstants.HP_TYPE},
        {typeof(IHpPropertyModel),            OdooDefaultsConstants.HP_PROPERTY},
        {typeof(IHpSettingModel),             OdooDefaultsConstants.HP_SETTINGS},
        {typeof(IIrAttachment),          OdooDefaultsConstants.IR_ATTACHMENT},
        {typeof(IHpUserModel),                OdooDefaultsConstants.RES_USERS},
    };

    public virtual void CompleteConstruction() { }
    // ID of the record in the database
    public virtual string? HpModel
    {
        get
        {
            var type = GetType();
            return HpModelDictionary.GetValueOrDefault(type);
        }
        set
        {
            var type = GetType();
            HpModelDictionary[type] = value;
        }
    }
    
    public bool IsRecord { get; set; }
    public Hashtable HashedValues { get; set; } = [];
    public string[]? ExcludedFields { get; set; }
    public string[]? InsertFields { get; set; }
    
    protected Hashtable ComputeHashtable(bool includeEmpty = true, in string[]? excludedFieldNames = null, bool isNew = false)
    {
	    Hashtable ht = [];
	    Type type = GetType();
	    List<string> excludeFields = [];
	    PropertyInfo[] fields = [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => Attribute.IsDefined(p, typeof(OdooPropAttribute)))];

	    foreach (PropertyInfo field in fields)
	    {
		    if (excludedFieldNames != null && excludedFieldNames.Contains(field.Name))
		    {
			    excludeFields.Add(field.Name);
			    continue;
		    }
		    if (!includeEmpty)
		    {
			    Type fType = field.PropertyType;
			    bool valueType = fType.IsValueType;

			    object fVal = field.GetValue(this);
			    if (valueType && Activator.CreateInstance(fType) == fVal) continue;
			    else if (!valueType && fVal == null) continue;
		    }

		    string fieldName = field.Name;
		    object fieldValue = field.GetValue(this);

		    if (isNew && fieldValue is DateTime dt)
		    {
			    string date = OdooDefaultsConstants.OdooDateFormat(dt);
			    fieldValue = date;
		    }
		    ht.Add(fieldName, fieldValue);
	    }
	    if (excludeFields.Count > 0) ExcludedFields = [.. excludeFields];

	    if (!isNew)
		    ht.Add("id", Id);

	    return ht;
    }
}
