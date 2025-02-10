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
        MD5
    }
    public enum MethodType : byte
    {
        PropertyOnly,
        FieldOnly,
        PropertyAndField,
    }
    public enum relationType
    {
        Parent,
        Child,
        Both,
    }
    public enum FileSize : byte
    {
        B,
        KB,
        MB,
        GB,
        TB,
    }
    public enum CatType : byte
    {
        CAD,
        DOCUMENT,
    }

    
}
