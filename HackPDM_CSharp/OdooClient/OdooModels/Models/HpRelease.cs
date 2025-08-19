//using static System.Net.Mime.MediaTypeNames;

namespace HackPDM
{
    public class HpRelease : HpBaseModel<HpRelease>
    {
        public string release_note;
        public int release_user_id;

        public HpRelease() { }
        public HpRelease(
            string release_note,
            int release_user_id = 0)
        {
            this.release_note = release_note;
            this.release_user_id = release_user_id;
        }
    }
}
