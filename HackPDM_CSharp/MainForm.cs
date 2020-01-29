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
using System.Diagnostics;
using System.ComponentModel;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Configuration;

//using System.Threading;
using System.IO;
using System.Data;
using Npgsql;
using System.Security.Cryptography;


//using net.kvdb.webdav;

// TODO: use UTC time for comparing file timestamps (DateTime.ToUniversalTime) (currently broken when users are in different timezones)


namespace HackPDM
{
    /// <summary>
    /// Description of MainForm.
    /// client_status_code:
    ///     ro = remote only
    ///     lo = local only
    ///     nv = new remote version
    ///     cm = checked out to me
    ///     co = checked out to other
    ///     if = ignore filter
    ///     ft = no remote file type
    ///     lm = local modification
    ///     dt = deleted
    ///     ds = destroyed
    /// </summary>


    public partial class MainForm : Form
    {


        #region declarations

        // public delegate void Delegate_ShowFileInTree(string filepath);

        public static ImgConvert imgConv = new ImgConvert();

        private string strDbConn;
        private NpgsqlConnection connDb = new NpgsqlConnection();
        private NpgsqlTransaction t;

        private WebDAVClient connDav = new WebDAVClient();

        private SWHelper connSwApi = new SWHelper();
        private SWDocMgr docMgr;
        private static String strSWDocMgrKey = "";
        bool blnUseSwApi = false;

        private Dictionary<string, Int32> dictTree;
        Dictionary<string, TreeNode> dictTreeNodes;
        private DataSet dsTree = new DataSet();
        private DataSet dsList = new DataSet();
        private List<string> lstSandboxed = new List<string>();

        private DataTable dtServSettings = new DataTable("settings");
        private int intSecondsTolerance;

        private Dictionary<string, int> dictPropIds;
        private Dictionary<int, string> dictPropTypes;

        private string strLocalFileRoot;
        private int intMyUserId;
        private int intMyNodeId;
        private bool blnShowDeleted = false;

        private long lngMaxFileSize = 2147483648;

        private StatusDialog dlgStatus;
        string[] strStatusParams = new String[2];

        private FileTypeManager ftmStart;

        string strCurrProfileId;
        DataRow drCurrProfile;

        private ListViewColumnSorter lvwColumnSorter;

        // For storing search parameters:
        string FileContainsText;
        string PropDropDownText;
        string PropContainsText;
        string CheckedOutMeBox;
        string DeletedLocalBox;
        string LocalOnlyBox;

        // For storing window layout settings
        List<int> listView1ColWidths;

        private int split1Dist;
        private int split2Dist;

        #endregion


        public MainForm()
        {
            //
            // The InitializeComponent() call is required for Windows Forms designer support.
            //
            InitializeComponent();
            RecallWindowSettings();

            // setup listview column sorting
            lvwColumnSorter = new ListViewColumnSorter();
            this.listView1.ListViewItemSorter = lvwColumnSorter;

            // get server connections and authenticate
            LoadProfile(false);
            DbConnect();
            DavConnect();

            // get (and set if necessary) my node_id
            intMyNodeId = GetNodeId();

            // get server settings
            LoadServerSettings();

            // get remote file type manager
            ftmStart = new FileTypeManager(connDb, strLocalFileRoot, Utils.GetRelativePath(strLocalFileRoot, strLocalFileRoot));

            // Populate data
            ResetView();

            // Initialize stored search params:
            FileContainsText = "";
            PropDropDownText = "";
            PropContainsText = "";
            CheckedOutMeBox = "0";
            DeletedLocalBox = "0";
            LocalOnlyBox = "0";
        }

        private void RecallWindowSettings()
        {
            // set window size and location
            this.Location = Properties.UserSettings.Default.usetWindowLocation;
            this.Size = Properties.UserSettings.Default.usetWindowSize;
            this.WindowState = Properties.UserSettings.Default.usetWindowState;

            // load list view column sizes from settings
            System.Collections.Specialized.StringCollection collection = Properties.UserSettings.Default.usetColumnWidths;
            List<int> integers = collection.Cast<string>().ToList().ConvertAll(s => Int32.Parse(s));
            listView1ColWidths = integers;

            // load splitter distances
            split1Dist = Properties.UserSettings.Default.usetSplitter1Distance;
            if (split1Dist == 0)
                split1Dist = splitContainer1.SplitterDistance;
            else
                splitContainer1.SplitterDistance = split1Dist;
            split2Dist = Properties.UserSettings.Default.usetSplitter2Distance;
            if (split2Dist == 0)
                split2Dist = splitContainer2.SplitterDistance;
            else
                splitContainer2.SplitterDistance = split2Dist;

            // load show deleted previous state
            blnShowDeleted = Properties.UserSettings.Default.usetShowDeleted;
        }

        private void DavConnect()
        {

            // build WebDAV connection string from profile values
            connDav.Server = (string)drCurrProfile["DavServ"];
            connDav.Port = Convert.ToInt32(drCurrProfile["DavPort"]);
            connDav.User = (string)drCurrProfile["DavUser"];
            connDav.Pass = (string)drCurrProfile["DavPass"];
            connDav.BasePath = (string)drCurrProfile["DavPath"];

            // test the connection
            try
            {
                List<string> strTest = connDav.List("/admin/", 1);
                //       List<string> strTest = connDav.List("/", 1);
            }
            catch (System.Exception e)
            {
                var result = MessageBox.Show("Failed to make WebDAV connection: " + e.Message + "\r\n WebRequest returned status: " + connDav.StatusString,
                "Startup Error",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Error);
                if (result == System.Windows.Forms.DialogResult.Cancel)
                {
                    Environment.Exit(1);
                }
                LoadProfile(true);
            }

        }

        private void DbConnect()
        {

            // build database connection string from profile key values
            strDbConn = String.Format("Server={0};Port={1};User Id={2};Password={3};Database={4};",
                (string)drCurrProfile["DbServ"],
                (string)drCurrProfile["DbPort"],
                (string)drCurrProfile["DbUser"],
                (string)drCurrProfile["DbPass"],
                (string)drCurrProfile["DbName"]);


            // connect to the database
            try
            {
                connDb.Close();
                connDb.ConnectionString = strDbConn;
                connDb.Open();
            }
            catch (System.Exception e)
            {
                var result = MessageBox.Show("Failed to make database connection: " + e.Message + "\r\nTry Again?",
                "Startup Error",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Error);
                if (result == System.Windows.Forms.DialogResult.Cancel)
                {
                    Environment.Exit(1);
                }
                LoadProfile(true);
            }

            // authenticate
            string strSql = String.Format("select user_id from hp_user where login_name='{0}' and passwd='{1}';",
                (string)drCurrProfile["Username"],
                (string)drCurrProfile["Password"]);
            NpgsqlCommand cmdGetId = new NpgsqlCommand(strSql, connDb);
            try
            {
                object oTemp = cmdGetId.ExecuteScalar();
                if (oTemp == null)
                {
                    // no user_id returned
                    var result = MessageBox.Show("Can't authenticate the user.  Try running the install tool again.",
                        "Authentication Error",
                        MessageBoxButtons.OKCancel,
                        MessageBoxIcon.Error);
                    if (result == System.Windows.Forms.DialogResult.Cancel)
                    {
                        Environment.Exit(1);
                    }
                    LoadProfile(true);
                }
                else
                {
                    // set the user_id
                    intMyUserId = (int)oTemp;
                }
            }
            catch (Exception ex)
            {
                // no database connection
                var result = MessageBox.Show("Can't access the database.  Try running the install tool again." + System.Environment.NewLine + ex.Message,
                    "Authentication Error",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Error);
                if (result == System.Windows.Forms.DialogResult.Cancel)
                {
                    Environment.Exit(1);
                }
                LoadProfile(true);
            }

        }

        private void LoadProfile(bool blnForceDlg)
        {

            // load profile info
            strCurrProfileId = Properties.UserSettings.Default.usetDefaultProfile;
            string strXmlProfiles = Properties.UserSettings.Default.usetProfiles;

#if DEBUG
            // TEMP: try to get a saved profile
            // running in the debugger causes the config file to be in a different place everytime
            // that means you have to create a new one everytime
            // TODO: erase this stuff when building for release
            try
            {
                var fileMap = new System.Configuration.ConfigurationFileMap("c:\\temp\\hackpdm_creds_production-admin.config");
                var configuration = ConfigurationManager.OpenMappedMachineConfiguration(fileMap);
                var sectionGroup = configuration.GetSectionGroup("tempSettingsGroup"); // This is the section group name, change to your needs
                var section = (ClientSettingsSection)sectionGroup.Sections.Get("tempSettingsSection"); // This is the section name, change to your needs
                //var setting = section.Settings.Get("usetDefaultProfile"); // This is the setting name, change to your needs
                strCurrProfileId = section.Settings.Get("usetDefaultProfile").Value.ValueXml.InnerText;
                strXmlProfiles = section.Settings.Get("usetProfiles").Value.ValueXml.InnerText;
            }
            catch
            {
                // These are for jared's testing (couldn't get the config file thing working...):
                strCurrProfileId = "96ab093f-f106-49bd-8626-6d3bb2877965";
                string myServerIP = "192.168.52.136";
                strXmlProfiles = "<DocumentElement>\r\n  <profiles>\r\n    <PfGuid>96ab093f-f106-49bd-8626-6d3bb2877965</PfGuid>\r\n    <PfName>jared</PfName>\r\n    <DbServ>" + myServerIP + "</DbServ>\r\n    <DbPort>5432</DbPort>\r\n    <DbUser>demouser</DbUser>\r\n    <DbPass>demo</DbPass>\r\n    <DbName>hackpdm</DbName>\r\n    <DavServ>http://" + myServerIP + "</DavServ>\r\n    <DavPort>80</DavPort>\r\n    <DavUser/>\r\n    <DavPass/>\r\n    <DavPath>webdav</DavPath>\r\n    <FsRoot>D:\\Desktop Stuff\\Work\\Asphalt Zipper</FsRoot>\r\n    <Username>demo</Username>\r\n    <Password>demo</Password>\r\n  </profiles>\r\n</DocumentElement>";
                //strXmlProfiles = "<DocumentElement>\r\n  <profiles>\r\n    <PfGuid>96ab093f-f106-49bd-8626-6d3bb2877965</PfGuid>\r\n    <PfName>jared</PfName>\r\n    <DbServ>192.168.52.135</DbServ>\r\n    <DbPort>5432</DbPort>\r\n    <DbUser>hackuser</DbUser>\r\n    <DbPass>demo</DbPass>\r\n    <DbName>hackpdm</DbName>\r\n    <DavServ>http://192.168.52.135</DavServ>\r\n    <DavPort>80</DavPort>\r\n    <DavUser/>\r\n    <DavPass/>\r\n    <DavPath>webdav</DavPath>\r\n    <FsRoot>D:\\Desktop Stuff\\Work\\Asphalt Zipper</FsRoot>\r\n    <Username>demo</Username>\r\n    <Password>demo</Password>\r\n  </profiles>\r\n</DocumentElement>";
            }
#endif

            // check existence
            if (blnForceDlg || strXmlProfiles == "" || strCurrProfileId == "")
            {

                // launch the profile manager
                ProfileManager dlgPM = new ProfileManager();
                DialogResult pmResult = dlgPM.ShowDialog();

                // try again to get the profiles
                strCurrProfileId = Properties.UserSettings.Default.usetDefaultProfile;
                strXmlProfiles = Properties.UserSettings.Default.usetProfiles;

                // failed again
                if (strXmlProfiles == "" || strCurrProfileId == "")
                {
                    var result = MessageBox.Show("Still can't get a profile.  Can't connect to the server.\nRetry?",
                        "Startup Error",
                        MessageBoxButtons.OKCancel,
                        MessageBoxIcon.Error);
                    if (result == System.Windows.Forms.DialogResult.Cancel)
                    {
                        Environment.Exit(1);
                    }
                    LoadProfile(true);
                }

            }

            // read the profile xml into the datatable
            StringReader reader = new StringReader(strXmlProfiles);
            DataTable dtProfiles = new DataTable("profiles");
            dtProfiles.ReadXmlSchema(reader);
            reader = new StringReader(strXmlProfiles);
            dtProfiles.ReadXml(reader);

            // try to get the default profile
            drCurrProfile = dtProfiles.Select("PfGuid='" + strCurrProfileId + "'")[0];
            if (drCurrProfile == null)
            {
                var result = MessageBox.Show("Still can't get a profile.  Can't connect to the server.\nRetry?",
                    "Startup Error",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Error);
                if (result == System.Windows.Forms.DialogResult.Cancel)
                {
                    Environment.Exit(1);
                }
                LoadProfile(true);
            }

            // check and hand off local file root directory
            if (Directory.Exists((string)drCurrProfile["FsRoot"]))
            {
                strLocalFileRoot = (string)drCurrProfile["FsRoot"];
            }
            else
            {
                var result = MessageBox.Show("Still can't get a profile.  Can't find the local pwa directory.",
                    "Startup Error",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Error);
                if (result == System.Windows.Forms.DialogResult.Cancel)
                {
                    Environment.Exit(1);
                }
                LoadProfile(true);
            }


        }

        private int GetNodeId()
        {

            int intNodeId;

            // start a database transaction
            t = connDb.BeginTransaction();

            // try to get the node_id
            string strSqlGet = "select node_id from hp_node where node_name ilike '" + System.Environment.MachineName + "';";
            NpgsqlCommand cmdGetNode = new NpgsqlCommand(strSqlGet, connDb, t);
            object oTemp = cmdGetNode.ExecuteScalar();

            // check for a return value
            if (oTemp == null)
            {

                // no node_id returned: create one
                string strSqlGetNew = "select nextval('seq_hp_node_node_id'::regclass);";
                cmdGetNode.CommandText = strSqlGetNew;
                object oNewNodeId = cmdGetNode.ExecuteScalar();
                intNodeId = Convert.ToInt32(oNewNodeId);

                // insert the new node
                string strSqlSet = "insert into hp_node (node_id,node_name,create_user) values (" + intNodeId.ToString() + ",'" + System.Environment.MachineName.ToLower() + "'," + intMyUserId + ");";
                cmdGetNode.CommandText = strSqlSet;
                int intRows = cmdGetNode.ExecuteNonQuery();

            }
            else
            {

                // convert the node_id
                intNodeId = (int)oTemp;

            }

            t.Commit();
            return intNodeId;

        }

        private void LoadServerSettings()
        {
            // try to get settings data from the server
            string strSql = "select * from hp_settings;";
            NpgsqlDataAdapter daTemp = new NpgsqlDataAdapter(strSql, connDb);
            daTemp.Fill(dtServSettings);

            // seconds tolerance on file timestamps
            Decimal decSecondsTolerance = dtServSettings.Select("setting_name='seconds_tolerance'")[0].Field<Decimal>("setting_number_value");
            intSecondsTolerance = (Int32)decSecondsTolerance;

            // get license key
            strSWDocMgrKey = dtServSettings.Select("setting_name='swdocmgr_key'")[0].Field<String>("setting_text_value");
            docMgr = new SWDocMgr(strSWDocMgrKey);

        }


        private void LoadDirData()
        {

            // clear the dataset
            dsTree = new DataSet();

            // load remote directories to a DataTable
            string strSql = @"
                select
                    t.dir_id,
                    t.parent_id,
                    t.dir_name,
                    d.create_stamp,
                    d.create_user,
                    d.modify_stamp,
                    d.modify_user,
                    t.sandboxed, -- view_dir_tree cascades the sandboxed flag down through child directories
                    t.active, -- view_dir_tree cascades the active flag down through child directories
                    true as is_remote,
                    false as is_local,
                    replace('pwa' || t.rel_path, '/', '\') as path
                from view_dir_tree as t
                left join hp_directory as d on d.dir_id=t.dir_id
                order by t.rel_path;
            ";
            NpgsqlDataAdapter daTemp = new NpgsqlDataAdapter(strSql, connDb);
            daTemp.Fill(dsTree);

            // create parent-child relationship
            // can't do this because all local directories get dir_id=0, which violates implied uniquness
            //dsTree.Relations.Add("rsParentChild", dsTree.Tables[0].Columns["dir_id"], dsTree.Tables[0].Columns["parent_id"]);

            // add remotes to dictionary
            foreach (DataRow row in dsTree.Tables[0].Rows)
            {
                dictTree.Add(row.Field<string>("path"), row.Field<int>("dir_id"));
                if (row.Field<bool>("sandboxed")) lstSandboxed.Add(row.Field<string>("path"));
            }

            // get all local directories
            DirectoryInfo dirBase = new DirectoryInfo(strLocalFileRoot);
            foreach (DirectoryInfo dir in dirBase.GetDirectories("*", SearchOption.AllDirectories))
            {

                string strAbsPath = dir.FullName;
                string strRelPath = Utils.GetRelativePath(strLocalFileRoot, strAbsPath);
                string strParentRelPath = Utils.GetParentDirectory(strRelPath);

                int intDirId = 0;
                dictTree.TryGetValue(strRelPath, out intDirId);

                int intParentId = 0;
                dictTree.TryGetValue(strParentRelPath, out intParentId);

                // check for remote existence
                if (intDirId != 0)
                {
                    // update is_local flag
                    DataRow drRemote = dsTree.Tables[0].Select("dir_id=" + intDirId)[0];
                    drRemote.SetField<bool>("is_local", true);
                }
                else
                {
                    // get parent active status
                    DataRow drParent = dsTree.Tables[0].Select("path='" + strParentRelPath.Replace("'", "''") + "'")[0];
                    bool blnActive = drParent.Field<bool>("active");

                    // add to sandbox list
                    bool blnSandboxed = drParent.Field<bool>("sandboxed");
                    if (blnSandboxed) lstSandboxed.Add(strRelPath);

                    // add local-only directory
                    dsTree.Tables[0].Rows.Add(
                        intDirId,    // dir_id
                        intParentId, // parent_id
                        dir.Name,    // dir_name
                        null,        // create_stamp
                        null,        // create_user
                        null,        // modify_stamp
                        null,        // modify_user
                        blnSandboxed,// sandboxed
                        blnActive,   // active
                        false,       // is_remote
                        true,        // is_local
                        strRelPath   // path
                    );
                }

            }

        }

        private void LoadPropertyMaps()
        {

            // clear the ids dict
            dictPropIds = new Dictionary<string, int>();

            // load custom property info to dictionary
            string strSql = @"
                select
                    prop_name,
                    prop_id
                from hp_property
                where active=true;
            ";
            NpgsqlCommand command = new NpgsqlCommand(strSql, connDb);
            using (NpgsqlDataReader dr = command.ExecuteReader())
            {
                while (dr.Read())
                {
                    dictPropIds.Add((string)dr[0], (int)dr[1]);
                }
            }

            // clear the types dict
            dictPropTypes = new Dictionary<int, string>();

            // load custom property info to dictionary
            strSql = @"
                select
                    prop_id,
                    prop_type
                from hp_property
                where active=true;
            ";
            command = new NpgsqlCommand(strSql, connDb);
            using (NpgsqlDataReader dr = command.ExecuteReader())
            {
                while (dr.Read())
                {
                    dictPropTypes.Add((int)dr[0], (string)dr[1]);
                }
            }

        }

        void CmdRefreshViewClick(object sender, EventArgs e)
        {
            ResetView(treeView1.SelectedNode.FullPath);
        }

        private void ResetView(string strTreePath = "")
        {

            // Set cursor as hourglass
            Cursor.Current = Cursors.WaitCursor;

            // get a list of selected items
            List<string> lstEntrySel = new List<string>();
            if (listView1.SelectedItems.Count != 0)
            {
                foreach (ListViewItem lviSelected in listView1.SelectedItems)
                {
                    lstEntrySel.Add(lviSelected.Text);
                }
            }

            // clear and re-populate the directory data
            dictTree = new Dictionary<string, Int32>(StringComparer.OrdinalIgnoreCase);
            InitTreeView();

            // get root tree node
            TreeNode tnRoot = treeView1.Nodes[0];

            // build the tree view structure
            PopulateTree(tnRoot);

            // clear the list window
            InitListView();

            // select the top node or the specified node
            TreeNode tnSelect = new TreeNode();
            if (strTreePath == "")
            {
                treeView1.SelectedNode = tnRoot;
                PopulateList(tnRoot);
            }
            else
            {
                tnSelect = FindNode(strTreePath);
                if (tnSelect != null)
                {
                    treeView1.SelectedNode = tnSelect;
                    PopulateList(tnSelect);
                }
                else
                {
                    treeView1.SelectedNode = tnRoot;
                    PopulateList(tnRoot);
                }
            }

            // select the previously selected entries
            if (lstEntrySel == null) lstEntrySel = new List<string>();
            int intLastIndex = 0;
            foreach (ListViewItem li in listView1.Items)
            {
                if (lstEntrySel.Contains(li.Text))
                {
                    li.Selected = true;
                    intLastIndex = li.Index;
                }
            }
            if (listView1.Items.Count > 0) listView1.EnsureVisible(intLastIndex);

            // reset file type manager
            ftmStart.RefreshRemote();

            // set the Show Deleted checkbox
            //chkShowDeleted.Checked = blnShowDeleted;

            // Set cursor as default arrow
            Cursor.Current = Cursors.Default;

        }

        protected void InitTreeView()
        {

            // reset context menu
            foreach (ToolStripMenuItem tsmiItem in cmsTree.Items)
            {
                tsmiItem.Enabled = false;
            }

            // load remote directory structure
            LoadDirData();

            // get the root directory row and set values
            DataRow drRoot = dsTree.Tables[0].Select("dir_id=1")[0];
            drRoot.SetField<bool>("is_local", true);

            // clear the tree
            treeView1.Nodes.Clear();

            // insert the root node where tag = dir_id = 1
            TreeNode tnRoot = new TreeNode("pwa");
            tnRoot.Tag = (object)(int)1;
            tnRoot.ImageIndex = 0;
            tnRoot.SelectedImageIndex = 0;
            treeView1.Nodes.Add(tnRoot);

        }

        protected void PopulateTree(TreeNode tnParent)
        {

            // dsTree contains the following columns:
            //   parent_id
            //   dir_name
            //   create_stamp
            //   create_user
            //   modify_stamp
            //   modify_user
            //   active
            //   is_remote
            //   is_local
            //   path

            // directory icons are as follows:
            //   0 - simple-folder-icon
            //   1 - folder-icon_localonly
            //   2 - folder-icon_remoteonly
            //   3 - folder-icon_checkme
            //   4 - folder-icon_checkedother
            //   5 - folder-icon_deleted

            // get all directories
            DataRow[] drShows;
            if (blnShowDeleted)
            {
                drShows = dsTree.Tables[0].Select("dir_id<>1", "path asc");
            }
            else
            {
                drShows = dsTree.Tables[0].Select("dir_id<>1 and (active=true or (active=false and is_local=true))", "path asc");
            }

            // create dictionary of tree nodes
            dictTreeNodes = new Dictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase);
            dictTreeNodes.Add(tnParent.FullPath, tnParent);

            // loop through directories, adding nodes to the treeview
            foreach (DataRow dr in drShows)
            {

                string strRelPath = dr.Field<string>("path");
                string strParentRelPath = Utils.GetParentDirectory(strRelPath);

                // create the node
                TreeNode tnChild = new TreeNode(dr.Field<string>("dir_name"));
                tnChild.Tag = dr.Field<object>("dir_id");

                // select icon
                bool blnShow = false;
                if (dr.Field<bool>("active") && dr.Field<bool>("is_local") && dr.Field<bool>("is_remote"))
                {
                    // is active, is local, is remote
                    tnChild.ImageIndex = 0;
                    tnChild.SelectedImageIndex = 0;
                    blnShow = true;
                }
                else if (dr.Field<bool>("active") && dr.Field<bool>("is_local") && !dr.Field<bool>("is_remote"))
                {
                    // is active, is local, not remote
                    tnChild.ImageIndex = 1;
                    tnChild.SelectedImageIndex = 1;
                    blnShow = true;
                }
                else if (dr.Field<bool>("active") && !dr.Field<bool>("is_local") && dr.Field<bool>("is_remote"))
                {
                    // is active, not local, is remote
                    tnChild.ImageIndex = 2;
                    tnChild.SelectedImageIndex = 2;
                    blnShow = true;
                }
                else if (!dr.Field<bool>("active") && (dr.Field<bool>("is_local") || blnShowDeleted))
                {
                    // not active, and either it's local or the user wants to see deleted
                    tnChild.ImageIndex = 5;
                    tnChild.SelectedImageIndex = 5;
                    blnShow = true;
                }

                // show the node
                if (blnShow)
                {
                    dictTreeNodes[strParentRelPath].Nodes.Add(tnChild);
                    dictTreeNodes.Add(strRelPath, tnChild);
                }

            }

        }

        protected void InitListView()
        {

            //init ListView control
            listView1.Clear();

            // configure sorting
            //listView1.Sorting = SortOrder.None;
            //listView1.ColumnClick += new ColumnClickEventHandler(lv1ColumnClick);

            //create columns for ListView
            listView1.Columns.Add("Name", 300, System.Windows.Forms.HorizontalAlignment.Left);
            listView1.Columns.Add("Size", 75, System.Windows.Forms.HorizontalAlignment.Right);
            listView1.Columns.Add("Type", 120, System.Windows.Forms.HorizontalAlignment.Left);
            listView1.Columns.Add("Stat", 60, System.Windows.Forms.HorizontalAlignment.Left);
            listView1.Columns.Add("Local-Date", 120, System.Windows.Forms.HorizontalAlignment.Left);
            listView1.Columns.Add("Remote-Date", 120, System.Windows.Forms.HorizontalAlignment.Left);
            listView1.Columns.Add("CheckOut", 120, System.Windows.Forms.HorizontalAlignment.Left);
            listView1.Columns.Add("Category", 140, System.Windows.Forms.HorizontalAlignment.Left);
            listView1.Columns.Add("FullName", 0, System.Windows.Forms.HorizontalAlignment.Left);

            // reset context menu
            foreach (ToolStripMenuItem tsmiItem in cmsList.Items)
            {
                tsmiItem.Enabled = false;
            }

            // set column widths
            for (int i = 0; i < listView1.Columns.Count; i++)
                listView1.Columns[i].Width = this.listView1ColWidths[i];

        }

        protected void LoadListData(string strRelPath)
        {

            // TODO: consider changing this method so that it calls the LoadCombinedData() method
            // the main problem with doing that, is that the delete methods don't need to load all the file data for deletes
            // for example, they don't need the checksum or the icon

            // clear dataset
            dsList = new DataSet();

            DataSet dsCombined = new DataSet();
            // get directory path
            string strAbsPath = Utils.GetAbsolutePath(strLocalFileRoot, strRelPath);

            // get remote entries
            int intDirId = 0;
            dictTree.TryGetValue(strRelPath, out intDirId);
            if (intDirId != 0)
            {

                // initialize sql command for remote entry list
                string strSql = @"
                    select
                        e.entry_id,
                        v.version_id,
                        e.dir_id,
                        e.entry_name,
                        t.type_id,
                        t.file_ext,
                        e.cat_id,
                        c.cat_name,
                        v.file_size as latest_size,
                        pg_size_pretty(v.file_size) as str_latest_size,
                        null as local_size,
                        '' as str_local_size,
                        v.file_modify_stamp as latest_stamp,
                        to_char(v.file_modify_stamp, 'yyyy-MM-dd HH24:MI:SS') as str_latest_stamp,
                        null as local_stamp,
                        '' as str_local_stamp,
                        v.md5sum as latest_md5,
                        null as local_md5,
                        e.checkout_user,
                        u.last_name || ', ' || u.first_name as ck_user_name,
                        e.checkout_date,
                        to_char(e.checkout_date, 'yyyy-MM-dd HH24:MI:SS') as str_checkout_date,
                        e.checkout_node,
                        n.node_name as checkout_node_name,
                        false as is_local,
                        true as is_remote,
                        case
                            when e.destroyed then 'ds'::varchar
                            when e.active then 'ro'::varchar
                            else 'dt'::varchar end as client_status_code,
                        :rel_path as relative_path,
                        :abs_path as absolute_path,
                        t.icon,
                        false as is_depend_searched,
                        null::boolean as is_readonly,
                        e.active,
                        e.destroyed
                    from hp_entry as e
                    left join hp_user as u on u.user_id=e.checkout_user
                    left join hp_category as c on c.cat_id=e.cat_id
                    left join hp_type as t on t.type_id=e.type_id
                    left join (
                        select distinct on (entry_id)
                            entry_id,
                            version_id,
                            file_size,
                            create_stamp,
                            file_modify_stamp,
                            md5sum
                        from hp_version
                        order by entry_id, create_stamp desc
                    ) as v on v.entry_id=e.entry_id
                    left join hp_node as n on n.node_id=e.checkout_node
                    where e.dir_id=:dir_id
                    order by dir_id,entry_id;
                ";

                // put the remote list in the DataSet
                NpgsqlDataAdapter daTemp = new NpgsqlDataAdapter(strSql, connDb);
                daTemp.SelectCommand.Parameters.Add(new NpgsqlParameter("dir_id", NpgsqlTypes.NpgsqlDbType.Integer));
                daTemp.SelectCommand.Parameters.Add(new NpgsqlParameter("rel_path", NpgsqlTypes.NpgsqlDbType.Text));
                daTemp.SelectCommand.Parameters.Add(new NpgsqlParameter("abs_path", NpgsqlTypes.NpgsqlDbType.Text));
                daTemp.SelectCommand.Parameters["dir_id"].Value = intDirId;
                daTemp.SelectCommand.Parameters["rel_path"].Value = strRelPath;
                daTemp.SelectCommand.Parameters["abs_path"].Value = strAbsPath;
                daTemp.Fill(dsList);

            }

            if (dsList.Tables.Count == 0)
            {
                // make an empty DataTable
                dsList.Tables.Add(CreateFileTable());
            }

            dsList.Tables[0].TableName = "files";

            // get local files
            LoadCombinedData(null, null, ref dsList, strRelPath);

        }

        protected void PopulateList(TreeNode nodeCurrent)
        {

            // clear list
            InitListView();
            InitTabPages();
            LoadListData(nodeCurrent.FullPath);

            // if we have any files to show, then populate listview with files
            if (dsList.Tables[0] != null)
            {
                foreach (DataRow row in dsList.Tables[0].Select("", "entry_name asc"))
                {

                    string strCheckout = "";
                    if (row.Field<string>("ck_user_name") != null)
                    {
                        strCheckout = String.Format("{0} ({1}), {2}", row.Field<string>("ck_user_name"), row.Field<string>("checkout_node_name"), row.Field<string>("str_checkout_date"));
                    }
                    string[] lvData = new string[9];
                    lvData[0] = row.Field<string>("entry_name"); // Name
                    lvData[1] = row.Field<string>("str_latest_size"); // Size
                    lvData[2] = row.Field<string>("file_ext"); // Type
                    lvData[3] = row.Field<string>("client_status_code"); // Stat
                    lvData[4] = row.Field<string>("str_local_stamp"); // Local Modified
                    lvData[5] = row.Field<string>("str_latest_stamp"); // Server Modified
                    lvData[6] = strCheckout; // CheckOut
                    lvData[7] = row.Field<string>("cat_name"); // Category
                    lvData[8] = row.Field<string>("absolute_path") + "\\" + row.Field<string>("entry_name"); // FullName

                    // get file type
                    string strFileExt = row.Field<string>("file_ext");

                    // convert status code to overlay image name
                    string strOverlay = row.Field<string>("client_status_code");
                    strOverlay = strOverlay == "ok" ? "" : "." + strOverlay;

                    // get icon images for new file types (new to this session)
                    if (ilListIcons.Images[strFileExt] == null)
                    {

                        Image imgCurrent;
                        byte[] img = row.Field<byte[]>("icon");
                        if (img == null)
                        {
                            // extract an image locally
                            string strFullName = row.Field<string>("absolute_path") + "\\" + row.Field<string>("entry_name");
                            imgCurrent = GetLocalIcon(strFullName);
                        }
                        else
                        {
                            // get remote image
                            MemoryStream ms = new MemoryStream();
                            ms.Write(img, 0, img.Length);
                            imgCurrent = Image.FromStream(ms);
                        }

                        // add bare icon to icon list
                        ilListIcons.Images.Add(strFileExt, imgCurrent);

                        // add overlayed icons to icon list
                        // these are added at design time on the MainForm.cs designer ("Choose images" context menu item)
                        ilListIcons.Images.Add(strFileExt + ".ro", ImageOverlay(imgCurrent, ilListIcons.Images["ro"])); // remote only
                        ilListIcons.Images.Add(strFileExt + ".lo", ImageOverlay(imgCurrent, ilListIcons.Images["lo"])); // local only
                        ilListIcons.Images.Add(strFileExt + ".nv", ImageOverlay(imgCurrent, ilListIcons.Images["nv"])); // new remote version
                        ilListIcons.Images.Add(strFileExt + ".cm", ImageOverlay(imgCurrent, ilListIcons.Images["cm"])); // checked out to me
                        ilListIcons.Images.Add(strFileExt + ".co", ImageOverlay(imgCurrent, ilListIcons.Images["co"])); // checked out to other
                        ilListIcons.Images.Add(strFileExt + ".if", ImageOverlay(imgCurrent, ilListIcons.Images["if"])); // ignore filter
                        ilListIcons.Images.Add(strFileExt + ".ft", ImageOverlay(imgCurrent, ilListIcons.Images["ft"])); // no remote file type
                        ilListIcons.Images.Add(strFileExt + ".lm", ImageOverlay(imgCurrent, ilListIcons.Images["lm"])); // local modification
                        ilListIcons.Images.Add(strFileExt + ".dt", ImageOverlay(imgCurrent, ilListIcons.Images["dt"])); // deleted
                        ilListIcons.Images.Add(strFileExt + ".ds", ImageOverlay(imgCurrent, ilListIcons.Images["ds"])); // destroyed

                    }

                    // create the listview item
                    ListViewItem lvItem = new ListViewItem(lvData);
                    lvItem.ImageKey = strFileExt + strOverlay;

                    // show it in the list view
                    if (row.Field<bool>("active") || blnShowDeleted)
                    {
                        // but, only if it is active (not deleted)
                        listView1.Items.Add(lvItem);
                    }
                    else if (row.Field<bool>("is_local"))
                    {
                        // or, if it is deleted, but still exists locally
                        listView1.Items.Add(lvItem);
                    }

                }
            }

        }


        protected void InitTabPages()
        {

            // clear the preview image
            pbPreview.Image = null;

            // reset the history page
            lvHistory.Clear();

            // reset the parents page
            lvParents.Clear();

            // reset the children page
            lvChildren.Clear();

            // reset the properties page
            lvProperties.Clear();

        }

        protected void PopulatePreviewImage(string FileName, int intVersionId = 0)
        {

            DataRow dr = dsList.Tables[0].Select("entry_name='" + FileName + "'")[0];
            byte[] bImage;
            MemoryStream ms;

            // if local only or checked out to me, load image from local file
            if (dr.Field<bool>("is_remote") == false || dr.Field<string>("client_status_code") == "cm")
            {
                bImage = GetLocalPreview(dr);
            }
            else
            {

                NpgsqlCommand command;
                if (intVersionId != 0)
                {
                    // if specific version selected, load its image
                    command = new NpgsqlCommand("select preview_image from hp_version where entry_id=:entry_id and version_id=:version_id;", connDb);
                    command.Parameters.AddWithValue("entry_id", dr.Field<int>("entry_id"));
                    command.Parameters.AddWithValue("version_id", intVersionId);
                }
                else
                {
                    // otherwise, load the latest version image
                    command = new NpgsqlCommand("select preview_image from hp_version where entry_id=:entry_id order by version_id desc limit 1;", connDb);
                    command.Parameters.Add(new NpgsqlParameter("entry_id", NpgsqlTypes.NpgsqlDbType.Integer));
                    command.Parameters["entry_id"].Value = dr.Field<int>("entry_id");
                }

                try
                {
                    bImage = (byte[])command.ExecuteScalar();
                }
                catch
                {
                    // use the icon
                    bImage = ImageToByteArray(ilListIcons.Images[dr.Field<string>("file_ext")]);
                }

            }

            if (bImage == null) return;
            ms = new MemoryStream(bImage);
            pbPreview.Image = Image.FromStream(ms);

        }

        protected void PopulateHistoryPage(string FileName)
        {

            // clear list
            lvHistory.Clear();
            lvHistory.Columns.Add("Version", 50, System.Windows.Forms.HorizontalAlignment.Left);
            lvHistory.Columns.Add("ModUser", 140, System.Windows.Forms.HorizontalAlignment.Left);
            lvHistory.Columns.Add("ModDate", 140, System.Windows.Forms.HorizontalAlignment.Left);
            lvHistory.Columns.Add("Size", 75, System.Windows.Forms.HorizontalAlignment.Right);
            lvHistory.Columns.Add("RelDate", 75, System.Windows.Forms.HorizontalAlignment.Right);

            // new dataset
            DataSet dsTemp = new DataSet();

            // get entry_id
            DataRow dr = dsList.Tables[0].Select("entry_name='" + FileName + "'")[0];
            if (dr.Field<bool>("is_remote") == false) return;
            int intEntryId = dr.Field<int>("entry_id");

            // initialize sql command for history data
            string strSql = @"
                select
                    v.version_id,
                    v.entry_id,
                    pg_size_pretty(v.file_size) as version_size,
                    to_char(v.create_stamp, 'yyyy-MM-dd HH24:MI:SS') as action_date,
                    u.last_name || ', ' || u.first_name as action_user,
                    to_char(r.release_stamp, 'yyyy-MM-dd HH24:MI:SS') as release_stamp
                from hp_version as v
                left join hp_user as u on u.user_id=v.create_user
                left join (
                    select
                        rv.rel_version_id,
                        max(r.release_stamp) as release_stamp
                    from hp_release_version_rel as rv
                    left join hp_release as r on r.release_id=rv.rel_version_id
                    group by rv.rel_version_id
                ) as r on r.rel_version_id=v.version_id
                where v.entry_id=:entry_id
                order by action_date desc;
            ";

            // put the remote list in the DataSet
            NpgsqlDataAdapter daTemp = new NpgsqlDataAdapter(strSql, connDb);
            daTemp.SelectCommand.Parameters.Add(new NpgsqlParameter("entry_id", NpgsqlTypes.NpgsqlDbType.Integer));
            daTemp.SelectCommand.Parameters["entry_id"].Value = intEntryId;
            daTemp.Fill(dsTemp);

            // if we have no data to show, then quit
            if (dsTemp.Tables.Count == 0)
            {
                return;
            }

            foreach (DataRow row in dsTemp.Tables[0].Rows)
            {

                // build array
                string[] lvData = new string[5];
                lvData[0] = row.Field<int>("version_id").ToString();
                lvData[1] = row.Field<string>("action_user");
                lvData[2] = row.Field<string>("action_date");
                lvData[3] = row.Field<string>("version_size");
                lvData[4] = row.Field<string>("release_stamp");

                // create actual list item
                ListViewItem lvItem = new ListViewItem(lvData);
                lvHistory.Items.Add(lvItem);

            }

        }

        protected void PopulateParentsPage(string FileName)
        {

            // clear list
            lvParents.Clear();
            lvParents.Columns.Add("Version", 140, System.Windows.Forms.HorizontalAlignment.Left);
            lvParents.Columns.Add("Name", 300, System.Windows.Forms.HorizontalAlignment.Left);

            // new dataset
            DataSet dsTemp = new DataSet();

            // get entry_id
            DataRow dr = dsList.Tables[0].Select("entry_name='" + FileName + "'")[0];
            if (dr.Field<bool>("is_remote") == false) return;
            int intEntryId = dr.Field<int>("entry_id");

            // initialize sql command for history data
            string strSql = @"
                select
                    r.rel_parent_id,
                    r.rel_child_id,
                    e.entry_name
                from hp_version_relationship as r
                left join hp_version as vp on vp.version_id=r.rel_parent_id
                left join hp_version as vc on vc.version_id=r.rel_child_id
                left join hp_entry as e on e.entry_id=vp.entry_id
                where vc.entry_id=:entry_id
                order by r.rel_child_id desc, e.entry_name;
            ";

            // put the remote list in the DataSet
            NpgsqlDataAdapter daTemp = new NpgsqlDataAdapter(strSql, connDb);
            daTemp.SelectCommand.Parameters.Add(new NpgsqlParameter("entry_id", NpgsqlTypes.NpgsqlDbType.Integer));
            daTemp.SelectCommand.Parameters["entry_id"].Value = intEntryId;
            daTemp.Fill(dsTemp);

            // if we have no data to show, then quit
            if (dsTemp.Tables.Count == 0)
            {
                return;
            }

            foreach (DataRow row in dsTemp.Tables[0].Rows)
            {

                // build array
                string[] lvData = new string[2];
                lvData[0] = row.Field<int>("rel_child_id").ToString();
                lvData[1] = row.Field<string>("entry_name") + " (v" + row.Field<int>("rel_parent_id").ToString() + ")";

                // create actual list item
                ListViewItem lvItem = new ListViewItem(lvData);
                lvParents.Items.Add(lvItem);

            }

        }

        protected void PopulateChildrenPage(string FileName)
        {

            // clear list
            lvChildren.Clear();
            lvChildren.Columns.Add("Version", 50, System.Windows.Forms.HorizontalAlignment.Left);
            lvChildren.Columns.Add("Name", 350, System.Windows.Forms.HorizontalAlignment.Left);
            lvChildren.Columns.Add("PWA", 70, System.Windows.Forms.HorizontalAlignment.Left);
            lvChildren.Columns.Add("FullName", 120, System.Windows.Forms.HorizontalAlignment.Left);

            // new dataset
            DataSet dsTemp = new DataSet();

            // get data row
            DataRow dr = dsList.Tables[0].Select("entry_name='" + FileName + "'")[0];
            string strFullName = dr.Field<string>("absolute_path") + "\\" + FileName;

            if (dr.Field<bool>("is_remote") == false || dr.Field<string>("client_status_code") == "cm")
            {
                // get from SolidWorks
                List<string[]> lstDepends;
                if (blnUseSwApi)
                {
                    lstDepends = connSwApi.GetDependencies(strFullName);
                }
                else
                {
                    SWDocMgr docMgr = new SWDocMgr(strSWDocMgrKey);
                    lstDepends = docMgr.GetDependencies(strFullName);
                }

                if (lstDepends != null)
                {
                    dsTemp.Tables.Add();
                    dsTemp.Tables[0].Columns.Add("rel_parent_id", Type.GetType("System.Int32"));
                    dsTemp.Tables[0].Columns.Add("rel_child_id", Type.GetType("System.Int32"));
                    dsTemp.Tables[0].Columns.Add("entry_name", Type.GetType("System.String"));
                    dsTemp.Tables[0].Columns.Add("full_name", Type.GetType("System.String"));
                    dsTemp.Tables[0].Columns.Add("outside_pwa", Type.GetType("System.Boolean"));

                    foreach (string[] strDepends in lstDepends)
                    {
                        dsTemp.Tables[0].Rows.Add(
                                0,
                                0,
                                strDepends[0],
                                strDepends[1],
                                IsInPwa(strDepends[1])
                            );
                    }
                }

            }
            else
            {
                // get from db
                int intEntryId = dr.Field<int>("entry_id");

                // initialize sql command for history data
                string strSql = @"
                    select
                        r.rel_parent_id,
                        r.rel_child_id,
                        e.entry_name,
                        :absolute_path || '\' || e.entry_name as full_name,
                        false as outside_pwa
                    from hp_version_relationship as r
                    left join hp_version as vp on vp.version_id=r.rel_parent_id
                    left join hp_version as vc on vc.version_id=r.rel_child_id
                    left join hp_entry as e on e.entry_id=vc.entry_id
                    where vp.entry_id=:entry_id
                    order by r.rel_parent_id desc, e.entry_name;
                ";

                // put the remote list in the DataSet
                NpgsqlDataAdapter daTemp = new NpgsqlDataAdapter(strSql, connDb);
                daTemp.SelectCommand.Parameters.Add(new NpgsqlParameter("absolute_path", NpgsqlTypes.NpgsqlDbType.Varchar));
                daTemp.SelectCommand.Parameters["absolute_path"].Value = dr.Field<string>("absolute_path");
                daTemp.SelectCommand.Parameters.Add(new NpgsqlParameter("entry_id", NpgsqlTypes.NpgsqlDbType.Integer));
                daTemp.SelectCommand.Parameters["entry_id"].Value = intEntryId;
                daTemp.Fill(dsTemp);

            }

            // if we have no child data to show, then quit
            if (dsTemp.Tables.Count == 0)
            {
                return;
            }

            foreach (DataRow row in dsTemp.Tables[0].Rows)
            {

                // build array
                string[] lvData = new string[4];
                lvData[0] = row.Field<int>("rel_parent_id").ToString();
                lvData[1] = row.Field<string>("entry_name") + " (v" + row.Field<int>("rel_child_id").ToString() + ")";
                if (row.Field<Boolean>("outside_pwa"))
                {
                    lvData[2] = "OUTSIDE";
                }
                lvData[3] = row.Field<string>("full_name");

                // create actual list item
                ListViewItem lvItem = new ListViewItem(lvData);
                lvChildren.Items.Add(lvItem);

            }

        }

        protected void PopulatePropertiesPage(string FileName)
        {

            // clear list
            lvProperties.Clear();
            lvProperties.Columns.Add("Version", 50, System.Windows.Forms.HorizontalAlignment.Left);
            lvProperties.Columns.Add("Configuration", 100, System.Windows.Forms.HorizontalAlignment.Left);
            lvProperties.Columns.Add("Property", 140, System.Windows.Forms.HorizontalAlignment.Left);
            lvProperties.Columns.Add("Value", 400, System.Windows.Forms.HorizontalAlignment.Left);
            lvProperties.Columns.Add("Type", 140, System.Windows.Forms.HorizontalAlignment.Left);

            // new dataset
            DataSet dsTemp = new DataSet();

            // get entry_id
            DataRow dr = dsList.Tables[0].Select("entry_name='" + FileName + "'")[0];
            if (dr.Field<bool>("is_remote") == false) return;
            int intEntryId = dr.Field<int>("entry_id");

            // initialize sql command for history data
            string strSql = @"
                select
                    vp.version_id,
                    vp.config_name,
                    vp.prop_id,
                    p.prop_name,
                    p.prop_type,
                    vp.text_value,
                    vp.date_value,
                    vp.number_value,
                    vp.yesno_value
                from hp_version_property as vp
                left join hp_property as p on p.prop_id=vp.prop_id
                left join hp_version as v on v.version_id=vp.version_id
                where v.entry_id=:entry_id
                order by vp.version_id desc, vp.config_name, p.prop_name;
            ";

            // put the remote list in the DataSet
            NpgsqlDataAdapter daTemp = new NpgsqlDataAdapter(strSql, connDb);
            daTemp.SelectCommand.Parameters.Add(new NpgsqlParameter("entry_id", NpgsqlTypes.NpgsqlDbType.Integer));
            daTemp.SelectCommand.Parameters["entry_id"].Value = intEntryId;
            daTemp.Fill(dsTemp);

            // if we have no data to show, then quit
            if (dsTemp.Tables.Count == 0)
            {
                return;
            }

            foreach (DataRow row in dsTemp.Tables[0].Rows)
            {

                // get property value
                string strPropField = row.Field<string>("prop_type") + "_value";
                string strValue = "";
                var varValue = row[strPropField];
                if (varValue != null) strValue = varValue.ToString();

                // build array
                string[] lvData = new string[5];
                lvData[0] = row.Field<int>("version_id").ToString();
                lvData[1] = row.Field<string>("config_name");
                lvData[2] = row.Field<string>("prop_name");
                lvData[3] = strValue;
                lvData[4] = row.Field<string>("prop_type");

                // create actual list item
                ListViewItem lvItem = new ListViewItem(lvData);
                lvProperties.Items.Add(lvItem);

            }

        }


        #region utility functions

        private Boolean IsInPwa(string FullName)
        {
            // terminate paths to prevent matching with paths that begin the same as strLocalFileRoot
            string strRootTest = strLocalFileRoot.ToUpper() + "\\";
            string strPathTest = (FullName.EndsWith("\\") ? FullName + "\\" : FullName + "\\");

            // only return a value if this path is in strLocalFileRoot
            if (strPathTest.Length < strRootTest.Length) return false;
            if (!strPathTest.ToUpper().StartsWith(strRootTest))
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private byte[] GetLocalPreview(DataRow dr)
        {

            IconFromFile ico = new IconFromFile();
            Bitmap bmpImage;
            byte[] bImage;
            string strFullName = dr.Field<string>("absolute_path") + "\\" + dr.Field<string>("entry_name");
            string strFileExt = dr.Field<string>("file_ext").ToLower();

            if (isSolidWorks(strFullName))
            {
                //SWDocMgr docMgr = new SWDocMgr(strSWDocMgrKey);
                //stdole.IPictureDisp ipicPreview = docMgr.GetPreview(strFullName);
                //bmpImage = (Bitmap)imgConv.GetPicture(ipicPreview);

                //bImage = docMgr.GetPreview3(strFullName);

                bmpImage = (Bitmap)docMgr.GetPreview(strFullName);
                bImage = ImageToByteArray(bmpImage);
            }
            else
            {
                bmpImage = (Bitmap)ico.GetThumbnail(strFullName);
                bImage = ImageToByteArray(bmpImage);
                // try to get preview image from other compound document
            }

            return bImage;

        }

        private DataTable CreateFileTable()
        {

            // entry_id
            // version_id
            // dir_id
            // entry_name
            // type_id
            // file_ext
            // cat_id
            // cat_name
            // latest_size
            // str_latest_size
            // local_size
            // str_local_size
            // latest_stamp
            // str_latest_stamp
            // local_stamp
            // str_local_stamp
            // latest_md5
            // local_md5
            // checkout_user
            // ck_user_name
            // checkout_date
            // str_checkout_date
            // checkout_node
            // checkout_node_name
            // is_local
            // is_remote
            // client_status_code
            // relative_path
            // absolute_path
            // icon
            // is_depend_searched
            // is_readonly
            // active
            // destroyed

            DataTable dtList = new DataTable();
            dtList.Columns.Add("entry_id", Type.GetType("System.Int32"));
            dtList.Columns.Add("version_id", Type.GetType("System.Int32"));
            dtList.Columns.Add("dir_id", Type.GetType("System.Int32"));
            dtList.Columns.Add("entry_name", Type.GetType("System.String"));
            dtList.Columns.Add("type_id", Type.GetType("System.Int32"));
            dtList.Columns.Add("file_ext", Type.GetType("System.String"));
            dtList.Columns.Add("cat_id", Type.GetType("System.Int32"));
            dtList.Columns.Add("cat_name", Type.GetType("System.String"));
            dtList.Columns.Add("latest_size", Type.GetType("System.Int64"));
            dtList.Columns.Add("str_latest_size", Type.GetType("System.String"));
            dtList.Columns.Add("local_size", Type.GetType("System.Int64"));
            dtList.Columns.Add("str_local_size", Type.GetType("System.String"));
            dtList.Columns.Add("latest_stamp", Type.GetType("System.DateTime"));
            dtList.Columns.Add("str_latest_stamp", Type.GetType("System.String"));
            dtList.Columns.Add("local_stamp", Type.GetType("System.DateTime"));
            dtList.Columns.Add("str_local_stamp", Type.GetType("System.String"));
            dtList.Columns.Add("latest_md5", Type.GetType("System.String"));
            dtList.Columns.Add("local_md5", Type.GetType("System.String"));
            dtList.Columns.Add("checkout_user", Type.GetType("System.Int32"));
            dtList.Columns.Add("ck_user_name", Type.GetType("System.String"));
            dtList.Columns.Add("checkout_date", Type.GetType("System.DateTime"));
            dtList.Columns.Add("str_checkout_date", Type.GetType("System.String"));
            dtList.Columns.Add("checkout_node", Type.GetType("System.Int32"));
            dtList.Columns.Add("checkout_node_name", Type.GetType("System.String"));
            dtList.Columns.Add("is_local", Type.GetType("System.Boolean"));
            dtList.Columns.Add("is_remote", Type.GetType("System.Boolean"));
            dtList.Columns.Add("client_status_code", Type.GetType("System.String"));
            dtList.Columns.Add("relative_path", Type.GetType("System.String"));
            dtList.Columns.Add("absolute_path", Type.GetType("System.String"));
            dtList.Columns.Add("icon", typeof(Byte[]));
            dtList.Columns.Add("is_depend_searched", Type.GetType("System.Boolean"));
            dtList.Columns.Add("is_readonly", Type.GetType("System.Boolean"));
            dtList.Columns.Add("active", Type.GetType("System.Boolean"));
            dtList.Columns.Add("destroyed", Type.GetType("System.Boolean"));
            dtList.TableName = "files";
            return dtList;

        }

        public Image ImageOverlay(Image imgOrig, Image imgOverlay)
        {
            Bitmap bitmap = new Bitmap(32, 32);
            Graphics canvas = Graphics.FromImage(bitmap);
            canvas.DrawImage(imgOrig, new Point(0, 0));
            canvas.DrawImage(imgOverlay, new Point(0, 0));
            canvas.Save();
            return Image.FromHbitmap(bitmap.GetHbitmap());
        }

        private static byte[] ImageToByteArray(System.Drawing.Image imageIn)
        {
            if (imageIn == null) return null;
            using (MemoryStream ms = new MemoryStream())
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

        private Image ByteArrayToImage(byte[] byteArrayIn)
        {
            if (byteArrayIn == null) return null;
            using (MemoryStream ms = new MemoryStream(byteArrayIn))
            {
                Image returnImage = Image.FromStream(ms);
                return returnImage;
            }
        }

        //protected string GetAbsolutePath(string stringPath) {
        //    //Get Full path
        //    string stringParse = "";
        //    if (stringPath.Substring(0, 3) == "pwa")
        //    {
        //        //replace pwa with actual root path
        //        stringParse = strLocalFileRoot + stringPath.Substring(3);
        //    } 
        //    else
        //    {
        //        // TODO:  This assumes that the path is absolute already if it doesn't start with "pwa"...
        //        stringParse = stringPath;
        //    }
        //    return stringParse;
        //}

        //protected string GetRelativePath(string stringPath) {
        //    // get tree path
        //    string stringParse = "";
        //    // replace actual root path with pwa
        //    if (stringPath.IndexOf(strLocalFileRoot, 0, StringComparison.CurrentCultureIgnoreCase) != -1)
        //    {
        //        stringParse = "pwa" + stringPath.Substring(strLocalFileRoot.Length);
        //    }
        //    return stringParse;
        //}

        //protected string GetShortName(string FullName) {
        ////    return FullName.Substring(FullName.LastIndexOf("\\") + 1);
        //    return Utils.GetBaseName(FullName);
        //}

        ////protected string GetFileExt(string strFileName) {
        ////    //Get Name of folder
        ////    string[] strSplit = strFileName.Split('.');
        ////    int _maxIndex = strSplit.Length-1;
        ////    return strSplit[_maxIndex];
        ////}

        //protected string FormatDate(DateTime dtDate) {

        //    // if file not in local current day light saving time, then add an hour?
        //    if (TimeZone.CurrentTimeZone.IsDaylightSavingTime(dtDate) == false) {
        //        dtDate = dtDate.AddHours(1);
        //    }

        //    // get date and time in short format and return it
        //    string stringDate = "";
        //    //stringDate = dtDate.ToShortDateString().ToString() + " " + dtDate.ToShortTimeString().ToString();
        //    stringDate = dtDate.ToString("yyyy-MM-dd HH:mm:ss");
        //    return stringDate;

        //}

        //protected string FormatSize(Int64 lSize)
        //{
        //    //Format number to KB
        //    string stringSize = "";
        //    NumberFormatInfo myNfi = new NumberFormatInfo();

        //    Int64 lKBSize = 0;

        //    if (lSize < 1024 )
        //    {
        //        if (lSize == 0)
        //        {
        //            //zero byte
        //            stringSize = "0";
        //        }
        //        else
        //        {
        //            //less than 1K but not zero byte
        //            stringSize = "1";
        //        }
        //    }
        //    else
        //    {
        //        //convert to KB
        //        lKBSize = lSize / 1024;
        //        //format number with default format
        //        stringSize = lKBSize.ToString("n",myNfi);
        //        //remove decimal
        //        stringSize = stringSize.Replace(".00", "");
        //    }

        //    return stringSize + " KB";

        //}

        //static string StringMD5(string FileName)
        //{
        //    // get local file checksum
        //    using (var md5 = MD5.Create())
        //    {
        //        //using (var stream = File.OpenRead(FileName))
        //        using (var stream = File.Open(FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        //        {
        //            return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
        //        }
        //    }
        //}

        protected TreeNode FindNode(string strPath)
        {
            TreeNode tnMatch;
            dictTreeNodes.TryGetValue(strPath, out tnMatch);
            return tnMatch;
        }

        protected virtual bool IsFileLocked(FileInfo file)
        {

            FileStream stream = null;

            if (file.Exists)
            {
                try
                {
                    stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
                }
                catch (IOException)
                {
                    //the file is unavailable because it is:
                    //still being written to
                    //or being processed by another thread
                    //or does not exist (has already been processed)
                    return true;
                }
                finally
                {
                    if (stream != null) stream.Close();
                }
            }

            //file is not locked
            return false;

        }

        private Image GetLocalIcon(string FileName)
        {

            IconFromFile icoHelper = new IconFromFile();
            Icon ico = icoHelper.GetLargeFileIcon(FileName);

            if (ico == null || ico.Size.IsEmpty == true)
            {
                return ilListIcons.Images["unknown"];
            }
            else
            {
                return (Image)ico.ToBitmap();
            }

        }

        private static bool isSolidWorks(string fileName)
        {
            string solidworksFilter = ".sldprt,.sldasm,.slddrw";
            string ext = Path.GetExtension(fileName).ToLower();
            if (ext == "")
                return false;
            return (solidworksFilter.IndexOf(ext) != -1 && File.Exists(fileName));
        }

        #endregion


        #region thread worker methods

        // worker tasks
        // - Get Latest
        // - Check Out
        // - Commit
        // - Undo Check Out
        void worker_GetLatest(object sender, DoWorkEventArgs e)
        {

            bool blnFailed = false;

            BackgroundWorker myWorker = sender as BackgroundWorker;
            dlgStatus.AddStatusLine("INFO", "Starting worker");

            // get arguments
            List<object> genericlist = e.Argument as List<object>;
            NpgsqlTransaction t = (NpgsqlTransaction)genericlist[0];
            string strBasePath = (string)genericlist[1];
            List<int> lstSelected = (List<int>)genericlist[2];

            // refresh file data in the currently selected directory
            LoadListData(strBasePath);

            // load fetch data
            DataSet dsFetches = LoadFetchData(sender, e, t, strBasePath, lstSelected);
            DataRow[] drBads;

            // check for cancellation
            if ((myWorker.CancellationPending == true))
            {
                e.Cancel = true;
                return;
            }

            // check for files modified, but not checked out
            // TODO: handle conflicts by making copies of the conflicting files and letting the user resolve the conflict
            //  - theirs.<filename>
            //  - ours.<filename>
            drBads = dsFetches.Tables["files"].Select("str_local_stamp>str_latest_stamp and client_status_code<>'cm'");
            foreach (DataRow drBad in drBads)
            {
                string strFullName = drBad.Field<string>("absolute_path") + "\\" + drBad.Field<string>("entry_name");
                dlgStatus.AddStatusLine("ERROR", "File has been modified, but is not checked out: " + strFullName);
                blnFailed = true;
            }
            if (blnFailed)
            {
                var result = MessageBox.Show("Files have been modified locally, and will be overwritten.\nOverwrite and continue?",
                    "Overwriting Locally Modified Files",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Error,
                    MessageBoxDefaultButton.Button2);
                switch (result)
                {
                    case System.Windows.Forms.DialogResult.Cancel:
                        blnFailed = true;
                        e.Cancel = true;
                        return;
                    case System.Windows.Forms.DialogResult.Yes:
                        blnFailed = false;
                        break;
                    default:
                        blnFailed = true;
                        e.Cancel = true;
                        return;
                }
            }

            // check for cancellation
            if ((myWorker.CancellationPending == true))
            {
                e.Cancel = true;
                return;
            }

            // check for files writeable, but not checked out
            drBads = dsFetches.Tables["files"].Select("str_latest_stamp=str_local_stamp and is_local=true and is_remote=true and is_readonly=false and client_status_code<>'cm'");
            foreach (DataRow drBad in drBads)
            {
                string strFullName = drBad.Field<string>("absolute_path") + "\\" + drBad.Field<string>("entry_name");
                dlgStatus.AddStatusLine("WARNING", "File is writeable, but is not checked out: " + strFullName);
            }

            // check for cancellation
            if ((myWorker.CancellationPending == true))
            {
                e.Cancel = true;
                return;
            }

            // get files to be updated
            // files that are both local and remote
            // having differing timestamps
            // not checked out to me on this node
            // this will include newer remote versions (nv), and locally modified but not checked out to me (lm)
            // this may also include checked-out-to-other
            // TODO: don't everwrite files checkout out to other, but in the same pwa
            //DataRow[] drUpdateFiles = dsFetches.Tables["files"].Select("client_status_code in ('lm','nv')");
            DataRow[] drUpdateFiles = dsFetches.Tables["files"].Select("is_local=true and is_remote=true and str_latest_stamp <> str_local_stamp and client_status_code <> 'cm'");
            int intUpdateCount = drUpdateFiles.Length;

            // notify all up-to-date
            if (intUpdateCount < 1)
            {
                dlgStatus.AddStatusLine("INFO", "All selected files are already up to date");
            }

            // check for files locked, but needing update
            for (int i = 0; i < intUpdateCount; i++)
            {
                DataRow drCurrent = drUpdateFiles[i];

                // check for cancellation
                if ((myWorker.CancellationPending == true))
                {
                    e.Cancel = true;
                    return;
                }

                string strFileName = drCurrent.Field<string>("entry_name");
                string strAbsolutePath = drCurrent.Field<string>("absolute_path");
                string strFullName = strAbsolutePath + "\\" + strFileName;
                FileInfo fiCurrFile = new FileInfo(strFullName);

                // report status
                dlgStatus.AddStatusLine("INFO", "Checking for write access (" + (i + 1).ToString() + " of " + intUpdateCount.ToString() + "): " + strFileName);

                // check if file is writeable
                if (IsFileLocked(fiCurrFile) == true)
                {
                    // file is in use: don't continue
                    dlgStatus.AddStatusLine("ERROR", "File is locked. Can't update: " + strFullName);
                    blnFailed = true;
                }

            }

            if (blnFailed)
            {
                return;
            }

            for (int i = 0; i < intUpdateCount; i++)
            {
                DataRow drCurrent = drUpdateFiles[i];

                // check for cancellation
                if ((myWorker.CancellationPending == true))
                {
                    e.Cancel = true;
                    return;
                }

                string strFileName = drCurrent.Field<string>("entry_name");
                string strAbsolutePath = drCurrent.Field<string>("absolute_path");
                string strFullName = strAbsolutePath + "\\" + strFileName;
                FileInfo fiCurrFile = new FileInfo(strFullName);

                // report status
                if (drCurrent.Field<string>("client_status_code") == "lm")
                {
                    dlgStatus.AddStatusLine("WARNING", "Overwriting locally modified file (" + (i + 1).ToString() + " of " + intUpdateCount.ToString() + "): " + strFileName);
                }
                else
                {
                    dlgStatus.AddStatusLine("INFO", "Updating file (" + (i + 1).ToString() + " of " + intUpdateCount.ToString() + "): " + strFileName);
                }

                // name and download the file
                // webdav is case sensitive, so the server should be forced to all lower case
                int intEntryId = (int)drCurrent["entry_id"];
                int intVersionId = (int)drCurrent["version_id"];
                string strFileExt = drCurrent.Field<string>("file_ext");
                string strDavName = "/" + intEntryId.ToString() + "/" + intVersionId.ToString() + "." + strFileExt.ToLower();

                // set the file not readonly
                fiCurrFile.IsReadOnly = false;

                // report status and stream file
                string strFileSize = drCurrent.Field<string>("str_latest_size");
                dlgStatus.AddStatusLine("INFO", "Retrieving Content (" + strFileSize + "): " + strDavName);
                connDav.Download(strDavName, strFullName);

                // webdav does not handle file time stamps
                // set last write time from server data
                File.SetLastWriteTime(fiCurrFile.FullName, drCurrent.Field<DateTime>("latest_stamp"));
                File.SetCreationTime(fiCurrFile.FullName, drCurrent.Field<DateTime>("latest_stamp"));

                // set the file readonly
                fiCurrFile.IsReadOnly = true;

                // report status
                dlgStatus.AddStatusLine("INFO", "File transfer complete: " + strFileName);

            }

            // get and create files new to this node
            DataRow[] drNewFiles = dsFetches.Tables["files"].Select("client_status_code='ro'", "absolute_path asc");
            int intNewCount = drNewFiles.Length;
            for (int i = 0; i < intNewCount; i++)
            {
                DataRow drCurrent = drNewFiles[i];

                // check for cancellation
                if ((myWorker.CancellationPending == true))
                {
                    e.Cancel = true;
                    return;
                }

                string strFileName = drCurrent.Field<string>("entry_name");
                string strAbsolutePath = drCurrent.Field<string>("absolute_path");
                string strFullName = strAbsolutePath + "\\" + strFileName;

                // create a directory, if necessary
                if (!Directory.Exists(strAbsolutePath))
                {
                    Directory.CreateDirectory(strAbsolutePath);
                }
                FileInfo fiCurrFile = new FileInfo(strFullName);

                // report status
                dlgStatus.AddStatusLine("INFO", "Downloading new file (" + (i + 1).ToString() + " of " + intNewCount.ToString() + "): " + strFileName);

                // name and download the file
                int intEntryId = (int)drCurrent["entry_id"];
                int intVersionId = (int)drCurrent["version_id"];
                string strFileExt = drCurrent.Field<string>("file_ext");
                string strDavName = "/" + intEntryId.ToString() + "/" + intVersionId.ToString() + "." + strFileExt.ToLower();

                // report status and stream file
                string strFileSize = drCurrent.Field<string>("str_latest_size");
                dlgStatus.AddStatusLine("INFO", "Retrieving Content (" + strFileSize + "): " + strDavName);
                connDav.Download(strDavName, strFullName);

                // webdav does not handle file time stamps
                // set last write time from server data
                File.SetLastWriteTime(fiCurrFile.FullName, drCurrent.Field<DateTime>("latest_stamp"));
                File.SetCreationTime(fiCurrFile.FullName, drCurrent.Field<DateTime>("latest_stamp"));

                // set the file readonly
                fiCurrFile.IsReadOnly = true;

                // report status
                dlgStatus.AddStatusLine("INFO", "File transfer complete:" + strFileName);

            }

            // don't commit the transaction, because getting latest doesn't change the database
            // why are we even using a transaction here?
            // TODO: don't use a transaction for GetLatest
            //t.Commit();

        }

        void worker_Commit(object sender, DoWorkEventArgs e)
        {

            BackgroundWorker myWorker = sender as BackgroundWorker;
            dlgStatus.AddStatusLine("INFO", "Starting Commit worker");

            // get arguments
            List<object> genericlist = e.Argument as List<object>;
            NpgsqlTransaction t = (NpgsqlTransaction)genericlist[0];
            string strRelBasePath = (string)genericlist[1];
            List<string> lstSelectedNames = (List<string>)genericlist[2];

            // get commits data
            DataSet dsCommits = LoadCommitsData(sender, e, t, strRelBasePath, lstSelectedNames);

            // log files outside PWA
            DataRow[] drNonPwaFiles = dsCommits.Tables["files"].Select(String.Format("client_status_code<>'if' and (absolute_path + '\\') not like '{0}\\%'", strLocalFileRoot.Replace("'", "''")));
            foreach (DataRow drCurrent in drNonPwaFiles)
            {
                // check for cancellation
                if ((myWorker.CancellationPending == true))
                {
                    e.Cancel = true;
                    return;
                }

                dlgStatus.AddStatusLine("WARNING", String.Format("File outside PWA: {0}\\{1}", drCurrent.Field<string>("absolute_path"), drCurrent.Field<string>("entry_name")));
            }

            // log blocked files
            DataRow[] drBlockedFiles = dsCommits.Tables["files"].Select(String.Format("client_status_code='if' and (absolute_path + '\\') like '{0}\\%'", strLocalFileRoot.Replace("'", "''")));
            foreach (DataRow drCurrent in drBlockedFiles)
            {
                // check for cancellation
                if ((myWorker.CancellationPending == true))
                {
                    e.Cancel = true;
                    return;
                }

                dlgStatus.AddStatusLine("INFO", "File is blocked: " + drCurrent.Field<string>("entry_id"));
            }

            // check for files with no remote file type
            DataRow[] drNoRemFiles = dsCommits.Tables["files"].Select("client_status_code<>'if' and client_status_code='ft'");
            foreach (DataRow drCurrent in drNoRemFiles)
            {
                // check for cancellation
                if ((myWorker.CancellationPending == true))
                {
                    e.Cancel = true;
                    return;
                }

                dlgStatus.AddStatusLine("INFO", "File has no remote type: " + drCurrent.Field<string>("entry_id"));
            }
            if (drNoRemFiles.Length != 0)
            {
                throw new System.Exception("Files with no remote file type " + drNoRemFiles.Length.ToString());
            }

            // check for files over 2GB
            DataRow[] drBigFiles = dsCommits.Tables["files"].Select(String.Format("client_status_code<>'if' and local_size>{0} and absolute_path like '{1}\\%'", lngMaxFileSize, strLocalFileRoot.Replace("'", "''")));
            foreach (DataRow drCurrent in drBigFiles)
            {
                // check for cancellation
                if ((myWorker.CancellationPending == true))
                {
                    e.Cancel = true;
                    return;
                }

                dlgStatus.AddStatusLine("ERROR", String.Format("File is too big: {0} ({1})", drCurrent.Field<string>("entry_id"), drCurrent.Field<string>("str_local_size")));
            }
            if (drBigFiles.Length != 0)
            {
                throw new System.Exception("Cannot commit files larger than 2GB: " + drBigFiles.Length.ToString() + " files found");
            }

            // check for write access
            DataRow[] drNewFiles = dsCommits.Tables["files"].Select(String.Format("client_status_code<>'if' and (absolute_path + '\\') like '{0}\\%'", strLocalFileRoot.Replace("'", "''")));
            Int32 intNewCount = drNewFiles.Length;
            for (int i = 0; i < drNewFiles.Length; i++)
            {
                DataRow drCurrent = drNewFiles[i];

                // check for cancellation
                if ((myWorker.CancellationPending == true))
                {
                    e.Cancel = true;
                    return;
                }

                string strFileName = drCurrent.Field<string>("entry_name");
                string strAbsolutePath = drCurrent.Field<string>("absolute_path");
                string strFullName = strAbsolutePath + "\\" + strFileName;
                FileInfo fiCurrFile = new FileInfo(strFullName);

                // report status
                dlgStatus.AddStatusLine("INFO", "Checking for write access (" + (i + 1).ToString() + " of " + drNewFiles.Length.ToString() + "): " + strFullName);

                // check if file is writeable
                if (IsFileLocked(fiCurrFile) == true)
                {
                    // file is in use: don't continue
                    //throw new System.Exception("File \"" + fiCurrFile.Name + "\" is locked.  Release it first.");
                    //return;
                }

            }

            // start commiting data
            bool blnFailed = false;

            // add remote directories
            if (blnFailed == false)
            {
                blnFailed = blnFailed || AddRemoteDirs(sender, e, t, ref dsCommits);
            }

            // add remote files
            if (blnFailed == false)
            {
                blnFailed = blnFailed || AddNewVersions(sender, e, t, ref dsCommits);
            }

            // add remote relationships (file dependencies)
            if (blnFailed == false)
            {
                blnFailed = blnFailed || AddVersionDepends(sender, e, t, ref dsCommits);
            }

            // clear checkout data
            if (blnFailed == false)
            {

                // get files that were checked out to me
                DataRow[] drCheckins = dsCommits.Tables["files"].Select("client_status_code='cm'");
                int intRowCount = drCheckins.Length;
                if (intRowCount != 0)
                {

                    string strEntryIds = "";
                    foreach (DataRow dr in drCheckins)
                    {
                        strEntryIds += dr.Field<int>("entry_id").ToString() + ",";
                    }
                    strEntryIds = strEntryIds.Remove(strEntryIds.Length - 1); // remove trailing comma

                    dlgStatus.AddStatusLine("INFO", "Clearing checkout data on " + intRowCount.ToString() + " files");
                    string strSql;
                    strSql = @"
                        update hp_entry
                        set
                            checkout_user=null,
                            checkout_date=null,
                            checkout_node=null
                        where checkout_user=:myuser
                        and checkout_node=:mynode
                        and entry_id in (" + strEntryIds + ");";
                    NpgsqlCommand cmdUpdateEntry = new NpgsqlCommand(strSql, connDb, t);
                    cmdUpdateEntry.Parameters.AddWithValue("myuser", intMyUserId);
                    cmdUpdateEntry.Parameters.AddWithValue("mynode", intMyNodeId);
                    int intUpdateCount = cmdUpdateEntry.ExecuteNonQuery();

                    if (intUpdateCount != intRowCount)
                    {
                        // throw warning, and continue
                        dlgStatus.AddStatusLine("WARNING", "Cleared checkout data for " + (intRowCount - intUpdateCount).ToString() + " files; expected " + intRowCount.ToString() + " files");
                    }

                }

            }

            // commit to database and set files ReadOnly
            if (blnFailed == true)
            {
                t.Rollback();

                // TODO: figure out how to rollback WebDav changes
                // we could just look for orphaned ids on the webdav server, and then call methods to delete those files

                throw new System.Exception("Operation failed. Rolling back the database transaction.");
            }
            else
            {
                t.Commit();

                // set the local files readonly
                for (int i = 0; i < intNewCount; i++)
                {

                    DataRow drCurrent = drNewFiles[i];

                    string strFileName = drCurrent.Field<string>("entry_name");
                    string strFilePath = drCurrent.Field<string>("absolute_path");
                    string strFullName = strFilePath + "\\" + strFileName;
                    FileInfo fiCurrFile = new FileInfo(strFullName);
                    if (fiCurrFile.Exists)
                    {
                        dlgStatus.AddStatusLine("INFO", "Setting file ReadOnly (" + (i + 1).ToString() + " of " + intNewCount.ToString() + "): " + strFileName);
                        try
                        {
                            fiCurrFile.IsReadOnly = true;
                        }
                        catch (Exception ex)
                        {
                            dlgStatus.AddStatusLine("WARNING", "Failed to set file \"" + fiCurrFile.Name + "\" to readonly." + System.Environment.NewLine + ex.ToString());
                        }
                    }
                    else
                    {
                        dlgStatus.AddStatusLine("WARNING", "File doesn't exist locally, can't set ReadOnly (" + (i + 1).ToString() + " of " + intNewCount.ToString() + "): " + strFileName);
                    }

                } // end for

            }

        }

        void worker_CheckOut(object sender, DoWorkEventArgs e)
        {

            // TODO: make this method work for tree checkout (not yet implemented)

            // get latest versions, including dependencies
            worker_GetLatest(sender, e);
            if (e.Cancel)
            {
                return;
            }

            BackgroundWorker myWorker = sender as BackgroundWorker;
            dlgStatus.AddStatusLine("INFO", "Beginning checkout worker");

            // get arguments
            List<object> genericlist = e.Argument as List<object>;
            NpgsqlTransaction t = (NpgsqlTransaction)genericlist[0];
            string strRelBasePath = (string)genericlist[1];
            List<int> lstSelected = (List<int>)genericlist[2];

            // refresh file data in the currently selected directory
            LoadListData(strRelBasePath);

            // concatenate selected entries into a comma separated string
            string strSelected = String.Join(",", lstSelected.ToArray());

            DataRow[] drBads;

            // warn checked-out-by-other
            drBads = dsList.Tables[0].Select("client_status_code='co' and entry_id in (" + strSelected + ")");
            foreach (DataRow drBad in drBads)
            {
                string strFullName = drBad.Field<string>("absolute_path") + "\\" + drBad.Field<string>("entry_name");
                dlgStatus.AddStatusLine("INFO", "File is checked out by another user: " + strFullName);
            }

            // warn checked-out-by-me
            drBads = dsList.Tables[0].Select("client_status_code='cm'  and entry_id in (" + strSelected + ")");
            foreach (DataRow drBad in drBads)
            {
                string strFullName = drBad.Field<string>("absolute_path") + "\\" + drBad.Field<string>("entry_name");
                dlgStatus.AddStatusLine("INFO", "File is already checked out to you: " + strFullName);
            }

            // get comma separated list of allowed files
            DataRow[] drAllowed = dsList.Tables[0].Select("client_status_code in ('ok','ro') and entry_id in (" + strSelected + ")");
            int intRowCount = drAllowed.Length;
            if (intRowCount == 0)
            {
                throw new System.Exception("None of the selected files can be checked out");
            }

            // concatenate allowed entries into a comma separated string
            string strAllowed = "";
            foreach (DataRow dr in drAllowed)
            {
                strAllowed += dr.Field<int>("entry_id").ToString() + ",";
            }
            strAllowed = strAllowed.Remove(strAllowed.Length - 1); // remove trailing comma

            // prepare to checkout file
            string strSql = @"
                    update hp_entry
                    set
                        checkout_user=:user_id,
                        checkout_date=now(),
                        checkout_node=:node_id
                    where entry_id in ({0});
                ";
            strSql = String.Format(strSql, strAllowed);
            NpgsqlCommand cmdCheckOut = new NpgsqlCommand(strSql, connDb, t);
            cmdCheckOut.Parameters.Add(new NpgsqlParameter("user_id", NpgsqlTypes.NpgsqlDbType.Integer));
            cmdCheckOut.Parameters.Add(new NpgsqlParameter("node_id", NpgsqlTypes.NpgsqlDbType.Integer));

            // checkout
            cmdCheckOut.Parameters["user_id"].Value = intMyUserId;
            cmdCheckOut.Parameters["node_id"].Value = intMyNodeId;

            int intRows = 0;
            try
            {
                intRows = cmdCheckOut.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                dlgStatus.AddStatusLine("ERROR", "Failed to set checkout info: user_id=" + intMyUserId.ToString() + ", node_id=" + intMyNodeId.ToString());
                dlgStatus.AddStatusLine("ERROR", "Message was: " + ex.ToString());

                dlgStatus.AddStatusLine("DEBUG", "user_id=" + intMyUserId.ToString());
                dlgStatus.AddStatusLine("DEBUG", "node_id=" + intMyNodeId.ToString());
                dlgStatus.AddStatusLine("DEBUG", "strAllowed=" + strAllowed);
            }

            if (intRows == drAllowed.Length)
            {
                foreach (DataRow drRow in drAllowed)
                {
                    string strFullName = drRow.Field<string>("absolute_path") + "\\" + drRow.Field<string>("entry_name");
                    dlgStatus.AddStatusLine("INFO", "File checkout info set: " + strFullName);
                }
                t.Commit();
            }
            else
            {
                dlgStatus.AddStatusLine("WARNING", "Failed to checkout " + (drAllowed.Length - intRows).ToString() + " of " + drAllowed.Length + " files" + Environment.NewLine + "We already got latest versions, but we won't checkout");
                t.Rollback();
            }

            // set the local files writeable
            for (int i = 0; i < intRowCount; i++)
            {

                DataRow drCurrent = drAllowed[i];

                string strFileName = drCurrent.Field<string>("entry_name");
                string strFilePath = drCurrent.Field<string>("absolute_path");
                string strFullName = strFilePath + "\\" + strFileName;
                FileInfo fiCurrFile = new FileInfo(strFullName);
                dlgStatus.AddStatusLine("INFO", "Setting file Writeable (" + (i + 1).ToString() + " of " + intRowCount.ToString() + "): " + strFileName);
                try
                {
                    fiCurrFile.IsReadOnly = false;
                }
                catch (Exception ex)
                {
                    dlgStatus.AddStatusLine("WARNING", "Failed to set file \"" + fiCurrFile.Name + "\" to writeable." + System.Environment.NewLine + ex.ToString());
                }

            } // end for

        }

        void worker_UndoCheckout(object sender, DoWorkEventArgs e)
        {

            // this method will be called from the tree view or list view
            // that means it needs to handle directory traversing as well as a selected file list

            // required steps
            //  - get files to be undo-checkout-ed
            //  - update hp_entry table, setting checkout_* fields to null
            //  - download server version of the files, overwriting our local copies
            //   - use worker_GetLatest
            //   - this has the noteable side effect that, when executed on a directory, it downloads files new to this node
            //   - worker_GetLatest sets files readonly, so we don't need to do that here

            BackgroundWorker myWorker = sender as BackgroundWorker;
            dlgStatus.AddStatusLine("INFO", "Beginning undo checkout worker");

            // get arguments
            List<object> genericlist = e.Argument as List<object>;
            NpgsqlTransaction t = (NpgsqlTransaction)genericlist[0];
            string strRelBasePath = (string)genericlist[1];
            List<int> lstSelectedIds = (List<int>)genericlist[2];

            string strEntries = "";
            int intRowCount;
            DataRow[] drSelected;
            if (lstSelectedIds != null)
            {
                // get selected file data
                strEntries = String.Join(",", lstSelectedIds.ToArray());
                drSelected = dsList.Tables[0].Select("client_status_code='cm' and entry_id in (" + strEntries + ")");
            }
            else
            {
                // we don't want to get commits data and use it to undo checkouts i.e. LoadCommitsData()
                // that's too heavy
                // maybe we should make our own lighter method for this

                // get data from selected directory
                DataSet dsUndos = LoadCommitsData(sender, e, t, strRelBasePath, null);
                drSelected = dsUndos.Tables["files"].Select("client_status_code='cm'");
                foreach (DataRow dr in drSelected)
                {
                    strEntries += dr.Field<int>("entry_id").ToString() + ",";
                }
                strEntries = strEntries.Remove(strEntries.Length - 1); // remove trailing comma
            }
            intRowCount = drSelected.Length;
            if (intRowCount == 0)
            {
                dlgStatus.AddStatusLine("WARNING", "No checked-out files were found");
                e.Cancel = true;
                return;
            }

            // TODO: think of some conflicts to test for

            // clear checkout data on selected files
            dlgStatus.AddStatusLine("INFO", "Clearing checkout data on selected files");
            string strSql;
            strSql = @"
                update hp_entry
                set
                    checkout_user=null,
                    checkout_date=null,
                    checkout_node=null
                where checkout_user=:myuser
                and checkout_node=:mynode
                and entry_id in (" + strEntries + ");";
            NpgsqlCommand cmdUpdateEntry = new NpgsqlCommand(strSql, connDb, t);
            cmdUpdateEntry.Parameters.AddWithValue("myuser", intMyUserId);
            cmdUpdateEntry.Parameters.AddWithValue("mynode", intMyNodeId);
            int intUpdateCount = cmdUpdateEntry.ExecuteNonQuery();

            if (intUpdateCount != intRowCount)
            {
                // throw exception, and let worker_RunWorkerCompleted rollback the database
                throw new System.Exception("Failed to clear checkout data for " + (intRowCount - intUpdateCount).ToString() + " of " + intRowCount.ToString() + " files");
            }

            // get latest versions, including dependencies
            worker_GetLatest(sender, e);

            // make sure all files got set to ReadOnly
            foreach (DataRow drUndo in drSelected)
            {
                string strFileName = drUndo.Field<string>("entry_name");
                string strAbsolutePath = drUndo.Field<string>("absolute_path");
                string strFullName = strAbsolutePath + "\\" + strFileName;
                FileInfo fiCurrFile = new FileInfo(strFullName);
                if (fiCurrFile.IsReadOnly == false)
                {
                    fiCurrFile.IsReadOnly = true;
                    dlgStatus.AddStatusLine("INFO", "Setting file ReadOnly: " + strFullName);
                }
            }

            // commit to database
            t.Commit();

        }

        void worker_Delete(object sender, DoWorkEventArgs e)
        {

            BackgroundWorker myWorker = sender as BackgroundWorker;
            dlgStatus.AddStatusLine("INFO", "Starting Permanent Delete worker");

            // get arguments
            List<object> genericlist = e.Argument as List<object>;
            NpgsqlTransaction t = (NpgsqlTransaction)genericlist[0];
            string strRelBasePath = (string)genericlist[1];
            List<string> lstSelectedNames = (List<string>)genericlist[2];
            bool blnDestroy = (bool)genericlist[3];

            bool blnDeleteDirs = (lstSelectedNames == null);

            // get deletes data
            DataSet dsDeletes = LoadDeletesData(sender, e, t, strRelBasePath, lstSelectedNames);

            // start commiting data
            bool blnFailed = false;
            string strGuid = "";

            // check for files that won't delete
            DataRow[] drWontFiles = dsDeletes.Tables["files"].Select("checkout_user is not null");
            foreach (DataRow drCurrent in drWontFiles)
            {
                string strFileName = drCurrent.Field<string>("entry_name");
                string strAbsolutePath = drCurrent.Field<string>("absolute_path");
                string strFullName = strAbsolutePath + "\\" + strFileName;
                dlgStatus.AddStatusLine("ERROR", "File is checked out: " + strFullName);
                blnFailed = true;
            }

            // TODO: check for locked files
            DataRow[] drDeleteFiles = dsDeletes.Tables["files"].Select("checkout_user is null");
            Int32 intNewCount = drDeleteFiles.Length;
            for (int i = 0; i < drDeleteFiles.Length; i++)
            {
                DataRow drCurrent = drDeleteFiles[i];

                // check for cancellation
                if ((myWorker.CancellationPending == true))
                {
                    e.Cancel = true;
                    return;
                }

                string strFileName = drCurrent.Field<string>("entry_name");
                string strAbsolutePath = drCurrent.Field<string>("absolute_path");
                string strFullName = strAbsolutePath + "\\" + strFileName;
                FileInfo fiCurrFile = new FileInfo(strFullName);

                // report status
                dlgStatus.AddStatusLine("INFO", "Checking for write access (" + (i + 1).ToString() + " of " + drDeleteFiles.Length.ToString() + "): " + strFullName);

                // check if file is writeable
                if (IsFileLocked(fiCurrFile) == true)
                {
                    dlgStatus.AddStatusLine("ERROR", "File is locked: " + strFullName);
                    blnFailed = true;
                    // file is in use: don't continue
                    //throw new System.Exception("File \"" + fiCurrFile.Name + "\" is locked.  Release it first.");
                }

            }

            // verify with user
            if (blnDestroy)
            {
                var result = MessageBox.Show(String.Format("You are destroying {0} entries.  Continue?", intNewCount.ToString()),
                    "Confirm Destruction",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (result == System.Windows.Forms.DialogResult.No)
                {
                    return;
                }
            }

            // flag as deleted all remote versions, entries (set the destroyed flag)
            if (blnFailed == false)
            {
                blnFailed = blnFailed || DeleteRemoteEntries(sender, e, t, blnDestroy, ref dsDeletes);
            }

            // flag as deleted all remote directories (set the destroyed flag)
            if (blnFailed == false && blnDeleteDirs == true)
            {
                blnFailed = blnFailed || DeleteRemoteDirs(sender, e, t, ref dsDeletes);
            }

            // stage remote files for deletion from webdav server
            if (blnFailed == false)
            {
                if (blnDestroy)
                {
                    strGuid = Guid.NewGuid().ToString().ToUpper();
                    blnFailed = blnFailed || StageRemotesForDelete(sender, e, t, strGuid, ref dsDeletes);
                }
            }

            // remove local files and directories
            if (blnFailed == false)
            {
                blnFailed = blnFailed || DeleteLocalFiles(sender, e, t, blnDeleteDirs, ref dsDeletes);
            }

            // TODO: purge files staged for delete from the webdav server
            if (blnFailed == false)
            {
                if (blnDestroy)
                {
                    //strGuid = Guid.NewGuid().ToString().ToUpper();
                    blnFailed = blnFailed || PurgeRemoteFiles(sender, e, t, strGuid, ref dsDeletes);
                }
            }

            // commit to database
            if (blnFailed == true)
            {
                // roll back database work
                t.Rollback();

                // undo webdav work
                if (strGuid != "")
                {
                    RollbackDeletes(sender, e, t, strGuid, ref dsDeletes);
                }

                throw new System.Exception("Operation failed. Rolling back the database transaction.");
            }
            else
            {
                t.Commit();

            }

        }

        void worker_UnDelete(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker myWorker = sender as BackgroundWorker;
            dlgStatus.AddStatusLine("INFO", "Starting UnDelete worker");

            // get arguments
            List<object> genericlist = e.Argument as List<object>;
            NpgsqlTransaction t = (NpgsqlTransaction)genericlist[0];
            string strRelBasePath = (string)genericlist[1];
            List<string> lstSelectedNames = (List<string>)genericlist[2];

            bool blnDeleteDirs = (lstSelectedNames == null);

            // get deletes data
            DataSet dsDeletes = LoadDeletesData(sender, e, t, strRelBasePath, lstSelectedNames);

            // check that files are logically deleted and not destroyed
            DataRow[] drDestroyed = dsDeletes.Tables["files"].Select("destroyed=true");
            foreach (DataRow dr in drDestroyed)
            {
                dlgStatus.AddStatusLine("WARNING", String.Format("{0} was destroyed. Can't undelete.", dr.Field<string>("entry_name")));
            }

            // start commiting data
            bool blnFailed = false;

            // unflag as deleted all remote versions, entries
            if (blnFailed == false)
            {
                blnFailed = blnFailed || UnDeleteRemoteEntries(sender, e, t, ref dsDeletes);
            }

            // unflag as deleted all remote directories
            if (blnFailed == false && blnDeleteDirs == true)
            {
                blnFailed = blnFailed || UnDeleteRemoteDirs(sender, e, t, ref dsDeletes);
            }

            // commit to database
            if (blnFailed == true)
            {
                // roll back database work
                t.Rollback();
                throw new System.Exception("Operation failed. Rolling back the database transaction.");
            }
            else
            {
                t.Commit();
            }

        }

        void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled == true)
            {
                t.ToString();
                if (t.Connection != null)
                {
                    t.Rollback();
                }
                // TODO: figure out how to rollback WebDav changes
                dlgStatus.AddStatusLine("CANCEL", "Operation canceled");
            }
            else if (e.Error != null)
            {
                dlgStatus.AddStatusLine("ERROR", e.Error.Message);
                if (t.Connection != null)
                {
                    t.Rollback();
                }
                // TODO: figure out how to rollback WebDav changes
                dlgStatus.AddStatusLine("ERROR", "Operation failed");
            }
            else
            {
                if (t.Connection != null)
                {
                    t.Rollback();
                    // TODO: figure out how to rollback WebDav changes
                }
                dlgStatus.AddStatusLine("INFO", "Operation completed");
                dlgStatus.OperationCompleted();
            }
        }

        private DataSet BuildCommitsDataSet()
        {
            // init DataSet
            DataSet dsCommits = new DataSet();

            // init directories data table
            dsCommits.Tables.Add("dirs");
            dsCommits.Tables["dirs"].Columns.Add("dir_id", Type.GetType("System.Int32"));
            dsCommits.Tables["dirs"].Columns.Add("parent_id", Type.GetType("System.Int32"));
            dsCommits.Tables["dirs"].Columns.Add("dir_name", Type.GetType("System.String"));
            dsCommits.Tables["dirs"].Columns.Add("relative_path", Type.GetType("System.String"));
            dsCommits.Tables["dirs"].Columns.Add("absolute_path", Type.GetType("System.String"));

            // init files data table
            dsCommits.Tables.Add(CreateFileTable());
            dsCommits.Tables[1].TableName = "files";

            // init relationships (dependencies) data table
            dsCommits.Tables.Add("rels");
            dsCommits.Tables["rels"].Columns.Add("parent_id", Type.GetType("System.Int32"));
            dsCommits.Tables["rels"].Columns.Add("child_id", Type.GetType("System.Int32"));
            dsCommits.Tables["rels"].Columns.Add("parent_name", Type.GetType("System.String"));
            dsCommits.Tables["rels"].Columns.Add("child_name", Type.GetType("System.String"));
            dsCommits.Tables["rels"].Columns.Add("parent_absolute_path", Type.GetType("System.String"));
            dsCommits.Tables["rels"].Columns.Add("child_absolute_path", Type.GetType("System.String"));
            dsCommits.Tables["rels"].Columns.Add("parent_sandboxed", Type.GetType("System.Boolean"));
            dsCommits.Tables["rels"].Columns.Add("child_sandboxed", Type.GetType("System.Boolean"));

            return dsCommits;
        }

        // commits data (assuming actions like Commit or CheckIn):
        // files in the selected path
        // files that are depended upon by files in the selected path
        // file dependency relationships
        // directories in which the above files reside
        // additional parent directories, if they need to be created remotely
        private DataSet LoadCommitsData(object sender, DoWorkEventArgs e, NpgsqlTransaction t, string strRelBasePath, List<string> lstSelectedNames)
        {
            // running in separate thread
            BackgroundWorker myWorker = sender as BackgroundWorker;

            bool blnFailed = false;

            // make sure we are working inside of a transaction
            if (t.Connection == null)
            {
                MessageBox.Show("The database transaction is not functional");
                return null;
            }

            // init DataSet
            DataSet dsCommits = new DataSet();

            // init directories data table
            dsCommits.Tables.Add("dirs");
            dsCommits.Tables["dirs"].Columns.Add("dir_id", Type.GetType("System.Int32"));
            dsCommits.Tables["dirs"].Columns.Add("parent_id", Type.GetType("System.Int32"));
            dsCommits.Tables["dirs"].Columns.Add("dir_name", Type.GetType("System.String"));
            dsCommits.Tables["dirs"].Columns.Add("relative_path", Type.GetType("System.String"));
            dsCommits.Tables["dirs"].Columns.Add("absolute_path", Type.GetType("System.String"));

            // add this directory to the dirs table
            Int32 intDirId = 0;
            Int32 intParentId = 0;
            //string strBaseName = strRelBasePath.Substring(strRelBasePath.LastIndexOf("\\") + 1);
            //string strRelParentPath = strRelBasePath.Substring(0, strRelBasePath.LastIndexOf("\\"));
            //string strParentName = strRelParentPath.Substring(strRelParentPath.LastIndexOf("\\") + 1);
            string strBaseName = Utils.GetBaseName(strRelBasePath);
            string strRelParentPath = Utils.GetParentDirectory(strRelBasePath);
            string strParentName = "";
            if (strRelParentPath != "")
                strParentName = Utils.GetBaseName(strRelParentPath);

            //Int32 intParentId = 0;
            //string strBaseName = strRelBasePath;
            //string strRelParentPath = strRelBasePath;
            //string strParentName = strRelParentPath;
            //if (strRelBasePath.Contains("\\"))
            //{
            //    strBaseName = strRelBasePath.Substring(strRelBasePath.LastIndexOf("\\") + 1);
            //    strRelParentPath = strRelBasePath.Substring(0, strRelBasePath.LastIndexOf("\\"));
            //    strParentName = strRelParentPath.Substring(strRelParentPath.LastIndexOf("\\") + 1);
            //}
            dictTree.TryGetValue(strRelBasePath, out intDirId);
            dictTree.TryGetValue(strRelParentPath, out intParentId);
            dsCommits.Tables["dirs"].Rows.Add(intDirId, intParentId, strBaseName, strRelBasePath, Utils.GetAbsolutePath(strLocalFileRoot, strRelBasePath));

            // check for cancellation
            if ((myWorker.CancellationPending == true))
            {
                e.Cancel = true;
                return dsCommits;
            }

            if (lstSelectedNames == null)
            {
                // init files data table
                dsCommits.Tables.Add(CreateFileTable());
                dsCommits.Tables[1].TableName = "files";

                // get files and directories recursively
                blnFailed = LoadCommitsDataRecurse(sender, e, ref dsCommits, strRelBasePath);
            }
            else
            {
                // get files and directories from list
                string strEntries = String.Join("','", lstSelectedNames.ToArray());
                DataTable dtTemp = dsList.Tables["files"].Select("entry_name in ('" + strEntries + "')").CopyToDataTable<System.Data.DataRow>();
                dtTemp.TableName = "files";
                dsCommits.Tables.Add(dtTemp);
            }

            // init relationships (dependencies) data table
            dsCommits.Tables.Add("rels");
            dsCommits.Tables["rels"].Columns.Add("parent_id", Type.GetType("System.Int32"));
            dsCommits.Tables["rels"].Columns.Add("child_id", Type.GetType("System.Int32"));
            dsCommits.Tables["rels"].Columns.Add("parent_name", Type.GetType("System.String"));
            dsCommits.Tables["rels"].Columns.Add("child_name", Type.GetType("System.String"));
            dsCommits.Tables["rels"].Columns.Add("parent_absolute_path", Type.GetType("System.String"));
            dsCommits.Tables["rels"].Columns.Add("child_absolute_path", Type.GetType("System.String"));
            dsCommits.Tables["rels"].Columns.Add("parent_sandboxed", Type.GetType("System.Boolean"));
            dsCommits.Tables["rels"].Columns.Add("child_sandboxed", Type.GetType("System.Boolean"));

            // get dependencies of local files, while appending to list of directories
            GetSWDepends(sender, e, ref dsCommits);

            // get and match remote files
            MatchRemoteFiles(sender, e, ref dsCommits);

            // climb the tree until we find a parent directory that exists remotely
            ClimbToRemoteParents(sender, e, ref dsCommits);

            return dsCommits;

        }

        private bool LoadCommitsDataRecurse(object sender, DoWorkEventArgs e, ref DataSet dsCommits, string strRelativePath)
        {
            // this method is only called when the user executes an action on a direcory in the tree view

            // running in separate thread
            BackgroundWorker myWorker = sender as BackgroundWorker;

            // get absolute directory path 
            string strAbsolutePath = Utils.GetAbsolutePath(strLocalFileRoot, strRelativePath);

            // get remote directory id (0 if doesn't exist remotely)
            int intDirId = 0;
            dictTree.TryGetValue(strRelativePath, out intDirId);

            // log status
            dlgStatus.AddStatusLine("INFO", "Processing Directory: " + strAbsolutePath);

            // get local files
            string[] strFiles = Directory.GetFiles(strAbsolutePath);
            string strFileName = "";
            DateTime dtModifyDate;
            Int64 lngFileSize = 0;

            // log status
            dlgStatus.AddStatusLine("INFO", "Processing local files: " + strFiles.Length);

            //loop through all files in this directory
            foreach (string strFile in strFiles)
            {

                // check for cancellation
                if ((myWorker.CancellationPending == true))
                {
                    e.Cancel = true;
                    return true;
                }

                // get file info
                strFileName = Utils.GetShortName(strFile);
                FileInfo fiCurrFile = new FileInfo(strFile);
                string strFileExt = fiCurrFile.Extension.Substring(1, fiCurrFile.Extension.Length - 1).ToLower();
                lngFileSize = fiCurrFile.Length;
                dtModifyDate = fiCurrFile.LastWriteTime;

                // skip temp files
                if (strFileName.StartsWith("~"))
                {
                    continue;
                }

                // log status
                dlgStatus.AddStatusLine("INFO", "Processing local file: " + strFile);

                // check for file exists
                if (!File.Exists(strFile))
                {
                    dlgStatus.AddStatusLine("WARNING", "Skipping missing file: " + strFile);
                    continue;
                }

                // insert new row for presumed local-only file
                dsCommits.Tables["files"].Rows.Add(
                    null, // entry_id
                    null, // version_id
                    intDirId, // dir_id
                    strFileName, // entry_name
                    null, // type_id
                    strFileExt, // file_ext
                    null, // cat_id
                    null, // cat_name
                    null, // latest_size
                    null, // str_latest_size
                    lngFileSize, // local_size
                    Utils.FormatSize(lngFileSize), // str_local_size
                    null, // latest_stamp
                    null, // str_latest_stamp
                    dtModifyDate, // local_stamp
                    Utils.FormatDate(dtModifyDate), // str_local_stamp
                    null, // latest_md5
                    null, // local_md5
                    null, // checkout_user
                    null, // ck_user_name
                    null, // checkout_date
                    null, // str_checkout_date
                    null, // checkout_node
                    null, // checkout_node_name
                    true, // is_local
                    false, // is_remote
                    "lo", // client_status_code
                    strRelativePath, // relative_path
                    strAbsolutePath, // absolute_path
                    null, // icon
                    false, // is_depend_searched
                    fiCurrFile.IsReadOnly, // is_readonly
                    true, // active
                    false // destroyed
                );

            }

            // loop through all subdirectories
            bool blnFailed = false;
            string[] ChildDirectories = Directory.GetDirectories(strAbsolutePath);
            foreach (string strChildAbsPath in ChildDirectories)
            {
                string strChildName = Utils.GetBaseName(strChildAbsPath);
                string strChildRelPath = Utils.GetRelativePath(strLocalFileRoot, strChildAbsPath);

                int intChildId = 0;
                dictTree.TryGetValue(strChildRelPath, out intChildId);
                dsCommits.Tables["dirs"].Rows.Add(
                    intChildId,      // dir_id
                    intDirId,        // parent_id
                    strChildName,    // dir_name
                    strChildRelPath, // relative_path
                    strChildAbsPath  // absolute_path
                );

                blnFailed = LoadCommitsDataRecurse(sender, e, ref dsCommits, strChildRelPath);
            }

            return (blnFailed);

        }

        private bool GetSWDepends(object sender, DoWorkEventArgs e, ref DataSet dsCommits)
        {
            // running in separate thread
            BackgroundWorker myWorker = sender as BackgroundWorker;

            // trying to do this without recursion

            // given data table of files, find and append dependent files
            // while getting dependencies, append parent/child relationships to data table
            // parent/child relationships are many2many with the files table, so this requires a second table
            // use long file name as foreign key

            // get initial set of files on which to search dependencies
            DataRow[] drNeedsDepends = dsCommits.Tables["files"].Select("is_depend_searched=false and file_ext in ('SLDASM','SLDPRT','SLDDRW','sldasm','sldprt','slddrw')");

            while (drNeedsDepends.Length != 0)
            {

                foreach (DataRow row in drNeedsDepends)
                {
                    string strParentShortName = row.Field<string>("entry_name");
                    string strParentAbsPath = row.Field<string>("absolute_path");
                    string strParentFullName = strParentAbsPath + "\\" + strParentShortName;

                    // get solidworks dependencies
                    List<string[]> lstDepends;
                    if (blnUseSwApi)
                    {
                        lstDepends = connSwApi.GetDependencies(strParentFullName, false);
                    }
                    else
                    {
                        SWDocMgr docMgr = new SWDocMgr(strSWDocMgrKey);
                        lstDepends = docMgr.GetDependencies(strParentFullName, false);
                    }

                    if (lstDepends != null)
                    {

                        foreach (string[] strDepends in lstDepends)
                        {

                            // check for cancellation
                            if (sender != null && (myWorker.CancellationPending == true))
                            {
                                e.Cancel = true;
                                return true;
                            }

                            string strShortName = strDepends[0];
                            string strLongName = strDepends[1];

                            // log status
                            dlgStatus.AddStatusLine("INFO", "Processing dependency: " + strLongName);

                            FileInfo fiCurrFile = new FileInfo(strLongName);
                            string strAbsolutePath = fiCurrFile.DirectoryName;
                            string strRelativePath = Utils.GetRelativePath(strLocalFileRoot, strAbsolutePath);

                            // check for existing relationships
                            string searchRelExists = String.Format("parent_name='{0}' and child_name='{1}'and child_absolute_path='{2}' and parent_absolute_path='{3}'",
                                    strParentShortName.Replace("'", "''"),
                                    strDepends[0].Replace("'", "''"),
                                    strAbsolutePath.Replace("'", "''"),
                                    strParentAbsPath.Replace("'", "''"));
                            if (dsCommits.Tables["rels"].Select(searchRelExists).Length > 0) continue;

                            // get exact file extension for status check
                            Tuple<string, string, string, string> tplExtensions = ftmStart.GetFileExt(strShortName);
                            string strFileExt = tplExtensions.Item1;

                            long lngFileSize = 0;
                            DateTime dtModifyDate = DateTime.Now;
                            if (fiCurrFile.Exists)
                            {
                                lngFileSize = fiCurrFile.Length;
                                dtModifyDate = fiCurrFile.LastWriteTime;
                            }
                            else
                            {
                                dlgStatus.AddStatusLine("WARNING", "Skipping missing file: " + strLongName);
                                continue;
                            }

                            // add the referenced file, if it has not already been added to the files table
                            string searchFileExists = String.Format("entry_name='{0}' and absolute_path='{1}'",
                                strDepends[0].Replace("'", "''"),
                                strAbsolutePath.Replace("'", "''"));
                            if (dsCommits.Tables["files"].Select(searchFileExists).Length == 0)
                            {
                                // get directory id
                                Int32 intDirId = 0;
                                dictTree.TryGetValue(strRelativePath, out intDirId);

                                dsCommits.Tables["files"].Rows.Add(
                                    null, // entry_id
                                    null, // version_id
                                    intDirId, // dir_id
                                    strDepends[0], // entry_name
                                    null, // type_id
                                    strFileExt, // file_ext
                                    null, // cat_id
                                    null, // cat_name
                                    null, // latest_size
                                    null, // str_latest_size
                                    lngFileSize, // local_size
                                    Utils.FormatSize(lngFileSize), // str_local_size
                                    null, // latest_stamp
                                    null, // str_latest_stamp
                                    dtModifyDate, // local_stamp
                                    Utils.FormatDate(dtModifyDate), // str_local_stamp
                                    null, // latest_md5
                                    null, // local_md5
                                    null, // checkout_user
                                    null, // ck_user_name
                                    null, // checkout_date
                                    null, // str_checkout_date
                                    null, // checkout_node
                                    null, // checkout_node_name
                                    true, // is_local
                                    false, // is_remote
                                    "lo", // client_status_code
                                    strRelativePath, // relative_path
                                    strAbsolutePath, // absolute_path
                                    null, // icon
                                    false, // is_depend_searched
                                    fiCurrFile.IsReadOnly, // is_readonly
                                    true, // active
                                    false // destroyed
                                );
                                //DataRow drFile = dsCommits.Tables["files"].NewRow();
                                //drFile["entry_id"] = null;
                                //drFile["version_id"] = null;
                                //drFile["dir_id"] = intDirId;
                                //drFile["entry_name"] = strDepends[0];
                                //drFile["type_id"] = null;
                                //drFile["file_ext"] = strFileExt;
                                //drFile["cat_id"] = null;
                                //drFile["cat_name"] = null;
                                //drFile["latest_size"] = null;
                                //drFile["str_latest_size"] = null;
                                //drFile["local_size"] = lngFileSize;
                                //drFile["str_local_size"] = Utils.FormatSize(lngFileSize);
                                //drFile["latest_stamp"] = null;
                                //drFile["str_latest_stamp"] = null;
                                //drFile["local_stamp"] = dtModifyDate;
                                //drFile["str_local_stamp"] = Utils.FormatDate(dtModifyDate);
                                //drFile["latest_md5"] = null;
                                //drFile["local_md5"] = null;
                                //drFile["checkout_user"] = null;
                                //drFile["ck_user_name"] = null;
                                //drFile["checkout_date"] = null;
                                //drFile["str_checkout_date"] = null;
                                //drFile["checkout_node"] = null;
                                //drFile["checkout_node_name"] = null;
                                //drFile["is_local"] = true;
                                //drFile["is_remote"] = false;
                                //drFile["client_status_code"] = "lo";
                                //drFile["relative_path"] = strRelativePath;
                                //drFile["absolute_path"] = strAbsolutePath;
                                //drFile["icon"] = null;
                                //drFile["is_depend_searched"] = false;
                                //drFile["is_readonly"] = fiCurrFile.IsReadOnly;
                                //drFile["active"] = true;
                                //drFile["destroyed"] = false;
                                //dsCommits.Tables["files"].Rows.Add(drFile);

                                // add this file's directory, if necessary
                                DataRow[] drCheck = dsCommits.Tables["dirs"].Select("absolute_path = '" + strAbsolutePath.Replace("'", "''") + "'");
                                if (drCheck.Length == 0)
                                {
                                    // get parent directory name
                                    string strDirName = Utils.GetBaseName(strRelativePath);

                                    // get parent directory id
                                    Int32 intParentId = 0;
                                    if (strRelativePath != "")
                                    {
                                        // if the path is in pwa
                                        string strParentPath = Utils.GetParentDirectory(strRelativePath);
                                        dictTree.TryGetValue(strParentPath, out intParentId);
                                    }
                                    dsCommits.Tables["dirs"].Rows.Add(intDirId, intParentId, strDirName, strRelativePath, strAbsolutePath);
                                }
                            }

                            // check for sandboxing
                            bool blnParentSandboxed = lstSandboxed.Contains(Utils.GetRelativePath(strLocalFileRoot, strParentAbsPath));
                            bool blnChildSandboxed = lstSandboxed.Contains(strRelativePath);

                            // add relationships
                            // the calling method will associate entry ids for only the files to be committed
                            //dsCommits.Tables["rels"].Rows.Add(
                            //    0,                  // parent_id
                            //    0,                  // child_id
                            //    strParentShortName, // parent_name
                            //    strDepends[0],      // child_name
                            //    strParentAbsPath,   // parent_absolute_path
                            //    strAbsolutePath,    // child_absolute_path
                            //    blnParentSandboxed, // parent_sandboxed
                            //    blnChildSandboxed   // child_sandboxed
                            //);
                            DataRow drRel = dsCommits.Tables["rels"].NewRow();
                            drRel["parent_id"] = 0;
                            drRel["child_id"] = 0;
                            drRel["parent_name"] = strParentShortName;
                            drRel["child_name"] = strDepends[0];
                            drRel["parent_absolute_path"] = strParentAbsPath;
                            drRel["child_absolute_path"] = strAbsolutePath;
                            drRel["parent_sandboxed"] = blnParentSandboxed;
                            drRel["child_sandboxed"] = blnChildSandboxed;
                            dsCommits.Tables["rels"].Rows.Add(drRel);
                        }
                    }

                    // set is_depend_searched = true
                    row.SetField<bool>("is_depend_searched", true);

                }

                // check for new files on which to search dependencies
                drNeedsDepends = dsCommits.Tables["files"].Select("is_depend_searched=false and file_ext in ('SLDASM','SLDPRT','SLDDRW','sldasm','sldprt','slddrw')");
            }

            return false;

        }

        private bool MatchRemoteFiles(object sender, DoWorkEventArgs e, ref DataSet dsCommits)
        {
            // return true if something failed, false if successful

            // running in separate thread
            BackgroundWorker myWorker = sender as BackgroundWorker;

            // get list of remote directories
            DataSet dsTemp = new DataSet();
            DataRow[] drRemoteDirs = dsCommits.Tables["dirs"].Select("dir_id<>0");
            if (drRemoteDirs.Length == 0)
            {
                // no remote data for the included directories
                // make an empty DataTable
                dsTemp.Tables.Add(CreateFileTable());
            }
            else
            {

                // build comma separated list of remote directories to query
                string[] strDirList = new string[drRemoteDirs.Length];
                string strDirs = "";
                foreach (DataRow row in drRemoteDirs)
                {
                    strDirs += row.Field<int>("dir_id").ToString() + ",";
                }

                // drop trailing comma
                strDirs = strDirs.Substring(0, strDirs.Length - 1);

                // get remote entries in a DataSet
                string strSql = "select * from fcn_latest_w_depends_by_dir_list( array [" + strDirs + "] );";
                NpgsqlDataAdapter daTemp = new NpgsqlDataAdapter(strSql, connDb);
                daTemp.SelectCommand.Parameters.AddWithValue("strLocalFileRoot", strLocalFileRoot);
                daTemp.Fill(dsTemp);

            }


            // loop through the local files and match remote files
            DataRow[] drLocalFiles = dsCommits.Tables["files"].Select("is_remote=false");
            for (int i = 0; i < drLocalFiles.Length; i++)
            {

                // check for cancellation
                if ((myWorker.CancellationPending == true))
                {
                    e.Cancel = true;
                    return true;
                }

                DataRow drLocalFile = drLocalFiles[i];
                string strFileName = drLocalFile.Field<string>("entry_name");
                string strAbsPath = drLocalFile.Field<string>("absolute_path");
                Int32 intDirId = drLocalFile.Field<Int32>("dir_id");
                DataRow[] drRemoteFile = dsTemp.Tables[0].Select(String.Format("entry_name='{0}' and dir_id={1}", strFileName.Replace("'", "''"), intDirId));

                if (drRemoteFile.Length != 0)
                {

                    DataRow drTemp = drRemoteFile[0];
                    int intVersionId = drTemp.Field<Int32>("version_id");

                    drLocalFile.SetField<Int32>("entry_id", drTemp.Field<Int32>("entry_id"));
                    drLocalFile.SetField<Int32>("version_id", intVersionId);
                    drLocalFile.SetField<Int32>("dir_id", drTemp.Field<Int32>("dir_id"));
                    drLocalFile.SetField<string>("entry_name", drTemp.Field<string>("entry_name"));
                    drLocalFile.SetField<Int32>("type_id", drTemp.Field<Int32>("type_id"));
                    drLocalFile.SetField<string>("file_ext", drTemp.Field<string>("file_ext"));
                    drLocalFile.SetField<Int32>("cat_id", drTemp.Field<Int32>("cat_id"));
                    drLocalFile.SetField<string>("cat_name", drTemp.Field<string>("cat_name"));
                    drLocalFile.SetField<Int64>("latest_size", drTemp.Field<Int64>("latest_size"));
                    drLocalFile.SetField<string>("str_latest_size", Utils.FormatSize(drTemp.Field<long>("latest_size")));
                    //drLocalFile.SetField<Int64>("local_size", drTemp.Field<Int64>("local_size"));
                    //drLocalFile.SetField<string>("str_local_size", drTemp.Field<string>("str_local_size"));
                    drLocalFile.SetField<DateTime>("latest_stamp", drTemp.Field<DateTime>("latest_stamp"));
                    drLocalFile.SetField<string>("str_latest_stamp", Utils.FormatDate(drTemp.Field<DateTime>("latest_stamp")));
                    //drLocalFile.SetField<DateTime>("local_stamp", drTemp.Field<DateTime>("local_stamp"));
                    //drLocalFile.SetField<string>("str_local_stamp", drTemp.Field<string>("str_local_stamp"));
                    drLocalFile.SetField<string>("latest_md5", drTemp.Field<string>("latest_md5"));
                    //drLocalFile.SetField<string>("local_md5", drTemp.Field<string>("local_md5"));
                    drLocalFile.SetField<Int32?>("checkout_user", drTemp.Field<Int32?>("checkout_user"));
                    drLocalFile.SetField<string>("ck_user_name", drTemp.Field<string>("ck_user_name"));
                    drLocalFile.SetField<DateTime?>("checkout_date", drTemp.Field<DateTime?>("checkout_date"));
                    drLocalFile.SetField<string>("str_checkout_date", drTemp.Field<string>("str_checkout_date"));
                    drLocalFile.SetField<int?>("checkout_node", drTemp.Field<int?>("checkout_node"));
                    drLocalFile.SetField<string>("checkout_node_name", drTemp.Field<string>("checkout_node_name"));
                    //drLocalFile.SetField<Boolean>("is_local", drTemp.Field<Boolean>("is_local"));
                    drLocalFile.SetField<Boolean>("is_remote", drTemp.Field<Boolean>("is_remote"));
                    //drLocalFile.SetField<string>("relative_path", drTemp.Field<string>("relative_path"));
                    //drLocalFile.SetField<string>("absolute_path", drTemp.Field<string>("absolute_path"));
                    drLocalFile.SetField<Byte[]>("icon", drTemp.Field<Byte[]>("icon"));
                    //drLocalFile.SetField<Boolean>("is_depend_searched", drTemp.Field<Boolean>("is_depend_searched"));
                    drLocalFile.SetField<Boolean>("active", drTemp.Field<Boolean>("active"));
                    drLocalFile.SetField<Boolean>("destroyed", drTemp.Field<Boolean>("destroyed"));

                    // set status code for remote file
                    string strClientStatusCode = "";
                    object oTest = drLocalFile["checkout_user"];
                    if (oTest != System.DBNull.Value)
                    {
                        if (drLocalFile.Field<int>("checkout_user") == intMyUserId)
                        {
                            // checked out to me (current user)
                            // cm: checked out to me
                            strClientStatusCode = "cm";
                        }
                        else
                        {
                            // checked out to someone else
                            // co: checked out to other
                            strClientStatusCode = "co";
                        }
                    }
                    if (strClientStatusCode != "cm" && drTemp.Field<DateTime>("latest_stamp") > drLocalFile.Field<DateTime>("latest_stamp"))
                    {
                        // nv: new remote version
                        strClientStatusCode = "nv";
                    }
                    drLocalFile.SetField<string>("client_status_code", strClientStatusCode);

                    // update version ids in relationships
                    if (strClientStatusCode != "cm")
                    {
                        DataRow[] drRels = dsCommits.Tables["rels"].Select(String.Format("(child_name='{0}' and child_absolute_path='{1}') or (parent_name='{0}' and parent_absolute_path='{1}')", strFileName.Replace("'", "''"), strAbsPath.Replace("'", "''")));
                        foreach (DataRow drRel in drRels)
                        {

                            // either set the id on the parent side
                            if (drRel.Field<string>("parent_name") == strFileName) drRel.SetField<int>("parent_id", intVersionId);

                            // or set the id on the child side
                            if (drRel.Field<string>("child_name") == strFileName) drRel.SetField<int>("child_id", intVersionId);

                        }
                    }

                }
                else
                {

                    // get exact file extension for status check
                    Tuple<string, string, string, string> tplExtensions = ftmStart.GetFileExt(strFileName);
                    string strFileExt = tplExtensions.Item1;
                    string strRemFileExt = tplExtensions.Item2;
                    string strRemBlockExt = tplExtensions.Item3;
                    string strWinFileExt = tplExtensions.Item4;
                    drLocalFile.SetField<string>("file_ext", strFileExt);

                    // set status code for local file
                    // lo: local only
                    string strStatusCode = "lo";
                    if (strRemBlockExt == strFileExt)
                    {
                        // file type filtered/blocked
                        // if: ignore filter
                        strStatusCode = "if";
                    }
                    else if (strRemFileExt == "")
                    {
                        // file type doesn't exist remotely
                        // ft: no remote file type
                        strStatusCode = "ft";
                    }
                    else
                    {
                        // get default type_id for this file extension
                        DataRow[] drType = ftmStart.RemoteFileTypes.Select("file_ext = '" + strFileExt.Replace("'", "''") + "'");
                        if (drType.Length != 0)
                        {
                            drLocalFile.SetField<Int32>("type_id", drType[0].Field<int>("type_id"));
                            drLocalFile.SetField<Int32>("cat_id", drType[0].Field<int>("default_cat"));
                            drLocalFile.SetField<string>("cat_name", drType[0].Field<string>("cat_name"));
                        }
                    }
                    drLocalFile.SetField<string>("client_status_code", strStatusCode);

                }

            }

            return false;

        }

        private void ClimbToRemoteParents(object sender, DoWorkEventArgs e, ref DataSet dsCommits)
        {
            // if we will need to create directories, and we don't have a remote parent directory id to start creating from,
            // then we need to add parent directories to our list of directories to create
            // we will continue climbing up the tree, adding parent directories to our list, until we find a parent directory that exists remotely

            // climb upward from the set of shortest unique paths
            //
            // for example, given the following list of directories:
            // C:\pwa\Designed\AZ212
            // C:\pwa\Designed\AZ212\Cutter Head
            // C:\pwa\Designed\AZ212\Cutter Head\Bell Shaft
            // C:\pwa\Designed\AZ400
            // C:\pwa\Designed\AZ400\Frame
            // C:\pwa\Designed\Mining\ZM1800\Top Assemblies
            //
            // only operate on the following list of directories:
            // C:\pwa\Designed\AZ212
            // C:\pwa\Designed\AZ400
            // C:\pwa\Designed\Mining\ZM1800\Top Assemblies

            // get directories with no dir_id and no parent_id and that are inside the pwa
            DataRow[] OtherDirs = dsCommits.Tables["dirs"].Select(String.Format("dir_id=0 and parent_id=0 and (absolute_path + '\\') like '{0}\\%'", strLocalFileRoot.Replace("'", "''")), "relative_path asc");

            string strPrevDir = "";
            foreach (DataRow dir in OtherDirs)
            {
                // only process the shallowest path on this set of unique paths
                string strCurrPath = dir.Field<string>("relative_path");
                if (strCurrPath.StartsWith(strPrevDir) && strPrevDir != "")
                {
                    continue;
                }

                // get parents
                Int32 intParentId = 0;
                while (intParentId < 1)
                {
                    // get parent directory name and path
                    string strParentRelPath = Utils.GetParentDirectory(strCurrPath);
                    string strParentName = Utils.GetBaseName(strParentRelPath);

                    // get parent directory's parent id
                    string strParentsParentRelPath = Utils.GetParentDirectory(strParentRelPath);
                    if (strParentsParentRelPath == "")
                        // we reached the pwa root directory
                        break;
                    dictTree.TryGetValue(strParentsParentRelPath, out intParentId);
                    string strParentAbsPath = Utils.GetAbsolutePath(strLocalFileRoot, strParentRelPath);

                    // add the parent directory to table
                    // dir_id=0 because no remote directory exists for the current path
                    dsCommits.Tables["dirs"].Rows.Add(
                        0,                // dir_id
                        intParentId,      // parent_id
                        strParentName,    // dir_name
                        strParentRelPath, // relative_path
                        strParentAbsPath  // absolute_path
                    );
                    strCurrPath = strParentRelPath;
                }
            }

        }

        private bool AddRemoteDirs(object sender, DoWorkEventArgs e, NpgsqlTransaction t, ref DataSet dsCommits)
        {

            // running in separate thread
            BackgroundWorker myWorker = sender as BackgroundWorker;

            // prepare to get directory ids
            string strSql;
            strSql = "select nextval('seq_hp_directory_dir_id'::regclass);";
            NpgsqlCommand cmdGetId = new NpgsqlCommand(strSql, connDb, t);
            cmdGetId.Prepare();

            // prepare the database command
            strSql = @"
                insert into hp_directory (
                    dir_id,
                    parent_id,
                    dir_name,
                    default_cat,
                    create_user,
                    modify_user
                ) values (
                    :dir_id,
                    :parent_id,
                    :dir_name,
                    :default_cat,
                    :create_user,
                    :modify_user
                );
            ";
            NpgsqlCommand cmdInsert = new NpgsqlCommand(strSql, connDb, t);
            cmdInsert.Parameters.Add(new NpgsqlParameter("dir_id", NpgsqlTypes.NpgsqlDbType.Integer));
            cmdInsert.Parameters.Add(new NpgsqlParameter("parent_id", NpgsqlTypes.NpgsqlDbType.Integer));
            cmdInsert.Parameters.Add(new NpgsqlParameter("dir_name", NpgsqlTypes.NpgsqlDbType.Text));
            cmdInsert.Parameters.AddWithValue("default_cat", (int)1);
            cmdInsert.Parameters.AddWithValue("create_user", intMyUserId);
            cmdInsert.Parameters.AddWithValue("modify_user", intMyUserId);

            Boolean blnFailed = false;
            // create only directories that need to be created for files being commited
            // sorting by path ensures that parents will be created before children
            // having called ClimbToRemoteParents, the highest directory in each unique path will have a remote parent id (parent_id != 0)
            DataRow[] drNewDirs = dsCommits.Tables["dirs"].Select(String.Format("(absolute_path + '\\') like '{0}\\%' and dir_id=0", strLocalFileRoot.Replace("'", "''")), "relative_path asc");
            foreach (DataRow drCurrent in drNewDirs)
            {
                // check for cancellation
                if ((myWorker.CancellationPending == true))
                {
                    e.Cancel = true;
                    return true;
                }

                // get values
                string strDirName = drCurrent.Field<string>("dir_name");
                string strChildRelPath = drCurrent.Field<string>("relative_path");

                // check if we have already created it in a previous run
                // I believe this would only happen if we have a duplicate entry in dsCommits.Tables["dirs"]
                // TODO: find out why we are getting duplicate entries in dsCommits.Tables["dirs"]
                int intChildId = 0;
                dictTree.TryGetValue(strChildRelPath, out intChildId);
                if (intChildId != 0)
                {
                    drCurrent.SetField<Int32>("dir_id", (Int32)intChildId);
                    continue;
                }

                // check if we are creating any files that depend on this directory
                // by comparing relative path, we avoid directories outside pwa
                DataRow[] drNewFiles = dsCommits.Tables["files"].Select(String.Format("relative_path like '{0}%' and client_status_code in ('lo','cm')", drCurrent.Field<string>("relative_path").Replace("'", "''")));
                if (drNewFiles.Length < 1) continue;

                // when starting a new unique path, get this path's pre-existing remote parentid
                int intParentId = 0;
                if (drCurrent.Field<int?>("parent_id") != 0)
                {
                    intParentId = drCurrent.Field<int>("parent_id");
                }
                if (intParentId == 0)
                {
                    // the data row doesn't have a parent directory id
                    // try getting one from the dictionary
                    // we are updating the dictionary below, as we add new directories to the database
                    string strParentRelPath = Utils.GetParentDirectory(strChildRelPath);
                    dictTree.TryGetValue(strParentRelPath, out intParentId);
                    if (intParentId == 0)
                    {
                        throw new System.Exception("Failed to get parent directory ID (" + strParentRelPath + " for child " + strDirName + ")");
                    }
                    else
                    {
                        drCurrent.SetField<int>("parent_id", intParentId);
                    }
                }

                // get the next directory id
                object oTemp = cmdGetId.ExecuteScalar();
                if (oTemp != null)
                {
                    intChildId = (int)(long)oTemp;
                }
                else
                {
                    throw new System.Exception("Failed to get the next directory ID");
                }
                //int? intChildId = (int?)cmdGetId.ExecuteScalar();
                //if (intChildId == null)
                //{
                //    throw new System.Exception("Failed to get the next directory ID");
                //}

                // set parameters
                cmdInsert.Parameters["dir_id"].Value = intChildId;
                cmdInsert.Parameters["parent_id"].Value = intParentId;
                cmdInsert.Parameters["dir_name"].Value = strDirName;

                // insert row
                try
                {
                    cmdInsert.ExecuteNonQuery();
                }
                catch (NpgsqlException ex)
                {
                    // if unique key/index violation
                    dlgStatus.AddStatusLine("DEBUG", "dir_id=" + intChildId.ToString());
                    dlgStatus.AddStatusLine("DEBUG", "parent_id=" + intParentId.ToString());
                    dlgStatus.AddStatusLine("DEBUG", "dir_name=" + strDirName);
                    throw new System.Exception("Directory \"" + strDirName + "\" already exists on the server.  Refresh your view.  " + System.Environment.NewLine + ex.Message);
                }

                // update row with the new remote directory id
                drCurrent.SetField<Int32>("dir_id", (Int32)intChildId);
                dictTree.Add(drCurrent.Field<string>("relative_path"), (Int32)intChildId);

                // pass this directory id as the next parent id
                intParentId = (Int32)intChildId;

            }

            return blnFailed;

        }

        private bool AddNewVersions(object sender, DoWorkEventArgs e, NpgsqlTransaction t, ref DataSet dsCommits)
        {

            BackgroundWorker myWorker = sender as BackgroundWorker;
            bool blnFailed = false;

            // make sure we are working inside of a transaction
            if (t.Connection == null)
            {
                MessageBox.Show("The database transaction is not functional");
                return true;
            }

            // load custom property map
            LoadPropertyMaps();

            string strSql;

            // prepare to get new entry ids
            strSql = "select nextval('seq_hp_entry_entry_id'::regclass);";
            NpgsqlCommand cmdGetEntryId = new NpgsqlCommand(strSql, connDb, t);

            // prepare a database command to insert the entry
            strSql = @"
                insert into hp_entry (
                    entry_id,
                    dir_id,
                    entry_name,
                    type_id,
                    cat_id,
                    create_user
                ) values (
                    :entry_id,
                    :dir_id,
                    :entry_name,
                    :type_id,
                    :cat_id,
                    :create_user
                );
            ";
            NpgsqlCommand cmdInsertEntry = new NpgsqlCommand(strSql, connDb, t);
            cmdInsertEntry.Parameters.Add(new NpgsqlParameter("entry_id", NpgsqlTypes.NpgsqlDbType.Integer));
            cmdInsertEntry.Parameters.Add(new NpgsqlParameter("dir_id", NpgsqlTypes.NpgsqlDbType.Integer));
            cmdInsertEntry.Parameters.Add(new NpgsqlParameter("entry_name", NpgsqlTypes.NpgsqlDbType.Text));
            cmdInsertEntry.Parameters.Add(new NpgsqlParameter("type_id", NpgsqlTypes.NpgsqlDbType.Integer));
            cmdInsertEntry.Parameters.Add(new NpgsqlParameter("cat_id", NpgsqlTypes.NpgsqlDbType.Integer));
            cmdInsertEntry.Parameters.Add(new NpgsqlParameter("create_user", NpgsqlTypes.NpgsqlDbType.Integer));

            // prepare to get new version ids
            strSql = "select nextval('seq_hp_version_version_id'::regclass);";
            NpgsqlCommand cmdGetVersionId = new NpgsqlCommand(strSql, connDb, t);

            // prepare a database command to insert the version
            strSql = @"
                insert into hp_version (
                    version_id,
                    entry_id,
                    file_size,
                    file_modify_stamp,
                    create_user,
                    create_node,
                    preview_image,
                    md5sum
                ) values (
                    :version_id,
                    :entry_id,
                    :file_size,
                    :file_modify_stamp,
                    :create_user,
                    :create_node,
                    :preview_image,
                    :md5sum
                );
            ";
            NpgsqlCommand cmdInsertVersion = new NpgsqlCommand(strSql, connDb, t);
            cmdInsertVersion.Parameters.Add(new NpgsqlParameter("version_id", NpgsqlTypes.NpgsqlDbType.Integer));
            cmdInsertVersion.Parameters.Add(new NpgsqlParameter("entry_id", NpgsqlTypes.NpgsqlDbType.Integer));
            cmdInsertVersion.Parameters.Add(new NpgsqlParameter("file_size", NpgsqlTypes.NpgsqlDbType.Bigint));
            cmdInsertVersion.Parameters.Add(new NpgsqlParameter("file_modify_stamp", NpgsqlTypes.NpgsqlDbType.Timestamp));
            cmdInsertVersion.Parameters.Add(new NpgsqlParameter("create_user", NpgsqlTypes.NpgsqlDbType.Integer));
            cmdInsertVersion.Parameters.Add(new NpgsqlParameter("create_node", NpgsqlTypes.NpgsqlDbType.Integer));
            cmdInsertVersion.Parameters.Add(new NpgsqlParameter("preview_image", NpgsqlTypes.NpgsqlDbType.Bytea));
            cmdInsertVersion.Parameters.Add(new NpgsqlParameter("md5sum", NpgsqlTypes.NpgsqlDbType.Text));

            // get new files, write to db, upload to webdav
            DataRow[] drNewFiles = dsCommits.Tables["files"].Select(String.Format("client_status_code in ('lo','cm') and (absolute_path + '\\') like '{0}\\%'", strLocalFileRoot.Replace("'", "''")));
            Int32 intNewCount = drNewFiles.Length;
            for (int i = 0; i < intNewCount; i++)
            {
                DataRow drNewFile = drNewFiles[i];

                // check for cancellation
                if ((myWorker.CancellationPending == true))
                {
                    e.Cancel = true;
                    return true;
                }

                string strFileName = drNewFile.Field<string>("entry_name");
                string strFileExt = drNewFile.Field<string>("file_ext");
                string strRelPath = drNewFile.Field<string>("relative_path");
                string strAbsPath = drNewFile.Field<string>("absolute_path");
                string strFullName = strAbsPath + "\\" + strFileName;
                int intTypeId = drNewFile.Field<int>("type_id");

                FileInfo fiNewFile = new FileInfo(strFullName);
                long lngFileSize = fiNewFile.Length;
                DateTime dtModifyDate = fiNewFile.LastWriteTime;
                string strMd5sum = Utils.StringMD5(strFullName);

                // report status
                dlgStatus.AddStatusLine("INFO", "Adding new file to db (" + (i + 1).ToString() + " of " + intNewCount.ToString() + "): " + strFileName);

                // get the parent directory id
                int intParentDir = drNewFile.Field<int>("dir_id");
                if (intParentDir == 0)
                {
                    dictTree.TryGetValue(strRelPath, out intParentDir);
                    drNewFile.SetField<int>("dir_id", intParentDir);
                }

                // get an entry_id
                int intEntryId = 0;
                if (drNewFile.Field<int?>("entry_id") == null)
                {
                    // get a new entry id
                    intEntryId = (int)(long)cmdGetEntryId.ExecuteScalar();

                    // insert the entry
                    cmdInsertEntry.Parameters["entry_id"].Value = intEntryId;
                    cmdInsertEntry.Parameters["dir_id"].Value = intParentDir;
                    cmdInsertEntry.Parameters["entry_name"].Value = strFileName;
                    cmdInsertEntry.Parameters["type_id"].Value = intTypeId;
                    cmdInsertEntry.Parameters["cat_id"].Value = drNewFile.Field<int>("cat_id");
                    cmdInsertEntry.Parameters["create_user"].Value = intMyUserId;
                    try
                    {
                        cmdInsertEntry.ExecuteNonQuery();
                    }
                    catch (NpgsqlException ex)
                    {
                        // integrity constraint violation?
                        dlgStatus.AddStatusLine("ERROR", ex.Message);
                        blnFailed = true;
                    }
                }
                else
                {
                    // entry already exists, creating a new version
                    intEntryId = drNewFile.Field<int>("entry_id");
                }

                // get a new version id
                int intVersionId = (int)(long)cmdGetVersionId.ExecuteScalar();

                // insert the version
                cmdInsertVersion.Parameters["version_id"].Value = intVersionId;
                cmdInsertVersion.Parameters["entry_id"].Value = intEntryId;
                cmdInsertVersion.Parameters["file_size"].Value = lngFileSize;
                cmdInsertVersion.Parameters["file_modify_stamp"].Value = dtModifyDate;
                cmdInsertVersion.Parameters["create_user"].Value = intMyUserId;
                cmdInsertVersion.Parameters["create_node"].Value = intMyNodeId;
                //SWDocMgr docMgr = new SWDocMgr(strSWDocMgrKey);
                byte[] preview = GetLocalPreview(drNewFile);
                if (preview == null)
                    cmdInsertVersion.Parameters["preview_image"].Value = System.DBNull.Value;
                else
                    cmdInsertVersion.Parameters["preview_image"].Value = preview;
                cmdInsertVersion.Parameters["md5sum"].Value = strMd5sum;
                try
                {
                    cmdInsertVersion.ExecuteNonQuery();
                }
                catch (NpgsqlException ex)
                {
                    // integrity constraint violation?
                    dlgStatus.AddStatusLine("ERROR", ex.Message);
                    blnFailed = true;

                    dlgStatus.AddStatusLine("DEBUG", "version_id=" + cmdInsertVersion.Parameters["version_id"].Value.ToString());
                    dlgStatus.AddStatusLine("DEBUG", "entry_id=" + cmdInsertVersion.Parameters["entry_id"].Value.ToString());
                    dlgStatus.AddStatusLine("DEBUG", "file_size=" + cmdInsertVersion.Parameters["file_size"].Value.ToString());
                    dlgStatus.AddStatusLine("DEBUG", "file_modify_stamp=" + cmdInsertVersion.Parameters["file_modify_stamp"].Value.ToString());
                    dlgStatus.AddStatusLine("DEBUG", "create_user=" + cmdInsertVersion.Parameters["create_user"].Value.ToString());
                    dlgStatus.AddStatusLine("DEBUG", "create_node=" + cmdInsertVersion.Parameters["create_node"].Value.ToString());
                    dlgStatus.AddStatusLine("DEBUG", "md5sum=" + cmdInsertVersion.Parameters["md5sum"].Value.ToString());
                }

                // get and insert custom properties
                dlgStatus.AddStatusLine("INFO", "Adding file properties db (" + (i + 1).ToString() + " of " + intNewCount.ToString() + "): " + strFileName);
                blnFailed = AddFileProps(sender, e, t, intVersionId, strFullName);

                // update relationships parent_id or child_id with new version_id
                DataRow[] drRels = dsCommits.Tables["rels"].Select(String.Format("(child_name='{0}' and child_absolute_path='{1}') or (parent_name='{0}' and parent_absolute_path='{1}')", strFileName.Replace("'", "''"), strAbsPath.Replace("'", "''")));
                foreach (DataRow drRel in drRels)
                {
                    // either set the id on the parent side
                    if (drRel.Field<string>("parent_name").ToLower() == strFileName.ToLower()) drRel.SetField<int>("parent_id", intVersionId);

                    // or set the id on the child side
                    if (drRel.Field<string>("child_name").ToLower() == strFileName.ToLower()) drRel.SetField<int>("child_id", intVersionId);
                }


                // name the file for webdav storage
                string strDavName = "/" + intEntryId.ToString() + "/" + intVersionId.ToString() + "." + strFileExt.ToLower();

                // check database success
                if (blnFailed)
                {
                    dlgStatus.AddStatusLine("ERROR", "Not uploading file " + strFileName);
                }
                else
                {

                    if (drNewFile.Field<int?>("entry_id") == null)
                    {
                        // create webdav directory (don't need to check existence)
                        connDav.CreateDir("/" + intEntryId.ToString());
                    }

                    // upload the file to webdav server
                    dlgStatus.AddStatusLine("INFO", "Uploading " + Utils.FormatSize(lngFileSize) + " (file " + strFileName + ")");
                    connDav.Upload(strFullName, strDavName);
                    if (connDav.StatusCode - 200 >= 100)
                    {
                        dlgStatus.AddStatusLine("ERROR", connDav.StatusCode + "(file " + strFileName + ")");
                        blnFailed = true;
                    }

                }

                // update row with new version_id and/or entry_id
                drNewFile.SetField<int>("entry_id", intEntryId);
                drNewFile.SetField<int>("version_id", intVersionId);

            }

            return blnFailed;

        }

        private bool AddFileProps(object sender, DoWorkEventArgs e, NpgsqlTransaction t, int VersionId, string FullName)
        {
            BackgroundWorker myWorker = sender as BackgroundWorker;
            bool blnFailed = false;

            // make sure we are working inside of a transaction
            if (t.Connection == null)
            {
                MessageBox.Show("The database transaction is not functional");
                return true;
            }

            // get custom properties
            // 1 - config name
            // 2 - property name
            // 3 - property type
            // 4 - resolved value (boxed object)
            List<Tuple<string, string, string, object>> lstProps;
            if (blnUseSwApi)
            {
                lstProps = connSwApi.GetProperties(FullName);
            }
            else
            {
                SWDocMgr docMgr = new SWDocMgr(strSWDocMgrKey);
                lstProps = docMgr.GetProperties(FullName);
            }
            //List<Tuple<string, string, string, object>> 


            // get server setting
            bool blnRestrict = dtServSettings.Select("setting_name = 'restrict_properties'")[0].Field<bool>("setting_bool_value");

            // return if no properties
            if (lstProps == null)
            {
                return blnFailed;
            }

            // initialize sql statement
            string strSql = @"
                insert into hp_version_property (
                    version_id,
                    config_name,
                    prop_id,
                    text_value,
                    date_value,
                    number_value,
                    yesno_value
                ) values
                ";
            string strSqlTemplate = "({0}, {1}, {2}, {3}, {4}, {5}, {6}),";

            int intInsertCount = 0;
            foreach (Tuple<string, string, string, object> tplProps in lstProps)
            {
                string strConfigName = tplProps.Item1; // config name
                string strPropName = tplProps.Item2;   // property name
                string strPropType = tplProps.Item3;   // property type
                object oResolved = tplProps.Item4;     // resolved value

                // check for property definition on server
                int intPropId = 0;
                dictPropIds.TryGetValue(strPropName, out intPropId);
                string strServPropType = "";
                dictPropTypes.TryGetValue(intPropId, out strServPropType);

                // if not defined on server
                if (intPropId == 0)
                {
                    // if restricting undefined properties
                    if (blnRestrict)
                    {
                        // send a warning, and skip this property
                        dlgStatus.AddStatusLine("WARNING", String.Format("Undefined property ({0}) on file {1}", strPropName, FullName));
                        continue;
                    }
                    else
                    {
                        // send info and process this property definition
                        dlgStatus.AddStatusLine("INFO", String.Format("Creating new property ({0}) on file {1}", strPropName, FullName));

                        // get new property id
                        NpgsqlCommand cmdGetPropId = new NpgsqlCommand("select nextval('seq_hp_entry_entry_id'::regclass);", connDb, t);
                        intPropId = (int)cmdGetPropId.ExecuteScalar();

                        // TODO: add to sql for inserting property definitions

                    }
                }

                // if property types do not match
                if (strPropType != strServPropType)
                {
                    // send a warning, and skip this property
                    dlgStatus.AddStatusLine("WARNING", String.Format("Mismatched property type ({0}) ({1}!={2}): {3}", strPropName, strServPropType, strPropType, FullName));
                    continue;
                }

                // establish string values
                string strText = (strPropType == "text" ? "'" + ((string)oResolved).Replace("'", "''") + "'" : "NULL");
                string strDate = (strPropType == "date" ? "'" + ((DateTime)oResolved).ToString("yyyy-mm-dd HH:mm:ss") + "'" : "NULL");
                string strNumber = (strPropType == "number" ? ((Decimal)oResolved).ToString() : "NULL");
                string strBool = (strPropType == "yesno" ? ((Boolean)oResolved).ToString() : "NULL");

                // add to the sql command
                strSql += String.Format(strSqlTemplate,
                    VersionId,
                    "'" + strConfigName.Replace("'", "''") + "'",
                    intPropId,
                    strText,
                    strDate,
                    strNumber,
                    strBool
                );
                intInsertCount++;

            }

            // execute the command
            if (intInsertCount > 0)
            {
                strSql = strSql.Remove(strSql.Length - 1); // remove trailing comma
                NpgsqlCommand cmdInsert = new NpgsqlCommand(strSql, connDb, t);
                try
                {
                    cmdInsert.ExecuteNonQuery();
                }
                catch (NpgsqlException ex)
                {
                    // integrity constraint violation?
                    dlgStatus.AddStatusLine("ERROR", ex.Message);
                    blnFailed = true;
                }
            }

            return blnFailed;

        }

        private bool AddVersionDepends(object sender, DoWorkEventArgs e, NpgsqlTransaction t, ref DataSet dsCommits)
        {
            // return true if failed
            bool blnFailed = false;

            // running in separate thread
            BackgroundWorker myWorker = sender as BackgroundWorker;

            // get files to be committed and build list of version ids
            List<int> lstVersions = new List<int>();
            DataRow[] drNewFiles = dsCommits.Tables["files"].Select(String.Format("client_status_code in ('lo','cm') and (absolute_path + '\\') like '{0}\\%'", strLocalFileRoot.Replace("'", "''")));
            foreach (DataRow drFile in drNewFiles)
            {
                lstVersions.Add(drFile.Field<int>("version_id"));
            }

            if (lstVersions.Count < 1)
            {
                // nothing to do here.  exit the method.
                return blnFailed;
            }

            string strVersions = String.Join(",", lstVersions);

            // check for children with no version id inside the pwa
            // this can happen when a child is outside the pwa, but we will ignore those
            DataRow[] drBads = dsCommits.Tables["rels"].Select(String.Format("child_id=0 and (child_absolute_path + '\\') like '{0}\\%'", strLocalFileRoot.Replace("'", "''")));
            foreach (DataRow dr in drBads)
            {
                string strDepend = String.Format("{0}\\{1} <-- {2}\\{3}", dr.Field<string>("parent_absolute_path"), dr.Field<string>("parent_name"), dr.Field<string>("child_absolute_path"), dr.Field<string>("child_name"));
                dlgStatus.AddStatusLine("ERROR", "Ignoring Failed Dependency: Could not get remote id for dependency: " + strDepend);
                blnFailed = true;
            }

            // check for parents not sandboxed with children that are sandboxed
            drBads = dsCommits.Tables["rels"].Select("parent_sandboxed=false and child_sandboxed=true");
            foreach (DataRow dr in drBads)
            {
                string strDepend = String.Format("{0}\\{1} <-- {2}\\{3}", dr.Field<string>("parent_absolute_path"), dr.Field<string>("parent_name"), dr.Field<string>("child_absolute_path"), dr.Field<string>("child_name"));
                dlgStatus.AddStatusLine("ERROR", "Ignoring Failed Dependency: Child sandboxed, parent not: " + strDepend);
                blnFailed = true;
            }

            // get relationships to be created remotely
            DataRow[] drNewRels = dsCommits.Tables["rels"].Select(String.Format("parent_id in ({0}) and child_id<>0", strVersions));

            // prepare the database command
            string strSql = @"
                insert into hp_version_relationship (
                    rel_parent_id,
                    rel_child_id
                ) values (
                    :rel_parent_id,
                    :rel_child_id
                );
            ";
            NpgsqlCommand cmdInsert = new NpgsqlCommand(strSql, connDb, t);
            cmdInsert.Parameters.Add(new NpgsqlParameter("rel_parent_id", NpgsqlTypes.NpgsqlDbType.Integer));
            cmdInsert.Parameters.Add(new NpgsqlParameter("rel_child_id", NpgsqlTypes.NpgsqlDbType.Integer));

            // insert new relationships
            // we never update relationships because changes to dependencies require a new version of the entry
            // since these are new versions, we should not need to check existence
            foreach (DataRow drNewRel in drNewRels)
            {

                // check for cancellation
                if ((myWorker.CancellationPending == true))
                {
                    e.Cancel = true;
                    return true;
                }

                // set parameters
                cmdInsert.Parameters["rel_parent_id"].Value = drNewRel.Field<int>("parent_id");
                cmdInsert.Parameters["rel_child_id"].Value = drNewRel.Field<int>("child_id");

                // insert row
                try
                {
                    cmdInsert.ExecuteNonQuery();
                }
                catch (NpgsqlException ex)
                {
                    string strDepend = String.Format("{0}\\{1} <-- {2}\\{3}", drNewRel.Field<string>("parent_name"), drNewRel.Field<string>("parent_absolute_path"), drNewRel.Field<string>("child_name"), drNewRel.Field<string>("child_absolute_path"));
                    dlgStatus.AddStatusLine("ERROR", "Failed inserting relationship (" + ex.Message + ") " + strDepend);
                    blnFailed = true;
                }

            }

            return blnFailed;

        }

        private bool ClearCheckout(object sender, DoWorkEventArgs e, NpgsqlTransaction t, ref DataSet dsCommits)
        {
            throw new NotImplementedException();
        }

        // fetch data (called from get latest method)
        // very similar to getting commits data, only in reverse
        // get data from remote and match to local stuff
        private DataSet LoadFetchData(object sender, DoWorkEventArgs e, NpgsqlTransaction t, string strRelBasePath = null, List<int> lstSelected = null)
        {
            // running in separate thread
            BackgroundWorker myWorker = sender as BackgroundWorker;

            bool blnFailed = false;

            // make sure we are working inside of a transaction
            if (t.Connection == null)
            {
                MessageBox.Show("The database transaction is not functional");
                return null;
            }

            // init DataSet
            DataSet dsFetches = new DataSet();

            // list or tree selection
            if (lstSelected != null)
            {
                // sql command for remote entry list
                // select listed entries
                // also select dependencies of those entries, wherever they are, based on remote dependency data
                string strEntries = String.Join(",", lstSelected.ToArray());
                string strSql = "select * from fcn_latest_w_depends_by_entry_list( array [" + strEntries + "] );";

                // put the remote list in the DataSet
                NpgsqlDataAdapter daTemp = new NpgsqlDataAdapter(strSql, connDb);
                daTemp.Fill(dsFetches);
            }
            else
            {
                // get directory id
                int intBaseDirId = 0;
                dictTree.TryGetValue(strRelBasePath, out intBaseDirId);

                // sql command for remote entry list
                // select entries in or below the specified direcory
                // also select dependencies of those entries, wherever they are, based on remote dependency data
                string strSql = "select * from fcn_latest_w_depends_by_dir(:dir_id);";

                // put the remote list in the DataSet
                NpgsqlDataAdapter daTemp = new NpgsqlDataAdapter(strSql, connDb);
                daTemp.SelectCommand.Parameters.AddWithValue("dir_id", intBaseDirId);
                daTemp.Fill(dsFetches);

            }

            if (dsFetches.Tables.Count == 0)
            {
                // make an empty DataTable
                dsFetches.Tables.Add(CreateFileTable());
            }
            dsFetches.Tables[0].TableName = "files";

            // get and match local files
            blnFailed = MatchLocalFiles(sender, e, ref dsFetches);

            // the Directory.CreateDirectory(string) method creates parents, too
            // no need to find parent directories

            return dsFetches;

        }

        private bool MatchLocalFiles(object sender, DoWorkEventArgs e, ref DataSet dsCommits)
        {
            // return true if something failed, false if successful

            // running in separate thread
            BackgroundWorker myWorker = sender as BackgroundWorker;

            // loop through remote files, checking for local version
            foreach (DataRow drRemote in dsCommits.Tables["files"].Rows)
            {

                string strFileName = drRemote.Field<string>("entry_name");
                string strAbsPath = Utils.GetAbsolutePath(strLocalFileRoot, drRemote.Field<string>("relative_path"));
                drRemote.SetField<string>("absolute_path", strAbsPath);
                string strFullName = strAbsPath + "\\" + strFileName;

                drRemote.SetField<string>("absolute_path", Utils.GetAbsolutePath(strLocalFileRoot, drRemote.Field<string>("relative_path")));

                // log status
                dlgStatus.AddStatusLine("INFO", "Checking for local copy: " + strFullName);

                if (File.Exists(strFullName))
                {
                    // file is also local -- get file info
                    FileInfo fiCurrFile = new FileInfo(strFullName);
                    string strFileExt = drRemote.Field<string>("file_ext");
                    Int64 lngFileSize = fiCurrFile.Length;
                    DateTime dtModifyDate = fiCurrFile.LastWriteTime;

                    // flag remote file as also being local
                    drRemote.SetField<bool>("is_local", true);

                    // update the local file size
                    drRemote.SetField<long>("local_size", lngFileSize);
                    drRemote.SetField<string>("str_local_size", Utils.FormatSize(lngFileSize));

                    // update the local modify date
                    drRemote.SetField<DateTime>("local_stamp", dtModifyDate);
                    drRemote.SetField<string>("str_local_stamp", Utils.FormatDate(dtModifyDate));

                    // update client_status_code
                    string strClientStatusCode = drRemote.Field<string>("client_status_code");
                    if (drRemote.Field<int?>("checkout_user") != null)
                    {
                        if (drRemote.Field<int>("checkout_user") == intMyUserId)
                        {
                            strClientStatusCode = "cm";
                        }
                        else
                        {
                            strClientStatusCode = "co";
                        }

                    }
                    if (strClientStatusCode != "cm")
                    {
                        // if local stamp is greater than remote stamp
                        if (dtModifyDate.Subtract(drRemote.Field<DateTime>("latest_stamp")).TotalSeconds > 1)
                        {
                            // lm: modified locally without checking out (apparently)
                            strClientStatusCode = "lm";
                        }
                        // if remote stamp is greater than local stamp
                        else if (dtModifyDate.Subtract(drRemote.Field<DateTime>("latest_stamp")).TotalSeconds < -1)
                        {
                            // nv: new remote version
                            strClientStatusCode = "nv";
                        }
                        else
                        {
                            // ""(empty string): identical files
                            strClientStatusCode = "ok";
                        }
                    }
                    drRemote.SetField<string>("client_status_code", strClientStatusCode);
                }

            }

            return false;

        }

        // load deletes data (called from permanent and logical delete methods)
        // fetching looks at remote files and matches local files (like left joining local on remote)
        // committing looks at local files and matches remote files (like left joining remote on local)
        // deleting needs both local and remote (like full outer join of local and remote)
        // deletes data is like the data loaded for the list view
        // we will delete files with the following client_status_code:
        // - ro: remote only
        // - lo: local only
        // - lm: modified locally without checking out
        // - nv: new remote version
        // - ft: no remote file type
        // - if: ignore filter
        // - ok: nothing to report (none of the above)
        // we will not delete files with the following client_status_code:
        // - cm: checked out to me
        // - co: checked out to other
        private DataSet LoadDeletesData(object sender, DoWorkEventArgs e, NpgsqlTransaction t, string strRelBasePath, List<string> lstSelectedNames)
        {
            // running in separate thread
            BackgroundWorker myWorker = sender as BackgroundWorker;

            bool blnFailed = false;

            // init DataSet
            DataSet dsDeletes = new DataSet();

            // check for cancellation
            if ((myWorker.CancellationPending == true))
            {
                e.Cancel = true;
                return null;
            }

            // get remote directory id
            int intBaseDirId = 0;
            dictTree.TryGetValue(strRelBasePath, out intBaseDirId);
            string strAbsBasePath = Utils.GetAbsolutePath(strLocalFileRoot, strRelBasePath);

            // get deletes data starting from remote data
            if (lstSelectedNames == null && intBaseDirId != 0)
            {
                // if we get here, the user selected a remote directory in the tree view

                // get remote directories
                string strSql = @"
                    select
                        s.dir_id,
                        s.parent_id,
                        t.dir_name,
                        replace('pwa' || coalesce(t.rel_path,''), '/', '\') as relative_path,
                        null::text as absolute_path,
                        true as is_remote,
                        case when c.dir_id is null then false else true end as wont_delete
                    from fcn_directory_recursive(:dir_id) as s
                    left join view_dir_tree as t on s.dir_id=t.dir_id and s.parent_id=t.parent_id
                    left join (
                        select distinct dir_id
                        from hp_entry
                        where checkout_user is not null
                    ) as c on c.dir_id=s.dir_id
                    order by relative_path desc;
                ";

                // put the remote directories in the DataSet
                NpgsqlDataAdapter daTemp1 = new NpgsqlDataAdapter(strSql, connDb);
                daTemp1.SelectCommand.Parameters.AddWithValue("dir_id", intBaseDirId);
                daTemp1.SelectCommand.Parameters.AddWithValue("local_root", strLocalFileRoot);
                dsDeletes.Tables.Add("dirs");
                daTemp1.Fill(dsDeletes.Tables["dirs"]);

                // populate absolute paths
                foreach (DataRow dr in dsDeletes.Tables["dirs"].Rows)
                {
                    dr.SetField<string>("absolute_path", Utils.GetAbsolutePath(strLocalFileRoot, dr.Field<string>("relative_path")));
                }

                // select entries in or below the specified directory
                // don't select dependencies of those entries
                strSql = "select * from fcn_latest_by_dir(:dir_id);";

                // put the remote entries in the DataSet
                NpgsqlDataAdapter daTemp2 = new NpgsqlDataAdapter(strSql, connDb);
                daTemp2.SelectCommand.Parameters.AddWithValue("dir_id", intBaseDirId);
                dsDeletes.Tables.Add("files");
                daTemp2.Fill(dsDeletes.Tables["files"]);

                // populate absolute paths
                foreach (DataRow dr in dsDeletes.Tables["files"].Rows)
                {
                    dr.SetField<string>("absolute_path", Utils.GetAbsolutePath(strLocalFileRoot, dr.Field<string>("relative_path")));
                }

                // loop local directories, matching or adding directories/files to be deleted
                DirectoryInfo dirBase = new DirectoryInfo(strAbsBasePath);
                blnFailed = LoadCombinedData(sender, e, ref dsDeletes, strRelBasePath);

                if (dirBase.Exists)
                {
                    foreach (DirectoryInfo dir in dirBase.GetDirectories("*", SearchOption.AllDirectories))
                    {

                        string strAbsPath = dir.FullName;
                        string strRelPath = Utils.GetRelativePath(strLocalFileRoot, strAbsPath);
                        string strParentRelPath = Utils.GetParentDirectory(strRelPath);

                        int intDirId = 0;
                        dictTree.TryGetValue(strRelPath, out intDirId);

                        int intParentId = 0;
                        dictTree.TryGetValue(strParentRelPath, out intParentId);

                        // check for remote existence
                        DataRow[] drTest = dsDeletes.Tables["dirs"].Select("absolute_path='" + strAbsPath.Replace("'", "''") + "'");
                        if (drTest.Length == 0)
                        {
                            // add local-only directory
                            dsDeletes.Tables["dirs"].Rows.Add(
                                intDirId, // dir_id
                                intParentId, // parent_id
                                dir.Name, // dir_name
                                strRelPath, // relative_path
                                strAbsPath, // absolute_path
                                (intDirId != 0), // is_remote
                                false //wont_delete
                            );
                        }

                        // combine remote data with local data
                        blnFailed = LoadCombinedData(sender, e, ref dsDeletes, strRelPath);

                    }
                }

                // return results
                return dsDeletes;

            }

            // init directories data table
            dsDeletes.Tables.Add("dirs");
            dsDeletes.Tables["dirs"].Columns.Add("dir_id", Type.GetType("System.Int32"));
            dsDeletes.Tables["dirs"].Columns.Add("parent_id", Type.GetType("System.Int32"));
            dsDeletes.Tables["dirs"].Columns.Add("dir_name", Type.GetType("System.String"));
            dsDeletes.Tables["dirs"].Columns.Add("relative_path", Type.GetType("System.String"));
            dsDeletes.Tables["dirs"].Columns.Add("absolute_path", Type.GetType("System.String"));
            dsDeletes.Tables["dirs"].Columns.Add("is_remote", Type.GetType("System.Boolean"));
            dsDeletes.Tables["dirs"].Columns.Add("wont_delete", Type.GetType("System.Boolean"));

            // get data for selected entries
            if (lstSelectedNames != null)
            {
                // if we get here, the user selected individual entries (files) from the list view

                // get selected entries (files) into a new datatable
                string strSelectedNames = String.Join("','", lstSelectedNames.ToArray());
                DataTable dtTemp = dsList.Tables["files"].Select("entry_name in ('" + strSelectedNames + "')").CopyToDataTable();
                dsDeletes.Tables.Add(dtTemp);
                dsDeletes.Tables[1].TableName = "files";

                // return results
                return dsDeletes;

            }

            if (strRelBasePath != null)
            {
                // if we get here, we are operating on a local only directory

                // add this directory to the dirs table
                // this directory is local only, so all child directories will be local only
                // that means they won't have a directory id
                // we don't care if the parent is remote, since we do nothing with the parent
                string strBaseName = Utils.GetBaseName(strRelBasePath);

                dsDeletes.Tables["dirs"].Rows.Add(
                    0, // dir_id
                    0, // parent_id
                    strBaseName, // dir_name
                    strRelBasePath, // relative_path
                    strAbsBasePath, // absolute_path
                    false, // is_remote
                    false // wont_delete
                );

                // add an empty DataTable for files
                dsDeletes.Tables.Add(CreateFileTable());

                // loop list of local directories, adding local files to be deleted
                DirectoryInfo dirBase = new DirectoryInfo(strAbsBasePath);
                blnFailed = LoadCombinedData(sender, e, ref dsDeletes, strRelBasePath);

                foreach (DirectoryInfo dir in dirBase.GetDirectories("*", SearchOption.AllDirectories))
                {
                    string strRelPath = Utils.GetRelativePath(strLocalFileRoot, dir.FullName);
                    dsDeletes.Tables["dirs"].Rows.Add(
                        0, // dir_id
                        0, // parent_id
                        dir.Name, // dir_name
                        strRelPath, // relative_path
                        dir.FullName, // absolute_path
                        false, // is_remote
                        false // wont_delete
                    );
                    blnFailed = LoadCombinedData(sender, e, ref dsDeletes, strRelPath);
                }

                return dsDeletes;

            }

            // if we get this far, there's something wrong
            return null;

        }

        protected bool LoadCombinedData(object sender, DoWorkEventArgs e, ref DataSet dsCombined, string strRelPath)
        {

            // TODO: we may need multiple status fields
            // the current status codes are not mutually exclusive
            // however, we only have room for one overlay icon
            //    new status fields:
            //        locality
            //            local only
            //            remote only
            //            both
            //        checkouts
            //            checked out to me here
            //            checked out to me elsewhere
            //            checked out to other
            //        type-filter
            //            no remote type
            //            type ignored/filtered

            // TODO: add additional status codes
            // nv and co could co-exist --> add new icon for nvco
            // ro and co could co-exist --> add new icon for roco
            // ro and cm could co-exist --> add new icon for rocm
            //
            // until I get around to adding the additional codes, let these take affect in the following order
            // for example, nv comes before co and ro comes before co
            // that means if a file is both nv and co, it should get assigned co
            //
            // ro: remote only
            // lo: local only
            // lm: modified locally without checking out
            // nv: new remote version
            // cm: checked out to me
            // co: checked out to other
            // ft: no remote file type
            // if: ignore filter
            // dt: deleted
            // ok: nothing to report (none of the above)

            // running in separate thread
            BackgroundWorker myWorker = sender as BackgroundWorker;

            // check for cancellation
            if (sender != null && (myWorker.CancellationPending == true))
            {
                e.Cancel = true;
                return true;
            }

            // check for non-local directory
            string strAbsPath = Utils.GetAbsolutePath(strLocalFileRoot, strRelPath);
            if (!Directory.Exists(strAbsPath))
            {
                return false;
            }

            // try to get remote directory id
            int intDirId = 0;
            dictTree.TryGetValue(strRelPath, out intDirId);

            // try to get a list of local files from this directory
            string[] strFiles;
            try
            {

                strFiles = Directory.GetFiles(strAbsPath);
            }
            catch (IOException ex)
            {
                MessageBox.Show("Error: Drive not ready or directory does not exist: " + ex);
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show("Error: Drive or directory access denided: " + ex);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex);
                return true;
            }

            string strFileName = "";
            DateTime dtModifyDate;
            Int64 lngFileSize = 0;

            //loop through local files
            foreach (string strFile in strFiles)
            {

                // check for cancellation
                if (sender != null && (myWorker.CancellationPending == true))
                {
                    e.Cancel = true;
                    return true;
                }

                // get file info
                strFileName = Utils.GetShortName(strFile);
                FileInfo fiCurrFile = new FileInfo(strFile);
                lngFileSize = fiCurrFile.Length;
                dtModifyDate = fiCurrFile.LastWriteTime;

                // skip temp files
                if (strFileName.StartsWith("~"))
                {
                    continue;
                }

                // get matching remote file
                DataRow[] drRemFile = dsCombined.Tables["files"].Select(String.Format("entry_name='{0}' and absolute_path='{1}'", strFileName.Replace("'", "''"), strAbsPath.Replace("'", "''")));

                // set status code empty, so we can detect unexpected status combinations
                string strClientStatusCode = "";

                if (drRemFile.Length != 0)
                {
                    // update row for remote file with local data

                    // flag remote file as also being local
                    DataRow drTemp = drRemFile[0];
                    drTemp.SetField<bool>("is_local", true);

                    // set status code
                    #region set status code
                    // if local stamp is greater than remote stamp
                    if (dtModifyDate.Subtract(drTemp.Field<DateTime>("latest_stamp")).TotalSeconds > intSecondsTolerance)
                    {
                        // lm: modified locally without checking out (apparently)
                        strClientStatusCode = "lm";
                    }
                    // if remote stamp is greater than local stamp
                    else if (dtModifyDate.Subtract(drTemp.Field<DateTime>("latest_stamp")).TotalSeconds < -intSecondsTolerance)
                    {
                        // nv: new remote version
                        strClientStatusCode = "nv";
                    }
                    else
                    {
                        // ok: identical files
                        strClientStatusCode = "ok";
                    }

                    if (drTemp.Field<int?>("checkout_user") != null)
                    {
                        if (drTemp.Field<int?>("checkout_user") == intMyUserId && drTemp.Field<int?>("checkout_node") == intMyNodeId)
                        {
                            // checked out to me (current user)
                            strClientStatusCode = "cm";
                        }
                        else
                        {
                            // checked out to someone else
                            strClientStatusCode = "co";
                        }
                    }

                    if (drTemp.Field<bool>("active") == false)
                    {
                        // deleted existing locally
                        strClientStatusCode = "dt";
                    }

                    // we have identified the correct client_status_code, now set it
                    drTemp.SetField<string>("client_status_code", strClientStatusCode);
                    #endregion

                    // format the remote file size
                    drTemp.SetField<string>("str_latest_size", Utils.FormatSize(drTemp.Field<long>("latest_size")));

                    // format the remote modify date
                    drTemp.SetField<string>("str_latest_stamp", Utils.FormatDate(drTemp.Field<DateTime>("latest_stamp")));

                    // format the local file size
                    drTemp.SetField<Int64>("local_size", lngFileSize);
                    drTemp.SetField<string>("str_local_size", Utils.FormatSize(lngFileSize));

                    // format the local modify date
                    drTemp.SetField<DateTime>("local_stamp", dtModifyDate);
                    drTemp.SetField<string>("str_local_stamp", Utils.FormatDate(dtModifyDate));

                    // format the checkout date
                    object oDate = drTemp["checkout_date"];
                    if (oDate == System.DBNull.Value)
                    {
                        drTemp.SetField<string>("str_checkout_date", null);
                    }
                    else
                    {
                        drTemp.SetField<string>("str_checkout_date", Utils.FormatDate(Convert.ToDateTime(oDate)));
                    }

                    // get local checksum
                    drTemp.SetField<string>("local_md5", Utils.StringMD5(strFile));

                }
                else
                {
                    // insert new row for local-only file

                    // identify file type
                    Tuple<string, string, string, string> tplExtensions = ftmStart.GetFileExt(strFile);
                    string strFileExt = tplExtensions.Item1;
                    string strRemFileExt = tplExtensions.Item2;
                    string strRemBlockExt = tplExtensions.Item3;
                    string strWinFileExt = tplExtensions.Item4;

                    // set status code
                    strClientStatusCode = "lo";
                    if (strRemBlockExt == strFileExt)
                    {
                        strClientStatusCode = "if";
                    }
                    else if (strRemFileExt == "")
                    {
                        strClientStatusCode = "ft";
                    }

                    dsList.Tables[0].Rows.Add(
                            null, // entry_id
                            null, // version_id
                            intDirId, // dir_id
                            strFileName, // entry_name
                            null, // type_id
                            strFileExt, // file_ext
                            null, // cat_id
                            null, // cat_name
                            lngFileSize, // latest_size
                            Utils.FormatSize(lngFileSize), // str_latest_size
                            lngFileSize, // local_size
                            Utils.FormatSize(lngFileSize), // str_local_size
                            null, // latest_stamp
                            "", // str_latest_stamp
                            dtModifyDate, // local stamp
                            Utils.FormatDate(dtModifyDate), // local formatted stamp
                            null, // latest_md5
                            null, // local_md5 (set null because if we AddNew, we will calculate MD5 then)
                            null, // checkout_user
                            null, // ck_user_name
                            null, // checkout_date
                            null, // str_checkout_date
                            null, // checkout_node
                            null, // checkout_node_name
                            true, // is_local
                            false, // is_remote
                            strClientStatusCode, // client_status_code
                            strRelPath, // relative_path
                            strAbsPath, // absolute_path
                            null, // icon
                            false, // is_depend_searched
                            fiCurrFile.IsReadOnly, // is_readonly
                            true, // active
                            false // destroyed
                        );

                } // end else

            } // end foreach

            return false;

        }

        private bool DeleteLocalFiles(object sender, DoWorkEventArgs e, NpgsqlTransaction t, bool blnDeleteDirs, ref DataSet dsDeletes)
        {
            // running in separate thread
            BackgroundWorker myWorker = sender as BackgroundWorker;

            bool blnFailed = false;

            // if deleting directories
            if (blnDeleteDirs)
            {

                // all directories will be in a single tree
                // in other words, we won't have any independent paths
                // that means we can get the highest level path (shortest path) simply by sorting and taking the first one
                string strRelPath = dsDeletes.Tables["dirs"].Select("", "relative_path")[0].Field<string>("relative_path");
                string strAbsPath = Utils.GetAbsolutePath(strLocalFileRoot, strRelPath);

                // delete recursively
                if (Directory.Exists(strAbsPath))
                {
                    dlgStatus.AddStatusLine("INFO", "Deleting directory: " + strAbsPath);
                    DirectoryInfo dir = new DirectoryInfo(strAbsPath) { Attributes = FileAttributes.Normal };

                    foreach (var info in dir.GetFileSystemInfos("*", SearchOption.AllDirectories))
                    {
                        info.Attributes = FileAttributes.Normal;
                    }

                    try
                    {
                        dir.Delete(true);
                    }
                    catch (Exception ex)
                    {
                        dlgStatus.AddStatusLine("ERROR", "Failed on recursive delete of directory (" + ex.Message + "): " + strAbsPath);
                        blnFailed = true;
                    }
                }

            }
            else
            {

                foreach (DataRow drFile in dsDeletes.Tables["files"].Rows)
                {

                    // check for cancellation
                    if ((myWorker.CancellationPending == true))
                    {
                        e.Cancel = true;
                        return true;
                    }

                    // get the file
                    string strFileName = drFile.Field<string>("entry_name");
                    string strAbsolutePath = drFile.Field<string>("absolute_path");
                    string strFullName = strAbsolutePath + "\\" + strFileName;
                    FileInfo fiCurrent = new FileInfo(strFullName);

                    if (!fiCurrent.Exists) continue;

                    // try deleting it
                    dlgStatus.AddStatusLine("INFO", "Deleting file: " + strFullName);
                    try
                    {
                        // set the file not readonly
                        fiCurrent.IsReadOnly = false;
                        fiCurrent.Delete();
                    }
                    catch (Exception ex)
                    {
                        dlgStatus.AddStatusLine("ERROR", "Failed deleting file:" + ex.Message);
                        blnFailed = true;
                    }
                }

            }

            return blnFailed;

        }

        private bool StageRemotesForDelete(object sender, DoWorkEventArgs e, NpgsqlTransaction t, string strGuid, ref DataSet dsDeletes)
        {

            // move entry directories to a temporary directory
            // we will actually delete them later, upon successful completion of all other operations

            // running in separate thread
            BackgroundWorker myWorker = sender as BackgroundWorker;

            bool blnFailed = false;

            // create webdav directory to be deleted
            connDav.CreateDir("/tmp/" + strGuid);

            DataRow[] drRemotes = dsDeletes.Tables["files"].Select("entry_id is not null");
            foreach (DataRow drRemote in drRemotes)
            {

                // check for cancellation
                if ((myWorker.CancellationPending == true))
                {
                    e.Cancel = true;
                    return true;
                }

                // get the collection name (that's webdav terminology for a directory name)
                // the collection is named after the entry_id
                int intEntryId = drRemote.Field<int>("entry_id");
                string strSrcPath = "/" + intEntryId.ToString();
                string strDestPath = "/tmp/" + strGuid + "/" + intEntryId.ToString();

                // get the file name
                string strFileName = drRemote.Field<string>("entry_name");
                string strAbsolutePath = drRemote.Field<string>("absolute_path");
                string strFullName = strAbsolutePath + "\\" + strFileName;

                // move the collection inside our new temporary collection (guid) on the webdav server
                // later, we will delete the temporary collection, and everything inside
                dlgStatus.AddStatusLine("INFO", "Staging file for delete: " + strFullName);
                try
                {
                    connDav.MoveDir(strSrcPath, strDestPath);
                }
                catch (Exception ex)
                {
                    dlgStatus.AddStatusLine("WARNING", ex.Message);
                    //if (connDav.StatusCode - 200 >= 100)
                    //{
                    //    dlgStatus.AddStatusLine("ERROR", connDav.StatusCode + "(file: " + strFullName + ")");
                    //    blnFailed = true;
                    //}
                }

            }

            return blnFailed;

        }

        private bool RollbackDeletes(object sender, DoWorkEventArgs e, NpgsqlTransaction t, string strGuid, ref DataSet dsDeletes)
        {

            // move entry directories from the temporary directory, back to the root directory

            // running in separate thread
            BackgroundWorker myWorker = sender as BackgroundWorker;

            bool blnFailed = false;

            // get list of entries to be restored
            List<string> lstDirs = connDav.List("/tmp/" + strGuid);
            lstDirs.Remove("/tmp/" + strGuid + "/");

            foreach (string strDir in lstDirs)
            {

                // check for cancellation
                if ((myWorker.CancellationPending == true))
                {
                    e.Cancel = true;
                    return true;
                }

                // get the collection name (that's webdav terminology for a directory name)
                // the collection is named after the entry_id
                string strSrcPath = "/" + strDir;
                string strDestPath = strDir.Replace("/tmp/" + strGuid, "");

                // move the collection inside our new temporary collection (guid) on the webdav server
                // later, we will delete the temporary collection, and everything inside
                dlgStatus.AddStatusLine("INFO", "Restoring entry ID: " + strDir);
                connDav.MoveDir(strSrcPath, strDestPath);
                if (connDav.StatusCode - 200 >= 100)
                {
                    dlgStatus.AddStatusLine("WARNING", connDav.StatusCode + "(entry_id: " + strDir + ")");
                    blnFailed = true;
                }

            }

            // delete the now-empty staging directory
            connDav.Delete("/tmp/" + strGuid);

            return blnFailed;

        }

        private bool DeleteRemoteEntries(object sender, DoWorkEventArgs e, NpgsqlTransaction t, bool blnDestroy, ref DataSet dsDeletes)
        {

            // we don't actually ever delete from the database
            // for logical deletes, we set the active field to false
            // for permanent deletes, we also set the destroyed field to true

            BackgroundWorker myWorker = sender as BackgroundWorker;
            bool blnFailed = false;

            // make sure we are working inside of a transaction
            if (t.Connection == null)
            {
                MessageBox.Show("The database transaction is not functional");
                return true;
            }

            string strSql;

            // build comma separated list of entries
            string strEntries = "";
            foreach (DataRow row in dsDeletes.Tables["files"].Rows)
            {
                strEntries += row.Field<int>("entry_id").ToString() + ",";
            }

            // drop trailing comma
            strEntries = strEntries.Substring(0, strEntries.Length - 1);

            // prepare a database command to delete (inactivate) entries, all-at-once
            strSql = @"
                update hp_entry set
                    active=:active_flag,
                    delete_user=:delete_user,
                    delete_stamp=:delete_stamp,
                    destroyed=:destroyed_flag,
                    destroy_user=:destroy_user,
                    destroy_stamp=:destroy_stamp
                where entry_id in (" + strEntries + ")";

            NpgsqlCommand cmdInactivateEntry = new NpgsqlCommand(strSql, connDb, t);
            cmdInactivateEntry.Parameters.Add(new NpgsqlParameter("active_flag", NpgsqlTypes.NpgsqlDbType.Boolean));
            cmdInactivateEntry.Parameters.Add(new NpgsqlParameter("destroyed_flag", NpgsqlTypes.NpgsqlDbType.Boolean));
            cmdInactivateEntry.Parameters.Add(new NpgsqlParameter("delete_user", NpgsqlTypes.NpgsqlDbType.Integer));
            cmdInactivateEntry.Parameters.Add(new NpgsqlParameter("delete_stamp", NpgsqlTypes.NpgsqlDbType.Timestamp));
            cmdInactivateEntry.Parameters.Add(new NpgsqlParameter("destroy_user", NpgsqlTypes.NpgsqlDbType.Integer));
            cmdInactivateEntry.Parameters.Add(new NpgsqlParameter("destroy_stamp", NpgsqlTypes.NpgsqlDbType.Timestamp));
            cmdInactivateEntry.Parameters["active_flag"].Value = false;
            cmdInactivateEntry.Parameters["destroyed_flag"].Value = blnDestroy;
            cmdInactivateEntry.Parameters["delete_user"].Value = intMyUserId;
            cmdInactivateEntry.Parameters["delete_stamp"].Value = DateTime.Now;

            if (blnDestroy)
            {
                cmdInactivateEntry.Parameters["destroy_user"].Value = intMyUserId;
                cmdInactivateEntry.Parameters["destroy_stamp"].Value = DateTime.Now;
            }
            else
            {
                cmdInactivateEntry.Parameters["destroy_user"].Value = DBNull.Value;
                cmdInactivateEntry.Parameters["destroy_stamp"].Value = DBNull.Value;
            }

            // try deleting (inactivating)
            try
            {
                cmdInactivateEntry.ExecuteNonQuery();
            }
            catch (NpgsqlException ex)
            {
                // integrity constraint violation?
                dlgStatus.AddStatusLine("ERROR", ex.Message);
                blnFailed = true;
            }

            return blnFailed;

        }

        private bool DeleteRemoteDirs(object sender, DoWorkEventArgs e, NpgsqlTransaction t, ref DataSet dsDeletes)
        {

            // we don't actually ever delete from the database
            // directories only get logically deleted, there's nothing to destroy on the webdav side

            BackgroundWorker myWorker = sender as BackgroundWorker;
            bool blnFailed = false;

            // make sure we are working inside of a transaction
            if (t.Connection == null)
            {
                MessageBox.Show("The database transaction is not functional");
                return true;
            }

            // return when no directories to be deleted
            if (dsDeletes.Tables["dirs"].Rows.Count < 1) return blnFailed;

            string strSql;

            // build comma separated list of dirs
            string strDirs = "";
            foreach (DataRow row in dsDeletes.Tables["dirs"].Rows)
            {
                strDirs += row.Field<int>("dir_id").ToString() + ",";
            }

            // drop trailing comma
            strDirs = strDirs.Substring(0, strDirs.Length - 1);

            // prepare a database command to delete (inactivate) entries, all-at-once
            strSql = @"
                update hp_directory set
                    active=:active_flag,
                    modify_user=:mod_user,
                    modify_stamp=:mod_stamp
                where dir_id in (" + strDirs + ")";

            NpgsqlCommand cmdInactivateDir = new NpgsqlCommand(strSql, connDb, t);
            cmdInactivateDir.Parameters.AddWithValue("active_flag", false);
            cmdInactivateDir.Parameters.AddWithValue("mod_user", intMyUserId);
            cmdInactivateDir.Parameters.AddWithValue("mod_stamp", DateTime.Now);

            // try deleting (inactivating)
            try
            {
                cmdInactivateDir.ExecuteNonQuery();
            }
            catch (NpgsqlException ex)
            {
                // integrity constraint violation?
                dlgStatus.AddStatusLine("ERROR", ex.Message);
                blnFailed = true;
            }

            return blnFailed;

        }

        private bool UnDeleteRemoteEntries(object sender, DoWorkEventArgs e, NpgsqlTransaction t, ref DataSet dsDeletes)
        {

            // we can't undelete permanent deletes, only logical deletes

            BackgroundWorker myWorker = sender as BackgroundWorker;
            bool blnFailed = false;

            // make sure we are working inside of a transaction
            if (t.Connection == null)
            {
                MessageBox.Show("The database transaction is not functional");
                return true;
            }

            string strSql;

            // build comma separated list of entries
            string strEntries = "";
            foreach (DataRow row in dsDeletes.Tables["files"].Rows)
            {
                strEntries += row.Field<int>("entry_id").ToString() + ",";
            }

            // drop trailing comma
            strEntries = strEntries.Substring(0, strEntries.Length - 1);

            // prepare a database command to undelete (activate) entries, all-at-once
            strSql = @"
                update hp_entry set
                    active=true,
                    delete_user=NULL,
                    delete_stamp=NULL
                where destroyed=false
                and entry_id in (" + strEntries + ")";

            NpgsqlCommand cmdActivateEntry = new NpgsqlCommand(strSql, connDb, t);

            // try undeleting (activating)
            try
            {
                cmdActivateEntry.ExecuteNonQuery();
            }
            catch (NpgsqlException ex)
            {
                // integrity constraint violation?
                dlgStatus.AddStatusLine("ERROR", ex.Message);
                blnFailed = true;
            }

            return blnFailed;

        }

        private bool UnDeleteRemoteDirs(object sender, DoWorkEventArgs e, NpgsqlTransaction t, ref DataSet dsDeletes)
        {

            // we can't undelete permanent deletes, only logical deletes

            BackgroundWorker myWorker = sender as BackgroundWorker;
            bool blnFailed = false;

            // make sure we are working inside of a transaction
            if (t.Connection == null)
            {
                MessageBox.Show("The database transaction is not functional");
                return true;
            }

            // return when no directories to be undeleted
            if (dsDeletes.Tables["dirs"].Rows.Count < 1) return blnFailed;

            string strSql;

            // build comma separated list of dirs
            string strDirs = "";
            foreach (DataRow row in dsDeletes.Tables["dirs"].Rows)
            {
                strDirs += row.Field<int>("dir_id").ToString() + ",";
            }

            // drop trailing comma
            strDirs = strDirs.Substring(0, strDirs.Length - 1);

            // prepare a database command to undelete (activate) directories, all-at-once
            strSql = @"
                update hp_directory set
                    active=true,
                    modify_user=NULL,
                    modify_stamp=NULL
                where dir_id in (" + strDirs + ")";

            NpgsqlCommand cmdInactivateDir = new NpgsqlCommand(strSql, connDb, t);

            // try undeleting (activating)
            try
            {
                cmdInactivateDir.ExecuteNonQuery();
            }
            catch (NpgsqlException ex)
            {
                // integrity constraint violation?
                dlgStatus.AddStatusLine("ERROR", ex.Message);
                blnFailed = true;
            }

            return blnFailed;

        }

        private bool PurgeRemoteFiles(object sender, DoWorkEventArgs e, NpgsqlTransaction t, string strGuid, ref DataSet dsDeletes)
        {

            // delete files staged for delete on the webdav server

            // running in separate thread
            BackgroundWorker myWorker = sender as BackgroundWorker;

            // check for cancellation
            if ((myWorker.CancellationPending == true))
            {
                e.Cancel = true;
                return true;
            }

            bool blnFailed = false;

            // write status
            dlgStatus.AddStatusLine("INFO", "Deleting staged files from webdav server: staging directory /tmp/" + strGuid + "");
            connDav.Delete("/tmp/" + strGuid);

            if (connDav.StatusCode - 200 >= 100)
            {
                dlgStatus.AddStatusLine("ERROR", connDav.StatusCode + "(staging directory: /tmp/" + strGuid + ")");
                blnFailed = true;
            }

            return blnFailed;

        }

        #endregion


        #region TreeView actions

        void TreeView1AfterSelect(object sender, TreeViewEventArgs e)
        {

            // Set cursor as hourglass
            Cursor.Current = Cursors.WaitCursor;

            TreeNode tnCurrent = treeView1.SelectedNode;
            PopulateList(tnCurrent);

            // Set cursor as default arrow
            Cursor.Current = Cursors.Default;

        }

        void TreeRightMouseClick(object sender, MouseEventArgs e)
        {

            // get latest
            // checkout
            // add new
            // commit
            // undo checkout

            // check for the right mouse button
            if (e.Button != MouseButtons.Right)
            {
                return;
            }

            // verify that a tree node was right clicked, and select it
            treeView1.SelectedNode = treeView1.GetNodeAt(e.X, e.Y);
            if (treeView1.SelectedNode == null)
            {
                return;
            }
            TreeNode tnClicked = treeView1.SelectedNode;

            // reset context menu items
            //   get latest
            //   checkout
            //   commit
            //   undo checkout
            foreach (ToolStripMenuItem tsmiItem in cmsTree.Items)
            {
                tsmiItem.Enabled = true;
                tsmiItem.Visible = true;
            }

            // test for remote
            if (tnClicked.Tag != null)
            {

                // exists remotely
                // still allow AddNew and handle remotely existing files one-at-a-time
                //cmsTree.Items["cmsTreeAddNew"].Enabled = false;

                // test for local
                if (Directory.Exists(Utils.GetAbsolutePath(strLocalFileRoot, tnClicked.FullPath)) != true)
                {
                    // does not exist locally, so we can't commit or undo a checkout
                    cmsTree.Items["cmsTreeCommit"].Enabled = false;
                    cmsTree.Items["cmsTreeUndoCheckout"].Enabled = false;
                }
            }
            else
            {
                // does not exist remotely (local only)
                // can't do any of the following
                cmsTree.Items["cmsTreeGetLatest"].Enabled = false;
                cmsTree.Items["cmsTreeCheckout"].Enabled = false;
                cmsTree.Items["cmsTreeUndoCheckout"].Enabled = false;
            }

            return;
        }

        void CmsTreeGetLatestClick(object sender, EventArgs e)
        {

            // create the status dialog
            dlgStatus = new StatusDialog();

            // get directory info
            TreeNode tnCurrent = treeView1.SelectedNode;
            if (tnCurrent.Tag == null)
            {
                MessageBox.Show("The current directory doesn't exist remotely, therefore you can't get latest on any of the selected files.");
                return;
            }
            string strRelBasePath = tnCurrent.FullPath;

            // start the database transaction
            t = connDb.BeginTransaction();

            // package arguments for the background worker
            List<object> arguments = new List<object>();
            arguments.Add(t);
            arguments.Add(strRelBasePath);
            arguments.Add(null);

            // launch the background thread
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerSupportsCancellation = true;
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
            worker.DoWork += new DoWorkEventHandler(worker_GetLatest);
            worker.RunWorkerAsync(arguments);


            // handle the cancel button
            bool blnWorkCanceled = dlgStatus.ShowStatusDialog("Get Latest");
            if (blnWorkCanceled == true)
            {
                worker.CancelAsync();
            }

            // refresh the main window
            ResetView(tnCurrent.FullPath);

        }

        void CmsTreeCheckoutClick(object sender, EventArgs e)
        {

        }

        void CmsTreeCommitClick(object sender, EventArgs e)
        {

            // create the status dialog
            dlgStatus = new StatusDialog();

            // start the database transaction
            t = connDb.BeginTransaction();

            // get directory info
            TreeNode tnCurrent = treeView1.SelectedNode;
            string strRelBasePath = tnCurrent.FullPath;

            // package arguments for the background worker
            List<object> arguments = new List<object>();
            arguments.Add(t); // transaction
            arguments.Add(strRelBasePath); // selected path
            arguments.Add(null); // selected list

            // launch the background thread
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerSupportsCancellation = true;
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
            worker.DoWork += new DoWorkEventHandler(worker_Commit);
            worker.RunWorkerAsync(arguments);


            // handle the cancel button
            bool blnWorkCanceled = dlgStatus.ShowStatusDialog("Tree Commit");
            if (blnWorkCanceled == true)
            {
                worker.CancelAsync();
            }

            // refresh the main window
            ResetView(tnCurrent.FullPath);

        }

        void CmsTreeAnalyzeClick(object sender, EventArgs e)
        {

            // do something useful for all directories and files beneath this node:
            //   load another window with a list of special items
            //   local only items
            //   checked-out items
            //   remote changes

        }

        void CmsTreeUndoCheckoutClick(object sender, EventArgs e)
        {

            // create the status dialog
            dlgStatus = new StatusDialog();

            // start the database transaction
            t = connDb.BeginTransaction();

            // get directory info
            TreeNode tnCurrent = treeView1.SelectedNode;
            if (tnCurrent.Tag == null)
            {
                // it shouldn't be possible to get here because we disable the option on the context menu
                MessageBox.Show("The current directory doesn't exist remotely, therefore you can't have any of the contained files checked out.");
                return;
            }
            string strRelBasePath = tnCurrent.FullPath;

            // package arguments for the background worker
            List<object> arguments = new List<object>();
            arguments.Add(t); // transaction
            arguments.Add(strRelBasePath); // selected path
            arguments.Add(null); // selected list

            // launch the background thread
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerSupportsCancellation = true;
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
            worker.DoWork += new DoWorkEventHandler(worker_Commit);
            worker.RunWorkerAsync(arguments);


            // handle the cancel button
            bool blnWorkCanceled = dlgStatus.ShowStatusDialog("Tree Undo Checkout");
            if (blnWorkCanceled == true)
            {
                worker.CancelAsync();
            }

            // refresh the main window
            ResetView(tnCurrent.FullPath);

        }

        private void CmsTreeDeleteLogicalClick(object sender, EventArgs e)
        {

            // create the status dialog
            dlgStatus = new StatusDialog();

            // get directory info
            TreeNode tnCurrent = treeView1.SelectedNode;
            string strRelBasePath = tnCurrent.FullPath;

            // start the database transaction
            t = connDb.BeginTransaction();

            // package arguments for the background worker
            List<object> arguments = new List<object>();

            arguments.Add(t);
            arguments.Add(strRelBasePath);
            arguments.Add(null);
            arguments.Add(false); // destroy flag

            // launch the background thread
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerSupportsCancellation = true;
            worker.DoWork += new DoWorkEventHandler(worker_Delete);
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
            worker.RunWorkerAsync(arguments);

            // show dialog and handle cancel button
            bool blnWorkCanceled = dlgStatus.ShowStatusDialog("Delete Logical");
            if (blnWorkCanceled == true)
            {
                worker.CancelAsync();
            }

            ResetView(tnCurrent.FullPath);

        }

        private void CmsTreeDeletePermanentClick(object sender, EventArgs e)
        {

            // check user
            if (intMyUserId != 1)
            {
                var result = MessageBox.Show("Only the administrator can destroy",
                "Permission Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
                return;
            }

            // create the status dialog
            dlgStatus = new StatusDialog();

            // get directory info
            TreeNode tnCurrent = treeView1.SelectedNode;
            string strRelBasePath = tnCurrent.FullPath;

            // start the database transaction
            t = connDb.BeginTransaction();

            // package arguments for the background worker
            List<object> arguments = new List<object>();

            arguments.Add(t);
            arguments.Add(strRelBasePath);
            arguments.Add(null);
            arguments.Add(true); // destroy flag

            // launch the background thread
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerSupportsCancellation = true;
            worker.DoWork += new DoWorkEventHandler(worker_Delete);
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
            worker.RunWorkerAsync(arguments);

            // show dialog and handle cancel button
            bool blnWorkCanceled = dlgStatus.ShowStatusDialog("Delete Permanent");
            if (blnWorkCanceled == true)
            {
                worker.CancelAsync();
            }

            ResetView(tnCurrent.FullPath);

        }

        #endregion


        #region ListView actions

        void lv1ColumnClick(object sender, ColumnClickEventArgs e)
        {

            // Determine if clicked column is already the column that is being sorted.
            if (e.Column == lvwColumnSorter.SortColumn)
            {
                // Reverse the current sort direction for this column.
                if (lvwColumnSorter.Order == SortOrder.Ascending)
                {
                    lvwColumnSorter.Order = SortOrder.Descending;
                }
                else
                {
                    lvwColumnSorter.Order = SortOrder.Ascending;
                }
            }
            else
            {
                // Set the column number that is to be sorted; default to ascending.
                lvwColumnSorter.SortColumn = e.Column;
                lvwColumnSorter.Order = SortOrder.Ascending;
            }

            // Perform the sort with these new sort options.
            this.listView1.Sort();

        }

        void ListRightMouseClick(object sender, MouseEventArgs e)
        {

            // get latest
            // checkout
            // add new
            // commit
            // undo checkout

            // check for the right mouse button
            if (e.Button != MouseButtons.Right)
            {
                return;
            }

            // verify that a list item was right clicked, and select it/them
            ListView.SelectedListViewItemCollection lviSelection = listView1.SelectedItems;
            if (lviSelection.Count == 0)
            {
                // we never actually get here because the handler only gets called when an item is selected
                return;
            }
            // reset context menu items
            //   get latest     (remote)
            //   checkout       (remote)
            //   add new        (local only)
            //   commit         (checked-out to me)
            //   undo checkout  (checked-out to me)
            foreach (ToolStripMenuItem tsmiItem in cmsList.Items)
            {
                tsmiItem.Enabled = true;
            }

            // test for remote directory
            int dir_id;
            if (treeView1.SelectedNode.Tag != null)
            {
                dir_id = (int)treeView1.SelectedNode.Tag;

                foreach (ListViewItem lviSelected in lviSelection)
                {

                    string strFileName = (string)lviSelected.SubItems[0].Text;
                    DataRow drCurrent = dsList.Tables[0].Select("dir_id=" + dir_id + " and entry_name='" + strFileName + "'")[0];

                    if (drCurrent.Field<bool>("is_remote") == false)
                    {
                        // local only: need to disable "getlatest" and "checkout" but we
                        // really shouldn't do it now because there may be other items in the
                        // list that do exist remotely
                        cmsList.Items["cmsListGetLatest"].Enabled = false;
                        cmsList.Items["cmsListCheckout"].Enabled = false;
                    }

                    // test for checked-out-by-me
                    int? intCoUserId = drCurrent.Field<int?>("checkout_user");
                    if (intCoUserId != null)
                    {
                        if (intCoUserId == intMyUserId)
                        {
                            // it is checked out to me
                        }
                        else
                        {
                            // it is not checked out to me
                            cmsList.Items["cmsListCommit"].Enabled = false;
                            cmsList.Items["cmsListUndoCheckout"].Enabled = false;
                        }
                    }
                }

            }
            else
            {
                // all items are local only
                cmsList.Items["cmsListGetLatest"].Enabled = false;
                cmsList.Items["cmsListCheckout"].Enabled = false;
                cmsList.Items["cmsListUndoCheckout"].Enabled = false;
            }

            return;
        }

        void CmsListGetLatestClick(object sender, EventArgs e)
        {

            // refresh file data
            LoadListData(treeView1.SelectedNode.FullPath);

            // create the status dialog
            dlgStatus = new StatusDialog();

            // get directory info
            TreeNode tnCurrent = treeView1.SelectedNode;
            if (tnCurrent.Tag == null)
            {
                MessageBox.Show("The current directory doesn't exist remotely, therefore you can't get latest on any of the selected files.");
                return;
            }
            string strRelBasePath = tnCurrent.FullPath;

            // get a list of selected items
            List<int> lstSelected = new List<int>();
            ListView.SelectedListViewItemCollection lviSelection = listView1.SelectedItems;
            foreach (ListViewItem lviSelected in lviSelection)
            {
                DataRow drSelected = dsList.Tables[0].Select("entry_name='" + lviSelected.SubItems[0].Text + "'")[0];
                if (drSelected.Field<int?>("entry_id") != null)
                {
                    // ignore local-only files
                    int intSelectedEntryId = drSelected.Field<int>("entry_id");
                    lstSelected.Add(intSelectedEntryId);
                }
            }
            if (lstSelected.Count == 0)
            {
                MessageBox.Show("No remote files were selected.");
                return;
            }

            // start the database transaction
            t = connDb.BeginTransaction();

            // package arguments for the background worker
            List<object> arguments = new List<object>();

            arguments.Add(t);
            arguments.Add(strRelBasePath);
            arguments.Add(lstSelected);

            // launch the background thread
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerSupportsCancellation = true;
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
            worker.DoWork += new DoWorkEventHandler(worker_GetLatest);
            worker.RunWorkerAsync(arguments);


            // show dialog and handle cancel button
            bool blnWorkCanceled = dlgStatus.ShowStatusDialog("Get Latest");
            if (blnWorkCanceled == true)
            {
                worker.CancelAsync();
            }

            // refresh the main window
            ResetView(tnCurrent.FullPath);

        }

        void CmsListCheckOutClick(object sender, EventArgs e)
        {

            // create the status dialog
            dlgStatus = new StatusDialog();

            // get directory info
            TreeNode tnCurrent = treeView1.SelectedNode;
            if (tnCurrent.Tag == null)
            {
                MessageBox.Show("The current directory doesn't exist remotely, therefore you can't get latest on any of the selected files.");
                return;
            }
            string strRelBasePath = tnCurrent.FullPath;

            // get a list of selected items
            List<int> lstSelected = new List<int>();
            ListView.SelectedListViewItemCollection lviSelection = listView1.SelectedItems;
            foreach (ListViewItem lviSelected in lviSelection)
            {
                DataRow drSelected = dsList.Tables[0].Select("entry_name='" + lviSelected.SubItems[0].Text + "'")[0];
                if (drSelected.Field<int?>("entry_id") != null)
                {
                    // ignore local-only files
                    int intSelectedEntryId = drSelected.Field<int>("entry_id");
                    lstSelected.Add(intSelectedEntryId);
                }
            }
            if (lstSelected.Count == 0)
            {
                MessageBox.Show("No remote files were selected.");
                return;
            }

            // start the database transaction
            t = connDb.BeginTransaction();

            // package arguments for the background worker
            List<object> arguments = new List<object>();

            arguments.Add(t);
            arguments.Add(strRelBasePath);
            arguments.Add(lstSelected);

            // launch the background thread
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerSupportsCancellation = true;
            worker.DoWork += new DoWorkEventHandler(worker_CheckOut);
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
            worker.RunWorkerAsync(arguments);

            // show dialog and handle cancel button
            bool blnWorkCanceled = dlgStatus.ShowStatusDialog("Check Out");
            if (blnWorkCanceled == true)
            {
                worker.CancelAsync();
            }

            ResetView(tnCurrent.FullPath);

        }

        void CmsListCommitClick(object sender, EventArgs e)
        {

            // refresh file data
            LoadListData(treeView1.SelectedNode.FullPath);

            // create the status dialog
            dlgStatus = new StatusDialog();

            // get directory info
            TreeNode tnCurrent = treeView1.SelectedNode;
            string strRelBasePath = tnCurrent.FullPath;

            // get a list of selected items
            List<string> lstSelectedNames = new List<string>();
            ListView.SelectedListViewItemCollection lviSelection = listView1.SelectedItems;
            foreach (ListViewItem lviSelected in lviSelection)
            {
                lstSelectedNames.Add(lviSelected.SubItems[0].Text);
            }

            // start the database transaction
            t = connDb.BeginTransaction();

            // package arguments for the background worker
            List<object> arguments = new List<object>();

            arguments.Add(t);
            arguments.Add(strRelBasePath);
            arguments.Add(lstSelectedNames);

            // launch the background thread
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerSupportsCancellation = true;
            worker.DoWork += new DoWorkEventHandler(worker_Commit);
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
            worker.RunWorkerAsync(arguments);

            // show dialog and handle cancel button
            bool blnWorkCanceled = dlgStatus.ShowStatusDialog("List Commit");
            if (blnWorkCanceled == true)
            {
                worker.CancelAsync();
            }

            ResetView(tnCurrent.FullPath);

        }

        void CmsListUndoCheckoutClick(object sender, EventArgs e)
        {

            // refresh file data
            LoadListData(treeView1.SelectedNode.FullPath);

            // create the status dialog
            dlgStatus = new StatusDialog();

            // get directory info
            TreeNode tnCurrent = treeView1.SelectedNode;
            if (tnCurrent.Tag == null)
            {
                // it shouldn't be possible to get here because we disable the option on the context menu
                MessageBox.Show("The current directory doesn't exist remotely, therefore you can't have any of the selected files checked out.");
                return;
            }
            string strRelBasePath = tnCurrent.FullPath;

            // get a list of selected items
            List<int> lstSelected = new List<int>();
            ListView.SelectedListViewItemCollection lviSelection = listView1.SelectedItems;
            foreach (ListViewItem lviSelected in lviSelection)
            {
                DataRow drSelected = dsList.Tables[0].Select("entry_name='" + lviSelected.SubItems[0].Text + "'")[0];
                if (drSelected.Field<int?>("entry_id") != null)
                {
                    // ignore local-only files
                    int intSelectedEntryId = drSelected.Field<int>("entry_id");
                    lstSelected.Add(intSelectedEntryId);
                }
                else
                {
                    dlgStatus.AddStatusLine("WARNING", "Ignoring local only file: " + drSelected.Field<string>("entry_name"));
                }
            }
            if (lstSelected.Count == 0)
            {
                MessageBox.Show("No remote files were selected.");
                return;
            }

            // start the database transaction
            t = connDb.BeginTransaction();

            // package arguments for the background worker
            List<object> arguments = new List<object>();

            arguments.Add(t);
            arguments.Add(strRelBasePath);
            arguments.Add(lstSelected);

            // launch the background thread
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerSupportsCancellation = true;
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
            worker.DoWork += new DoWorkEventHandler(worker_UndoCheckout);
            worker.RunWorkerAsync(arguments);


            // show dialog and handle cancel button
            bool blnWorkCanceled = dlgStatus.ShowStatusDialog("List Undo Checkout");
            if (blnWorkCanceled == true)
            {
                worker.CancelAsync();
            }

            // refresh the main window
            ResetView(tnCurrent.FullPath);

        }

        void CmsListDeletePermanentClick(object sender, EventArgs e)
        {

            // check user
            if (intMyUserId != 1)
            {
                var result = MessageBox.Show("Only the administrator can destroy",
                "Permission Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
                return;
            }

            // refresh file data
            LoadListData(treeView1.SelectedNode.FullPath);

            // create the status dialog
            dlgStatus = new StatusDialog();

            // get directory info
            TreeNode tnCurrent = treeView1.SelectedNode;
            string strRelBasePath = tnCurrent.FullPath;

            // get a list of selected items
            List<string> lstSelectedNames = new List<string>();
            ListView.SelectedListViewItemCollection lviSelection = listView1.SelectedItems;
            foreach (ListViewItem lviSelected in lviSelection)
            {
                lstSelectedNames.Add(lviSelected.SubItems[0].Text.Replace("'", "''"));
            }

            // start the database transaction
            t = connDb.BeginTransaction();

            // package arguments for the background worker
            List<object> arguments = new List<object>();

            arguments.Add(t);
            arguments.Add(strRelBasePath);
            arguments.Add(lstSelectedNames);
            arguments.Add(true); // destroy flag

            // launch the background thread
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerSupportsCancellation = true;
            worker.DoWork += new DoWorkEventHandler(worker_Delete);
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
            worker.RunWorkerAsync(arguments);

            // show dialog and handle cancel button
            bool blnWorkCanceled = dlgStatus.ShowStatusDialog("Permanent Delete");
            if (blnWorkCanceled == true)
            {
                worker.CancelAsync();
            }

            ResetView(tnCurrent.FullPath);

        }

        void CmsListDeleteLogicalClick(object sender, EventArgs e)
        {

            // refresh file data
            LoadListData(treeView1.SelectedNode.FullPath);

            // create the status dialog
            dlgStatus = new StatusDialog();

            // get directory info
            TreeNode tnCurrent = treeView1.SelectedNode;
            string strRelBasePath = tnCurrent.FullPath;

            // get a list of selected items
            List<string> lstSelectedNames = new List<string>();
            ListView.SelectedListViewItemCollection lviSelection = listView1.SelectedItems;
            foreach (ListViewItem lviSelected in lviSelection)
            {
                lstSelectedNames.Add(lviSelected.SubItems[0].Text.Replace("'", "''"));
            }

            // start the database transaction
            t = connDb.BeginTransaction();

            // package arguments for the background worker
            List<object> arguments = new List<object>();

            arguments.Add(t);
            arguments.Add(strRelBasePath);
            arguments.Add(lstSelectedNames);
            arguments.Add(false); // destroy flag

            // launch the background thread
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerSupportsCancellation = true;
            worker.DoWork += new DoWorkEventHandler(worker_Delete);
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
            worker.RunWorkerAsync(arguments);

            // show dialog and handle cancel button
            bool blnWorkCanceled = dlgStatus.ShowStatusDialog("Delete Logical");
            if (blnWorkCanceled == true)
            {
                worker.CancelAsync();
            }

            ResetView(tnCurrent.FullPath);

        }

        private void listView1_ColumnWidthChanged(object sender, ColumnWidthChangedEventArgs e)
        {
            for (int i = 0; i < listView1.Columns.Count; i++)
            {
                listView1ColWidths[i] = listView1.Columns[i].Width;
            }
        }

        #endregion


        #region tab page actions

        void ListView1SelectedIndexChanged(object sender, EventArgs e)
        {

            if (listView1.SelectedItems.Count != 1)
            {
                // clear list
                InitTabPages();
                return;
            }
            TabControl1SelectedIndexChanged(sender, e);

        }

        void TabControl1SelectedIndexChanged(object sender, EventArgs e)
        {

            // Set cursor as hourglass
            Cursor.Current = Cursors.WaitCursor;

            if (listView1.SelectedItems.Count == 0) { return; }
            string strFileName = listView1.SelectedItems[0].SubItems[0].Text;
            PopulatePreviewImage(strFileName);

            if (tabControl1.SelectedIndex == 0)
            {
                // Entry History
                PopulateHistoryPage(strFileName);
            }

            if (tabControl1.SelectedIndex == 1)
            {
                // Parents
                PopulateParentsPage(strFileName);
            }

            if (tabControl1.SelectedIndex == 2)
            {
                // Children
                PopulateChildrenPage(strFileName);
            }

            if (tabControl1.SelectedIndex == 3)
            {
                // Properties
                PopulatePropertiesPage(strFileName);
            }

            // Set cursor as default arrow
            Cursor.Current = Cursors.Default;

        }

        void lvHistoryRightMouseClick(object sender, MouseEventArgs e)
        {
            // First, remove it, regardless of what is being done (if there is one assigned already):
            lvHistory.ContextMenu = null;

            // check for the right mouse button
            if (e.Button != MouseButtons.Right)
                return;

            // verify that a list item was right clicked, and select it/them
            ListView.SelectedListViewItemCollection lviSelection = lvHistory.SelectedItems;
            if (lviSelection.Count == 0)
            {
                // we never actually get here because the handler only gets called when an item is selected
                return;
            }

            // Create the context menu (if appropriate):
            ContextMenu cm = new ContextMenu();
            cm.MenuItems.Add("Get file only", new EventHandler(lvHistory_GetFile));
            cm.MenuItems.Add("Get dependencies", new EventHandler(lvHistory_GetDepends));
            lvHistory.ContextMenu = cm;

            return;
        }

        void lvHistory_GetFile(object sender, EventArgs e)
        {

            // this gets the currently-selected entry
            // we know only one item is selected in the main list, because the history tab is only visible
            // when a single entry in the main window is selected
            ListViewItem lviSelEntry = listView1.SelectedItems[0];
            string strSelName = lviSelEntry.Text;

            // all data about the entry are in the dsList DataSet
            DataRow drEntry = dsList.Tables[0].Select("entry_name='" + strSelName.Replace("'", "''") + "'")[0];
            int? intEntryId = drEntry.Field<int?>("entry_id");

            // when entry_id is null, it indicates a local-only file
            // we never actually return here because the history tab is empty for local-only files
            // therefore, this method can't be called
            if (intEntryId == null) return;

            // Get information about the selected file itself:
            string strFileName = drEntry.Field<string>("entry_name");
            string strFileExt = drEntry.Field<string>("file_ext");
            int intTypeId = drEntry.Field<int>("type_id");
            string strRelBasePath = drEntry.Field<string>("relative_path");
            string strAbsPath = Utils.GetAbsolutePath(strLocalFileRoot, strRelBasePath);

            // There should only be one historical version selected
            ListViewItem verSelected = lvHistory.SelectedItems[0];
            string intOldVersionId = (string)verSelected.SubItems[0].Text;

            // Get information about the type:
            string strSql = "select description from hp_type where type_id=" + intTypeId.ToString() + ";";
            NpgsqlCommand cmd = new NpgsqlCommand(strSql, connDb);
            NpgsqlDataAdapter daTemp = new NpgsqlDataAdapter(strSql, connDb);
            string strTypeDesc = (string)cmd.ExecuteScalar();

            // Get the name of the new file:
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = strTypeDesc + " (*" + strFileExt + ")|*" + strFileExt + "|All files (*)|*";
            dialog.Title = "Save File Version As...";
            dialog.InitialDirectory = strAbsPath;
            dialog.FileName = intOldVersionId + "." + strFileName;
            dialog.DefaultExt = strFileExt;
            dialog.OverwritePrompt = true;
            dialog.ShowDialog();
            if (dialog.FileName == "")
                return;

            // Get the file from WebDav:
            string strDavName = "/" + intEntryId.ToString() + "/" + intOldVersionId.ToString() + "." + strFileExt.ToLower();
            connDav.Download(strDavName, dialog.FileName);

            MessageBox.Show("The file has been successfully downloaded (I think)",
                "Download Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return;
        }

        void lvHistory_GetDepends(object sender, EventArgs e)
        {

            // this gets the currently-selected entry
            // we know only one item is selected in the main list, because the history tab is only visible
            // when a single entry in the main window is selected
            ListViewItem lviSelEntry = listView1.SelectedItems[0];
            string strSelName = lviSelEntry.Text;

            // all data about the entry are in the dsList DataSet
            DataRow drEntry = dsList.Tables[0].Select("entry_name='" + strSelName.Replace("'", "''") + "'")[0];
            int? intEntryId = drEntry.Field<int?>("entry_id");

            // when entry_id is null, it indicates a local-only file
            // we never actually return here because the history tab is empty for local-only files
            // therefore, this method can't be called
            if (intEntryId == null) return;

            // Get information about the selected file itself:
            string strFileName = drEntry.Field<string>("entry_name");
            string strFileExt = drEntry.Field<string>("file_ext");
            string strRelBasePath = drEntry.Field<string>("relative_path");
            string strAbsPath = Utils.GetAbsolutePath(strLocalFileRoot, strRelBasePath);

            // get the selected old version id
            // There can only be one historical version selected
            ListViewItem verSelected = lvHistory.SelectedItems[0];
            string intOldVersionId = (string)verSelected.SubItems[0].Text;

            // Request the dependency tree for this file version:
            string strSql = "select * from fcn_version_w_depends( " + intOldVersionId + " );";
            NpgsqlDataAdapter daTemp = new NpgsqlDataAdapter(strSql, connDb);
            DataSet dsFetches = new DataSet();
            daTemp.Fill(dsFetches);
            if (dsFetches.Tables.Count == 0)
                return;
            dsFetches.Tables[0].TableName = "files";

            // Ask the user where the dependency tree should be saved:
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            //dialog.Title = "Specify Version Dependency Directory";
            //dialog.InitialDirectory = strAbsPath;
            //dialog.OverwritePrompt = true;
            DialogResult result = dialog.ShowDialog();
            if (result != DialogResult.OK)
                return;

            string newdirectory = dialog.SelectedPath;

            // Check to make sure the new directory already exists (create it, if not):
            if (!Directory.Exists(newdirectory))
                Directory.CreateDirectory(newdirectory);
            else
            {
                // Make sure the directory is empty, if not warn the user:
                if (Directory.EnumerateFileSystemEntries(newdirectory).Any())
                    if (MessageBox.Show("The selected directory is not empty.  Continue anyway?", "Overwrite files?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
                        return;
            }

            // Copy the parent file from WebDav first:
            string strDavName = "/" + intEntryId.ToString() + "/" + intOldVersionId + "." + strFileExt.ToLower();
            connDav.Download(strDavName, newdirectory + "/" + strFileName);

            // Download each of the dependency files:
            foreach (DataRow dr in dsFetches.Tables["files"].Rows)
            {
                // Get the file from WebDav:
                strDavName = "/" + dr.Field<int>("entry_id").ToString() + "/" + dr.Field<int>("version_id").ToString() + "." + dr.Field<string>("file_ext").ToLower();
                connDav.Download(strDavName, newdirectory + "/" + dr.Field<string>("entry_name"));
            }

            MessageBox.Show("The files have been successfully downloaded (I think)",
                "Download Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return;

        }

        private void lvHistory_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Remove the context menu (if one is assigned already)
            lvHistory.ContextMenu = null;

            // get selected file
            if (listView1.SelectedItems.Count == 0) { return; }
            string strFileName = listView1.SelectedItems[0].SubItems[0].Text;

            // get selected version
            int intVersionId = 0;
            if (lvHistory.SelectedItems.Count > 0)
            {
                int.TryParse(lvHistory.SelectedItems[0].SubItems[0].Text, out intVersionId);
            }

            // load  preview image
            pbPreview.Image = null;
            PopulatePreviewImage(strFileName, intVersionId);

        }

        #endregion


        void CmdManageFileTypesClick(object sender, EventArgs e)
        {
            // load file type manager dialog
            ftmStart.ShowDialog();
        }


        void CmdSearchClick(object sender, EventArgs e)
        {
            DoSearch();
        }

        void DoSearch()
        {
            // load the search dialog:
            SearchDialog srchdialog = new SearchDialog(connDb, strLocalFileRoot, intMyUserId, ShowFileInTree, StoreSearchParams, FileContainsText, PropDropDownText, PropContainsText, CheckedOutMeBox, DeletedLocalBox, LocalOnlyBox);
            srchdialog.ShowDialog();
            listView1.Focus();
        }


        void MainFormFormClosed(object sender, FormClosedEventArgs e)
        {
            // save window layout
            Properties.UserSettings.Default.usetWindowLocation = this.Location;
            Properties.UserSettings.Default.usetWindowSize = this.Size;
            Properties.UserSettings.Default.usetWindowState = this.WindowState;

            // save list view column size
            System.Collections.Specialized.StringCollection collection = new System.Collections.Specialized.StringCollection();
            foreach (int element in listView1ColWidths)
                collection.Add(element.ToString());
            Properties.UserSettings.Default.usetColumnWidths = collection;

            // save splitter distances
            Properties.UserSettings.Default.usetSplitter1Distance = split1Dist;
            Properties.UserSettings.Default.usetSplitter2Distance = split2Dist;

            // save show deleted previous state
            Properties.UserSettings.Default.usetShowDeleted = blnShowDeleted;

            Properties.UserSettings.Default.Save();
        }


        public void ShowFileInTree(string filepath)
        {

            string strShortName = Utils.GetShortName(filepath);
            string strRelPath = Utils.GetRelativePath(strLocalFileRoot, Utils.GetParentDirectory(strLocalFileRoot + filepath));

            TreeNode tnSelected = FindNode(strRelPath);
            treeView1.SelectedNode = tnSelected;
            treeView1.SelectedNode.Expand();

            dsList.Tables[0].Select(String.Format("entry_name='{0}'", strShortName.Replace("'", "''")));
            foreach (ListViewItem li in listView1.Items)
            {
                if (li.Text == strShortName)
                {
                    li.Selected = true;
                    listView1.EnsureVisible(li.Index);
                    break;
                }
            }

        }


        // This is used to store the parameters used in the search dialog:
        public void StoreSearchParams(List<string> paramlist)
        {
            FileContainsText = paramlist[0];
            PropDropDownText = paramlist[1];
            PropContainsText = paramlist[2];
            CheckedOutMeBox = paramlist[3];
            DeletedLocalBox = paramlist[4];
            LocalOnlyBox = paramlist[5];
        }

        // open files in the default application
        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {

            // how many items selected
            int intSelected = listView1.SelectedItems.Count;

            // get a list of selected items
            int intOpened = 0;
            ListView.SelectedListViewItemCollection lviSelection = listView1.SelectedItems;
            foreach (ListViewItem lviSelected in lviSelection)
            {
                DataRow drSelected = dsList.Tables[0].Select("entry_name='" + lviSelected.SubItems[0].Text + "'")[0];
                // ignore remote-only files
                if (drSelected.Field<Boolean>("is_local") == true)
                {
                    // get full file name
                    string strAbsPath = drSelected.Field<string>("absolute_path");
                    string strFullName = strAbsPath + "\\" + lviSelected.SubItems[0].Text;

                    // count successful opens
                    intOpened++;

                    // open the file
                    try
                    {
                        System.Diagnostics.Process.Start(@strFullName);
                    }
                    catch
                    {
                        intOpened--;
                    }
                }
            }

            // report
            if (intSelected > intOpened)
            {
                MessageBox.Show(String.Format("{0} of {1} files could not be opened", intSelected - intOpened, intSelected));
                return;
            }

        }

        private void CmsUnDeleteToolStripMenuItem_Click(object sender, EventArgs e)
        {

            // refresh file data
            LoadListData(treeView1.SelectedNode.FullPath);

            // create the status dialog
            dlgStatus = new StatusDialog();

            // get directory info from the tree view
            TreeNode tnCurrent = treeView1.SelectedNode;
            string strRelBasePath = tnCurrent.FullPath;

            // get a list of selected items
            List<string> lstSelectedNames = new List<string>();
            ListView.SelectedListViewItemCollection lviSelection = listView1.SelectedItems;
            foreach (ListViewItem lviSelected in lviSelection)
            {
                lstSelectedNames.Add(lviSelected.SubItems[0].Text.Replace("'", "''"));
            }

            // start the database transaction
            t = connDb.BeginTransaction();

            // package arguments for the background worker
            List<object> arguments = new List<object>();

            arguments.Add(t);
            arguments.Add(strRelBasePath);
            arguments.Add(lstSelectedNames);

            // launch the background thread
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerSupportsCancellation = true;
            worker.DoWork += new DoWorkEventHandler(worker_UnDelete);
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
            worker.RunWorkerAsync(arguments);

            // show dialog and handle cancel button
            bool blnWorkCanceled = dlgStatus.ShowStatusDialog("Delete Logical");
            if (blnWorkCanceled == true)
            {
                worker.CancelAsync();
            }

            ResetView(tnCurrent.FullPath);

        }

        private void ChkShowDeleted_CheckedChanged(object sender, EventArgs e)
        {
            blnShowDeleted = chkShowDeleted.Checked;
            ResetView(treeView1.SelectedNode.FullPath);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F))
            {
                DoSearch();
                return true;
            }
            if (keyData == Keys.F5)
            {
                ResetView(treeView1.SelectedNode.FullPath);
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void splitContainer2_SplitterMoved(object sender, SplitterEventArgs e)
        {
            split2Dist = splitContainer2.SplitterDistance;
        }

        private void splitContainer1_SplitterMoved(object sender, SplitterEventArgs e)
        {
            split1Dist = splitContainer1.SplitterDistance;
        }
    }

    public class ImgConvert : System.Windows.Forms.AxHost
    {

        public ImgConvert()
            : base("c932ba85-4374-101b-a56c-00aa003668dc")
        {
            // do nothing
        }

        private delegate Image GetPictureDel(stdole.IPictureDisp inPic);
        public Image GetPicture(stdole.IPictureDisp inPic)
        {
            // expose GetPictureFromIPicture()
            if (this.InvokeRequired)
            {

                // this is a worker thread so delegate the task to the UI thread
                GetPictureDel del = new GetPictureDel(GetPicture);
                this.Invoke(del, inPic);

            }
            else
            {

                // we are executing in the UI thread
                return GetPictureFromIPicture(inPic);

            }

            return null;

        }

    }



}
