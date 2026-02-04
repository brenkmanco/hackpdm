namespace HackPDM.SchemaGenerator.GeneratedTypes;

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
    Image,         // byte[] (special optimized image) -----|
    Json,          // JSON object / dict                    |
				   //                                       |      
				   // Relational types                      |
	Many2One,      // foreign key to another model ---------|
    One2Many,      // collection of related records         |
    Many2Many,     // many-to-many relation ----------------|
    //                                                      |
    // Special / computed types                             |
    Selection,     // enum-like choice field                |
    Reference,     // polymorphic relation (model + id) ----|
    Serialized,    // JSON/dict stored in DB                |
    //                                                      |
    Unknown,       // Catch all case to bruteforce type ----|
}