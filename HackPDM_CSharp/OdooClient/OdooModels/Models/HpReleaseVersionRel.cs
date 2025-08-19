//using static System.Net.Mime.MediaTypeNames;

namespace HackPDM
{
    public class HpReleaseVersionRel : HpBaseModel<HpReleaseVersionRel>
    {
        public int release_id;
        public int release_version;
        public int release_user;

        public HpReleaseVersionRel() { }
        public HpReleaseVersionRel(
            int release_id = 0,
            int release_version = 0,
            int release_user = 0)
        {
            this.release_id = release_id;
            this.release_version = release_version;
            this.release_user = release_user;
        }
    }
}
