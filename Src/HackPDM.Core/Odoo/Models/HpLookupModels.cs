namespace HackPDM.Core.Odoo.Models;

public sealed class HpCategory : HpRecord<HpCategory>, IOdooModel<HpCategory>
{
	public static string ModelName => "hp.category";
	public static string[] AllFields { get; } = [ "name", "cat_description", "track_version", "track_depends" ];

	public string? name { get; set; }
	public string? cat_description { get; set; }
	public bool? track_version { get; set; }
	public bool? track_depends { get; set; }

	public override bool TrySetField( string field, object? value )
	{
		switch( field )
		{
			case "name": name = OdooValues.ReadString( value ); return true;
			case "cat_description": cat_description = OdooValues.ReadString( value ); return true;
			case "track_version": track_version = OdooValues.ReadBool( value ); return true;
			case "track_depends": track_depends = OdooValues.ReadBool( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => new()
	{
		[ "name" ] = name,
		[ "cat_description" ] = cat_description,
		[ "track_version" ] = OdooValues.OutBool( track_version ),
		[ "track_depends" ] = OdooValues.OutBool( track_depends ),
	};

	public override string ToString() => name ?? "";
}

public sealed class HpCategoryProperty : HpRecord<HpCategoryProperty>, IOdooModel<HpCategoryProperty>
{
	public static string ModelName => "hp.category.property";
	public static string[] AllFields { get; } = [ "cat_id", "prop_id" ];

	public Many2One? cat_id { get; set; }
	public Many2One? prop_id { get; set; }

	public override bool TrySetField( string field, object? value )
	{
		switch( field )
		{
			case "cat_id": cat_id = OdooValues.ReadMany2One( value ); return true;
			case "prop_id": prop_id = OdooValues.ReadMany2One( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => new()
	{
		[ "cat_id" ] = cat_id?.id,
		[ "prop_id" ] = prop_id?.id,
	};
}

public sealed class HpEntryNameFilter : HpRecord<HpEntryNameFilter>, IOdooModel<HpEntryNameFilter>
{
	public static string ModelName => "hp.entry.name.filter";
	public static string[] AllFields { get; } = [ "name_proto", "name_regex", "description" ];

	public string? name_proto { get; set; }
	public string? name_regex { get; set; }
	public string? description { get; set; }

	public override bool TrySetField( string field, object? value )
	{
		switch( field )
		{
			case "name_proto": name_proto = OdooValues.ReadString( value ); return true;
			case "name_regex": name_regex = OdooValues.ReadString( value ); return true;
			case "description": description = OdooValues.ReadString( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => new()
	{
		[ "name_proto" ] = name_proto,
		[ "name_regex" ] = name_regex,
		[ "description" ] = description,
	};
}

public sealed class HpNode : HpRecord<HpNode>, IOdooModel<HpNode>
{
	public static string ModelName => "hp.node";
	public static string[] AllFields { get; } = [ "name" ];

	public string? name { get; set; }

	public override bool TrySetField( string field, object? value )
	{
		if( field != "name" )
			return false;
		name = OdooValues.ReadString( value );
		return true;
	}

	public override Dictionary<string, object?> ToOdoo() => new() { [ "name" ] = name };
}

public sealed class HpProperty : HpRecord<HpProperty>, IOdooModel<HpProperty>
{
	public static string ModelName => "hp.property";
	public static string[] AllFields { get; } = [ "name", "prop_type", "active" ];

	public string? name { get; set; }
	public string? prop_type { get; set; }
	public bool? active { get; set; }

	public override bool TrySetField( string field, object? value )
	{
		switch( field )
		{
			case "name": name = OdooValues.ReadString( value ); return true;
			case "prop_type": prop_type = OdooValues.ReadString( value ); return true;
			case "active": active = OdooValues.ReadBool( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => new()
	{
		[ "name" ] = name,
		[ "prop_type" ] = prop_type,
		[ "active" ] = OdooValues.OutBool( active ),
	};
}

public sealed class HpRecordStaged : HpRecord<HpRecordStaged>, IOdooModel<HpRecordStaged>
{
	public static string ModelName => "hp.record.staged";
	public static string[] AllFields { get; } = [ "target_model", "target_id", "payload", "committing_id" ];

	public string? target_model { get; set; }
	public int? target_id { get; set; }
	public Dictionary<string, object?>? payload { get; set; }
	public Many2One? committing_id { get; set; }

	public override bool TrySetField( string field, object? value )
	{
		switch( field )
		{
			case "target_model": target_model = OdooValues.ReadString( value ); return true;
			case "target_id": target_id = OdooValues.ReadInt( value ); return true;
			case "payload": payload = OdooValues.ReadJsonObject( value ); return true;
			case "committing_id": committing_id = OdooValues.ReadMany2One( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => new()
	{
		[ "target_model" ] = target_model,
		[ "target_id" ] = target_id,
		[ "payload" ] = payload,
		[ "committing_id" ] = committing_id?.id,
	};
}

public sealed class HpSetting : HpRecord<HpSetting>, IOdooModel<HpSetting>
{
	public static string ModelName => "hp.settings";
	public static string[] AllFields { get; } =
	[
		"name", "description", "type", "bool_value", "int_value", "char_value", "float_value", "date_value",
	];

	public string? name { get; set; }
	public string? description { get; set; }
	public string? type { get; set; }
	public bool? bool_value { get; set; }
	public int? int_value { get; set; }
	public string? char_value { get; set; }
	public float? float_value { get; set; }
	public DateTime? date_value { get; set; }

	public override bool TrySetField( string field, object? value )
	{
		switch( field )
		{
			case "name": name = OdooValues.ReadString( value ); return true;
			case "description": description = OdooValues.ReadString( value ); return true;
			case "type": type = OdooValues.ReadString( value ); return true;
			case "bool_value": bool_value = OdooValues.ReadBool( value ); return true;
			case "int_value": int_value = OdooValues.ReadInt( value ); return true;
			case "char_value": char_value = OdooValues.ReadString( value ); return true;
			case "float_value": float_value = OdooValues.ReadFloat( value ); return true;
			case "date_value": date_value = OdooValues.ReadDateTime( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => new()
	{
		[ "name" ] = name,
		[ "description" ] = description,
		[ "type" ] = type,
		[ "bool_value" ] = OdooValues.OutBool( bool_value ),
		[ "int_value" ] = int_value,
		[ "char_value" ] = char_value,
		[ "float_value" ] = OdooValues.OutFloat( float_value ),
		[ "date_value" ] = OdooValues.OutDateTime( date_value ),
	};
}

public sealed class HpType : HpRecord<HpType>, IOdooModel<HpType>
{
	public static string ModelName => "hp.type";
	public static string[] AllFields { get; } = [ "description", "file_ext", "type_regex", "icon", "cat_id" ];

	public string? description { get; set; }
	public string? file_ext { get; set; }
	public string? type_regex { get; set; }
	public byte[]? icon { get; set; }
	public Many2One? cat_id { get; set; }

	public override bool TrySetField( string field, object? value )
	{
		switch( field )
		{
			case "description": description = OdooValues.ReadString( value ); return true;
			case "file_ext": file_ext = OdooValues.ReadString( value ); return true;
			case "type_regex": type_regex = OdooValues.ReadString( value ); return true;
			case "icon": icon = OdooValues.ReadBytes( value ); return true;
			case "cat_id": cat_id = OdooValues.ReadMany2One( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => new()
	{
		[ "description" ] = description,
		[ "file_ext" ] = file_ext,
		[ "type_regex" ] = type_regex,
		[ "icon" ] = OdooValues.OutBytes( icon ),
		[ "cat_id" ] = cat_id?.id,
	};
}

public sealed class HpUser : HpRecord<HpUser>, IOdooModel<HpUser>
{
	public static string ModelName => "res.users";
	public static string[] AllFields { get; } = [ "name", "login", "email", "signature", "login_date", "active", "company_id" ];

	public string? name { get; set; }
	public string? login { get; set; }
	public string? email { get; set; }
	public string? signature { get; set; }
	public DateTime? login_date { get; set; }
	public bool? active { get; set; }
	public Many2One? company_id { get; set; }

	public override bool TrySetField( string field, object? value )
	{
		switch( field )
		{
			case "name": name = OdooValues.ReadString( value ); return true;
			case "login": login = OdooValues.ReadString( value ); return true;
			case "email": email = OdooValues.ReadString( value ); return true;
			case "signature": signature = OdooValues.ReadString( value ); return true;
			case "login_date": login_date = OdooValues.ReadDateTime( value ); return true;
			case "active": active = OdooValues.ReadBool( value ); return true;
			case "company_id": company_id = OdooValues.ReadMany2One( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => new()
	{
		[ "name" ] = name,
		[ "login" ] = login,
		[ "email" ] = email,
		[ "signature" ] = signature,
		[ "login_date" ] = OdooValues.OutDateTime( login_date ),
		[ "active" ] = OdooValues.OutBool( active ),
		[ "company_id" ] = company_id?.id,
	};

	public override string ToString() => name ?? id?.ToString() ?? "";
}

public sealed class HpVersionRelationship : HpRecord<HpVersionRelationship>, IOdooModel<HpVersionRelationship>
{
	public static string ModelName => "hp.version.relationship";
	public static string[] AllFields { get; } = [ "parent_id", "child_id" ];

	public Many2One? parent_id { get; set; }
	public Many2One? child_id { get; set; }

	public override bool TrySetField( string field, object? value )
	{
		switch( field )
		{
			case "parent_id": parent_id = OdooValues.ReadMany2One( value ); return true;
			case "child_id": child_id = OdooValues.ReadMany2One( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => new()
	{
		[ "parent_id" ] = parent_id?.id,
		[ "child_id" ] = child_id?.id,
	};
}

public sealed class IrAttachment : HpRecord<IrAttachment>, IOdooModel<IrAttachment>
{
	public static string ModelName => "ir.attachment";
	public static string[] AllFields { get; } = [ "name", "res_model", "checksum", "mimetype", "type", "file_size", "res_id" ];

	public string? name { get; set; }
	public string? res_model { get; set; }
	public string? checksum { get; set; }
	public string? mimetype { get; set; }
	public string? type { get; set; }
	public int? file_size { get; set; }
	public Many2One? res_id { get; set; }

	public override bool TrySetField( string field, object? value )
	{
		switch( field )
		{
			case "name": name = OdooValues.ReadString( value ); return true;
			case "res_model": res_model = OdooValues.ReadString( value ); return true;
			case "checksum": checksum = OdooValues.ReadString( value ); return true;
			case "mimetype": mimetype = OdooValues.ReadString( value ); return true;
			case "type": type = OdooValues.ReadString( value ); return true;
			case "file_size": file_size = OdooValues.ReadInt( value ); return true;
			case "res_id": res_id = OdooValues.ReadMany2One( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => new()
	{
		[ "name" ] = name,
		[ "res_model" ] = res_model,
		[ "checksum" ] = checksum,
		[ "mimetype" ] = mimetype,
		[ "type" ] = type,
		[ "file_size" ] = file_size,
		[ "res_id" ] = res_id?.id,
	};
}
