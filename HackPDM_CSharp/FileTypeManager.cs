/*
 * 
 * (C) 2013 Matt Taylor
 * Date: 2/18/2013
 * 
 * This file is part of HackPDM.
 * 
 * HackPDM is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * HackPDM is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with HackPDM.  If not, see <http://www.gnu.org/licenses/>.
 * 
 */


using System;
using System.Drawing;
using System.Windows.Forms;

using System.IO;
using System.Data;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;

namespace HackPDM
{
    /// <summary>
    /// Manipulate file types
    /// </summary>
    public partial class FileTypeManager : Form
    {


        #region declarations

        private NpgsqlConnection connDb;

        private NpgsqlDataAdapter daFilters;
        private DataSet dsFilters;
        private DataTable dtFiles;
        private Regex reAllFilters;
        BindingSource bsFilters = new BindingSource();

        private DataTable dtLocTypes;
        private DataSet dsRemTypes = new DataSet();
        private DataTable dtRemTypes;
        private Regex reAllTypes;
        BindingSource bsTypes = new BindingSource();

        private string strFilePath;

        private ListViewColumnSorter lvwRemColumnSorter;
        private ListViewColumnSorter lvwLocColumnSorter;


        /// <summary>
        /// Return File Ignore Filter DataTable
        /// </summary>
        public DataTable IgnoreFilters
        {
            get { return dsFilters.Tables[0]; }
        }

        /// <summary>
        /// Return Regex matching all ignore filters
        /// </summary>
        public Regex ReFilters
        {
            get { return reAllFilters; }
        }

        /// <summary>
        /// Return File Types DataTable
        /// - type_id
        /// - file_ext
        /// - default_cat
        /// - cat_name
        /// - icon
        /// - type_regex
        /// - description
        /// </summary>
        public DataTable RemoteFileTypes
        {
            get { return dtRemTypes; }
        }

        /// <summary>
        /// Return Regex Matching all remote file types
        /// </summary>
        public Regex ReTypes
        {
            get { return reAllTypes; }
        }



        #endregion


        public FileTypeManager(NpgsqlConnection dbConn, string FilePath, string TreePath)
        {
            //
            // The InitializeComponent() call is required for Windows Forms designer support.
            //
            InitializeComponent();

            // setup listview column sorting
            lvwRemColumnSorter = new ListViewColumnSorter();
            this.lvRemTypes.ListViewItemSorter = lvwRemColumnSorter;
            lvwLocColumnSorter = new ListViewColumnSorter();
            this.lvLocTypes.ListViewItemSorter = lvwLocColumnSorter;

            connDb = dbConn;
            strFilePath = FilePath;

            LoadFilters();
            LoadRemoteTypes();
            
        }

        void lvRemTypesColumnClick(object sender, ColumnClickEventArgs e)
        {

            // Determine if clicked column is already the column that is being sorted.
            if (e.Column == lvwRemColumnSorter.SortColumn)
            {
                // Reverse the current sort direction for this column.
                if (lvwRemColumnSorter.Order == SortOrder.Ascending)
                {
                    lvwRemColumnSorter.Order = SortOrder.Descending;
                }
                else
                {
                    lvwRemColumnSorter.Order = SortOrder.Ascending;
                }
            }
            else
            {
                // Set the column number that is to be sorted; default to ascending.
                lvwRemColumnSorter.SortColumn = e.Column;
                lvwRemColumnSorter.Order = SortOrder.Ascending;
            }

            // Perform the sort with these new sort options.
            this.lvRemTypes.Sort();

        }

        void lvLocTypesColumnClick(object sender, ColumnClickEventArgs e)
        {
            
            // Determine if clicked column is already the column that is being sorted.
            if (e.Column == lvwLocColumnSorter.SortColumn)
            {
                // Reverse the current sort direction for this column.
                if (lvwLocColumnSorter.Order == SortOrder.Ascending)
                {
                    lvwLocColumnSorter.Order = SortOrder.Descending;
                }
                else
                {
                    lvwLocColumnSorter.Order = SortOrder.Ascending;
                }
            }
            else
            {
                // Set the column number that is to be sorted; default to ascending.
                lvwLocColumnSorter.SortColumn = e.Column;
                lvwLocColumnSorter.Order = SortOrder.Ascending;
            }

            // Perform the sort with these new sort options.
            this.lvLocTypes.Sort();

        }


        #region Utility Methods

        public void RefreshRemote()
        {
            LoadFilters();
            LoadRemoteTypes();
            LoadCategories();
        }

        private void LoadCategories()
        {

            // initialize sql command for remote type list
            string strSql = @"
                select
                    cat_id,
                    cat_name
                from hp_category
                order by cat_name;
            ";

            // put the remote list in the DataSet
            NpgsqlCommand command = new NpgsqlCommand(strSql, connDb);

            Dictionary<Int32,string> items = new Dictionary<Int32,string>();
            items.Add(0,"");
            try
            {
                NpgsqlDataReader dr = command.ExecuteReader();
                while (dr.Read())
                {
                    items.Add( (Int32)dr["cat_id"], (string)dr["cat_name"] );
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to get categories." + System.Environment.NewLine + ex.Message,
                    "Data Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            cboCat.DataSource = new BindingSource(items, null);
            cboCat.DisplayMember = "Value";
            cboCat.ValueMember = "Key";

        }

        private void LoadFilters()
        {

            // clear dataset
            dsFilters = new DataSet();

            // initialize sql command for remote filter list
            string strSql = @"
                select
                    filter_id,
                    name_proto,
                    name_regex,
                    description
                from hp_entry_name_filter
                order by name_proto;
            ";

            // build data adapter
            daFilters = new NpgsqlDataAdapter(strSql, connDb);

            // build insert command
            daFilters.InsertCommand = new NpgsqlCommand("insert into hp_entry_name_filter (name_proto, name_regex, description) values (:nameproto, :nameregex, :description)", connDb);
            daFilters.InsertCommand.Parameters.Add(new NpgsqlParameter("nameproto", NpgsqlDbType.Varchar));
            daFilters.InsertCommand.Parameters.Add(new NpgsqlParameter("nameregex", NpgsqlDbType.Varchar));
            daFilters.InsertCommand.Parameters.Add(new NpgsqlParameter("description", NpgsqlDbType.Varchar));
            daFilters.InsertCommand.Parameters[0].Direction = ParameterDirection.Input;
            daFilters.InsertCommand.Parameters[1].Direction = ParameterDirection.Input;
            daFilters.InsertCommand.Parameters[2].Direction = ParameterDirection.Input;
            daFilters.InsertCommand.Parameters[0].SourceColumn = "name_proto";
            daFilters.InsertCommand.Parameters[1].SourceColumn = "name_regex";
            daFilters.InsertCommand.Parameters[2].SourceColumn = "description";

            // build update command
            daFilters.UpdateCommand = new NpgsqlCommand("update hp_entry_name_filter set name_proto=:nameproto, name_regex=:nameregex, description=:description where filter_id=:filterid", connDb);
            daFilters.UpdateCommand.Parameters.Add(new NpgsqlParameter("nameproto", NpgsqlDbType.Varchar));
            daFilters.UpdateCommand.Parameters.Add(new NpgsqlParameter("nameregex", NpgsqlDbType.Varchar));
            daFilters.UpdateCommand.Parameters.Add(new NpgsqlParameter("description", NpgsqlDbType.Integer));
            daFilters.UpdateCommand.Parameters.Add(new NpgsqlParameter("filterid", NpgsqlDbType.Integer));
            daFilters.UpdateCommand.Parameters[0].Direction = ParameterDirection.Input;
            daFilters.UpdateCommand.Parameters[1].Direction = ParameterDirection.Input;
            daFilters.UpdateCommand.Parameters[2].Direction = ParameterDirection.Input;
            daFilters.UpdateCommand.Parameters[3].Direction = ParameterDirection.Input;
            daFilters.UpdateCommand.Parameters[0].SourceColumn = "name_proto";
            daFilters.UpdateCommand.Parameters[1].SourceColumn = "name_regex";
            daFilters.UpdateCommand.Parameters[2].SourceColumn = "description";
            daFilters.UpdateCommand.Parameters[3].SourceColumn = "filter_id";

            // build delete command
            daFilters.DeleteCommand = new NpgsqlCommand("delete from hp_entry_name_filter where filter_id=:filterid", connDb);
            daFilters.DeleteCommand.Parameters.Add(new NpgsqlParameter("filterid", NpgsqlDbType.Integer));
            daFilters.DeleteCommand.Parameters[0].Direction = ParameterDirection.Input;
            daFilters.DeleteCommand.Parameters[0].SourceColumn = "filter_id";

            // put the remote list in the DataSet
            daFilters.Fill(dsFilters);

            dsFilters.Tables[0].Columns.Add("regex", Type.GetType("System.Object"));
            string strAllFilters = "";
            foreach (DataRow dr in dsFilters.Tables[0].Rows)
            {
                string strThis = dr["name_regex"].ToString();
                if (strAllFilters != "")
                {
                    strAllFilters += "|" + strThis;
                }
                else
                {
                    strAllFilters = strThis;
                }
                dr["regex"] = new Regex(strThis, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
            reAllFilters = new Regex(strAllFilters, RegexOptions.IgnoreCase | RegexOptions.Compiled);

        }

        private void LoadRemoteTypes()
        {

            // get remote file types
            dsRemTypes = new DataSet();

            // initialize sql command for remote type list
            string strSql = @"
                select
                    t.type_id,
                    t.file_ext,
                    t.default_cat,
                    c.cat_name,
                    t.icon,
                    t.type_regex,
                    t.description
                from hp_type as t
                left join hp_category as c on c.cat_id=t.default_cat
                order by t.file_ext;
            ";

            // put the remote list in the DataSet
            NpgsqlDataAdapter daTemp = new NpgsqlDataAdapter(strSql, connDb);
            daTemp.Fill(dsRemTypes);

            // if there are no remote types
            if (dsRemTypes.Tables.Count == 0)
            {
                // make an empty DataTable
                dsRemTypes.Tables.Add(CreateRemTypeTable());
            }

            // copy to remote only datatable
            // are we doing this because we can't bind a Regex object to a field in the datagrid?
            dtRemTypes = dsRemTypes.Tables[0].Copy();
            dtRemTypes.Columns.Add("regex", Type.GetType("System.Object"));

            // convert all regexs to one big regex
            string strAllTypes = "";
            foreach (DataRow dr in dtRemTypes.Rows)
            {
                string strThis = dr["type_regex"].ToString();
                dr["regex"] = new Regex(strThis, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                if (strAllTypes != "")
                {
                    strAllTypes += "|" + strThis;
                }
                else
                {
                    strAllTypes = strThis;
                }
            }
            reAllTypes = new Regex(strAllTypes, RegexOptions.IgnoreCase | RegexOptions.Compiled);

        }

        private DataTable CreateRemTypeTable()
        {

            DataTable dtTypes = new DataTable();
            dtTypes.Columns.Add("type_id", Type.GetType("System.Int32"));
            dtTypes.Columns.Add("file_ext", Type.GetType("System.String"));
            dtTypes.Columns.Add("default_cat", Type.GetType("System.Int32"));
            dtTypes.Columns.Add("cat_name", Type.GetType("System.String"));
            dtTypes.Columns.Add("icon", typeof(Byte[]));
            dtTypes.Columns.Add("type_regex", Type.GetType("System.String"));
            dtTypes.Columns.Add("description", Type.GetType("System.String"));
            return dtTypes;

        }

        private DataTable CreateLocTypeTable()
        {

            DataTable dtTypes = new DataTable();
            dtTypes.Columns.Add("file_ext", Type.GetType("System.String"));
            dtTypes.Columns.Add("icon", typeof(Byte[]));
            dtTypes.Columns.Add("can_add", Type.GetType("System.Boolean"));
            dtTypes.Columns.Add("status", Type.GetType("System.String"));
            dtTypes.Columns.Add("example", Type.GetType("System.String"));
            return dtTypes;

        }

        private void LoadLocalTypes()
        {

            // get all local file types
            dtLocTypes = CreateLocTypeTable();
            GetFilesRecursive(strFilePath);

            // push to display
            PopulateLocList();

        }

        protected void GetFilesRecursive(string ParentDir)
        {

            // get local types
            foreach (string d in Directory.GetDirectories(ParentDir))
            {
                foreach (string f in Directory.GetFiles(d))
                {

                    // get file extensions
                    Tuple<string, string, string, string> tplExtensions = GetFileExt(f);
                    string strFileExt = tplExtensions.Item1;
                    string strRemFileExt = tplExtensions.Item2;
                    string strRemBlockExt = tplExtensions.Item3;
                    string strWinFileExt = tplExtensions.Item4;

                    Boolean blnCanAdd = true;
                    string strStatus = "New Type";

                    if (strRemFileExt != "")
                    {
                        blnCanAdd = false;
                        strStatus = "Exists Remotely";
                    }
                    else if (strRemBlockExt != "") {
                        blnCanAdd = false;
                        strStatus = "Blocked";
                    }

                    // check if it has already been added
                    if (dtLocTypes.Select("file_ext='" + strFileExt + "'").Length == 0)
                    {
                        // insert a new type row, including icon
                        //   file_ext
                        //   icon
                        //   can_add
                        //   status
                        dtLocTypes.Rows.Add(
                            strFileExt,
                            ImageToByteArray(ExtractIcon(f)),
                            blnCanAdd,
                            strStatus,
                            f);
                    }

                }
                GetFilesRecursive(d);
            }
        }

        public Tuple<string,string,string,string> GetFileExt(string strFileName)
        {
            
            string strFileExt = "";
            string strRemFileExt = "";
            string strRemBlockExt = "";
            string strWinFileExt = "";

            // use FileInfo to get file extension as recognized by windows
            FileInfo fiCurrFile = new FileInfo(strFileName);
            if (fiCurrFile.Extension == "")
            {
                strWinFileExt = "";
            }
            else
            {
                strWinFileExt = fiCurrFile.Extension.Substring(1).ToLower();
            }

            // get an exact match
            if (reAllTypes.IsMatch(strFileName) && reAllTypes.ToString() != "")
            {

                strRemFileExt = reAllTypes.Match(strFileName).Value.Substring(1);
                strFileExt = strRemFileExt;

                // if the regex returns the same characters as the prototype
                if (dtRemTypes.Select("file_ext='" + strRemFileExt + "'").Length == 0)
                {
                    // otherwise, loop through the remotes types and find the matching prototype
                    // example: filename="somepart.prt.25" match="prt.25" prototype="prt.1"
                    foreach (DataRow drType in dtRemTypes.Rows)
                    {
                        // add matching prototype and continue
                        if (drType.Field<Regex>("regex").IsMatch(strFileName))
                        {
                            strRemFileExt = drType.Field<string>("file_ext");
                            strFileExt = strRemFileExt;
                            break;
                        }
                    }
                }
            }
            else if (reAllFilters.IsMatch(strFileName) && reAllFilters.ToString() != "")
            {
                strRemBlockExt = reAllFilters.Match(strFileName).Value.Substring(1);
                strFileExt = strRemBlockExt;

                // if the regex returns the same characters as the prototype
                // example: filename="somepart.prt.25" match="prt.25" prototype="prt.1"
                if (dsFilters.Tables[0].Select("name_proto='" + strRemBlockExt + "'").Length == 0)
                {
                    // loop through the remotes types and find the matching prototype
                    foreach (DataRow drFilter in dsFilters.Tables[0].Rows)
                    {
                        // add matching prototype and continue
                        if (drFilter.Field<Regex>("regex").IsMatch(strFileName))
                        {
                            strRemBlockExt = drFilter.Field<string>("name_proto");
                            strFileExt = strRemBlockExt;
                            break;
                        }
                    }
                }
            }
            else
            {
                strFileExt = strWinFileExt;
            }

            return Tuple.Create<string, string, string, string>(strFileExt, strRemFileExt, strRemBlockExt, strWinFileExt);

        }

        protected int GetRemoteType(string strFileName)
        {

            // get short file name
            FileInfo fiCurrFile = new FileInfo(strFileName);
            string strShortName = fiCurrFile.Name;

            // use a regular expression to match file names with remote extensions
            foreach (DataRow dr in dtRemTypes.Rows)
            {
                Regex rxExt = new Regex(dr["type_regex"].ToString(), RegexOptions.IgnoreCase | RegexOptions.Compiled);
                if (rxExt.IsMatch(strShortName))
                {
                    return dr.Field<int>("type_id");
                }
            }

            return 0;

        }

        protected bool FileBlocked(string strFileName)
        {

            // get short file name
            FileInfo fiCurrFile = new FileInfo(strFileName);
            string strShortName = fiCurrFile.Name;

            // check filters to see if the file is blocked
            foreach (DataRow dr in dsFilters.Tables[0].Rows)
            {
                Regex rxExt = new Regex(dr["name_regex"].ToString(), RegexOptions.IgnoreCase);
                if (rxExt.IsMatch(strShortName))
                {
                    return true;
                }
            }

            return false;

        }

        protected Image ExtractIcon(string fileName)
        {
            Icon ico = Icon.ExtractAssociatedIcon(fileName);
            Image img = Image.FromHbitmap(ico.ToBitmap().GetHbitmap());
            return img;
            //DeleteObject(img);
            //return Image.FromHbitmap(ico.ToBitmap().GetHbitmap());
        }

        private byte[] ImageToByteArray(System.Drawing.Image imageIn)
        {
            MemoryStream ms = new MemoryStream();
            imageIn.Save(ms,System.Drawing.Imaging.ImageFormat.Png);
            return ms.ToArray();
        }

        private Image ByteArrayToImage(byte[] byteArrayIn)
        {
            MemoryStream ms = new MemoryStream(byteArrayIn);
            Image returnImage = Image.FromStream(ms);
            return returnImage;
        }

        protected void InitRemTypesList()
        {

            //init ListView control
            lvRemTypes.Clear();

            //create columns for ListView
            lvRemTypes.Columns.Add("Extension", 100, System.Windows.Forms.HorizontalAlignment.Left);
            lvRemTypes.Columns.Add("Category", 250, System.Windows.Forms.HorizontalAlignment.Left);
            lvRemTypes.Columns.Add("RegEx", 100, System.Windows.Forms.HorizontalAlignment.Left);
            lvRemTypes.Columns.Add("Description", 250, System.Windows.Forms.HorizontalAlignment.Left);

        }

        protected void InitLocTypesList()
        {

            //init ListView control
            lvLocTypes.Clear();

            //create columns for ListView
            lvLocTypes.Columns.Add("Extension", 100, System.Windows.Forms.HorizontalAlignment.Left);
            lvLocTypes.Columns.Add("Status", 100, System.Windows.Forms.HorizontalAlignment.Left);
            lvLocTypes.Columns.Add("Example", 400, System.Windows.Forms.HorizontalAlignment.Left);

        }

        private void PopulateRemList()
        {

            foreach (DataRow row in dtRemTypes.Rows)
            {

                // insert data
                string[] lvData = new string[4];
                lvData[0] = row.Field<string>("file_ext");
                lvData[1] = row.Field<string>("cat_name");
                lvData[2] = row.Field<string>("type_regex");
                lvData[3] = row.Field<string>("description");

                // add image
                string strFileExt = row.Field<string>("file_ext");
                if (row.Field<byte[]>("icon") != null)
                {
                    Image imgCurrent = ByteArrayToImage(row.Field<byte[]>("icon"));
                    ilTypes.Images.Add(strFileExt, imgCurrent);
                }

                // create actual list item
                ListViewItem lvItem = new ListViewItem(lvData);
                lvItem.ImageKey = strFileExt;
                lvRemTypes.Items.Add(lvItem);

            }

        }

        private void PopulateLocList()
        {

            foreach (DataRow row in dtLocTypes.Rows)
            {

                // insert data
                string[] lvData = new string[3];
                lvData[0] = row.Field<string>("file_ext");
                lvData[1] = row.Field<string>("status");
                lvData[2] = row.Field<string>("example");

                // add image
                string strFileExt = row.Field<string>("file_ext");
                if (row.Field<byte[]>("icon") != null)
                {
                    Image imgCurrent = ByteArrayToImage(row.Field<byte[]>("icon"));
                    ilTypes.Images.Add(strFileExt, imgCurrent);
                }

                // create actual list item
                ListViewItem lvItem = new ListViewItem(lvData);
                lvItem.ImageKey = strFileExt;
                lvLocTypes.Items.Add(lvItem);

            }

        }

        #endregion


        private void btnRefreshFilters_Click(object sender, EventArgs e)
        {

            // Set cursor as hourglass
            Cursor.Current = Cursors.WaitCursor;

            // clear datagridview
            dgFilters.DataSource = null;

            // load the file ignore filters from the database
            LoadFilters();

            // bind datatable to datagridview
            dgFilters.DataSource = dsFilters.Tables[0];

            // set filter_id column readonly
            dgFilters.Columns["filter_id"].ReadOnly = true;
            dgFilters.Columns[0].DefaultCellStyle.BackColor = Color.LightGray;

            // refresh the datagridview
            dgFilters.Refresh();

            // Set cursor as default arrow
            Cursor.Current = Cursors.Default;

        }

        private void btnFiltersCommit_Click(object sender, EventArgs e)
        {

            //// get changes
            //DataSet dsTemp = dsFilters.GetChanges();

            //// update the database
            //daFilters.Update(dsTemp);

            //// merge changes back into dsFilters data set
            //dsFilters.Merge(dsTemp);
            //dsFilters.AcceptChanges();

            // try to do it all at once
            daFilters.Update(dsFilters.GetChanges());

            LoadFilters();

        }

        private void btnTypesCommit_Click(object sender, EventArgs e)
        {
            // validate
            string strExt = txtExt.Text;
            Int32 intCat = (Int32)cboCat.SelectedValue;
            Image imgIcon = pbIcon.Image;
            string strRegex = txtRegex.Text;
            string strDesc = txtDesc.Text;

            if ((strExt == "") || (strRegex == "") || (intCat == 0) || (strDesc == "") || (imgIcon == null))
            {
                MessageBox.Show("Missing or invalid data.",
                    "Data Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            NpgsqlCommand command = new NpgsqlCommand("insert into hp_type (file_ext, default_cat, icon, type_regex, description) values (:fileext, :defaultcat, :icon, :typeregex, :description)", connDb);
            command.Parameters.Add(new NpgsqlParameter("fileext", NpgsqlDbType.Varchar));
            command.Parameters.Add(new NpgsqlParameter("defaultcat", NpgsqlDbType.Integer));
            command.Parameters.Add(new NpgsqlParameter("icon", NpgsqlDbType.Bytea));
            command.Parameters.Add(new NpgsqlParameter("typeregex", NpgsqlDbType.Varchar));
            command.Parameters.Add(new NpgsqlParameter("description", NpgsqlDbType.Varchar));
            command.Parameters["fileext"].Value = strExt;
            command.Parameters["defaultcat"].Value = intCat;
            command.Parameters["icon"].Value = ImageToByteArray(imgIcon);
            command.Parameters["typeregex"].Value = strRegex;
            command.Parameters["description"].Value = strDesc;

            try
            {
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to insert new type:" + System.Environment.NewLine + ex.Message,
                    "Data Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
            btnRefreshRemote_Click(sender, e);

        }

        private void btnRefreshRemote_Click(object sender, EventArgs e)
        {

            // Set cursor as hourglass
            Cursor.Current = Cursors.WaitCursor;

            // clear objects
            dsRemTypes = new DataSet();
            InitRemTypesList();

            // process file types
            LoadRemoteTypes();
            LoadCategories();
            PopulateRemList();

            // Set cursor as default arrow
            Cursor.Current = Cursors.Default;

        }

        private void btnRefreshLocal_Click(object sender, EventArgs e)
        {

            // Set cursor as hourglass
            Cursor.Current = Cursors.WaitCursor;

            // clear objects
            dtLocTypes = new DataTable();
            InitLocTypesList();

            // load remote file type data first
            if (dsRemTypes.Tables.Count == 0) LoadRemoteTypes();
            LoadCategories();
            LoadLocalTypes();

            // Set cursor as default arrow
            Cursor.Current = Cursors.Default;

        }

        private void btnAddSel_Click(object sender, EventArgs e)
        {
            if (lvLocTypes.SelectedItems.Count != 0)
            {
                string strFileExt = lvLocTypes.SelectedItems[0].Text;

                txtExt.Text = strFileExt;
                txtRegex.Text = "";
                txtDesc.Text = "";
                cboCat.SelectedIndex = 0;
                pbIcon.Image = ilTypes.Images[strFileExt];

            }
        }

    }
}
