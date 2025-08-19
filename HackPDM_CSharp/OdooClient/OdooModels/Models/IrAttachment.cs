using System.Collections;


//using static System.Net.Mime.MediaTypeNames;


using OClient = OdooRpcCs.OdooClient;

namespace HackPDM
{
    public class IrAttachment : HpBaseModel<IrAttachment>
    {
        public string name;
        public int res_id;
        public int file_size;
        public string res_model;
        public string checksum;
        public string mimetype;
        public string type;

        private string fileContentsBase64;
        public IrAttachment() { }
        public IrAttachment(
            string name,
            int res_id = 0,
            int file_size = 0,
            string res_model = null,
            string checksum = null,
            string mimetype = null,
            string type = "binary",
            string fileContentsBase64 = null)
        {
            this.name = name;
            this.res_id = res_id;
            this.file_size = file_size;
            this.res_model = res_model;
            this.checksum = checksum;
            this.mimetype = mimetype;
            this.type = type;
            this.fileContentsBase64 = fileContentsBase64;
        }

		public string DownloadContents()
        {
            const string datas = "datas";
            if (this.IsRecord || this.ID != 0)
            {
                // reads the datas field in ir.attachment and returns an ArrayList with one record because of one ID
                // which contains a hashtable with keys: datas and id. datas has a value of string which is the base 64 file contents
                this.fileContentsBase64 = (string)((Hashtable)OClient.Read(HpModel, [this.ID], [datas])[0])[datas];
                return this.fileContentsBase64;
            }
            return null;
        }
        
        public string GetFileContentsB64() => fileContentsBase64;
    }
}
