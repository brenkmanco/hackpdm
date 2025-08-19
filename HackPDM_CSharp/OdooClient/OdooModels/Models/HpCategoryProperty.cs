//using static System.Net.Mime.MediaTypeNames;

namespace HackPDM
{
    public class HpCategoryProperty : HpBaseModel<HpCategoryProperty>
    {
        public int cat_id;
        public int prop_id;

        public HpCategoryProperty() { }
        public HpCategoryProperty(
            int cat_id = 0,
            int prop_id = 0)
        {
            this.cat_id = cat_id;
            this.prop_id = prop_id;
        }
    }
}
