using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HackPDM.Src.ClientUtils.Types;

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
public enum DomainOperators
{
    And,
    Or,
    Not,
}

public enum EntryReturnType
{
	Created,
	GotExisting,
	Failed,
	InvalidType,
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
public enum FormType
{
    Hfm,
    HSet,
    Oftm,
    OSet,
    Pm,
    So,
    Sd,
    Ss,
    Ts
}
public enum HorizontalAlignment
{
    Left,
    Center,
    Right,
    Stretch
}
[Serializable]
public enum ThemeType
{
    Default,
    Dark,
    Light,
    Preset1,
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