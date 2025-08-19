using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HackPDM.ClientUtils
{
    public enum FileStatus
    {
        LO, // Local Only
        RO, // Remote Only
        OK, // Latest
        NV, // Newer Remote Version
        LM, // Local Modification
        DT, // Deleted
        DS, // Destroyed
        IF, // Ignore Filter
        FT, // Filter Type
        CM, // Checked Out To Me
        CO, // Checked Out To Other

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
        ILike,
        NotILike,
        ILikeEqual,
        In,
        NotIn,
        ChildOf,
        ParentOf
    }
    public enum ValueConversion
    {
        NULL,
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
        SHA1,
        MD5,
        SHA256,
        SHA512,
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
    [Serializable]
    public enum ThemeType
    {
        Default,
        Dark,
        Light,
        Preset1,
    }
}
