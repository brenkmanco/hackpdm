using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;

using HackPDM.Extensions.General;


//using static System.Net.Mime.MediaTypeNames;


using OClient = OdooRpcCs.OdooClient;

namespace HackPDM
{
    public class HpEntry : HpBaseModel<HpEntry>
    {
        public string name;
        public string checkout_date;
        public bool deleted;
        public int latest_version_id;
        public int dir_id;
        public int type_id;
        public int cat_id;
        public int? checkout_user;
        public int? checkout_node;
        internal bool IsLatest { get; set; } = false;

        public HpEntry() {  }
        public HpEntry(
            string name,
            string checkout_date = null,
            bool active = true,
            int latest_version_id = 0,
            int dir_id = 0,
            int type_id = 0,
            int cat_id = 0,
            int checkout_user = 0,
            int checkout_node = 0)
        {
            this.name = name;
            this.deleted = !active;
            this.latest_version_id = latest_version_id;
            this.dir_id = dir_id;
            this.type_id = type_id;
            this.cat_id = cat_id;
            this.checkout_node = checkout_node;

            if (checkout_user == 0) this.checkout_user = OdooDefaults.OdooID;
            else this.checkout_user = checkout_user;
            if (checkout_date == null) this.checkout_date = OdooDefaults.OdooDateFormat(DateTime.Now);
            else this.checkout_date = checkout_date;
        }
        public static ArrayList GetLatestIDs(ArrayList ids)
        {
            const string latest = "latest_version_id";
           
            ArrayList list = OClient.Read(GetHpModel(), ids, [latest], 10000);

            return list;
        }
        public ArrayList GetLatestIDs()
        {
            return GetLatestIDs([this.ID]);
        }
        public bool CanCheckOut() => (checkout_user is null or 0) && !deleted;
        public bool CanUnCheckOut() => (checkout_user is not null) && checkout_user == OdooDefaults.OdooID;
        
        public async Task CheckOut()
        {
            if (!CanCheckOut()) return;
            checkout_user = OdooDefaults.OdooID;
			checkout_date = OdooDefaults.OdooDateFormat( DateTime.Now );
            checkout_node = OdooDefaults.MyNode.ID;

            await WriteChangedValuesAsync("checkout_user", "checkout_date", "checkout_node");
            HpVersion version = new(node_id: checkout_node);
            version.ID = latest_version_id;
            await version.WriteChangedValuesAsync("node_id");
            if (HashedValues.TryGetValue("windows_complete_name", out object objpath) && objpath is string winpath)
            {
                string absPath = Path.Combine(HackDefaults.PWAPathAbsolute, winpath[5..]);
                FileInfo file = new(absPath);
                if (file.Exists)
                {
                    file.Attributes &= ~FileAttributes.ReadOnly;
                }
            }

		}
        public async Task UnCheckOut()
        {
            if (!CanUnCheckOut()) return;
			checkout_user = null;
			checkout_date = null;
			checkout_node = null;

			await WriteChangedValuesAsync( "checkout_user", "checkout_date", "checkout_node" );
            if (HashedValues.TryGetValue("windows_complete_name", out object objpath) && objpath is string winpath)
            {
                string absPath = Path.Combine(HackDefaults.PWAPathAbsolute, winpath[5..]);
                FileInfo file = new(absPath);
                if (file.Exists)
                {
                    file.Attributes |= FileAttributes.ReadOnly;
                }
            }
        }
        internal static async Task<HpEntry> CreateNew( HackFile hackFile, int dir_id )
        {
			if (OdooDefaults.RestrictTypes & !OdooDefaults.ExtToType.TryGetValue( hackFile.TypeExt.ToLower(), out HpType type ) )
				return null;

			HpEntry newEntry = new()
			{
				name = hackFile.Name,
				deleted = false,
				dir_id = dir_id,
			};
            if (type is not null)
            {
                newEntry.cat_id = type.cat_id;
                newEntry.type_id = type.ID;
            }
            await newEntry.CreateAsync( false );

            return newEntry.ID == 0 ? null : newEntry;
        }
        internal static async Task<ArrayList> GetEntryList(int[] entry_ids, bool update = false)
        {
            ArrayList arr = await OClient.CommandAsync<ArrayList>(HpVersion.GetHpModel(), "get_recursive_dependency_entries", [entry_ids.ToArrayList()], 1000000);
            return arr;
        }
        internal async Task LogicalDelete() 
        {
            deleted = true;
            await WriteChangedValuesAsync( "deleted" );
		}
		internal async Task LogicalUnDelete() 
        {
            deleted = false;
            await WriteChangedValuesAsync( "deleted" );
        }
		public override string ToString()
		{
			return name;
		}

	}
}
