//using static System.Net.Mime.MediaTypeNames;

using System;
// ReSharper disable InconsistentNaming

namespace HackPDM.Odoo.OdooModels.Models;

[OdooModel(OdooDefaults.HP_RELEASE_NAME, OdooDefaults.HP_RELEASE)]
public partial class HpRelease : HpBaseModel<HpRelease>
{
	[OdooField(OdooFieldType.Many2one)] public int version_id;
	[OdooField(OdooFieldType.Many2one)] public int release_user_id;
	[OdooField(OdooFieldType.DateTime)] public DateTime? release_stamp;
	[OdooField(OdooFieldType.Char)] public string release_note;

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