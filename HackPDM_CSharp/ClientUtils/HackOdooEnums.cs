using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HackPDM.ClientUtils
{
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
    public enum SortPredefined
    {
        String,
        Int,
        Date,
        Unknown,
    }
}
