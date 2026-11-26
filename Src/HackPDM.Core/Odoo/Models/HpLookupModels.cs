namespace HackPDM.Core.Odoo.Models;

public sealed class HpCategory : HpRecord<HpCategory>, IOdooModel<HpCategory>
{
	public static string ModelName => "hp.category";
	public static string[] AllFields { get; } = [ nameof( name ), nameof( cat_description ), nameof( track_version ), nameof( track_depends ) ];

	public string? name { get; set; }
	public string? cat_description { get; set; }
	public bool? track_version { get; set; }
	public bool? track_depends { get; set; }

	public override bool TrySetField( string field, object? value )
	{
		switch( field )
		{
			case nameof( name ): name = OdooValues.ReadString( value ); return true;
			case nameof( cat_description ): cat_description = OdooValues.ReadString( value ); return true;
			case nameof( track_version ): track_version = OdooValues.ReadBool( value ); return true;
			case nameof( track_depends ): track_depends = OdooValues.ReadBool( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => new()
	{
		[ nameof( name ) ] = name,
		[ nameof( cat_description ) ] = cat_description,
		[ nameof( track_version ) ] = OdooValues.OutBool( track_version ),
		[ nameof( track_depends ) ] = OdooValues.OutBool( track_depends ),
	};

	public override string ToString() => name ?? "";
}

public sealed class HpEntryNameFilter : HpRecord<HpEntryNameFilter>, IOdooModel<HpEntryNameFilter>
{
	public static string ModelName => "hp.entry.name.filter";
	public static string[] AllFields { get; } = [ nameof( name_proto ), nameof( name_regex ), nameof( description ) ];

	public string? name_proto { get; set; }
	public string? name_regex { get; set; }
	public string? description { get; set; }

	public override bool TrySetField( string field, object? value )
	{
		switch( field )
		{
			case nameof( name_proto ): name_proto = OdooValues.ReadString( value ); return true;
			case nameof( name_regex ): name_regex = OdooValues.ReadString( value ); return true;
			case nameof( description ): description = OdooValues.ReadString( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => new()
	{
		[ nameof( name_proto ) ] = name_proto,
		[ nameof( name_regex ) ] = name_regex,
		[ nameof( description ) ] = description,
	};
}

public sealed class HpNode : HpRecord<HpNode>, IOdooModel<HpNode>
{
	public static string ModelName => "hp.node";
	public static string[] AllFields { get; } = [ nameof( name ) ];

	public string? name { get; set; }

	public override bool TrySetField( string field, object? value )
	{
		if( field != nameof( name ) )
			return false;
		name = OdooValues.ReadString( value );
		return true;
	}

	public override Dictionary<string, object?> ToOdoo() => new() { [ nameof( name ) ] = name };
}

public sealed class HpProperty : HpRecord<HpProperty>, IOdooModel<HpProperty>
{
	public static string ModelName => "hp.property";
	public static string[] AllFields { get; } = [ nameof( name ), nameof( prop_type ), nameof( active ) ];

	public string? name { get; set; }
	public string? prop_type { get; set; }
	public bool? active { get; set; }

	public override bool TrySetField( string field, object? value )
	{
		switch( field )
		{
			case nameof( name ): name = OdooValues.ReadString( value ); return true;
			case nameof( prop_type ): prop_type = OdooValues.ReadString( value ); return true;
			case nameof( active ): active = OdooValues.ReadBool( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => new()
	{
		[ nameof( name ) ] = name,
		[ nameof( prop_type ) ] = prop_type,
		[ nameof( active ) ] = OdooValues.OutBool( active ),
	};
}

public sealed class HpRecordStaged : HpRecord<HpRecordStaged>, IOdooModel<HpRecordStaged>
{
	public static string ModelName => "hp.record.staged";
	public static string[] AllFields { get; } = [ nameof( target_model ), nameof( target_id ), nameof( payload ), nameof( committing_id ) ];

	public string? target_model { get; set; }
	public int? target_id { get; set; }
	public Dictionary<string, object?>? payload { get; set; }
	public Many2One? committing_id { get; set; }

	public override bool TrySetField( string field, object? value )
	{
		switch( field )
		{
			case nameof( target_model ): target_model = OdooValues.ReadString( value ); return true;
			case nameof( target_id ): target_id = OdooValues.ReadInt( value ); return true;
			case nameof( payload ): payload = OdooValues.ReadJsonObject( value ); return true;
			case nameof( committing_id ): committing_id = OdooValues.ReadMany2One( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => new()
	{
		[ nameof( target_model ) ] = target_model,
		[ nameof( target_id ) ] = target_id,
		[ nameof( payload ) ] = payload,
		[ nameof( committing_id ) ] = committing_id?.id,
	};
}

public sealed class HpSetting : HpRecord<HpSetting>, IOdooModel<HpSetting>
{
	public static string ModelName => "hp.settings";
	public static string[] AllFields { get; } =
	[
		nameof( name ), nameof( description ), nameof( type ), nameof( bool_value ), nameof( int_value ), nameof( char_value ), nameof( float_value ), nameof( date_value ),
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
			case nameof( name ): name = OdooValues.ReadString( value ); return true;
			case nameof( description ): description = OdooValues.ReadString( value ); return true;
			case nameof( type ): type = OdooValues.ReadString( value ); return true;
			case nameof( bool_value ): bool_value = OdooValues.ReadBool( value ); return true;
			case nameof( int_value ): int_value = OdooValues.ReadInt( value ); return true;
			case nameof( char_value ): char_value = OdooValues.ReadString( value ); return true;
			case nameof( float_value ): float_value = OdooValues.ReadFloat( value ); return true;
			case nameof( date_value ): date_value = OdooValues.ReadDateTime( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => new()
	{
		[ nameof( name ) ] = name,
		[ nameof( description ) ] = description,
		[ nameof( type ) ] = type,
		[ nameof( bool_value ) ] = OdooValues.OutBool( bool_value ),
		[ nameof( int_value ) ] = int_value,
		[ nameof( char_value ) ] = char_value,
		[ nameof( float_value ) ] = OdooValues.OutFloat( float_value ),
		[ nameof( date_value ) ] = OdooValues.OutDateTime( date_value ),
	};
}

public sealed class HpType : HpRecord<HpType>, IOdooModel<HpType>
{
	public static string ModelName => "hp.type";
	public static string[] AllFields { get; } = [ nameof( description ), nameof( file_ext ), nameof( type_regex ), nameof( icon ), nameof( cat_id ) ];

	public string? description { get; set; }
	public string? file_ext { get; set; }
	public string? type_regex { get; set; }
	public byte[]? icon { get; set; }
	public Many2One? cat_id { get; set; }

	public override bool TrySetField( string field, object? value )
	{
		switch( field )
		{
			case nameof( description ): description = OdooValues.ReadString( value ); return true;
			case nameof( file_ext ): file_ext = OdooValues.ReadString( value ); return true;
			case nameof( type_regex ): type_regex = OdooValues.ReadString( value ); return true;
			case nameof( icon ): icon = OdooValues.ReadBytes( value ); return true;
			case nameof( cat_id ): cat_id = OdooValues.ReadMany2One( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => new()
	{
		[ nameof( description ) ] = description,
		[ nameof( file_ext ) ] = file_ext,
		[ nameof( type_regex ) ] = type_regex,
		[ nameof( icon ) ] = OdooValues.OutBytes( icon ),
		[ nameof( cat_id ) ] = cat_id?.id,
	};
}

public sealed class HpUser : HpRecord<HpUser>, IOdooModel<HpUser>
{
	public static string ModelName => "res.users";
	public static string[] AllFields { get; } = [ nameof( name ), nameof( login ), nameof( email ), nameof( signature ), nameof( login_date ), nameof( active ), nameof( company_id ) ];

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
			case nameof( name ): name = OdooValues.ReadString( value ); return true;
			case nameof( login ): login = OdooValues.ReadString( value ); return true;
			case nameof( email ): email = OdooValues.ReadString( value ); return true;
			case nameof( signature ): signature = OdooValues.ReadString( value ); return true;
			case nameof( login_date ): login_date = OdooValues.ReadDateTime( value ); return true;
			case nameof( active ): active = OdooValues.ReadBool( value ); return true;
			case nameof( company_id ): company_id = OdooValues.ReadMany2One( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => new()
	{
		[ nameof( name ) ] = name,
		[ nameof( login ) ] = login,
		[ nameof( email ) ] = email,
		[ nameof( signature ) ] = signature,
		[ nameof( login_date ) ] = OdooValues.OutDateTime( login_date ),
		[ nameof( active ) ] = OdooValues.OutBool( active ),
		[ nameof( company_id ) ] = company_id?.id,
	};

	public override string ToString() => name ?? id?.ToString() ?? "";
}

public sealed class HpVersionRelationship : HpRecord<HpVersionRelationship>, IOdooModel<HpVersionRelationship>
{
	public static string ModelName => "hp.version.relationship";
	public static string[] AllFields { get; } = [ nameof( parent_id ), nameof( child_id ) ];

	public Many2One? parent_id { get; set; }
	public Many2One? child_id { get; set; }

	public override bool TrySetField( string field, object? value )
	{
		switch( field )
		{
			case nameof( parent_id ): parent_id = OdooValues.ReadMany2One( value ); return true;
			case nameof( child_id ): child_id = OdooValues.ReadMany2One( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => new()
	{
		[ nameof( parent_id ) ] = parent_id?.id,
		[ nameof( child_id ) ] = child_id?.id,
	};
}

public sealed class IrAttachment : HpRecord<IrAttachment>, IOdooModel<IrAttachment>
{
	public static string ModelName => "ir.attachment";
	public static string[] AllFields { get; } = [ nameof( name ), nameof( res_model ), nameof( checksum ), nameof( mimetype ), nameof( type ), nameof( file_size ), nameof( res_id ) ];

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
			case nameof( name ): name = OdooValues.ReadString( value ); return true;
			case nameof( res_model ): res_model = OdooValues.ReadString( value ); return true;
			case nameof( checksum ): checksum = OdooValues.ReadString( value ); return true;
			case nameof( mimetype ): mimetype = OdooValues.ReadString( value ); return true;
			case nameof( type ): type = OdooValues.ReadString( value ); return true;
			case nameof( file_size ): file_size = OdooValues.ReadInt( value ); return true;
			case nameof( res_id ): res_id = OdooValues.ReadMany2One( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => new()
	{
		[ nameof( name ) ] = name,
		[ nameof( res_model ) ] = res_model,
		[ nameof( checksum ) ] = checksum,
		[ nameof( mimetype ) ] = mimetype,
		[ nameof( type ) ] = type,
		[ nameof( file_size ) ] = file_size,
		[ nameof( res_id ) ] = res_id?.id,
	};
}
