using System.Collections.Generic;
using System.Reflection;
using HackPDM.Domain.Hack;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Domain.OdooModels;

public class ReflectionInfo
{
    public class FieldInfoEntry
    {
        public FieldInfo? FieldInfo;
        public string? Name;
        public ValueConversion? Conversion;

        public FieldInfoEntry(FieldInfo? fieldInfo, string? name, ValueConversion? conversion)
            => Init(fieldInfo, name, conversion);
        public FieldInfoEntry((FieldInfo? fieldInfo, string? name, ValueConversion? conversion) tup)
            => Init(tup.fieldInfo, tup.name, tup.conversion);

        public FieldInfoEntry() => Init(null, null, null);
        public FieldInfoEntry Assign(FieldInfo? fieldInfo, string? name, ValueConversion? conversion)
        {
            Init(fieldInfo, name, conversion);
            return this;
        }
        public void Init(FieldInfo? fieldInfo, string? name, ValueConversion? conversion)
        {
            this.FieldInfo = fieldInfo;
            this.Name = name;
            this.Conversion = conversion;
        }
    }
    public class PropInfoEntry
    {
        public PropertyInfo? PropInfo;
        public string? Name;
        public ValueConversion? Conversion;

        public PropInfoEntry(PropertyInfo? propInfo, string? name, ValueConversion? conversion) 
            => Init(propInfo, name, conversion);
        public PropInfoEntry((PropertyInfo? propInfo, string? name, ValueConversion? conversion) tup)
            => Init(tup.propInfo, tup.name, tup.conversion);

        public PropInfoEntry() => Init(null, null, null);
        public PropInfoEntry Assign(PropertyInfo? propInfo, string? name, ValueConversion? conversion)
        {
            Init(propInfo, name, conversion);
            return this;
        }
        public void Init(PropertyInfo? propertyInfo, string? name, ValueConversion? conversion)
        {
            this.PropInfo = propertyInfo;
            this.Name = name;
            this.Conversion = conversion;
        }
    }

    public struct HackFileResults
    {
        public List<IHackFileModel> CleanFiles { get; private set; } = new();
        public List<IHackFileModel> BrokenFiles { get; private set; } = new();

        public HackFileResults(List<IHackFileModel> cleanFiles, List<IHackFileModel> brokenFiles)
        {

        }
    }
}