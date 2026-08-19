using System.Collections;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Abstractions;

public struct DirectoryDict 
{
    public DirectoryDict[] Directories;
    public string Name;
    public HpEntryReturn[] Entries;
    public int Id;

    // public DirectoryDict ConvertFromHt(Hashtable ht) => ht;
    // public static implicit operator DirectoryDict(Hashtable ht)
    // {
    //     DirectoryDict[] directories =
    //         HackDefaults.ArrayListToModelsArray<DirectoryDict>((ArrayList)ht["directories"]);
    //     HpEntryReturn[] entries =
    //         HackDefaults.ArrayListToModelsArray<HpEntryReturn>((ArrayList)ht["entries"]);
    //
    //     return new DirectoryDict
    //     {
    //         Directories = [.. directories],
    //         Entries = [.. entries],
    //         Id = (int)ht["id"],
    //         Name = (string)ht["name"],
    //     };
    // }
}
public struct HpEntryReturn : IConvert<HpEntryReturn>
{
    public string Name;
    public int Id;

    public HpEntryReturn ConvertFromHt(Hashtable ht) => ht;

    public static implicit operator HpEntryReturn(Hashtable ht)
    {
        return new HpEntryReturn
        {
            Id = (int)ht["id"],
            Name = (string)ht["name"],
        };
    }
}

public struct ContentDialogInfo
{
    public ContentDialogInfo() { }

    public string Message { get; set; } = null;
    public string? Caption { get; set; } = null;
    public string? PrimaryText { get; set; } = null;
    public string? SecondaryText { get; set; } = null;
    public string? CloseText { get; set; } = null;
    public MessageBoxRepresentation ButtonRepresentation { get; set; } =  MessageBoxRepresentation.Primary;
}