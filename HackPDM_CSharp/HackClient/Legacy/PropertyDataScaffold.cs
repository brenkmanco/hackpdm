using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Drawing;

using System.Data;
using System.Data.Common;
using System.Data.SQLite;

namespace PropertyDataScaffold
{
    public class PropertyScaffold
    {

        #region Custom Property Data Structure

        // SolidWorks custom property types:
        // swCustomInfoType_e.swCustomInfoDate = 64
        // swCustomInfoType_e.swCustomInfoDouble = 5
        // swCustomInfoType_e.swCustomInfoNumber = 3
        // swCustomInfoType_e.swCustomInfoText = 30
        // swCustomInfoType_e.swCustomInfoUnknown = 0
        // swCustomInfoType_e.swCustomInfoYesOrNo = 11

        // ******************************************************************************
        // ***                                                                        ***
        // ***        The custom property for 'Units of Measure' was originally       ***
        // ***        named 'Alternate Quantity'.  When we added a property for       ***
        // ***        tracking alternate quantity values (double precision            ***
        // ***        numbers), we didn't want to go back and change the custom       ***
        // ***        properties in every part file.                                  ***
        // ***        As a work around:                                               ***
        // ***            -we labeled the 'Alternate Quantity' text field 'UOM'       ***
        // ***            -we are writing the units of measure to the                 ***
        // ***             'Alternate Quantity' custom property                       ***
        // ***            -we labeled the 'UOM' text field 'Alternate Quantity'       ***
        // ***            -we are writing the alternate quantity value to the         ***
        // ***             'UOM' custom property                                      ***
        // ***                                                                        ***
        // ******************************************************************************

        /*
         * 
         * 
         * 

        Data Structure Considerations
        =============================

        By keeping a copy of Odoo data in a separate DataTable, we have access to all 3 versions of each field's data:
            1. Current value
            2. Original SW value
            3. Original Odoo value
        This allows the user view and/or revert to either one of the original values.

        Main DataTable
        --------------

        The main DataTable includes all fields, whether sourced from Odoo or
        SolidWorks. This includes several Odoo field that will not be populated
        during a bom scan. Some of the fields get updated from
        odoo data.

        We only bind fields to controls when the field will be used to update
        custom properties in SolidWorks.

        Not all fields are displayed in the custom properties form.  Some of
        the Odoo fields are used for updating Odoo.  Some of the SolidWorks
        fields are used in bom scanning.

        Odoo DataTable
        --------------

        These are fields sourced only from odoo.  Some of them can be pushed
        back to Odoo.  Others are used to find the correct product records in
        Odoo.

        Some of the Odoo DataTable fields overlap with the main DataTable.  In
        these cases, field values may be overwritten in a one-way or both-ways
        fashion.  For example, when the Odoo data loads, the part description
        from SolidWorks is overwritter with the Odoo description.  If the 
        description is subsequently changed by the user, then both the
        SolidWorks and Odoo descriptions are overwritten with the user's entry.

        We must apply changes in the main DataTable to the Odoo DataTable
        manually.  This can be managed by a DataColumnChangeEvent handler
        method.

        Writing to Odoo and Solidworks in a single operation
        ----------------------------------------------------

        By applying changes to Odoo and SolidWorks with a single user command, we are able to accept or reject all changes to the DataRow with less code.

        Allowing the user to write data to Odoo and SolidWorks separately
        -----------------------------------------------------------------
            
        Let's suppose the user wants to commit changes to SolidWorks, but not
        commit the changes to Odoo.
        Question:
            What should we do with the changes that don't get committed to Odoo?
        Answer:
             1. Odoo's un-committed changes should already be stored in the Odoo DataTable
             2. Set the Odoo fields in the main DataTable to their original values
             3. Commit the changes to SolidWorks
             4. Accept changes to the DataRow
             5. Set the Odoo fields in the main DataTable back to the changed
                values, pulling from the Odoo DataTable

        *
        *
        *
        */

        // Field definition details loaded from DB file
        DataTable dtFieldDefs;
        public DataTable FieldDefs
        {
            get
            {
                return dtFieldDefs;
            }
        }

        // Table of all data fields, both Odoo and SolidWorks
        DataTable dtMain = new();
        public DataTable MainTable
        {
            get
            {
                return dtMain.Clone();
            }
        }

        // Get an empty row for the main table
        public DataRow MainRow
        {
            get
            {
                return dtMain.Rows[0];
            }
        }

        // Table of Odoo fields
        DataTable dtOdoo = new();
        public DataTable OdooTable
        {
            get
            {
                return dtOdoo.Clone();
            }
        }

        // SolidWorks custom property names
        private readonly List<string> swPropNames = [];
        public List<string> SwPropNames
        {
            get
            {
                return swPropNames;
            }
        }

        // DataTable field names for SolidWorks custom properties
        private readonly List<string> swPropFields = [];
        public List<string> SwPropFields
        {
            get
            {
                return swPropFields;
            }
        }

        // SQLite column definitions for bom scan table
        private readonly List<string> scanColumns = [];
        public List<string> ScanColumns
        {
            get
            {
                return scanColumns;
            }
        }

        // Field names to be populated for each component in a bom scan
        private readonly List<string> scanFields = [];
        public List<string> ScanFields
        {
            get
            {
                return scanFields;
            }
        }

        // SolidWords custom property data type property name
        private readonly Dictionary<string, int> propTypes = [];
        public Dictionary<string, int> PropTypes
        {
            get
            {
                return propTypes;
            }
        }

        // Main table fields that are not custom properties
        private readonly List<string> otherFields = [];
        public List<string> OtherFields
        {
            get
            {
                return otherFields;
            }
        }

        // Lookup field names using custom property names as key
        private Dictionary<string, string> dictPropField = [];
        public Dictionary<string, string> PropFieldMap
        {
            get
            {
                return dictPropField;
            }
        }

        // Lookup custom property names using field names as key
        private Dictionary<string, string> dictFieldProp = [];
        public Dictionary<string, string> FieldPropMap
        {
            get
            {
                return dictFieldProp;
            }
        }

        // Lookup main table field names using odoo field names as key
        // { odoo_field, field } e.g. { "uom_id", "Uom" }
        private Dictionary<string, string> dictOdooField = [];
        public Dictionary<string, string> OdooFieldMap
        {
            get
            {
                return dictOdooField;
            }
        }

        // Lookup odoo field names using main table field names as key
        // { field, odoo_field } e.g. { "Uom", "uom_id" }
        private Dictionary<string, string> dictFieldOdoo = [];
        public Dictionary<string, string> FieldOdooMap
        {
            get
            {
                return dictFieldOdoo;
            }
        }

        // Odoo fields that take precedence over SolidWorks fields
        private Dictionary<string, string> dictSwOverwriteFields = [];
        public Dictionary<string, string> SwOverwriteFields
        {
            get
            {
                return dictSwOverwriteFields;
            }
        }

        // SolidWorks fields that take precedence over Odoo fields
        private List<string> odooOverwriteFields = [];
        public List<string> OdooOverwriteFields
        {
            get
            {
                return odooOverwriteFields;
            }
        }

        // Standard custom property unit of measure codes
        private List<string> uoms = [];
        public List<string> Uoms
        {
            get
            {
                return uoms;
            }
        }

        // Dirty/old/inconsistent custom property unit of measure codes, mapped to standard ones
        // Includes mapping from odoo uom names to standard custom property codes
        Dictionary<string, string> uomMapping = [];
        public Dictionary<string, string> UomMapping
        {
            get
            {
                return uomMapping;
            }
        }

        // Dirty/old/inconsistent custom property coating codes, mapped to standard ones
        // Includes mapping from odoo coating names to standard custom property codes
        Dictionary<string, string> coatingMapping = [];
        public Dictionary<string, string> CoatingMapping
        {
            get
            {
                return coatingMapping;
            }
        }

        // Dirty/old/inconsistent custom property finish codes, mapped to standard ones
        // Includes mapping from odoo finish names to standard custom property codes
        Dictionary<string, string> finishMapping = [];
        public Dictionary<string, string> FinishMapping
        {
            get
            {
                return finishMapping;
            }
        }

        // Odoo boolean fields require special handling to distinguish between null and false
        private List<string> odooBools = [];
        public List<string> OdooBools
        {
            get
            {
                return odooBools;
            }
        }

        #endregion

        public PropertyScaffold()
        {
            // Get fields from db file
            DbConnection cnn = new SQLiteConnection("Data Source=|DataDirectory|\\property_field_mapping.db3");
            cnn.Open();
            DbCommand comm = cnn.CreateCommand();
            comm.CommandText = "select * from fields order by sequence";
            dtFieldDefs = new DataTable();
            using (DbDataReader dr = comm.ExecuteReader())
            {
                dtFieldDefs.Load(dr);
            }

            // Build DataTables
            BuildMainTable();
            BuildOdooTable();
            dtMain.Rows.Add(dtMain.NewRow());
            dtOdoo.Rows.Add(dtOdoo.NewRow());

            // Populate field lists and mappings
            foreach (DataRow dr in dtFieldDefs.Select("", "sequence asc"))
            {
                if (!dr.IsNull("sw_prop"))
                {
                    // build name lists
                    swPropNames.Add((string)dr["sw_prop"]);
                    swPropFields.Add((string)dr["field"]);
                    // Map SolidWorks custom property names to SolidWorks custom property types
                    propTypes[(string)dr["sw_prop"]] = (int)(long)dr["sw_type"];
                    // Map SolidWorks custom property names to DB field names
                    dictPropField[(string)dr["sw_prop"]] = (string)dr["field"];
                    // Map DB field names to SolidWorks custom property names
                    dictFieldProp[(string)dr["field"]] = (string)dr["sw_prop"];
                }
                else
                    otherFields.Add((string)dr["field"]);

                if (!dr.IsNull("odoo_field"))
                {
                    dictOdooField.Add((string)dr["odoo_field"], (string)dr["field"]);
                    dictFieldOdoo.Add((string)dr["field"], (string)dr["odoo_field"]);
                    if ((long)dr["pull_odoo"] == 1 && !dr.IsNull("sw_prop"))
                        dictSwOverwriteFields.Add((string)dr["odoo_field"], (string)dr["field"]);
                    if ((long)dr["push_odoo"] == 1 && (long)dr["pull_odoo"] == 0)
                        odooOverwriteFields.Add((string)dr["field"]);
                }

                if ((long)dr["bom_scan"] == 1)
                {
                    scanColumns.Add(String.Format("{0} {1}", dr["field"], dr["sqlite_type"]));
                    scanFields.Add((string)dr["field"]);
                }
            }

            // Get unit of measure mapping from db file
            comm.CommandText = "select * from uoms";
            DataTable dtUoms = new();
            using (DbDataReader dr = comm.ExecuteReader())
                dtUoms.Load(dr);
            foreach (DataRow dr in dtUoms.Rows)
            {
                uomMapping.Add(dr[0].ToString(), dr[1].ToString());
                if (!uoms.Contains(dr[1].ToString()))
                    uoms.Add(dr[1].ToString());
            }

            // Get coating mapping from db file
            comm.CommandText = "select * from coatings";
            DataTable dtCoatings = new();
            using (DbDataReader dr = comm.ExecuteReader())
                dtCoatings.Load(dr);
            foreach (DataRow dr in dtCoatings.Rows)
            {
                coatingMapping.Add(dr[0].ToString(), dr[1].ToString());
            }

            // Get finishes mapping from db file
            comm.CommandText = "select * from finishes";
            DataTable dtFinishes = new();
            using (DbDataReader dr = comm.ExecuteReader())
                dtFinishes.Load(dr);
            foreach (DataRow dr in dtFinishes.Rows)
            {
                finishMapping.Add(dr[0].ToString(), dr[1].ToString());
            }
        }

        private void BuildMainTable()
        {
            foreach (DataRow dr in dtFieldDefs.Select("", "sequence asc"))
            {
                string fieldName = dr["field"].ToString();
                string typeName = dr["dt_type"].ToString();

                DataColumn col = new(fieldName)
                {
                    DataType = System.Type.GetType(typeName)
                };
                if (typeName == "System.Boolean")
                    col.DefaultValue = false;
                if (typeName == "System.Decimal")
                    col.DefaultValue = 0.0;
                if (typeName == "System.Int32")
                    col.DefaultValue = 0;
                dtMain.Columns.Add(col);
            }
        }

        private void BuildOdooTable()
        {
            foreach (DataRow dr in dtFieldDefs.Select("odoo_field is not null", "sequence asc"))
            {
                string fieldName = dr["field"].ToString();
                string typeName = dr["dt_type"].ToString();

                DataColumn col = new(fieldName)
                {
                    DataType = System.Type.GetType(typeName)
                };
                if (typeName == "System.Boolean")
                    col.DefaultValue = false;
                if (typeName == "System.Decimal")
                    col.DefaultValue = 0.0;
                if (typeName == "System.Int32")
                    col.DefaultValue = 0;
                dtOdoo.Columns.Add(col);
            }
        }

        public static string ProductCode(DataTable dt, string thisConfigName)
        {
            /// Concatenates part number and revsion with a delimeter

            // Get and merge the current configuration and the file system level custom properties
            DataRow[] drs = dt.Select("configname='" + thisConfigName + "'", "configname desc");
            if (drs.Length < 1)
                return null;
            DataRow dr = drs[0];
            string partNum = "";
            string revision = "";
            if (!dr.IsNull("PartNum") && (string)dr["PartNum"] != "")
            {
                partNum = (string)dr["PartNum"];
                if (!dr.IsNull("Revision") && (string)dr["Revision"] != "")
                    revision = (string)dr["Revision"];
                else
                    revision = "-0";
            }
            if (partNum == "")
                return null;
            if (revision == "")
                revision = "-0";
            return partNum + "." + revision;
        }

        public static string EffectiveProductCode(DataTable dt, string thisConfigName)
        {
            /// This could be useful when scanning boms
            /// When a component is loaded, we select the configuration used in the assembly
            /// The part number is usually on the "" configuration
            /// This method finds the effective part number for a given SolidWorks configuration

            // Get and merge the current configuration and the file system level custom properties
            DataRow[] drs = dt.Select("configname='" + thisConfigName + "' or configname=''", "configname desc");
            string partNum = "";
            string revision = "";
            foreach (DataRow dr in drs)
            {
                if (!dr.IsNull("PartNum") && (string)dr["PartNum"] != "")
                {
                    partNum = (string)dr["PartNum"];
                    if (!dr.IsNull("Revision") && (string)dr["Revision"] != "")
                        revision = (string)dr["Revision"];
                    else
                        revision = "-0";
                    break;
                }
            }
            if (partNum == "")
                return null;
            if (revision == "")
                revision = "-0";
            return partNum + "." + revision;
        }

        //public object ConvertType(string propName, string propValue)
        //{
        //    object newObject;
        //    newObject = Convert.ChangeType(propValue, )
        //}

        public static byte[] ImageToByteArray(Bitmap bitmapIn)
        {
            Image imageIn = (Image)bitmapIn;
            if (imageIn == null) return null;
            using (MemoryStream ms = new())
            {
                try
                {
                    imageIn.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    return ms.ToArray();
                }
                catch
                {
                    return null;
                }
            }
        }

        public static Image ByteArrayToImage(byte[] byteArrayIn)
        {
            if (byteArrayIn == null) return null;
            using (MemoryStream ms = new(byteArrayIn))
            {
                Image returnImage = Image.FromStream(ms);
                return returnImage;
            }
        }

    }
}
