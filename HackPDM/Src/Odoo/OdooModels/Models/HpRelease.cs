//using static System.Net.Mime.MediaTypeNames;

using System;
// ReSharper disable InconsistentNaming

namespace HackPDM.Odoo.OdooModels.Models;

public partial class HpRelease : HpBaseModel<HpRelease>
{
	public int version_id;
    public int release_user_id;
    public DateTime? release_stamp;
    public string release_note;

    public HpRelease() { }
    public HpRelease(
        string releaseNote,
        int releaseUserId = 0,
        DateTime releaseStamp = default)
    {
        this.release_note = releaseNote;
        this.release_user_id = releaseUserId;
        this.release_stamp = releaseStamp;
    }
}
public partial class HpRelease : HpBaseModel<HpRelease>
{}