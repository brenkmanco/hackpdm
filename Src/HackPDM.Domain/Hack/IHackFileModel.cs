using System;
using System.IO;

namespace HackPDM.Domain.Hack;

public interface IHackFileModel : IHackBaseFileModel
{
    // file settings
    public string? TypeExt
    {
        get;
        set;
    }
    public DateTime ModifiedDate
    {
        get;
        set;
    }
    protected DateTime OverwriteDate
    {
        get;
        set;
    }
    public long? FileSize
    {
        get => Info?.Exists is true ? Info.Length : null;
    }
    
    public string? Checksum { get; set; }
        
    // odoo settings
	public int? HpEntryId { get; set; }
	public bool? HasRemoteEntry { get; set; }
    public int? HpVersionId { get; set; }
    public bool? HasRemoteVersion { get; set; }
    public bool? Owner { get; set; }

    public bool? Exists
    {
        get
        {
            if (Info?.Exists is true) return true;
            return FullPath is not null && Path.Exists(FullPath);
        }
    }
}
