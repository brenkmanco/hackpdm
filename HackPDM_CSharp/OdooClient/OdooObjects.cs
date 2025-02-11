using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using PropertyDataScaffold;
using System.ComponentModel.DataAnnotations;
using HackPDM;
using System.Runtime.Remoting.Messaging;
using CredentialManagement;
using OClient = OdooRpcCs.OdooClient;

namespace OdooObjects
{
    public class OdooProduct
    {
        private PropertyScaffold scaffold;
        private string productCode;
        private string engCode;
        private string engRev;
        private int productId;
        private int templateId;
        private ArrayList versionIds;
        private ArrayList prodFields = [];
        private ArrayList tmplFields = [];

        private Hashtable productRecord;
        public Hashtable ProductRecord
        {
            get
            {
                return productRecord;
            }
        }
        private Hashtable templateRecord;
        public Hashtable TemplateRecord
        {
            get
            {
                return templateRecord;
            }
        }
        public Dictionary<string, object> Record
        {
            get
            {
                return GetRecordDict();
            }
        }
        private DataTable productTable;
        public DataTable Table
        {
            get
            {
                return productTable;
            }
        }

        private DataTable pushTable;
        public DataTable PushTable
        {
            get
            {
                return pushTable;
            }
        }

        private DataTable ecoTable;
        public DataTable Ecos
        {
            get
            {
                return ecoTable;
            }
        }

        private string latestException = "";
        public string LatestException
        {
            get
            {
                return latestException;
            }
        }

        public bool HasProduct { get; set; }


        public OdooProduct(string productCode, PropertyScaffold scaffold)
        {
            this.scaffold = scaffold;
            this.productCode = productCode;
            (this.engCode, _, this.engRev) = ParseProductCode(productCode);
            foreach (DataRow dr in scaffold.FieldDefs.Select("odoo_model='product.product'"))
                prodFields.Add(dr["odoo_field"].ToString());
            foreach (DataRow dr in scaffold.FieldDefs.Select("odoo_model='product.template'"))
                tmplFields.Add(dr["odoo_field"].ToString());
            Refresh();
        }

        private (string code, string delimiter, string revision) ParseProductCode(string productCode)
        {
            if (productCode == null)
                return (code: null, delimiter: null, revision: null);
            Regex rx = new(@"([a-zA-Z_0-9\-]+)(\.)([-A-Z][0-9])");
            MatchCollection matches = rx.Matches(productCode);
            if (matches.Count == 0 || matches[0].Groups.Count != 4)
                return (code: null, delimiter: null, revision: null);
            return (
                code: matches[0].Groups[1].Value,
                delimiter: matches[0].Groups[2].Value,
                revision: matches[0].Groups[3].Value);
        }

        public void Refresh()
        {
            this.HasProduct = false;
            productTable = scaffold.OdooTable;
            DataRow dr = productTable.NewRow();
            productTable.Rows.Add(dr);

            if (this.engCode == null || productCode == null)
                return;

            // Get product versions
            ArrayList filter =
            [
                new ArrayList(3) { "eng_code", "=", this.engCode },
                "|",
                new ArrayList(3) { "active", "=", true },
                new ArrayList(3) { "active", "=", false },
            ];
            this.versionIds = OClient.Search("product.product", filter);

            // Build eco datatable
            GetEcos();

            // Get product.product data
            filter =
            [
                new ArrayList(3) { "default_code", "=", productCode },
                "|",
                new ArrayList(3) { "active", "=", true },
                new ArrayList(3) { "active", "=", false },
                new ArrayList() { this.prodFields },
            ];
            ArrayList records = OClient.Browse("product.product", filter);
            // ArrayList records = oClient.Browse("product.product", filter, this.prodFields);

            if (records == null || records.Count == 0)
            {
                latestException = OClient.LatestException;
                return;
            }
            this.productRecord = (Hashtable)records[0];
            this.productId = (int)this.productRecord["id"];

            // Get product.template data
            this.templateId = (int)((ArrayList)this.productRecord["product_tmpl_id"])[0];
            filter =
            [
                new ArrayList(3) { "id", "=", this.templateId },
                "|",
                new ArrayList(3) { "active", "=", true },
                new ArrayList(3) { "active", "=", false },
                new ArrayList() { this.tmplFields },
            ];
            records = OClient.Browse("product.template", filter);
            //records = oClient.Browse("product.template", filter, this.tmplFields);

            if (records == null || records.Count == 0)
            {
                latestException = OClient.LatestException;
                return;
            }
            this.templateRecord = (Hashtable)records[0];

            // Fill Odoo DataRow
            foreach (DataRow drField in scaffold.FieldDefs.Select("odoo_field is not null"))
            {
                string colName = drField["field"].ToString();
                string type = drField["dt_type"].ToString();
                string field = drField["odoo_field"].ToString();
                string model = drField["odoo_model"].ToString();
                string refModel = drField["odoo_ref_model"].ToString();
                string refField = drField["odoo_ref_field"].ToString();
                bool refInactives = (long)drField["ref_inactives"] != 0;
                object rawValue;
                if (model == "product.product")
                    rawValue = productRecord[field];
                else
                    rawValue = templateRecord[field];
                if (rawValue == null)
                    continue;

                if (refField != "" && rawValue.GetType() != Type.GetType("System.Boolean"))
                {
                    // this field references another, and we have a reference id
                    int refId = (int)((ArrayList)rawValue)[0];
                    dr[colName] = GetRefValue(refModel, refField, refId, refInactives);
                }
                else if (type != "System.Boolean" && rawValue.GetType() == Type.GetType("System.Boolean"))
                    // Odoo returned a null value for this field
                    dr[colName] = System.DBNull.Value;
                //else if (type == "System.Decimal" || type == "System.Int32" && rawValue.ToString() == "0")
                //    // This is a number field that is zero
                //    dr[colName] = System.DBNull.Value;
                else
                    dr[colName] = rawValue;
            }
            // Transform UOM
            scaffold.UomMapping.TryGetValue(dr["Uom"].ToString(), out string uom);
            dr["Uom"] = uom;

            this.HasProduct = true;

        }
        public void RawWrite(Dictionary<string, Object> values)
        {
            Hashtable prodValues = [];
            Hashtable tmplValues = [];
            foreach (KeyValuePair<string, object> field in values)
                if (prodFields.Contains(field.Key))
                    prodValues.Add(field.Key, field.Value);
                else if (tmplFields.Contains(field.Key))
                    tmplValues.Add(field.Key, field.Value);
            if (prodValues.Count > 0)
                OClient.Update("product.product", this.productId, prodValues);
            if (tmplValues.Count > 0)
                OClient.Update("product.template", this.templateId, tmplValues);
            Refresh();
        }
        public void WriteRowChanges(DataRow[] changes)
        {
            /// Calls to WriteRowChanges should be wrapped in a try-catch block
            DataRow drOld = changes[0];
            DataRow drNew = changes[1];

            // Write single-component/raw-material type BOM changes
            //
            // FIX
            //
            if (!drNew.IsNull("MaterialPn"))
            {
                if (drNew.IsNull("RouteTemplate") || (decimal)drNew["ChildQty"] == 0)
                    throw new Exception("If a raw material is specified, you must also specify quantity and routing");

                if (!drNew["MaterialPn"].Equals(drOld["MaterialPn"]) ||
                    !drNew["ChildQty"].Equals(drOld["ChildQty"]) ||
                    !drNew["RouteTemplate"].Equals(drOld["RouteTemplate"]))
                {
                    // Lookup raw material product
                    ArrayList rmIds = GetRefId("product.product", "default_code", drNew["MaterialPn"].ToString());
                    if (rmIds.Count == 0)
                        throw new Exception("Failed to get a matching raw material");

                    // Lookup routing template
                    ArrayList routeIds = GetRefId("mrp.routing", "name", drNew["RouteTemplate"].ToString());
                    if (routeIds.Count == 0)
                        throw new Exception("Failed to get a matching routing template");

                    // Execute the Create Bom wizard
                    Hashtable wizValues = new()
                    {
                        { "product_tmpl_id", templateId },
                        { "rm_product_id", rmIds[0] },
                        { "rm_qty", ((decimal)drNew["ChildQty"]).ToString("0.0") },
                        { "routing_id", routeIds[0] },
                    };
                    int wizId = OClient.Create("mfg.create.bom", wizValues);
                    OClient.Execute("mfg.create.bom", "button_create_bom", [wizId], 5000);
                }

            }
            
            Dictionary<string, Object> values = [];
            foreach (DataRow drField in scaffold.FieldDefs.Select("write_odoo=1"))
            {
                string colName = drField["field"].ToString();
                if (drNew[colName].Equals(drOld[colName]))
                    continue;

                string type = drField["dt_type"].ToString();
                string field = drField["odoo_field"].ToString();
                string model = drField["odoo_model"].ToString();
                string refModel = drField["odoo_ref_model"].ToString();
                string refField = drField["odoo_ref_field"].ToString();
                object rawValue = drNew[colName];

                if (rawValue.ToString() == "")
                    values.Add(field, false);
                else if (refField != "")
                {
                    // Lookup id in referenced model
                    ArrayList ids = GetRefId(refModel, refField, rawValue);
                    if (ids.Count == 0)
                        throw new Exception(String.Format("Failed to get a matching id for {0} in {1}.{2}", rawValue.ToString(), refModel, refField));
                    if (ids.Count > 1)
                        throw new Exception(String.Format("Found multiple matching records for {0} in {1}.{2}", rawValue.ToString(), refModel, refField));
                    values.Add(field, ids[0]);
                }
                else
                    if (type == "System.Decimal")
                        values.Add(field, ((decimal)rawValue).ToString("0.00"));
                    else
                        values.Add(field, rawValue);
            }
            if (values.Count > 0)
                RawWrite(values);
        }
        public object GetRefValue(string refModel, string refField, int refId, bool getInactive = false)
        {
            ArrayList filter = [new ArrayList(3) { "id", "=", refId }];
            if (getInactive)
            {
                filter.Add("|");
                filter.Add(new ArrayList(3) { "active", "=", true });
                filter.Add(new ArrayList(3) { "active", "=", false });
            }

            filter.Add(new ArrayList() { refField });
            ArrayList records = OClient.Browse(refModel, filter);
            //ArrayList records = oClient.Browse(refModel, filter, new ArrayList { refField });

            if (records.Count == 0)
                return DBNull.Value;
            else
                return ((Hashtable)records[0])[refField];
        }
        public ArrayList GetRefId(string refModel, string refField, object value)
        {
            ArrayList filter = [
                        new ArrayList(3) { refField, "=", value },
                    ];
            ArrayList ids = OClient.Search(refModel, filter);
            return ids;
        }
        private Dictionary<string, object> GetRecordDict()
        {
            Dictionary<string, object> dict = [];
            foreach (DataColumn dc in productTable.Columns)
            {
                string newKey = prodFields.Contains(dc.ColumnName) ?
                    "product.product." + dc.ColumnName :
                    "product.template." + dc.ColumnName;
                dict.Add(newKey, productTable.Rows[0][dc.ColumnName]);
            }
            return dict;
        }
        private DataTable GetEcoDataTable()
        {
            DataTable dt = new("ecos");
            dt.Columns.Add("Eco");
            dt.Columns.Add("Rev");
            dt.Columns.Add("Zone");
            dt.Columns.Add("Description");
            dt.Columns.Add("Date");
            dt.Columns.Add("Owner");
            return dt;
        }
        private DataTable GetEcos()
        {
            ecoTable = GetEcoDataTable();
            if (this.versionIds.Count == 0)
                return ecoTable;

            ArrayList filter = [new ArrayList(3) { "product_id", "in", this.versionIds }];
            ArrayList ids = OClient.Search("ecm.eco.rev.line", filter);

            ArrayList fields = ["eco_id", "new_rev", "zone", "description", "target_date", "owner_id"];
            ArrayList records = OClient.Read("ecm.eco.rev.line", ids, fields);

            foreach (Hashtable rec in records)
            {
                DataRow row = ecoTable.NewRow();
                row["Eco"] = (string)((ArrayList)rec["eco_id"])[1];
                row["Rev"] = (string)rec["new_rev"];
                row["Zone"] = rec["zone"].GetType() == Type.GetType("System.Boolean") ? "" : (string)rec["zone"];
                row["Description"] = rec["description"].GetType() == Type.GetType("System.bool") ? "" : (string)rec["description"];
                row["Date"] = (string)rec["target_date"];
                row["Owner"] = (string)((ArrayList)rec["owner_id"])[1];
                ecoTable.Rows.Add(row);
            }
            ecoTable.DefaultView.Sort = "Eco ASC";

            return ecoTable;
        }

    }

    public class OdooTools
    {
        private const string ROUTING_MODEL = "mrp.routing";
        private const string ENGINEER_PART_TYPE_MODEL = "engineering.part.type";
        private const string ENGINEER_COATING_MODEL = "engineering.coating";
        private const string ENGINEER_PREP_MODEL = "engineering.preparation";
        private const string PRODUCT_TEMPLATE_MODEL = "product.template";

        //public List<string> GetUomCodes()
        //{
        //    ArrayList filter = new ArrayList(1);
        //    filter.Add(new ArrayList(3) { "code", "!=", false });
        //    ArrayList ids = OClient.Search("product.uom", filter);
        //    ArrayList records = OClient.Read("product.uom", ids, new ArrayList(1) { "code" });

        //    List<string> uoms = new List<string>();
        //    foreach (Hashtable record in records)
        //    {
        //        if (!uoms.Contains((string)record["code"]))
        //            uoms.Add((string)record["code"]);
        //    }
        //    return uoms;
        //}



        public void TestMethods()
        {
            //HpEntry entryModel = new HpEntry(OClient, "xmlrpc_entry", dir_id:12);
            //entryModel.Create();
            //while(true)
            //{ 
            //    HpDirectory directoryModel = new HpDirectory(OClient, "test", "test directory");
            //    Hashtable list1 = directoryModel.HpSubdirectories();
            //    DirectoryDict HDR = list1;
            //    HackDefaults.CreateDirectories(HDR);
            //}

            //OdooDefaults.CreateRecord(new HpDirectory(OClient, "xmlrpc"));
        }
        public List<string> GetAllModels()
        {

            ArrayList filter = ["model"];
            ArrayList records = OClient.Browse(OdooDefaults.IR_MODEL, [new ArrayList(), filter]);

            List<string> models = [];
            foreach (Hashtable record in records)
            {
                models.Add((string)record["model"]);
            }
            models.Sort();
            return models;
        }
        public ArrayList GetModelInstances(string modelName, ArrayList filter = null)
        {
            if (filter == null) filter = ParameterHelper.Empty;
            ArrayList records = OClient.Browse(modelName, filter);
            return records;
        }
        public Hashtable GetAllFieldsForModel(string modelName)
        {
            ArrayList emptyFilter = [];
            Hashtable fields = OClient.GetFields(modelName, emptyFilter);
            return fields;
        } 
        // [("res_model", "=", "hp.version"), ("res_id", "=", 1)]
        public List<string> GetEntryModel()
        {
            ArrayList filter = [new ArrayList(3) { "name", "like", "_template" }];
            ArrayList ids = OClient.Search(OdooDefaults.HP_ENTRY, filter);
            ArrayList records = OClient.Read(OdooDefaults.HP_ENTRY, ids, ["name"]);

            List<string> entries = [];
            foreach (Hashtable record in records)
            {
                if (!entries.Contains((string)record["name"]))
                    entries.Add((string)record["name"]);
            }
            entries.Sort();
            return entries;
        }
        public List<string> GetRouteTemplates()
        {
            ArrayList filter = [new ArrayList(3) { "name", "like", "_template" }];
            ArrayList ids = OClient.Search(ROUTING_MODEL, filter);
            ArrayList records = OClient.Read(ROUTING_MODEL, ids, ["name"]);

            List<string> routes = [];
            foreach (Hashtable record in records)
            {
                if (!routes.Contains((string)record["name"]))
                    routes.Add((string)record["name"]);
            }
            routes.Sort();
            return routes;
        }
        public Dictionary<string, string> GetPartTypes()
        {
            Dictionary<string, string> types = [];

            ArrayList ids = OClient.Search(ENGINEER_PART_TYPE_MODEL, new ArrayList(1));
            ArrayList records = OClient.Read(ENGINEER_PART_TYPE_MODEL, ids, ["code", "name"]);
            if (records == null)
                return types;

            foreach (Hashtable record in records)
            {
                string code = (string)record["code"];
                string name = (string)record["code"] + " - " + (string)record["name"];
                types.Add(code, name);
            }
            return types;
        }
        public List<string> GetCoatings()
        {
            List<string> items = [];

            ArrayList ids = OClient.Search(ENGINEER_COATING_MODEL, new ArrayList(1));
            ArrayList records = OClient.Read(ENGINEER_COATING_MODEL, ids, new ArrayList(2) { "name" });
            if (records == null)
                return items;

            foreach (Hashtable record in records)
            {
                string name = (string)record["name"];
                items.Add(name);
            }
            return items;
        }
        public List<string> GetPreparations()
        {
            List<string> items = [];

            ArrayList ids = OClient.Search(ENGINEER_PREP_MODEL, new ArrayList(1));
            ArrayList records = OClient.Read(ENGINEER_PREP_MODEL, ids, new ArrayList(2) { "name" });
            if (records == null)
                return items;

            foreach (Hashtable record in records)
            {
                string name = (string)record["name"];
                items.Add(name);
            }
            return items;
        }
        public DataTable GetRawMaterials()
        {
            DataTable dtRawMaterial = new("raw_material");
            dtRawMaterial.Columns.Add("id");
            dtRawMaterial.Columns.Add("default_code");
            dtRawMaterial.Columns.Add("name");
            dtRawMaterial.Columns.Add("uom_name");
            dtRawMaterial.Columns.Add("categ_name");

            ArrayList filter = [new ArrayList(3) { "is_continuous", "=", true }, new ArrayList(3) { "eng_management", "=", true }];
            ArrayList ids = OClient.Search("product.template", filter);

            ArrayList fields = ["id", "default_code", "name", "uom_id", "eng_categ_id"];
            ArrayList records = OClient.Read("product.template", ids, fields);

            foreach (Hashtable rec in records)
            {
                DataRow row = dtRawMaterial.NewRow();
                row["id"] = (int)rec["id"];
                row["default_code"] = (string)rec["default_code"];
                row["name"] = (string)rec["name"];
                row["uom_name"] = (string)((ArrayList)rec["uom_id"])[1];
                string categName = (string)((ArrayList)rec["eng_categ_id"])[1];
                categName = categName.Substring(categName.LastIndexOf(" / ") + 3);
                row["categ_name"] = categName;
                dtRawMaterial.Rows.Add(row);
            }

            return dtRawMaterial;
        }

        private void DebugCreateModels()
        {
        
        }

        public static void DownloadFiles(IrAttachment[] attachments)
        {
            foreach (IrAttachment attachment in attachments)
            {
                string fileContents = attachment.DownloadContents();

            }
        }
    }

}
