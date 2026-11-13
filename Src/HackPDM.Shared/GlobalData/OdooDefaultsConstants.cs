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
	public const string HP_PDM_COMMIT = "hp.pdm.commit";
	public const string HP_RECORD_STAGED = "hp.record.staged";
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
	public const string HP_PDM_COMMIT_NAME = "hp_pdm_commit";
	public const string HP_RECORD_STAGED_NAME = "hp_record_staged";
	public const string RES_USERS_NAME = "res_users";
	public const string IR_ATTACHMENT_NAME = "ir_attachment";
	public const string IR_MODEL_NAME = "ir_model";
	// odoo name identifiers
	/// <summary>hp.settings: the oldest client version the server accepts.</summary>
	public const string ODOO_VERSION_KEY_NAME = "client_version";
	/// <summary>hp.settings: where the current client installer is downloaded from (https URL, or a file or network-share path).</summary>
	public const string CLIENT_DOWNLOAD_URL_NAME = "client_download_url";
	/// <summary>hp.settings: the SHA-256 (hex) of that installer; the updater refuses a file that does not match.</summary>
	public const string CLIENT_DOWNLOAD_SHA256_NAME = "client_download_sha256";
	public const string SW_KEY_NAME = "swdocmgr_key";
	public const string RESTRICT_PROP_NAME = "restrict_properties";
	public const string RESTRICT_TYPES_NAME = "restrict_types";

	// test model
	public const string HP_TEST = "hp.test";
	public const string HP_TEST_NAME = "hp_test";

	public static string? OdooDateFormat(DateTime? dt) => dt?.ToString("yyyy-MM-dd HH:mm:ss");
}
