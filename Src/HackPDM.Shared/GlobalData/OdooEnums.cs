using System;

namespace HackPDM.Shared.GlobalData;

public enum OdooFieldType
{
	// Basic scalar types
	Char,          // string -------------------------------|
	Text,          // long string / multiline               |
	Html,          // HTML content--------------------------|
	Integer,       // int                                   |
	Float,         // double/decimal -----------------------|
	Monetary,      // decimal with currency                 |
	Boolean,       // bool ---------------------------------|
	Date,          // DateOnly                              |
	DateTime,      // DateTime -----------------------------|
	Binary,        // byte[] (attachments, images)          |
	Image,         // byte[] (image field) -----------------|
	Json,          // JSON field                            |
				   //                                       |      
				   // Relational types                      |
	Many2One,      // foreign key to another model ---------|
	One2Many,      // collection of related records         |
	Many2Many,     // many-to-many relation ----------------|
				   //                                       |
				   // Special / computed types              |
	Selection,     // enum-like choice field                |
	Reference,     // polymorphic relation (model + id) ----|
	Serialized,    // JSON/dict stored in DB                |
				   //                                       |
	Unknown,       // Catch all case to bruteforce type ----|
}

public enum PropertyType
{
	Text,
	Number,
	Yesno,
	Date,
	None,
}

public enum DomainOperators
{
	And,
	Or,
	Not,
}

public enum Operators
{
	Equal,
	NotEqual,
	GreaterThan,
	GreaterThanOrEqual,
	LessThan,
	LessThanOrEqual,
	Unset,
	Like,
	LikeEqual,
	NotLike,
	NotILike,
	In,
	NotIn,
	ChildOf,
	ParentOf,
	ILike,
	ILikeEqual,
}

public enum ValueConversion
{
	Null,
	Assignable,
	Enum,
	DateTime,
	Nullable,
	OtherConvert,
	DoNothing,
	Skip,
}

public enum MethodType : byte
{
	PropertyOnly,
	FieldOnly,
	PropertyAndField,
}

public enum RelationType
{
	Parent,
	Child,
	Both,
}

public enum HashedValueStoring : byte
{
	None,
	ExistingFields,
	NonExistingFields,
	All,
}
