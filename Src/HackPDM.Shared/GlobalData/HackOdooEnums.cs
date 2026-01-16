using System;

namespace HackPDM.Shared.GlobalData;

public enum FileStatus
{
    Lo, // Local Only
    Ro, // Remote Only
    Ok, // Latest
    Nv, // Newer Remote Version
    Lm, // Local Modification
    Dt, // Deleted
    Ds, // Destroyed
    If, // Ignore Filter
    Ft, // Filter Type
    Cm, // Checked Out To Me
    Co, // Checked Out To Other

}

public enum EntryReturnType
{
	Created,
	GotExisting,
	Failed,
	InvalidType,
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
	ILikeEqual
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
public enum StatusMessage
{
    PROCESSING,
    SKIP,
    FOUND,
    INFO,
    SUCCESS,
    WARNING,
    ERROR,
    OTHER,
}
public enum ChecksumType
{
    Sha1,
    Md5,
    Sha256,
    Sha512,
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
    All
}
public enum ColumnGroup
{ 
    Row,
    History,
    Parent,
    Child,
    Property,
    Version,
    Search,
    SearchProp,
    FileType,
    FileTypeEntryFilter,
    FileTypeLoc,
    FileTypeLocDat,
}
public enum HackVersionType
{
    Latest,
    Older,
    Unique,
    New,
}
public enum TaskProgress
{
    InProgress,
    Finished,
    Cancelling,
}
public enum SortPredefined
{
    String,
    Int,
    Date,
    Unknown,
}
public enum ExtType
{
	Svg,
	Png,
	Other
}
public enum EntryReprType
{
	Remote,
	Local,
	Both,
}
[Flags]
public enum HackTestDepth : ushort
{
	NoTest              = 0,
	InPWATest           = 1,
	FileExistsTest      = 2,
	FileIsCorruptTest   = 4,
}
[Flags]
public enum HackResult : ushort
{
	NotTested           = 0,
	Clean               = 1,
	OutOfPWA            = 2,
	PropogatedFailure   = 4,
	CorruptDepFile      = 8,
	MissingFile         = 16,
	MissingDepFile      = 32,
}
