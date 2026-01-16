using System;
using System.Linq;

namespace HackPDM.Shared.GlobalData;

public static class OdooDefaultsConstants
{
	// made models
	public const string HP_NODE = "hp.node";
	public const string HP_ENTRY = "hp.entry";
	public const string HP_ENTRY_NAME_FILTER = "hp.entry.name.filter";
	public const string HP_DIRECTORY = "hp.directory";
	public const string HP_CATEGORY = "hp.category";
	public const string HP_CATEGORY_PROPERTY = "hp.category.property";
	public const string HP_VERSION = "hp.version";
	public const string HP_VERSION_PROPERTY = "hp.version.property";
	public const string HP_VERSION_RELATIONSHIP = "hp.version.relationship";
	public const string HP_RELEASE = "hp.release";
	public const string HP_RELEASE_REVIEW = "hp.release.review";
	public const string HP_RELEASE_VERSION_REL = "hp.release.version.rel";
	public const string HP_SETTINGS = "hp.settings";
	public const string HP_PROPERTY = "hp.property";
	public const string HP_TYPE = "hp.type";
	// adopted models
	public const string RES_USERS = "res.users";
	public const string IR_ATTACHMENT = "ir.attachment";
	public const string IR_MODEL = "ir.model";
	// name identifiers
	public const string HP_NODE_NAME = "hp_node";
	public const string HP_ENTRY_NAME = "hp_entry";
	public const string HP_ENTRY_NAME_FILTER_NAME = "hp_entry_name_filter";
	public const string HP_DIRECTORY_NAME = "hp_directory";
	public const string HP_CATEGORY_NAME = "hp_category";
	public const string HP_CATEGORY_PROPERTY_NAME = "hp_category_property";
	public const string HP_VERSION_NAME = "hp_version";
	public const string HP_VERSION_PROPERTY_NAME = "hp_version_property";
	public const string HP_VERSION_RELATIONSHIP_NAME = "hp_version_relationship";
	public const string HP_RELEASE_NAME = "hp_release";
	public const string HP_RELEASE_REVIEW_NAME = "hp_release_review";
	public const string HP_RELEASE_VERSION_REL_NAME = "hp_release_version_rel";
	public const string HP_SETTINGS_NAME = "hp_settings";
	public const string HP_PROPERTY_NAME = "hp_property";
	public const string HP_TYPE_NAME = "hp_type";
	public const string RES_USERS_NAME = "res_users";
	public const string IR_ATTACHMENT_NAME = "ir_attachment";
	public const string IR_MODEL_NAME = "ir_model";
	// odoo name identifiers
	public const string ODOO_VERSION_KEY_NAME = "client_version";
	public const string SW_KEY_NAME = "swdocmgr_key";
	public const string RESTRICT_PROP_NAME = "restrict_properties";
	public const string RESTRICT_TYPES_NAME = "restrict_types";

	public static readonly string[] DependentExt = [".SLDPRT", ".SLDASM", ".SLDDRW"];
	public static readonly string DependentExtRegex = "(?i)(" + string.Join("|", DependentExt.Select(s => $"(\\{s}$)")) + ")";
	public static string OdooDateFormat(DateTime dt) => dt.ToString("yyyy-MM-dd HH:mm:ss");
}
