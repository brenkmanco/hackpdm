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
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

//using System.Threading;
using System.IO;
using System.Data;
using Npgsql;
using NpgsqlTypes;
//using LibRSync.Core;
using System.Security.Cryptography;


//using net.kvdb.webdav;


namespace HackPDM
{
	/// <summary>
	/// Description of MainForm.
	/// </summary>


	public partial class MainForm : Form
	{


		#region declarations

		private string strDbConn;
		private NpgsqlConnection connDb = new NpgsqlConnection();
		private NpgsqlTransaction t;

		private WebDAVClient connDav = new WebDAVClient();

		private SWHelper connSw = new SWHelper();

		private DataSet dsTree = new DataSet();
		private DataSet dsList = new DataSet();

		private string strLocalFileRoot;
		private int intMyUserId;
		private int intMyNodeId;

		private StatusDialog dlgStatus;
		string[] strStatusParams = new String[2];

		private FileTypeManager ftmStart;

		string strCurrProfileId;
		DataRow drCurrProfile;

		private ListViewColumnSorter lvwColumnSorter;

		#endregion


		public MainForm() {
			//
			// The InitializeComponent() call is required for Windows Forms designer support.
			//
			InitializeComponent();

			// recall window size from last session
			this.WindowState = Properties.Settings.Default.usetWindowState;

			// setup listview column sorting
			lvwColumnSorter = new ListViewColumnSorter();
			this.listView1.ListViewItemSorter = lvwColumnSorter;

			// get server connections and authenticate
			LoadProfile(false);
			DbConnect();
			DavConnect();

			// get (and set if necessary) my node_id
			intMyNodeId = GetNodeId();

			// get remote file type manager
			ftmStart = new FileTypeManager(connDb, strLocalFileRoot, GetTreePath(strLocalFileRoot));

			// Populate data
			ResetView();

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
			   List<string> strTest = connDav.List("/",1);
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
			try {
				connDb.Close();
				connDb.ConnectionString = strDbConn;
				connDb.Open();
			} catch (System.Exception e) {
				var result = MessageBox.Show("Failed to make database connection: " + e.Message + "\\nTry Again?",
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
				(string)drCurrProfile["Password"] );
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
			strCurrProfileId = Properties.Settings.Default.usetDefaultProfile;
			string strXmlProfiles = Properties.Settings.Default.usetProfiles;

			// check existence
			if (blnForceDlg || strXmlProfiles == "" || strCurrProfileId == "")
			{

				// launch the profile manager
				ProfileManager dlgPM = new ProfileManager();
				DialogResult pmResult = dlgPM.ShowDialog();

				// try again to get the profiles
				strCurrProfileId = Properties.Settings.Default.usetDefaultProfile;
				strXmlProfiles = Properties.Settings.Default.usetProfiles;

				// failed again
				if (strXmlProfiles == "" || strCurrProfileId == "")
				{
					var result = MessageBox.Show("Still can't get a profile.  Can't connect to the server.",
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
				var result = MessageBox.Show("Still can't get a profile.  Can't connect to the server.",
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
			string strSqlGet = "select node_id from hp_node where node_name='" + System.Environment.MachineName + "';";
			NpgsqlCommand cmdGetNode = new NpgsqlCommand(strSqlGet, connDb, t);
			object oTemp = cmdGetNode.ExecuteScalar();

			// check for a return value
			if (oTemp == null) {

				// no node_id returned: create one
				string strSqlGetNew = "select nextval('seq_hp_node_node_id'::regclass);";
				cmdGetNode.CommandText = strSqlGetNew;
				object oNewNodeId = cmdGetNode.ExecuteScalar();
				intNodeId = Convert.ToInt32(oNewNodeId);

				// insert the new node
				string strSqlSet = "insert into hp_node (node_id,node_name,create_user) values (" + intNodeId.ToString() + ",'" + System.Environment.MachineName + "'," + intMyUserId + ");";
				cmdGetNode.CommandText = strSqlSet;
				int intRows = cmdGetNode.ExecuteNonQuery();

			} else {

				// convert the node_id
				intNodeId = (int)oTemp;

			}

			t.Commit();
			return intNodeId;

		}

		private void LoadRemoteDirs() {

			// clear the dataset
			dsTree = new DataSet();

			// load remote directories to a DataTable
			string strSql = @"
				select
					dir_id,
					parent_id,
					dir_name,
					create_stamp,
					create_user,
					modify_stamp,
					modify_user,
					true as is_remote
				from hp_directory
				order by parent_id,dir_id;
			";
			NpgsqlDataAdapter daTemp = new NpgsqlDataAdapter(strSql, connDb);
			daTemp.Fill(dsTree);
			DataTable dtTree = dsTree.Tables[0];

			// add a column for flagging directories that exist locally
			DataColumn dcLocal = new DataColumn("is_local");
			dcLocal.DataType = Type.GetType("System.Boolean");
			dcLocal.DefaultValue = false;
			dtTree.Columns.Add(dcLocal);

			// add a column for the path definition
			DataColumn dcPath = new DataColumn("path");
			dcPath.DataType = Type.GetType("System.String");
			dtTree.Columns.Add(dcPath);

			// create parent-child relationship
			dsTree.Relations.Add("rsParentChild", dtTree.Columns["dir_id"], dtTree.Columns["parent_id"]);

		}


		private void ResetView(string strTreePath = "")
		{

			// clear the tree
			InitTreeView();

			// get root tree node
			TreeNode tnRoot = treeView1.Nodes[0];
			TreeNode tnSelect = new TreeNode();

			// build the tree recursively
			PopulateTree(tnRoot, (int)0);


			// clear the list window
			InitListView();

			// select the top node or the specified node
			if (strTreePath == "") {
				treeView1.SelectedNode = tnRoot;
				PopulateList(tnRoot);
			} else {
				tnSelect = FindNode(tnRoot, strTreePath);
				if (tnSelect != null) {
					treeView1.SelectedNode = tnSelect;
					PopulateList(tnSelect);
				} else {
					treeView1.SelectedNode = tnRoot;
					PopulateList(tnRoot);
				}
			}

			// reset file type manager
			ftmStart.RefreshRemote();



			// get local file list
			// easy


			// get remote file list
			// no big deal getting a few files, but retrieving the entire list could be very time consuming
			// it might be better to store file info locally, and then only retrieve updates
			// that would necessitate timestamping and change tracking on the server


			// combine file lists
			// identify local files not existing remotely (lo overlay)
			// identify local files ignored by remote filter (if overlay)
			// identify local files not existing remotely with no remote file type (ft overlay)
			// identify remote files not existing locally (ro overlay)
			// identify remote files with newer versions (nv overlay)
			// no need to identify local files that have been changed.  If they have been checked out, identify those, and then just assume they have been changed. (cm overlay)


		}

		void CmdRefreshViewClick(object sender, EventArgs e)
		{
			ResetView(treeView1.SelectedNode.FullPath);
		}


		protected void InitTreeView() {

			// reset context menu
			foreach (ToolStripMenuItem tsmiItem in cmsTree.Items) {
				tsmiItem.Enabled = false;
			}

			// load remote directory structure
			LoadRemoteDirs();

			// get the root directory row and set values
			DataRow drRoot = dsTree.Tables[0].Select("dir_id=0")[0];
			drRoot.SetField<bool>("is_local", true);
			drRoot.SetField<string>("path", "pwa");

			// clear the tree
			treeView1.Nodes.Clear();

			// insert the root node where tag = dir_id = 0
			TreeNode tnRoot = new TreeNode("pwa");
			tnRoot.Tag = (object)(int)0;
			tnRoot.ImageIndex = 0;
			tnRoot.SelectedImageIndex = 0;
			treeView1.Nodes.Add(tnRoot);

		}

		protected void PopulateTree(TreeNode tnParentNode, int intParentId) {

			// get local sub-directories
			string[] stringDirectories = Directory.GetDirectories(GetFilePath(tnParentNode.FullPath));

			// loop through all local sub-directories
			foreach (string strDir in stringDirectories) {

				string strFilePath = strDir;
				string strTreePath = GetTreePath(strFilePath);
				string strDirName = GetShortName(strFilePath);
				TreeNode tnChild = new TreeNode(strDirName);

				// get matching remote directory
				DataRow[] drChilds = dsTree.Tables[0].Select(String.Format("parent_id={0} and dir_name='{1}'", intParentId, strDirName.ToString().Replace("'", "''")));
				if (drChilds.Length != 0) {

					DataRow drChild = drChilds[0];

					// local and remote
					drChild.SetField("is_local", true);
					drChild.SetField("path", strTreePath);
					int intChildId = (int)drChild["dir_id"];
					tnChild.Tag = (object)intChildId;
					tnChild.ImageIndex = 0;
					tnChild.SelectedImageIndex = 0;
					tnParentNode.Nodes.Add(tnChild);

					//Recursively build the tree
					PopulateTree(tnChild, intChildId);

				} else {

					// local only icon
					tnChild.ImageIndex = 1;
					tnChild.SelectedImageIndex = 1;
					tnParentNode.Nodes.Add(tnChild);

					//Recursively build the tree
					PopulateTreeLocal(tnChild);

				}

			}

			// get remote only sub-directories
			DataRow[] drRemChild = dsTree.Tables[0].Select("parent_id="+intParentId+" and is_local=0");
			foreach (DataRow row in drRemChild) {

				// remote only
				string strDirName = row["dir_name"].ToString();
				string strTreePath = tnParentNode.FullPath + "\\" + strDirName;
				string strFilePath = GetFilePath(strTreePath);
				int intDirId = (int)row["dir_id"];

				TreeNode tnChild = new TreeNode(strDirName);
				row.SetField("is_local", false);
				row.SetField("path", strTreePath);
				tnChild.Tag = (object)intDirId;
				tnChild.ImageIndex = 2;
				tnChild.SelectedImageIndex = 2;
				tnParentNode.Nodes.Add(tnChild);

				//Recursively build the tree
				PopulateTreeRemote(tnChild, intDirId);

			}

		}

		protected void PopulateTreeLocal(TreeNode tnParentNode) {

			// the parent is local only, so this is also local only

			// get local sub-directories
			string[] stringDirectories = Directory.GetDirectories(GetFilePath(tnParentNode.FullPath));

			// loop through all local sub-directories
			foreach (string strDir in stringDirectories) {

				string strFilePath = strDir;
				string strTreePath = GetTreePath(strFilePath);
				string strDirName = GetShortName(strFilePath);
				TreeNode tnChild = new TreeNode(strDirName);

				// local only icon
				tnChild.ImageIndex = 1;
				tnChild.SelectedImageIndex = 1;
				tnParentNode.Nodes.Add(tnChild);

				//Recursively build the tree
				PopulateTreeLocal(tnChild);

			}

		}

		protected void PopulateTreeRemote(TreeNode tnParentNode, int intParentId) {

			// the parent is remote only, so this is also remote only

			// get remote only sub-directories
			DataRow[] drRemChild = dsTree.Tables[0].Select("parent_id="+intParentId);
			foreach (DataRow row in drRemChild) {

				// remote only
				string strDirName = row["dir_name"].ToString();
				string strTreePath = tnParentNode.FullPath + "\\" + strDirName;
				string strFilePath = GetFilePath(strTreePath);
				int intDirId = (int)row["dir_id"];

				TreeNode tnChild = new TreeNode(strDirName);
				row.SetField("is_local", false);
				row.SetField("path", strTreePath);
				tnChild.Tag = (object)intDirId;

				// remote only icon
				tnChild.ImageIndex = 2;
				tnChild.SelectedImageIndex = 2;
				tnParentNode.Nodes.Add(tnChild);

				//Recursively build the tree
				PopulateTreeRemote(tnChild, intDirId);

			}

		}


		protected void InitListView() {

			//init ListView control
			listView1.Clear();

			// configure sorting
			//listView1.Sorting = SortOrder.None;
			//listView1.ColumnClick += new ColumnClickEventHandler(lv1ColumnClick);

			//create columns for ListView
			listView1.Columns.Add("Name",300,System.Windows.Forms.HorizontalAlignment.Left);
			listView1.Columns.Add("Size", 75, System.Windows.Forms.HorizontalAlignment.Right);
			listView1.Columns.Add("Type", 140, System.Windows.Forms.HorizontalAlignment.Left);
			listView1.Columns.Add("Modified", 140, System.Windows.Forms.HorizontalAlignment.Left);
			listView1.Columns.Add("CheckOut", 140, System.Windows.Forms.HorizontalAlignment.Left);
			listView1.Columns.Add("Category", 140, System.Windows.Forms.HorizontalAlignment.Left);
			listView1.Columns.Add("FullName", 0, System.Windows.Forms.HorizontalAlignment.Left);

			// reset context menu
			foreach (ToolStripMenuItem tsmiItem in cmsList.Items) {
				tsmiItem.Enabled = false;
			}

		}

		protected void LoadListData(TreeNode nodeCurrent) {

			// nv and co could co-exist --> add new icon for nvco
			// ro and co could co-exist --> add new icon for roco
			// for now, let the take affect in the following order
			// ro: remote only
			// lo: local only
			// nv: new remote version
			// cm: checked out to me
			// co: checked out to other
			// ft: no remote file type
			// if: ignore filter

			// clear dataset
			dsList = new DataSet();

			// get directory path
			string strFilePath = GetFilePath(nodeCurrent.FullPath);
			string strTreePath = nodeCurrent.FullPath;

			// get remote entries
			int intDirId = 0;
			if (nodeCurrent.Tag != null) {

				intDirId = (int)nodeCurrent.Tag;
				// initialize sql command for remote entry list
				string strSql = @"
					select
						e.entry_id,
						v.version_id,
						e.dir_id,
						e.entry_name,
						t.type_id,
						t.file_ext,
						c.cat_name,
						v.file_size as latest_size,
						pg_size_pretty(v.file_size) as str_latest_size,
						null as local_size,
						'' as str_local_size,
						v.file_modify_stamp as latest_stamp,
						to_char(v.file_modify_stamp, 'yyyy-MM-dd HH24:mm:ss') as str_latest_stamp,
						null as local_stamp,
						'' as str_local_stamp,
						v.md5sum as latest_md5,
						null as local_md5,
						e.checkout_user,
						u.last_name || ', ' || u.first_name as ck_user_name,
						e.checkout_date,
						to_char(e.checkout_date, 'yyyy-MM-dd HH24:mm:ss') as str_checkout_date,
						e.checkout_node,
						false as is_local,
						true as is_remote,
						'.ro' as client_status_code,
						:strTreePath as tree_path,
						:strFilePath as file_path,
						t.icon
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
					where e.dir_id=:dir_id
					order by dir_id,entry_id;
				";

				// put the remote list in the DataSet
				NpgsqlDataAdapter daTemp = new NpgsqlDataAdapter(strSql, connDb);
				daTemp.SelectCommand.Parameters.Add(new NpgsqlParameter("dir_id", NpgsqlTypes.NpgsqlDbType.Integer));
				daTemp.SelectCommand.Parameters.Add(new NpgsqlParameter("strTreePath", NpgsqlTypes.NpgsqlDbType.Text));
				daTemp.SelectCommand.Parameters.Add(new NpgsqlParameter("strFilePath", NpgsqlTypes.NpgsqlDbType.Text));
				daTemp.SelectCommand.Parameters["dir_id"].Value = intDirId;
				daTemp.SelectCommand.Parameters["strTreePath"].Value = strTreePath;
				daTemp.SelectCommand.Parameters["strFilePath"].Value = strFilePath;
				daTemp.Fill(dsList);

			}

			if (dsList.Tables.Count == 0) {
				// make an empty DataTable
				dsList.Tables.Add(CreateFileTable());
			}

			// get local files
			if(Directory.Exists(strFilePath) == true) {

				try {

					string[] strFiles = Directory.GetFiles(strFilePath);
					string strFileName = "";
					DateTime dtModifyDate;
					Int64 lngFileSize = 0;

					//loop through all files
					foreach (string strFile in strFiles) {

						// get file info
						strFileName = GetShortName(strFile);
						FileInfo fiCurrFile = new FileInfo(strFile);
						lngFileSize = fiCurrFile.Length;
						dtModifyDate = fiCurrFile.LastWriteTime;

						Tuple<string, string, string, string> tplExtensions = ftmStart.GetFileExt(strFile);
						string strFileExt = tplExtensions.Item1;
						string strRemFileExt = tplExtensions.Item2;
						string strRemBlockExt = tplExtensions.Item3;
						string strWinFileExt = tplExtensions.Item4;

						// get matching remote file
						DataRow[] drRemFile = dsList.Tables[0].Select("entry_name='"+strFileName+"'");

						// set status code to "ro" again, just to be safe
						string strStatusCode = ".ro";

						if (drRemFile.Length != 0)
						{

							// flag remote file as also being local
							DataRow drTemp = drRemFile[0];
							drTemp.SetField<bool>("is_local", true);

							// set status code
							strStatusCode = "";
							if (dtModifyDate < drTemp.Field<DateTime>("latest_stamp")) strStatusCode = "nv";

							// set status code if checked-out
							object oTest = drTemp["checkout_user"];
							if (oTest != System.DBNull.Value)
							{
								if (drTemp.Field<int>("checkout_user") == intMyUserId)
								{
									// checked out to me (current user)
									strStatusCode = ".cm";
								}
								else
								{
									// checked out to someone else
									strStatusCode = ".co";
								}
							}

							// set the status code
							drTemp.SetField<string>("client_status_code", strStatusCode);

							// format the remote file size
							drTemp.SetField<string>("str_latest_size", FormatSize(drTemp.Field<long>("latest_size")));

							// format the remote modify date
							drTemp.SetField<string>("str_latest_stamp", FormatDate(drTemp.Field<DateTime>("latest_stamp")));

							// format the local file size
							drTemp.SetField<Int64>("local_size", lngFileSize);
							drTemp.SetField<string>("str_local_size", FormatSize(lngFileSize));

							// format the local modify date
							drTemp.SetField<DateTime>("local_stamp", dtModifyDate);
							drTemp.SetField<string>("str_local_stamp", FormatDate(dtModifyDate));

							// format the checkout date
							object oDate = drTemp["checkout_date"];
							if (oDate == System.DBNull.Value) {
								drTemp.SetField<string>("str_checkout_date", null);
							} else {
								drTemp.SetField<string>("str_checkout_date", FormatDate(Convert.ToDateTime(oDate)));
							}

							// get local checksum
							drTemp.SetField<string>("local_md5", StringMD5(strFile));

						} else {

							// insert new row for local-only file

							// set status code
							strStatusCode = ".lo";
							if (strRemBlockExt == strFileExt)
							{
								strStatusCode = ".if";
							}
							else if (strRemFileExt == "")
							{
								strStatusCode = ".ft";
							}

							dsList.Tables[0].Rows.Add(
									null,
									null,
									intDirId,
									strFileName,
									null,
									strFileExt,
									null,
									lngFileSize, // latest_size
									FormatSize(lngFileSize), // str_latest_size
									lngFileSize, // local_size
									FormatSize(lngFileSize), // str_local_size
									null, // latest_stamp
									"", // str_latest_stamp
									dtModifyDate, // local stamp
									FormatDate(dtModifyDate), // local formatted stamp
									null, // latest_md5
									null, // local_md5 (set null because if we AddNew, we will calculate MD5 then)
									null,
									null,
									null,
									null,
									null,
									true, // is_local
									false, // is_remote
									strStatusCode,
									strTreePath,
									strFilePath
								);

						}

					}

				} catch (IOException e) {
					MessageBox.Show("Error: Drive not ready or directory does not exist: " + e);
				} catch (UnauthorizedAccessException e) {
					MessageBox.Show("Error: Drive or directory access denided: " + e);
				} catch (Exception e) {
					MessageBox.Show("Error: " + e);
				}

			}

		}

		protected void PopulateList(TreeNode nodeCurrent) {

			// clear list
			InitListView();
			InitTabPages();
			LoadListData(nodeCurrent);

			// if we have any files to show, then populate listview with files
			if (dsList.Tables[0] != null) {
				foreach (DataRow row in dsList.Tables[0].Rows) {

					string[] lvData =  new string[6];
					lvData[0] = row.Field<string>("entry_name"); // Name
					lvData[1] = row.Field<string>("str_latest_size"); // Size
					lvData[2] = row.Field<string>("file_ext"); // Name
					lvData[3] = row.Field<string>("str_latest_stamp"); // Modified
					lvData[4] = row.Field<string>("ck_user_name"); // CheckOut
					lvData[5] = row.Field<string>("cat_name"); // Category

					// get file type
					string strFileExt = row.Field<string>("file_ext");
					string strOverlay = row.Field<string>("client_status_code");

					// get icon images for new file types (new to this session)
					if (ilListIcons.Images[strFileExt] == null) {

						Image imgCurrent;
						byte[] img = row.Field<byte[]>("icon");
						if (img == null) {
							// extract an image locally
							string strFullName = row.Field<string>("file_path") + "\\" + row.Field<string>("entry_name");
							imgCurrent = GetLocalIcon(strFullName);
						} else {
							// get remote image
							MemoryStream ms = new MemoryStream();
							ms.Write(img,0,img.Length);
							imgCurrent = Image.FromStream(ms);
						}

						// add bare icon to icon list
						ilListIcons.Images.Add(strFileExt,imgCurrent);

						// add overlayed icons to icon list
						// these are added at design time on the MainForm.cs designer ("Choose images" context menu item)
						ilListIcons.Images.Add(strFileExt + ".ro", ImageOverlay(imgCurrent, ilListIcons.Images["ro"])); // remote only
						ilListIcons.Images.Add(strFileExt + ".lo", ImageOverlay(imgCurrent, ilListIcons.Images["lo"])); // local only
						ilListIcons.Images.Add(strFileExt + ".lo", ImageOverlay(imgCurrent, ilListIcons.Images["nv"])); // new remote version
						ilListIcons.Images.Add(strFileExt + ".cm", ImageOverlay(imgCurrent, ilListIcons.Images["cm"])); // checked out to me
						ilListIcons.Images.Add(strFileExt + ".co", ImageOverlay(imgCurrent, ilListIcons.Images["co"])); // checked out to other
						ilListIcons.Images.Add(strFileExt + ".if", ImageOverlay(imgCurrent, ilListIcons.Images["if"])); // ignore filter
						ilListIcons.Images.Add(strFileExt + ".ft", ImageOverlay(imgCurrent, ilListIcons.Images["ft"])); // no remote file type

					}

					// create actual list item
					ListViewItem lvItem = new ListViewItem(lvData);
					lvItem.ImageKey = strFileExt + strOverlay;
					listView1.Items.Add(lvItem);

				}
			}

		}


		protected void InitTabPages() {

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

		protected void PopulatePreviewImage(string FileName)
		{

			DataRow dr = dsList.Tables[0].Select("entry_name='" + FileName + "'")[0];
			byte[] bImage;
			MemoryStream ms;

			// if local only or checked out to me, load image from local file
			if (dr.Field<bool>("is_remote") == false || dr.Field<string>("client_status_code") == ".cm")
			{
				bImage = GetLocalPreview(dr);
			}
			else
			{
				// otherwise, load the image from the server
				NpgsqlCommand command = new NpgsqlCommand("select preview_image from hp_version where entry_id=:entry_id order by version_id limit 1;", connDb);
				command.Parameters.Add(new NpgsqlParameter("entry_id", NpgsqlTypes.NpgsqlDbType.Integer));
				command.Parameters["entry_id"].Value = dr.Field<int>("entry_id");

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
			lvHistory.Columns.Add("ModUser", 140, System.Windows.Forms.HorizontalAlignment.Left);
			lvHistory.Columns.Add("ModDate", 140, System.Windows.Forms.HorizontalAlignment.Left);
			lvHistory.Columns.Add("Size", 75, System.Windows.Forms.HorizontalAlignment.Right);
			lvHistory.Columns.Add("Release", 75, System.Windows.Forms.HorizontalAlignment.Right);
			lvHistory.Columns.Add("RelDate", 75, System.Windows.Forms.HorizontalAlignment.Right);
			lvHistory.Columns.Add("RelUser", 75, System.Windows.Forms.HorizontalAlignment.Right);

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
					to_char(v.create_stamp, 'yyyy-MM-dd HH24:mm:ss') as action_date,
					u.last_name || ', ' || u.first_name as action_user,
					v.release_tag,
					r.last_name || ', ' || r.first_name as release_user,
					to_char(v.release_date, 'yyyy-MM-dd HH24:mm:ss') as release_date
				from hp_version as v
				left join hp_user as u on u.user_id=v.create_user
				left join hp_user as r on r.user_id=v.release_user
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
				string[] lvData =  new string[6];
				lvData[0] = row.Field<string>("action_user");
				lvData[1] = row.Field<string>("action_date");
				lvData[2] = row.Field<string>("version_size");
				lvData[3] = row.Field<string>("release_tag");
				lvData[4] = row.Field<string>("release_date");
				lvData[5] = row.Field<string>("release_user");

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
				from hp_versionrelationship as r
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
				lvData[0] = row.Field<string>("rel_child_id");
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
			lvChildren.Columns.Add("Version", 140, System.Windows.Forms.HorizontalAlignment.Left);
			lvChildren.Columns.Add("Name", 300, System.Windows.Forms.HorizontalAlignment.Left);

			// new dataset
			DataSet dsTemp = new DataSet();

			// get data row
			DataRow dr = dsList.Tables[0].Select("entry_name='" + FileName + "'")[0];
			string strFullName = dr.Field<string>("file_path") + "\\" + FileName;

			if (dr.Field<bool>("is_remote") == false || dr.Field<string>("client_status_code") == ".cm")
			{
				// get from SolidWorks
				List<string[]> lstDepends = connSw.GetDependenciesShallow(strFullName);

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
								true
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
						:file_path || '\\' || e.entry_name as full_name,
						false as outside_pwa
					from hp_versionrelationship as r
					left join hp_version as vp on vp.version_id=r.rel_parent_id
					left join hp_version as vc on vc.version_id=r.rel_child_id
					left join hp_entry as e on e.entry_id=vc.entry_id
					where vp.entry_id=:entry_id
					order by r.rel_parent_id desc, e.entry_name;
				";

				// put the remote list in the DataSet
				NpgsqlDataAdapter daTemp = new NpgsqlDataAdapter(strSql, connDb);
				daTemp.SelectCommand.Parameters.Add(new NpgsqlParameter("file_path", NpgsqlTypes.NpgsqlDbType.Varchar));
				daTemp.SelectCommand.Parameters["entry_id"].Value = dr.Field<string>("file_path");
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
				string[] lvData = new string[2];
				lvData[0] = row.Field<int>("rel_parent_id").ToString();
				lvData[1] = row.Field<string>("entry_name") + " (v" + row.Field<int>("rel_child_id").ToString() + ")";

				// create actual list item
				ListViewItem lvItem = new ListViewItem(lvData);
				lvChildren.Items.Add(lvItem);

			}

		}

		protected void PopulatePropertiesPage(string FileName)
		{

			// clear list
			lvProperties.Clear();
			lvProperties.Columns.Add("Version", 140, System.Windows.Forms.HorizontalAlignment.Left);
			lvProperties.Columns.Add("Configuration", 140, System.Windows.Forms.HorizontalAlignment.Left);
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
				order by vp.version_id desc, p.prop_name;
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

				string strValue = "";
				if (row.Field<string>("text_value") != null) strValue = row.Field<string>("text_value");
				if (row.Field<string>("date_value") != null) strValue = FormatDate(row.Field<DateTime>("date_value"));
				if (row.Field<string>("number_value") != null) strValue = row.Field<Decimal>("number_value").ToString();
				if (row.Field<string>("yesno_value") != null) strValue = row.Field<Boolean>("yesno_value").ToString();

				// build array
				string[] lvData = new string[5];
				lvData[0] = row.Field<string>("version_id");
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

		protected byte[] GetLocalPreview(DataRow dr)
		{

			byte[] bImage;
			string strFullName = dr.Field<string>("file_path") + "\\" + dr.Field<string>("entry_name");
			string strFileExt = dr.Field<string>("file_ext").ToLower();
			IconFromFile ico = new IconFromFile();

			Image img = ico.GetThumbnail(strFullName);
			bImage = ImageToByteArray(img);
			return bImage;

		}

		private DataTable CreateFileTable() {

				DataTable dtList = new DataTable();
				dtList.Columns.Add("entry_id", Type.GetType("System.Int32"));
				dtList.Columns.Add("version_id", Type.GetType("System.Int32"));
				dtList.Columns.Add("dir_id", Type.GetType("System.Int32"));
				dtList.Columns.Add("entry_name", Type.GetType("System.String"));
				dtList.Columns.Add("type_id", Type.GetType("System.Int32"));
				dtList.Columns.Add("file_ext", Type.GetType("System.String"));
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
				dtList.Columns.Add("checkout_node", Type.GetType("System.String"));
				dtList.Columns.Add("is_local", Type.GetType("System.Boolean"));
				dtList.Columns.Add("is_remote", Type.GetType("System.Boolean"));
				dtList.Columns.Add("client_status_code", Type.GetType("System.String"));
				dtList.Columns.Add("tree_path", Type.GetType("System.String"));
				dtList.Columns.Add("file_path", Type.GetType("System.String"));
				dtList.Columns.Add("icon", typeof(Byte[]));
				return dtList;

		}

		public Image ImageOverlay(Image imgOrig, Image imgOverlay) {
			Bitmap bitmap = new Bitmap(32,32);
			Graphics canvas = Graphics.FromImage(bitmap);
			canvas.DrawImage(imgOrig, new Point(0, 0));
			canvas.DrawImage(imgOverlay, new Point(0, 0));
			canvas.Save();
			return Image.FromHbitmap(bitmap.GetHbitmap());
		}

		private byte[] ImageToByteArray(System.Drawing.Image imageIn)
		{
			if (imageIn == null) return null;
			using (MemoryStream ms = new MemoryStream())
			{
				imageIn.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
				return ms.ToArray();
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

		protected string GetFilePath(string stringPath) {
			//Get Full path
			string stringParse = "";
			//replace pwa with actual root path
			stringParse = strLocalFileRoot + stringPath.Substring(3);
			return stringParse;
		}

		protected string GetTreePath(string stringPath) {
			// get tree path
			string stringParse = "";
			// replace actual root path with pwa
			stringParse = "pwa" + stringPath.Substring(strLocalFileRoot.Length);
			return stringParse;
		}

		protected string GetShortName(string stringPath) {
			//Get Name of folder
			string[] stringSplit = stringPath.Split('\\');
			int _maxIndex = stringSplit.Length;
			return stringSplit[_maxIndex-1];
		}

		protected string GetFileExt(string strFileName) {
			//Get Name of folder
			string[] strSplit = strFileName.Split('.');
			int _maxIndex = strSplit.Length-1;
			return strSplit[_maxIndex];
		}

		protected string FormatDate(DateTime dtDate) {

			// if file not in local current day light saving time, then add an hour?
			if (TimeZone.CurrentTimeZone.IsDaylightSavingTime(dtDate) == false) {
				dtDate = dtDate.AddHours(1);
			}

			// get date and time in short format and return it
			string stringDate = "";
			//stringDate = dtDate.ToShortDateString().ToString() + " " + dtDate.ToShortTimeString().ToString();
			stringDate = dtDate.ToString("yyyy-MM-dd HH:mm:ss");
			return stringDate;

		}

		protected string FormatSize(Int64 lSize)
		{
			//Format number to KB
			string stringSize = "";
			NumberFormatInfo myNfi = new NumberFormatInfo();

			Int64 lKBSize = 0;

			if (lSize < 1024 )
			{
				if (lSize == 0)
				{
					//zero byte
					stringSize = "0";
				}
				else
				{
					//less than 1K but not zero byte
					stringSize = "1";
				}
			}
			else
			{
				//convert to KB
				lKBSize = lSize / 1024;
				//format number with default format
				stringSize = lKBSize.ToString("n",myNfi);
				//remove decimal
				stringSize = stringSize.Replace(".00", "");
			}

			return stringSize + " KB";

		}

		protected TreeNode FindNode(TreeNode tnParent, string strPath) {
			foreach (TreeNode tnChild in tnParent.Nodes) {
				if (tnChild.FullPath == strPath) {
					return tnChild;
				} else {
					TreeNode tnMatch = FindNode(tnChild, strPath);
					if (tnMatch != null) {
						return tnMatch;
					}
				}
			}
			return (TreeNode)null;
		}

		protected virtual bool IsFileLocked(FileInfo file) {

			FileStream stream = null;

			if (file.Exists) {
				try {
					stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
				}
				catch (IOException) {
					//the file is unavailable because it is:
					//still being written to
					//or being processed by another thread
					//or does not exist (has already been processed)
					return true;
				}
				finally {
					if (stream != null) stream.Close();
				}
			}

			//file is not locked
			return false;

		}

		private Image GetLocalIcon(string FileName) {

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

		private bool AddDirStructReverse(NpgsqlTransaction t, TreeNode tnChild) {

			// the caller must determine that this directory does not already exist remotely

			// make sure we are working inside of a transaction
			if (t.Connection == null) {
				MessageBox.Show("The database transaction is not functional");
				return(true);
			}

			// climb the tree until we find a parent directory that exists remotely
			List<TreeNode> tnlParents = new List<TreeNode>();
			tnlParents.Add(tnChild);
			TreeNode tnCurrent = tnChild;
			while (tnCurrent.Parent.Tag == null) {
				tnlParents.Add(tnCurrent.Parent);
				tnCurrent = tnCurrent.Parent;
			}
			int intParentDirId = (int)tnCurrent.Parent.Tag;

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
			cmdInsert.Parameters.Add(new NpgsqlParameter("default_cat", NpgsqlTypes.NpgsqlDbType.Integer));
			cmdInsert.Parameters.Add(new NpgsqlParameter("create_user", NpgsqlTypes.NpgsqlDbType.Integer));
			cmdInsert.Parameters.Add(new NpgsqlParameter("modify_user", NpgsqlTypes.NpgsqlDbType.Integer));
			cmdInsert.Parameters["default_cat"].Value = (int)1;
			cmdInsert.Parameters["create_user"].Value = intMyUserId;
			cmdInsert.Parameters["modify_user"].Value = intMyUserId;

			// create directories from the top down
			for (int i = tnlParents.Count-1; i >=0 ; i--) {

				// get the next directory id
				object oTemp = cmdGetId.ExecuteScalar();
				int intCurrentId;
				if (oTemp != null) {
					intCurrentId = (int)(long)oTemp;
				} else {
					MessageBox.Show("Failed to get the next directory ID.",
						"Cannot Add New Directory",
						MessageBoxButtons.OK,
						MessageBoxIcon.Exclamation,
						MessageBoxDefaultButton.Button1);
					return(true);
				}

				// set parameters
				string strDirName = tnlParents[i].Text;
				cmdInsert.Parameters["dir_id"].Value = intCurrentId;
				cmdInsert.Parameters["parent_id"].Value = intParentDirId;
				cmdInsert.Parameters["dir_name"].Value = strDirName;

				// insert row
				try {
					cmdInsert.ExecuteNonQuery();
				} catch (NpgsqlException e) {
					// if unique key/index violation
					MessageBox.Show("Directory \""+strDirName+"\" already exists on the server.  Refresh your view.  "+System.Environment.NewLine+e.Detail,
						"Cannot Add New Directory",
						MessageBoxButtons.OK,
						MessageBoxIcon.Exclamation,
						MessageBoxDefaultButton.Button1);
					return(true);
				}

				// set the node's tag to the remote directory id
				tnlParents[i].Tag = (object)intCurrentId;

				intParentDirId = intCurrentId;

			}

			return(false);

		}

		private bool AddDirStructForward(NpgsqlTransaction t, TreeNode tnParent) {

			// make sure we are working inside of a transaction
			if (t.Connection == null) {
				MessageBox.Show("The database transaction is not functional");
				return(true);
			}

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
			cmdInsert.Parameters.Add(new NpgsqlParameter("default_cat", (int)1));
			cmdInsert.Parameters.Add(new NpgsqlParameter("create_user", intMyUserId));
			cmdInsert.Parameters.Add(new NpgsqlParameter("modify_user", intMyUserId));

			bool blnFailed = AddDirStructForwardRecursive(tnParent, cmdGetId, cmdInsert);

			return(blnFailed);

		}

		private bool AddDirStructForwardRecursive(TreeNode tnParent, NpgsqlCommand cmdGetId, NpgsqlCommand cmdInsert) {

			// the caller must ensure that the parent directory already exists remotely

			// descend the tree creating child directories that do not exist remotely
			int intParentId = (int)tnParent.Tag;
			foreach (TreeNode tnChild in tnParent.Nodes) {

				if (tnChild.Tag == null) {

					// get the next directory id
					object oTemp = cmdGetId.ExecuteScalar();
					int intChildId;
					if (oTemp != null) {
						intChildId = (int)(long)oTemp;
					} else {
						MessageBox.Show("Failed to get the next directory ID.",
							"Cannot Add New Directory",
							MessageBoxButtons.OK,
							MessageBoxIcon.Exclamation,
							MessageBoxDefaultButton.Button1);
						return(true);
					}

					// set parameters
					string strDirName = GetShortName(tnChild.FullPath);
					cmdInsert.Parameters["dir_id"].Value = intChildId;
					cmdInsert.Parameters["parent_id"].Value = intParentId;
					cmdInsert.Parameters["dir_name"].Value = strDirName;

					// insert row
					try {
						cmdInsert.ExecuteNonQuery();
					} catch (NpgsqlException e) {
						// if unique key/index violation
						MessageBox.Show("Directory \""+strDirName+"\" already exists on the server.  Refresh your view.  "+System.Environment.NewLine+e.Detail,
							"Cannot Add New Directory",
							MessageBoxButtons.OK,
							MessageBoxIcon.Exclamation,
							MessageBoxDefaultButton.Button1);
						return(true);
					}

					// set the node's tag to the remote directory id
					tnChild.Tag = (object)intChildId;

				}

				// recurse on this child directory
				bool blnFailed = AddDirStructForward(t, tnChild);
				if (blnFailed) {
					return(true);
				}

			}

			return(false);


		}

		void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
			if (e.Cancelled == true)
			{
				t.ToString();
				if (t.Connection != null) {
					t.Rollback();
					// TODO: figure out how to rollback WebDav changes
				}
				dlgStatus.AddStatusLine("Cancel", "Operation canceled");
			}
			else if (e.Error != null)
			{
				dlgStatus.AddStatusLine("Error", e.Error.Message);
				if (t.Connection != null) {
					t.Rollback();
					// TODO: figure out how to rollback WebDav changes
				}
			}
			else
			{
				if (t.Connection != null) {
					t.Rollback();
					// TODO: figure out how to rollback WebDav changes
				}
				dlgStatus.AddStatusLine("Complete", "Operation completed");
				dlgStatus.OperationCompleted();
			}
		}

		protected void LoadFileDataRecursive(TreeNode tnParent, ref DataTable dt) {

			// could this work for AddNew and GetLatest?

			// get directory parameters
			string strTreePath = tnParent.FullPath;
			string strFilePath = GetFilePath(strTreePath);
			int intDirId = (int)tnParent.Tag;

			// log status
			dlgStatus.AddStatusLine("Get Directory Info", "Processing Directory: " + strFilePath);

			// get local files
			if (Directory.Exists(strFilePath) == true)
			{

				try {

					string[] strFiles = Directory.GetFiles(strFilePath);
					string strFileName = "";
					DateTime dtModifyDate;
					Int64 lngFileSize = 0;

					// log status
					dlgStatus.AddStatusLine("Process Files", "Processing local files: " + strFiles.Length);

					//loop through all files
					foreach (string strFile in strFiles)
					{

						// TODO: ignore filtered file names
						Regex reg = new Regex(@"^.+\..(.+~)|(msi)|(dll)|(exe)|(bak)|(db)|(dropbox)$", RegexOptions.IgnoreCase);
						
						//if (reg.IsMatch(strFile)) continue;
						

						// get file info
						strFileName = GetShortName(strFile);
						FileInfo fiCurrFile = new FileInfo(strFile);
						string strFileExt = fiCurrFile.Extension.Substring(1, fiCurrFile.Extension.Length - 1).ToLower();
						lngFileSize = fiCurrFile.Length;
						dtModifyDate = fiCurrFile.LastWriteTime;

						// log status
						dlgStatus.AddStatusLine("Processing Files", "Processing local file: " + strFile);

						// get matching remote file
						DataRow[] drRemFile = dt.Select("entry_name='" + strFileName + "' and file_path='" + strFilePath + "'");

						if (drRemFile.Length != 0)
						{

							// flag remote file as also being local
							DataRow drTemp = drRemFile[0];
							drTemp.SetField<bool>("is_local", true);

							// format the file size
							drTemp.SetField<string>("str_latest_size", FormatSize(drTemp.Field<long>("latest_size")));

							// format the modify date
							drTemp.SetField<string>("str_latest_stamp", FormatDate(drTemp.Field<DateTime>("latest_stamp")));

							// format the checkout date
							object oDate = drTemp["checkout_date"];
							if (oDate == System.DBNull.Value)
							{
								drTemp.SetField<string>("str_checkout_date", null);
							}
							else
							{
								drTemp.SetField<string>("str_checkout_date", FormatDate(Convert.ToDateTime(oDate)));
							}

							// if checked out here, then use local file size and modified date
							//if () {
							//
							//}

						}
						else
						{

							// insert new row for local-only file
							dt.Rows.Add(null,
								null,
								intDirId,
								strFileName,
								null,
								strFileExt,
								null,
								lngFileSize,
								FormatSize(lngFileSize),
								dtModifyDate,
								FormatDate(dtModifyDate),
								null,
								null,
								null,
								null,
								null,
								true,
								false,
								strTreePath,
								strFilePath
								);

						}

					}

				} catch (IOException e) {
					MessageBox.Show("Error: Drive not ready or directory does not exist: " + e);
				} catch (UnauthorizedAccessException e) {
					MessageBox.Show("Error: Drive or directory access denided: " + e);
				} catch (Exception e) {
					MessageBox.Show("Error: " + e);
				}

			}

			foreach (TreeNode tnChild in tnParent.Nodes) {
				LoadFileDataRecursive(tnChild, ref dt);
			}

		}

		static string StringMD5(string FileName)
		{
			// get local file checksum
			using (var md5 = MD5.Create())
			{
				using (var stream = File.OpenRead(FileName))
				{
					return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
				}
			}
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

		void TreeRightMouseClick(object sender, MouseEventArgs e) {

			// get latest
			// checkout
			// add new
			// commit
			// undo checkout

			// check for the right mouse button
			if (e.Button != MouseButtons.Right) {
				return;
			}

			// verify that a tree node was right clicked, and select it
			treeView1.SelectedNode = treeView1.GetNodeAt(e.X, e.Y);
			if (treeView1.SelectedNode == null) {
				return;
			}
			TreeNode tnClicked = treeView1.SelectedNode;

			// reset context menu items
			//   get latest
			//   checkout
			//   add new
			//   commit
			//   undo checkout
			foreach (ToolStripMenuItem tsmiItem in cmsTree.Items) {
				tsmiItem.Enabled = true;
				tsmiItem.Visible = true;
			}

			// test for remote
			if (tnClicked.Tag != null) {

				// exists remotely
				// still allow AddNew and handle remotely existing files one-at-a-time
				//cmsTree.Items["cmsTreeAddNew"].Enabled = false;

				// test for local
				if(Directory.Exists(GetFilePath(tnClicked.FullPath)) != true) {
					// does not exist locally, so we can't commit or undo a checkout
					cmsTree.Items["cmsTreeCommit"].Enabled = false;
					cmsTree.Items["cmsTreeUndoCheckout"].Enabled = false;
				}
			} else {
				// does not exist remotely (local only)
				cmsTree.Items["cmsTreeGetLatest"].Enabled = false;
				cmsTree.Items["cmsTreeCheckout"].Enabled = false;
				cmsTree.Items["cmsTreeCommit"].Enabled = false;
				cmsTree.Items["cmsTreeUndoCheckout"].Enabled = false;
			}

			return;
		}

		void CmsTreeGetLatestClick(object sender, EventArgs e) {

			// create the status dialog
			dlgStatus = new StatusDialog();

			// start the database transaction
			t = connDb.BeginTransaction();

			// get directory info
			TreeNode tnCurrent = treeView1.SelectedNode;
			int intDirId = (int)tnCurrent.Tag;

			// get remote entries into a dataset
			DataSet dsTemp = new DataSet();

			// initialize sql command for remote entry list
			string strSql = @"
				select
					e.entry_id,
					v.version_id,
					e.dir_id,
					e.entry_name,
					t.type_id,
					t.file_ext,
					c.cat_name,
					v.file_size as latest_size,
					pg_size_pretty(v.file_size) as str_latest_size,
					v.file_modify_stamp as latest_stamp,
					to_char(v.file_modify_stamp, 'yyyy-MM-dd HH24:mm:ss') as str_latest_stamp,
					e.checkout_user,
					u.last_name || ', ' || u.first_name as ck_user_name,
					e.checkout_date,
					to_char(e.checkout_date, 'yyyy-MM-dd HH24:mm:ss') as str_checkout_date,
					e.checkout_node,
					false as is_local,
					true as is_remote,
					'pwa' || replace(path, '/', '\') as tree_path,
					:strLocalFileRoot || replace(path, '/', '\') as file_path,
					t.icon
				from hp_entry as e
				left join hp_user as u on u.user_id=e.checkout_user
				left join hp_category as c on c.cat_id=e.cat_id
				left join hp_type as t on t.type_id=e.type_id
				left join view_dir_tree as d on d.dir_id = e.dir_id
				left join (
					select distinct on (entry_id)
						entry_id,
						version_id,
						file_size,
						create_stamp,
						file_modify_stamp
					from hp_version
					order by entry_id, create_stamp desc
				) as v on v.entry_id=e.entry_id
				where e.dir_id in (select dir_id from fcn_directory_recursive (:dir_id))
				order by dir_id,entry_id;
			";

			// put the remote list in the DataSet
			NpgsqlDataAdapter daTemp = new NpgsqlDataAdapter(strSql, connDb);
			daTemp.SelectCommand.Parameters.Add(new NpgsqlParameter("dir_id", intDirId));
			daTemp.SelectCommand.Parameters.Add(new NpgsqlParameter("strLocalFileRoot", strLocalFileRoot));
			daTemp.Fill(dsTemp);

			DataTable dt;
			if (dsList.Tables.Count == 0) {
				// make an empty DataTable
				dt = CreateFileTable();
			} else {
				// get the selected DataTable
				dt = dsTemp.Tables[0];
			}

			// merge remote data with local data
			LoadFileDataRecursive(tnCurrent, ref dt);

			// package arguments for the background worker
			List<object> arguments = new List<object>();
			arguments.Add(t);
			arguments.Add(dt);

			// launch the background thread
			BackgroundWorker worker = new BackgroundWorker();
			worker.WorkerSupportsCancellation = true;
			worker.DoWork += new DoWorkEventHandler(worker_TreeGetLatest);
			worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
			dlgStatus.AddStatusLine("Get Latest", "Selected items: "+dt.Rows.Count);
			worker.RunWorkerAsync(arguments);

			bool blnWorkCanceled = dlgStatus.ShowStatusDialog("Get Latest");
			if (blnWorkCanceled == true) {
				worker.CancelAsync();
			}

			ResetView(tnCurrent.FullPath);

		}

		void worker_TreeGetLatest(object sender, DoWorkEventArgs e) {

			BackgroundWorker myWorker = sender as BackgroundWorker;
			dlgStatus.AddStatusLine("Info", "Starting worker");

			// get arguments
			List<object> genericlist = e.Argument as List<object>;
			NpgsqlTransaction t = (NpgsqlTransaction)genericlist[0];
			DataTable dtItems = (DataTable)genericlist[1];

			// start the database transaction
			//LargeObjectManager lbm = new LargeObjectManager(connDb);

			// prepare to get latest version file id
//			string strSql;
//			strSql = @"
//					select blob_ref
//					from hp_version
//					where entry_id=:entry_id
//					order by create_stamp
//					limit 1;
//				";
//			NpgsqlCommand cmdGetId = new NpgsqlCommand(strSql, connDb, t);
//			cmdGetId.Parameters.Add(new NpgsqlParameter("entry_id", NpgsqlTypes.NpgsqlDbType.Integer));
//			cmdGetId.Prepare();

			int intRowCount = dtItems.Rows.Count;
			for (int i = 0; i < intRowCount; i++) {

				if ((myWorker.CancellationPending == true)) {
					e.Cancel = true;
					break;
				}

				DataRow drCurrent = dtItems.Rows[i];
				string strFileName = drCurrent.Field<string>("entry_name");
				string strFilePath = drCurrent.Field<string>("file_path");
				string strFullName = strFilePath+"\\"+strFileName;
				FileInfo fiCurrFile = new FileInfo(strFullName);
				DateTime dtLocalModifyDate = fiCurrFile.LastWriteTime;

				if (fiCurrFile.Directory.Exists == false) {
					fiCurrFile.Directory.Create();
				}

				// report status
				dlgStatus.AddStatusLine("Info", "Testing file fitness (" + (i+1).ToString() + " of " + intRowCount.ToString() + "): " + strFileName);

				// test for local file existence
				if (File.Exists(strFullName)) {

					// test for local only
					if (drCurrent.Field<bool>("is_remote") == false) {
						// can't pull this local, it doesn't exist remotely
						dlgStatus.AddStatusLine("Info", "File is local only (" + (i+1).ToString() + " of " + intRowCount.ToString() + "): " + strFileName);
						continue;
					}

					// test for checked-out-by-me
					object oTest = drCurrent["checkout_user"];
					if ( (oTest != System.DBNull.Value) && (drCurrent.Field<int>("checkout_user") == intMyUserId) ) {
						// it is checked out to me
						// we should already have the latest
						dlgStatus.AddStatusLine("Info", "We already have the file checked out (" + (i+1).ToString() + " of " + intRowCount.ToString() + "): " + strFileName);
						continue;
					}

					// test for newer version
					if ((DateTime)drCurrent["latest_stamp"] <= dtLocalModifyDate) {
						// we have the latest version
						dlgStatus.AddStatusLine("Info", "We already have the latest version (" + (i+1).ToString() + " of " + intRowCount.ToString() + "): " + strFileName);
						continue;
					}


				} else {

					dlgStatus.AddStatusLine("Info", "File is remote only (" + (i+1).ToString() + " of " + intRowCount.ToString() + "): " + strFileName);

				}

				// get the file oid
				//cmdGetId.Parameters["entry_id"].Value = (int)drCurrent["entry_id"];
				//object oTemp = cmdGetId.ExecuteScalar();
				//int intFileId;
				//if (oTemp != null) {
				//	intFileId = (int)(long)oTemp;
				//} else {
				//	throw new System.Exception("Failed to get file ID \""+fiCurrFile.Name+"\"");
				//	//return;
				//}

				// name and download the file
				int intEntryId = (int)drCurrent["entry_id"];
				int intVersionId = (int)drCurrent["version_id"];
				string strFileExt = drCurrent.Field<string>("file_ext");
				string strDavName = "/" + intEntryId.ToString() + "/" + intVersionId.ToString() + "." + strFileExt;

				// report status and stream file
				string strFileSize = drCurrent.Field<string>("str_latest_size");
				dlgStatus.AddStatusLine("Retrieving Content (" + strFileSize + ")", strDavName);
				connDav.Download(strDavName, strFullName);

				// pull the file local
				//LargeObject lo =  lbm.Open(intFileId,LargeObjectManager.READ);
				//lo =  lbm.Open(intFileId,LargeObjectManager.READ);

				// open the file stream
				//FileStream fsout;
				//try {
				//	fsout = File.OpenWrite(fiCurrFile.FullName);
				//	fsout.Lock(0,fsout.Length);
				//} catch {
				//	throw new System.Exception("The file \""+fiCurrFile.Name+"\" has been locked by another process.  Release it before adding it.");
				//	//return;
				//}
				//byte[] buf = new byte[lo.Size()];
				//buf = lo.Read(lo.Size());

				// write the file
				//fsout.Write(buf, 0, (int)lo.Size());
				//fsout.Flush();
				//fsout.Close();
				//lo.Close();

				// set the file readonly
				fiCurrFile.IsReadOnly = true;

				// report status
				dlgStatus.AddStatusLine("File transfer complete", strFileName);

			}

			t.Commit();

		}

		void CmsTreeCheckoutClick(object sender, EventArgs e) {

		}

		void CmsTreeAddNewClick(object sender, EventArgs e) {

			// create the status dialog
			dlgStatus = new StatusDialog();
			bool blnFailed;

			// start the database transaction
			t = connDb.BeginTransaction();

			// get directory info
			// TODO: make this thread safe
			TreeNode tnCurrent = treeView1.SelectedNode;
			int intDirId;
			if (tnCurrent.Tag != null) {
				intDirId = (int)tnCurrent.Tag;
			} else {
				// need to insert remote directory structure
				dlgStatus.AddStatusLine("Creating remote directory structure", tnCurrent.FullPath);
				blnFailed = AddDirStructReverse(t, tnCurrent);
				if (blnFailed) {
					t.Rollback();
					dlgStatus.AddStatusLine("Error", "Failed to create remote directory structure for current node: " + tnCurrent.FullPath);
					return;
				}
				intDirId = (int)tnCurrent.Tag;
			}

			// get remote entries into a dataset
			DataSet dsTemp = new DataSet();

			// initialize sql command for remote entry list
			string strSql = @"
				select
					e.entry_id,
					v.version_id,
					e.dir_id,
					e.entry_name,
					t.type_id,
					t.file_ext,
					c.cat_name,
					v.file_size as latest_size,
					pg_size_pretty(v.file_size) as str_latest_size,
					v.file_modify_stamp as latest_stamp,
					to_char(v.file_modify_stamp, 'yyyy-MM-dd HH24:mm:ss') as str_latest_stamp,
					e.checkout_user,
					u.last_name || ', ' || u.first_name as ck_user_name,
					e.checkout_date,
					to_char(e.checkout_date, 'yyyy-MM-dd HH24:mm:ss') as str_checkout_date,
					e.checkout_node,
					false as is_local,
					true as is_remote,
					'pwa' || replace(path, '/', '\') as tree_path,
					:strLocalFileRoot || replace(path, '/', '\') as file_path,
					t.icon
				from hp_entry as e
				left join hp_user as u on u.user_id=e.checkout_user
				left join hp_category as c on c.cat_id=e.cat_id
				left join hp_type as t on t.type_id=e.type_id
				left join view_dir_tree as d on d.dir_id = e.dir_id
				left join (
					select distinct on (entry_id)
						entry_id,
						version_id,
						file_size,
						create_stamp,
						file_modify_stamp
					from hp_version
					order by entry_id, create_stamp desc
				) as v on v.entry_id=e.entry_id
				where e.dir_id in (select dir_id from fcn_directory_recursive (:dir_id))
				order by dir_id,entry_id;
			";

			// put the remote list in the DataSet
			NpgsqlDataAdapter daTemp = new NpgsqlDataAdapter(strSql, connDb);
			daTemp.SelectCommand.Parameters.Add(new NpgsqlParameter("dir_id", intDirId));
			daTemp.SelectCommand.Parameters.Add(new NpgsqlParameter("strLocalFileRoot", strLocalFileRoot));
			daTemp.Fill(dsTemp);

			DataTable dt;
			if (dsList.Tables.Count == 0) {
				// make an empty DataTable
				dt = CreateFileTable();
			} else {
				// get the selected DataTable
				dt = dsTemp.Tables[0];
			}

			blnFailed = AddDirStructForward(t, tnCurrent);
			if (blnFailed) {
				t.Rollback();
				dlgStatus.AddStatusLine("Error", "Failed to create remote directory structure for subnodes: " + tnCurrent.FullPath);
				return;
			}

			// merge remote data with local data
			LoadFileDataRecursive(tnCurrent, ref dt);
			dlgStatus.AddStatusLine("Tree Add New", "Selected items: " + dt.Rows.Count);

			// package arguments for the background worker
			List<object> arguments = new List<object>();
			arguments.Add(t);
			arguments.Add(dt);

			// launch the background thread
			BackgroundWorker worker = new BackgroundWorker();
			worker.WorkerSupportsCancellation = true;
			worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
			worker.DoWork += new DoWorkEventHandler(worker_TreeAddNew);
			dlgStatus.AddStatusLine("Tree Add New", "Selected items: " + dt.Rows.Count);
			worker.RunWorkerAsync(arguments);


			// handle the cancel button
			bool blnWorkCanceled = dlgStatus.ShowStatusDialog("Add New");
			if (blnWorkCanceled == true) {
				worker.CancelAsync();
			}

			// refresh the main window
			ResetView(treeView1.SelectedNode.FullPath);

		}

		void worker_TreeAddNew(object sender, DoWorkEventArgs e) {

			BackgroundWorker myWorker = sender as BackgroundWorker;
			dlgStatus.AddStatusLine("Info", "Starting worker");

			// get arguments
			List<object> genericlist = e.Argument as List<object>;
			NpgsqlTransaction t = (NpgsqlTransaction)genericlist[0];
			DataTable dtItems = (DataTable)genericlist[1];


			// run critical tests
			int intRowCount = dtItems.Rows.Count;
			for (int i = 0; i < intRowCount; i++) {

				// check for cancellation
				if ((myWorker.CancellationPending == true)) {
					e.Cancel = true;
					return;
				}

				DataRow drCurrent = dtItems.Rows[i];
				string strFileName = drCurrent.Field<string>("entry_name");
				string strFilePath = drCurrent.Field<string>("file_path");
				string strFullName = strFilePath+"\\"+strFileName;
				FileInfo fiCurrFile = new FileInfo(strFullName);

				// report status
				dlgStatus.AddStatusLine("Info", "Testing file fitness (" + (i+1).ToString() + " of " + intRowCount.ToString() + "): " + strFileName);

				// test file is writeable
				if (IsFileLocked(fiCurrFile) == true) {
					// file is in use: don't continue
					throw new System.Exception("File \""+fiCurrFile.Name+"\" is locked.  Release it first.");
					//return;
				}

				// test file is less than 2GB
				if (fiCurrFile.Exists && fiCurrFile.Length > 2147483648) {
					// file is too large: don't continue
					throw new System.Exception("File \""+fiCurrFile.Name+"\" is larger than 2GB.  It can't be added.");
					//return;
				}

			}

			// add the files remotely
			bool blnFailed = false;
			for (int i = 0; i < intRowCount; i++) {

				// check for cancellation
				if ((myWorker.CancellationPending == true)) {
					e.Cancel = true;
					return;
				}

				// check for failure on previous loops
				if (blnFailed == true) {
					break;
				}

				DataRow drCurrent = dtItems.Rows[i];
				string strFileName = drCurrent.Field<string>("entry_name");
				string strFilePath = drCurrent.Field<string>("file_path");
				string strFullName = strFilePath+"\\"+strFileName;

				// test file does not exist remotely
				if (drCurrent.Field<bool>("is_remote") == true) {
					// already exist remotely, so skip it
					dlgStatus.AddStatusLine("Info", "File already exists remotely (" + (i+1).ToString() + " of " + intRowCount.ToString() + "): " + strFileName);
				} else {
					FileInfo fiCurrFile = new FileInfo(strFullName);
					blnFailed = AddNewEntry(sender, e, t, drCurrent, fiCurrFile);
				}

			}

			// commit to database and set files ReadOnly
			if (blnFailed == true) {
				t.Rollback();
				// TODO: figure out how to rollback WebDav changes
				throw new System.Exception("Operation failed. Rolling back the database");
			} else {
				t.Commit();

				// set the local files readonly
				for (int i = 0; i < intRowCount; i++) {

					DataRow drCurrent = dtItems.Rows[i];

					string strFileName = drCurrent.Field<string>("entry_name");
					string strFilePath = drCurrent.Field<string>("file_path");
					string strFullName = strFilePath+"\\"+strFileName;
					FileInfo fiCurrFile = new FileInfo(strFullName);
					if (fiCurrFile.Exists) {
						dlgStatus.AddStatusLine("Info", "Setting file ReadOnly (" + (i+1).ToString() + " of " + intRowCount.ToString() + "): " + strFileName);
						try {
							fiCurrFile.IsReadOnly = true;
						} catch (Exception ex) {
							dlgStatus.AddStatusLine("Info", "Failed to set file \""+fiCurrFile.Name+"\" to readonly." + System.Environment.NewLine + ex.ToString());
						}
					} else {
						dlgStatus.AddStatusLine("Info", "File doesn't exist locally, can't set ReadOnly (" + (i+1).ToString() + " of " + intRowCount.ToString() + "): " + strFileName);
					}

				} // end for

			}


		}

		void CmsTreeCommitClick(object sender, EventArgs e) {

		}

		void CmsTreeAnalyzeClick(object sender, EventArgs e) {

			// do something useful for all directories and files beneath this node:
			//   load another window with a list of special items
			//   local only items
			//   checked-out items
			//   remote changes

		}

		void CmsTreeUndoCheckoutClick(object sender, EventArgs e) {

		}

		#endregion


		#region ListView actions

		void lv1ColumnClick(object sender, ColumnClickEventArgs e) {

			// Determine if clicked column is already the column that is being sorted.
			if ( e.Column == lvwColumnSorter.SortColumn ) {
				// Reverse the current sort direction for this column.
				if (lvwColumnSorter.Order == SortOrder.Ascending) {
					lvwColumnSorter.Order = SortOrder.Descending;
				} else {
					lvwColumnSorter.Order = SortOrder.Ascending;
				}
			} else {
				// Set the column number that is to be sorted; default to ascending.
				lvwColumnSorter.SortColumn = e.Column;
				lvwColumnSorter.Order = SortOrder.Ascending;
			}

			// Perform the sort with these new sort options.
			this.listView1.Sort();

		}

		void ListRightMouseClick(object sender, MouseEventArgs e) {

			// get latest
			// checkout
			// add new
			// commit
			// undo checkout

			// check for the right mouse button
			if (e.Button != MouseButtons.Right) {
				return;
			}

			// verify that a list item was right clicked, and select it/them
			ListView.SelectedListViewItemCollection lviSelection = listView1.SelectedItems;
			if (lviSelection.Count == 0) {
				// we never actually get here because the handler only gets called when an item is selected
				return;
			}
				// reset context menu items
			//   get latest	 (remote)
			//   checkout	   (remote)
			//   add new		(local only)
			//   commit		 (checked-out to me)
			//   undo checkout  (checked-out to me)
			foreach (ToolStripMenuItem tsmiItem in cmsList.Items) {
				tsmiItem.Enabled = true;
			}

			// test for remote directory
			int dir_id;
			if (treeView1.SelectedNode.Tag != null) {
				dir_id = (int)treeView1.SelectedNode.Tag;

				foreach (ListViewItem lviSelected in lviSelection) {

					string strFileName = (string)lviSelected.SubItems[0].Text;
					DataRow drCurrent = dsList.Tables[0].Select("dir_id="+dir_id+" and entry_name='"+strFileName+"'")[0];

					if (drCurrent.Field<bool>("is_remote") == false) {
						// local only: need to disable "getlatest" and "checkout" but we
						// really shouldn't do it now because there may be other items in the
						// list that do exist remotely
						cmsList.Items["cmsListGetLatest"].Enabled = false;
						cmsList.Items["cmsListCheckout"].Enabled = false;
					} else {
						// exists remotely: don't let the user add it again
						cmsList.Items["cmsListAddNew"].Enabled = false;
					}

					// test for checked-out-by-me
					object oTest = drCurrent["checkout_user"];
					if ( (oTest != System.DBNull.Value) && (drCurrent.Field<int>("checkout_user") == intMyUserId) ) {
						// it is checked out to me
					} else {
						// it is not checked out to me
						cmsList.Items["cmsListCommit"].Enabled = false;
						cmsList.Items["cmsListUndoCheckout"].Enabled = false;
					}

				}

			} else {
				// all items are local only
				cmsList.Items["cmsListGetLatest"].Enabled = false;
				cmsList.Items["cmsListCheckout"].Enabled = false;
				cmsList.Items["cmsListCommit"].Enabled = false;
				cmsList.Items["cmsListUndoCheckout"].Enabled = false;
			}

			return;
		}

		void CmsListGetLatestClick(object sender, EventArgs e) {

			// get directory info
			int intDirId = (int)treeView1.SelectedNode.Tag;

			// get a data table of selected items
			DataTable dtSelected = dsList.Tables[0].Clone();
			ListView.SelectedListViewItemCollection lviSelection = listView1.SelectedItems;
			foreach (ListViewItem lviSelected in lviSelection) {
				string strFileName = (string)lviSelected.SubItems[0].Text;
				DataRow drSelected = dsList.Tables[0].Select("dir_id="+intDirId+" and entry_name='"+strFileName+"'")[0];
				dtSelected.ImportRow(drSelected);
			}

			// create the status dialog
			dlgStatus = new StatusDialog();

			// launch the background thread
			BackgroundWorker worker = new BackgroundWorker();
			worker.WorkerSupportsCancellation = true;
			worker.DoWork += new DoWorkEventHandler(worker_ListGetLatest);
			worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
			dlgStatus.AddStatusLine("Get Latest", "Selected items: "+lviSelection.Count);
			worker.RunWorkerAsync(dtSelected);

			bool blnWorkCanceled = dlgStatus.ShowStatusDialog("Get Latest");
			if (blnWorkCanceled == true) {
				worker.CancelAsync();
			}

			ResetView(treeView1.SelectedNode.FullPath);

		}

		void worker_ListGetLatest(object sender, DoWorkEventArgs e) {

			BackgroundWorker myWorker = sender as BackgroundWorker;
			DataTable dtItems = (DataTable)e.Argument;
			int intRowCount = dtItems.Rows.Count;
			dlgStatus.AddStatusLine("Info", "Starting worker");

			// start the database transaction
			t = connDb.BeginTransaction();
			//LargeObjectManager lbm = new LargeObjectManager(connDb);

			// prepare to get latest version file id
//			string strSql;
//			strSql = @"
//					select blob_ref
//					from hp_version
//					where entry_id=:entry_id
//					order by create_stamp
//					limit 1;
//				";
//			NpgsqlCommand cmdGetId = new NpgsqlCommand(strSql, connDb, t);
//			cmdGetId.Parameters.Add(new NpgsqlParameter("entry_id", NpgsqlTypes.NpgsqlDbType.Integer));
//			cmdGetId.Prepare();

			for (int i = 0; i < intRowCount; i++) {


				if ((myWorker.CancellationPending == true)) {
					e.Cancel = true;
					break;
				}

				DataRow drCurrent = dtItems.Rows[i];
				string strFileName = drCurrent.Field<string>("entry_name");
				string strFilePath = drCurrent.Field<string>("file_path");
				string strFullName = strFilePath+"\\"+strFileName;
				FileInfo fiCurrFile = new FileInfo(strFullName);
				DateTime dtLocalModifyDate = fiCurrFile.LastWriteTime;

				// report status
				dlgStatus.AddStatusLine("Testing file fitness (" + (i+1).ToString() + " of " + intRowCount.ToString() + ")", strFileName);

				// test for local file existence
				if (File.Exists(strFullName)) {

					// test for local only
					if (drCurrent.Field<bool>("is_remote") == false) {
						// can't pull this local, it doesn't exist remotely
						continue;
					}

					// test for checked-out-by-me
					object oTest = drCurrent["checkout_user"];
					if ( (oTest != System.DBNull.Value) && (drCurrent.Field<int>("checkout_user") == intMyUserId) ) {
						// it is checked out to me
						// we should already have the latest
						continue;
					}

					// test for newer version
					if ((DateTime)drCurrent["latest_stamp"] <= dtLocalModifyDate) {
						// we have the latest version
						continue;
					}

				} else {

					dlgStatus.AddStatusLine("File is remote only (" + (i+1).ToString() + " of " + intRowCount.ToString() + ")", strFileName);

				}

				// get the file oid
				//cmdGetId.Parameters["entry_id"].Value = (int)drCurrent["entry_id"];
				//object oTemp = cmdGetId.ExecuteScalar();
				//int intFileId;
				//if (oTemp != null) {
				//	intFileId = (int)(long)oTemp;
				//} else {
				//	throw new System.Exception("Failed to get file ID \""+fiCurrFile.Name+"\"");
				//	//return;
				//}

				// report status
				string strFileSize = drCurrent.Field<string>("str_latest_size");
				dlgStatus.AddStatusLine("Retrieve Content (" + strFileSize + ")", strFileName);

				// name and download the file
				int intEntryId = (int)drCurrent["entry_id"];
				int intVersionId = (int)drCurrent["version_id"];
				string strFileExt = drCurrent.Field<string>("file_ext");
				string strDavName = "/" + intEntryId.ToString() + "/" + intVersionId.ToString() + "." + strFileExt;
				connDav.Download(strDavName, strFullName);

				// pull the file local
				//LargeObject lo =  lbm.Open(intFileId,LargeObjectManager.READ);
				//lo =  lbm.Open(intFileId,LargeObjectManager.READ);

				// open the file stream
				//FileStream fsout;
				//try {
				//	fsout = File.OpenWrite(strFullName);
				//	fsout.Lock(0,fsout.Length);
				//} catch {
				//	throw new System.Exception("The file \""+fiCurrFile.Name+"\" has been locked by another process.  Release it before adding it.");
				//	//return;
				//}
				//byte[] buf = new byte[lo.Size()];
				//buf = lo.Read(lo.Size());

				// write the file
				//fsout.Write(buf, 0, (int)lo.Size());
				//fsout.Flush();
				//fsout.Close();
				//lo.Close();

				// report status
				dlgStatus.AddStatusLine("File transfer complete", strFileName);

			}

			t.Commit();

		}

		void CmsListCheckOutClick(object sender, EventArgs e) {

			// refresh file data
			LoadListData(treeView1.SelectedNode);

			// get directory info
			int intDirId = (int)treeView1.SelectedNode.Tag;

			// get a data table of selected items
			DataTable dtSelected = dsList.Tables[0].Clone();
			ListView.SelectedListViewItemCollection lviSelection = listView1.SelectedItems;
			foreach (ListViewItem lviSelected in lviSelection) {
				string strFileName = (string)lviSelected.SubItems[0].Text;
				DataRow drSelected = dsList.Tables[0].Select("dir_id="+intDirId+" and entry_name='"+strFileName+"'")[0];
				dtSelected.ImportRow(drSelected);
			}

			// create the status dialog
			dlgStatus = new StatusDialog();

			// launch the background thread
			BackgroundWorker worker = new BackgroundWorker();
			worker.WorkerSupportsCancellation = true;
			worker.DoWork += new DoWorkEventHandler(worker_ListCheckOut);
			worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
			dlgStatus.AddStatusLine("Check Out", "Selected items: "+lviSelection.Count);
			worker.RunWorkerAsync(dtSelected);

			bool blnWorkCanceled = dlgStatus.ShowStatusDialog("Check Out");
			if (blnWorkCanceled == true) {
				worker.CancelAsync();
			}

			ResetView(treeView1.SelectedNode.FullPath);

		}

		void worker_ListCheckOut(object sender, DoWorkEventArgs e) {

			BackgroundWorker myWorker = sender as BackgroundWorker;
			DataTable dtItems = (DataTable)e.Argument;
			int intRowCount = dtItems.Rows.Count;
			dlgStatus.AddStatusLine("Info", "Starting worker");

			// start the database transaction
			t = connDb.BeginTransaction();
			//LargeObjectManager lbm = new LargeObjectManager(connDb);

			// prepare to get latest version file id
//			string strSql;
//			strSql = @"
//					select blob_ref
//					from hp_version
//					where entry_id=:entry_id
//					order by create_stamp
//					limit 1;
//				";
//			NpgsqlCommand cmdGetId = new NpgsqlCommand(strSql, connDb, t);
//			cmdGetId.Parameters.Add(new NpgsqlParameter("entry_id", NpgsqlTypes.NpgsqlDbType.Integer));
//			cmdGetId.Prepare();

			// prepare to checkout file
			string strSql = @"
					update hp_entry
					set
						checkout_user=:user_id,
						checkout_date=now(),
						checkout_node=:node_id
					where entry_id=:entry_id;
				";
			NpgsqlCommand cmdCheckOut = new NpgsqlCommand(strSql, connDb, t);
			cmdCheckOut.Parameters.Add(new NpgsqlParameter("user_id", NpgsqlTypes.NpgsqlDbType.Integer));
			cmdCheckOut.Parameters.Add(new NpgsqlParameter("node_id", NpgsqlTypes.NpgsqlDbType.Integer));
			cmdCheckOut.Parameters.Add(new NpgsqlParameter("entry_id", NpgsqlTypes.NpgsqlDbType.Integer));
			cmdCheckOut.Prepare();

			for (int i = 0; i < intRowCount; i++) {


				if ((myWorker.CancellationPending == true)) {
					e.Cancel = true;
					break;
				}

				DataRow drCurrent = dtItems.Rows[i];
				string strFileName = drCurrent.Field<string>("entry_name");
				string strFilePath = drCurrent.Field<string>("file_path");
				string strFullName = strFilePath+"\\"+strFileName;
				FileInfo fiCurrFile = new FileInfo(strFullName);
				DateTime dtLocalModifyDate = fiCurrFile.LastWriteTime;

				// report status
				dlgStatus.AddStatusLine("Test file fitness (" + (i+1).ToString() + " of " + intRowCount.ToString() + ")", strFileName);

				// if the file exists locally, check the following
				if (File.Exists(strFullName)) {

					// test for local only
					if (drCurrent.Field<bool>("is_remote") == false) {
						// can't pull this local, it doesn't exist remotely
						dlgStatus.AddStatusLine("File doesn't exist on the server (" + (i+1).ToString() + " of " + intRowCount.ToString() + ")", strFileName);
						continue;
					}

					// test for checked-out-by-other
					object oTest = drCurrent["checkout_user"];
					if ( (oTest != System.DBNull.Value) && (drCurrent.Field<int>("checkout_user") != intMyUserId) ) {
						// it is checked out to someone else
						dlgStatus.AddStatusLine("Someone already has this file checked out (" + (i+1).ToString() + " of " + intRowCount.ToString() + ")", strFileName);
						continue;
					}

					// test for checked-out-by-me
					oTest = drCurrent["checkout_user"];
					if ( (oTest != System.DBNull.Value) && (drCurrent.Field<int>("checkout_user") == intMyUserId) ) {
						// it is checked out to me
						// we should already have the latest
						dlgStatus.AddStatusLine("You already have this file checked out (" + (i+1).ToString() + " of " + intRowCount.ToString() + ")", strFileName);
						continue;
					}

				}

				// test for newer version
				if ((DateTime)drCurrent["latest_stamp"] > dtLocalModifyDate) {

					if ((DateTime)drCurrent["latest_stamp"] < dtLocalModifyDate) {
						// file has been modified without checking out: data may be lost
						throw new System.Exception("File has been modified locally without checking out: \"" + strFileName + "\"");
						//return;
					}

					// get the file oid
					//cmdGetId.Parameters["entry_id"].Value = (int)drCurrent["entry_id"];
					//object oTemp = cmdGetId.ExecuteScalar();
					//int intFileId;
					//if (oTemp != null) {
					//	intFileId = (int)(long)oTemp;
					//} else {
					//	throw new System.Exception("Failed to get file blob_id: \"" + strFileName + "\".");
					//	//return;
					//}

					// report status
					string strFileSize = drCurrent.Field<string>("str_latest_size");
					dlgStatus.AddStatusLine("Begin streaming file to client (" + strFileSize + ")", strFileName);

					// name and download the file
					int intEntryId = (int)drCurrent["entry_id"];
					int intVersionId = (int)drCurrent["version_id"];
					string strFileExt = drCurrent.Field<string>("file_ext");
					string strDavName = "/" + intEntryId.ToString() + "/" + intVersionId.ToString() + "." + strFileExt;
					connDav.Download(strDavName, strFullName);

					// pull the file local
					//LargeObject lo =  lbm.Open(intFileId,LargeObjectManager.READ);
					//lo =  lbm.Open(intFileId,LargeObjectManager.READ);
					//Directory.CreateDirectory(strFilePath);
					//FileStream fsout = File.OpenWrite(strFullName);
					//byte[] buf = new byte[lo.Size()];
					//buf = lo.Read(lo.Size());
					//fsout.Write(buf, 0, (int)lo.Size());
					//fsout.Flush();
					//fsout.Close();
					//lo.Close();

					// report status
					//worker.ReportProgress((int)(i/lviSelection.Count));
					dlgStatus.AddStatusLine("File transfer complete", strFileName);
				}

				// checkout
				cmdCheckOut.Parameters["user_id"].Value = intMyUserId;
				cmdCheckOut.Parameters["node_id"].Value = intMyNodeId;
				cmdCheckOut.Parameters["entry_id"].Value = (int)drCurrent["entry_id"];
				int intRows = cmdCheckOut.ExecuteNonQuery();
				if (intRows > 0) {
					dlgStatus.AddStatusLine("File checkout info set", strFileName);
				}

			}

			t.Commit();

			// set the local files writeable
			for (int i = 0; i < intRowCount; i++) {

				DataRow drCurrent = dtItems.Rows[i];

				string strFileName = drCurrent.Field<string>("entry_name");
				string strFilePath = drCurrent.Field<string>("file_path");
				string strFullName = strFilePath+"\\"+strFileName;
				FileInfo fiCurrFile = new FileInfo(strFullName);
				dlgStatus.AddStatusLine("Setting file Writeable (" + (i+1).ToString() + " of " + intRowCount.ToString() + ")", strFileName);
				try {
					fiCurrFile.IsReadOnly = false;
				} catch (Exception ex) {
					dlgStatus.AddStatusLine("Error", "Failed to set file \""+fiCurrFile.Name+"\" to writeable." + System.Environment.NewLine + ex.ToString());
				}

			} // end for

		}

		void CmsListAddNewClick(object sender, EventArgs e) {

			// create the status dialog
			dlgStatus = new StatusDialog();

			// start the database transaction
			t = connDb.BeginTransaction();

			// get directory info
			TreeNode nodeCurrent = treeView1.SelectedNode;
			int intDirId;
			if (nodeCurrent.Tag != null) {
				intDirId = (int)nodeCurrent.Tag;
			} else {
				// need to insert remote directory structure
				dlgStatus.AddStatusLine("Creating remote directory structure", nodeCurrent.FullPath);
				bool blnFailed = AddDirStructReverse(t, nodeCurrent);
				if (blnFailed == true) {
					t.Rollback();
					dlgStatus.AddStatusLine("Failed to create remote directory structure", nodeCurrent.FullPath);
					return;
				}
				intDirId = (int)nodeCurrent.Tag;
			}

			// refresh data on both remote and local files
			LoadListData(nodeCurrent);

			// get a data table of selected items
			DataTable dtSelected = dsList.Tables[0].Clone();
			ListView.SelectedListViewItemCollection lviSelection = listView1.SelectedItems;
			foreach (ListViewItem lviSelected in lviSelection) {
				string strFileName = (string)lviSelected.SubItems[0].Text;
				DataRow drSelected = dsList.Tables[0].Select("dir_id="+intDirId+" and entry_name='"+strFileName+"'")[0];
				dtSelected.ImportRow(drSelected);
			}

			List<object> arguments = new List<object>();
			arguments.Add(t);
			arguments.Add(dtSelected);

			// launch the background thread
			BackgroundWorker worker = new BackgroundWorker();
			worker.WorkerSupportsCancellation = true;
			worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
			worker.DoWork += new DoWorkEventHandler(worker_ListAddNew);
			dlgStatus.AddStatusLine("Add New", "Selected items: "+lviSelection.Count);
			worker.RunWorkerAsync(arguments);

			bool blnWorkCanceled = dlgStatus.ShowStatusDialog("Add New Entries");
			if (blnWorkCanceled == true) {
				worker.CancelAsync();
			}

			ResetView(treeView1.SelectedNode.FullPath);

		}

		void worker_ListAddNew(object sender, DoWorkEventArgs e) {

			BackgroundWorker myWorker = sender as BackgroundWorker;
			dlgStatus.AddStatusLine("Info", "Starting worker");

			// get arguments
			List<object> genericlist = e.Argument as List<object>;
			NpgsqlTransaction t = (NpgsqlTransaction)genericlist[0];
			DataTable dtItems = (DataTable)genericlist[1];

			// run critical tests
			int intRowCount = dtItems.Rows.Count;
			for (int i = 0; i < intRowCount; i++) {

				// check for cancellation
				if ((myWorker.CancellationPending == true)) {
					e.Cancel = true;
					return;
				}

				DataRow drCurrent = dtItems.Rows[i];
				string strFileName = drCurrent.Field<string>("entry_name");
				string strFilePath = drCurrent.Field<string>("file_path");
				string strFullName = strFilePath+"\\"+strFileName;
				FileInfo fiCurrFile = new FileInfo(strFullName);

				// report status
				dlgStatus.AddStatusLine("Testing file fitness (" + (i+1).ToString() + " of " + intRowCount.ToString() + ")",  strFileName);

				// test file is writeable
				if (IsFileLocked(fiCurrFile) == true) {
					// file is in use: don't continue
					throw new System.Exception("File \""+fiCurrFile.Name+"\" is locked.  Release it first.");
					//return;
				}

				// test file is less than 2GB
				if (fiCurrFile.Length > 2147483648) {
					// file is too large: don't continue
					throw new System.Exception("File \""+fiCurrFile.Name+"\" is larger than 2GB.  It can't be added.");
					//return;
				}

			}

			// add the files remotely
			bool blnFailed = false;
			for (int i = 0; i < intRowCount; i++) {

				// check for cancellation
				if ((myWorker.CancellationPending == true)) {
					e.Cancel = true;
					return;
				}

				// check for failure on previous loops
				if (blnFailed == true) {
					break;
				}

				DataRow drCurrent = dtItems.Rows[i];
				string strFileName = drCurrent.Field<string>("entry_name");
				string strFilePath = drCurrent.Field<string>("file_path");
				string strFullName = strFilePath+"\\"+strFileName;

				// test file does not exist remotely
				if (drCurrent.Field<bool>("is_remote") == true) {
					// already exist remotely, so skip it
					dlgStatus.AddStatusLine("File already exists remotely (" + (i+1).ToString() + " of " + intRowCount.ToString() + ")", strFileName);
				} else {
					FileInfo fiCurrFile = new FileInfo(strFullName);
					blnFailed = AddNewEntry(sender, e, t, drCurrent, fiCurrFile);
				}

			}

			// commit to database and set files ReadOnly
			if (blnFailed == true) {
				t.Rollback();
				// TODO: figure out how to rollback WebDav changes
				throw new System.Exception("Operation failed. Rolling back the database");
			} else {
				t.Commit();

				// set the local files readonly
				for (int i = 0; i < intRowCount; i++) {

					DataRow drCurrent = dtItems.Rows[i];

					string strFileName = drCurrent.Field<string>("entry_name");
					string strFilePath = drCurrent.Field<string>("file_path");
					string strFullName = strFilePath+"\\"+strFileName;
					FileInfo fiCurrFile = new FileInfo(strFullName);
					dlgStatus.AddStatusLine("Setting file ReadOnly (" + (i+1).ToString() + " of " + intRowCount.ToString() + ")", strFileName);
					try {
						fiCurrFile.IsReadOnly = true;
					} catch (Exception ex) {
						dlgStatus.AddStatusLine("Error", "Failed to set file \""+fiCurrFile.Name+"\" to readonly." + System.Environment.NewLine + ex.ToString());
					}

				} // end for

			}


		}

		private bool AddNewEntry(object sender, DoWorkEventArgs e, NpgsqlTransaction t, DataRow drNewFile, FileInfo fiNewFile) {

			// make sure we are working inside of a transaction
			if (t.Connection == null) {
				MessageBox.Show("The database transaction is not functional");
				return(true);
			}

			// get the parent directory id
			int intParentDir = drNewFile.Field<int>("dir_id");

			// parameters
			string strSql;
			string strFileExt = drNewFile.Field<string>("file_ext");
			string strFileName = fiNewFile.Name;
			string strFullName = fiNewFile.FullName;
			long lngFileSize = fiNewFile.Length;
			DateTime dtModifyDate = fiNewFile.LastWriteTime;
			string strMd5sum = StringMD5(strFullName);


			// get entry type
			strSql = "select type_id from hp_type where file_ext ilike '" + strFileExt + "';";
			NpgsqlCommand cmdGetId = new NpgsqlCommand(strSql, connDb, t);
			object oTemp = cmdGetId.ExecuteScalar();
			int intTypeId;
			if (oTemp == null) {
				//intTypeId = CreateRemoteType(t, tnParent, strFileName);
				dlgStatus.AddStatusLine("Error", "No remote type exists for file extension "+strFileExt+".  Create the type first.");
				return(true);
			} else {
				intTypeId = (int)oTemp;
			}


			//
			// queue the file upload on the ThreadPool?
			// no... it's better to make the user wait
			//

			// setup the sql blob manager
			//LargeObjectManager lbm = new LargeObjectManager(connDb);
			//int noid = lbm.Create(LargeObjectManager.READWRITE);
			//LargeObject lo =  lbm.Open(noid,LargeObjectManager.READWRITE);

			// acquire and lock the file stream
			//FileStream fs = fiNewFile.OpenRead();
			//try {
			//	fs.Lock(0,fs.Length);
			//} catch {
			//	dlgStatus.AddStatusLine("File Locked by another process", fiNewFile.Name);
			//	return(true);
			//}

			// stream the file into the blob
			dlgStatus.AddStatusLine("Begin streaming file to server (" + FormatSize(lngFileSize) + ")", strFileName);
			//byte[] buf = new byte[fs.Length];
			//fs.Read(buf,0,(int)fs.Length);
			//lo.Write(buf);
			//lo.Close();


			// get a new entry id
			strSql = "select nextval('seq_hp_entry_entry_id'::regclass);";
			cmdGetId = new NpgsqlCommand(strSql, connDb, t);
			int intEntryId = (int)(long)cmdGetId.ExecuteScalar();

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
			cmdInsertEntry.Parameters.Add(new NpgsqlParameter("entry_id", intEntryId));
			cmdInsertEntry.Parameters.Add(new NpgsqlParameter("dir_id", intParentDir));
			cmdInsertEntry.Parameters.Add(new NpgsqlParameter("entry_name", strFileName));
			cmdInsertEntry.Parameters.Add(new NpgsqlParameter("type_id", intTypeId));
			cmdInsertEntry.Parameters.Add(new NpgsqlParameter("cat_id", (int)1));
			cmdInsertEntry.Parameters.Add(new NpgsqlParameter("create_user", intMyUserId));

			// insert entry
			try {
				cmdInsertEntry.ExecuteNonQuery();
			} catch (NpgsqlException ex) {
				// if unique key/index violation
				dlgStatus.AddStatusLine("File already exists on the server.  Refresh your view.", strFileName);
				dlgStatus.AddStatusLine("Error", ex.BaseMessage);
				return(true);
			}


			// get a new version id
			strSql = "select nextval('seq_hp_version_version_id'::regclass);";
			cmdGetId = new NpgsqlCommand(strSql, connDb, t);
			int intVersionId = (int)(long)cmdGetId.ExecuteScalar();

			// prepare a database command to insert the version
			strSql = @"
				insert into hp_version (
					version_id,
					entry_id,
					file_size,
					file_modify_stamp,
					create_user,
					preview_image,
					md5sum
				) values (
					:version_id,
					:entry_id,
					:file_size,
					:file_modify_stamp,
					:create_user,
					:preview_image,
					:md5sum
				);
			";
			NpgsqlCommand cmdInsertVersion = new NpgsqlCommand(strSql, connDb, t);
			cmdInsertVersion.Parameters.Add(new NpgsqlParameter("version_id", intVersionId));
			cmdInsertVersion.Parameters.Add(new NpgsqlParameter("entry_id", intEntryId));
			cmdInsertVersion.Parameters.Add(new NpgsqlParameter("file_size", lngFileSize));
			cmdInsertVersion.Parameters.Add(new NpgsqlParameter("file_modify_stamp", dtModifyDate.ToString("yyyy-MM-dd HH:mm:ss")));
			cmdInsertVersion.Parameters.Add(new NpgsqlParameter("create_user", intMyUserId));
			cmdInsertVersion.Parameters.Add(new NpgsqlParameter("preview_image", GetLocalPreview(drNewFile)));
			cmdInsertVersion.Parameters.Add(new NpgsqlParameter("md5sum", strMd5sum));

			// insert version
			try {
				cmdInsertVersion.ExecuteNonQuery();
			} catch (NpgsqlException ex) {
				// if unique key/index violation
				dlgStatus.AddStatusLine("File version already exists on the server.  Refresh your view.", strFileName);
				dlgStatus.AddStatusLine("Error", ex.BaseMessage);
				return(true);
			}

			//
			// insert file properties
			// for CAD files:
			//   get and insert dependencies
			//   get and insert custom properties
			//

			// name the file
			string strDavName = "/" + intEntryId.ToString() + "/" + intVersionId.ToString() + "." + strFileExt;

			// create directory if necessary
			connDav.CreateDir("/" + intEntryId.ToString());

			// upload the file
			connDav.Upload(strFullName, strDavName);

			return(false);

		}

		void CmsListCommitClick(object sender, EventArgs e) {

			// get directory info
			int intDirId = (int)treeView1.SelectedNode.Tag;

			// get a data table of selected items
			DataTable dtSelected = dsList.Tables[0].Clone();
			ListView.SelectedListViewItemCollection lviSelection = listView1.SelectedItems;
			foreach (ListViewItem lviSelected in lviSelection) {
				string strFileName = (string)lviSelected.SubItems[0].Text;
				DataRow drSelected = dsList.Tables[0].Select("dir_id=" + intDirId + " and entry_name='" + strFileName + "'")[0];
				dtSelected.ImportRow(drSelected);
			}

			// create the status dialog
			dlgStatus = new StatusDialog();

			// launch the background thread
			BackgroundWorker worker = new BackgroundWorker();
			worker.WorkerSupportsCancellation = true;
			worker.DoWork += new DoWorkEventHandler(worker_ListCommit);
			worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
			dlgStatus.AddStatusLine("Commit", "Selected items: "+lviSelection.Count);
			worker.RunWorkerAsync(dtSelected);

			bool blnWorkCanceled = dlgStatus.ShowStatusDialog("Get Latest");
			if (blnWorkCanceled == true) {
				worker.CancelAsync();
			}

			ResetView(treeView1.SelectedNode.FullPath);

		}

		void worker_ListCommit(object sender, DoWorkEventArgs e) {

			BackgroundWorker myWorker = sender as BackgroundWorker;
			DataTable dtItems = (DataTable)e.Argument;
			int intRowCount = dtItems.Rows.Count;
			dlgStatus.AddStatusLine("Info", "Starting worker");

			// begin database transaction
			t = connDb.BeginTransaction();

			// run critical tests
			for (int i = 0; i < intRowCount; i++) {

				// check for cancellation
				if ((myWorker.CancellationPending == true)) {
					e.Cancel = true;
					return;
				}

				DataRow drCurrent = dtItems.Rows[i];
				string strFileName = drCurrent.Field<string>("entry_name");
				string strFilePath = drCurrent.Field<string>("file_path");
				string strFullName = strFilePath+"\\"+strFileName;
				FileInfo fiCurrFile = new FileInfo(strFullName);

				// report status
				dlgStatus.AddStatusLine("Testing file fitness (" + (i+1).ToString() + " of " + intRowCount.ToString() + ")", strFileName);

				// test file is writeable
				if (IsFileLocked(fiCurrFile) == true) {
					// file is in use: don't continue
					throw new System.Exception("File is locked.  Release it first. \"" + fiCurrFile.Name + "\"");
					//return;
				}

				// test file is less than 2GB
				if (fiCurrFile.Length > 2147483648) {
					// file is too large: don't continue
					throw new System.Exception("File is larger than 2GB.  It can't be checked in. \"" + fiCurrFile.Name + "\"");
					//return;
				}

			}

			// add the files remotely
			bool blnFailed = false;
			for (int i = 0; i < intRowCount; i++) {

				// check for cancellation
				if ((myWorker.CancellationPending == true)) {
					e.Cancel = true;
					return;
				}

				// check for failure on previous loop
				if (blnFailed == true) {
					break;
				}

				DataRow drCurrent = dtItems.Rows[i];
				string strFileName = drCurrent.Field<string>("entry_name");
				string strFilePath = drCurrent.Field<string>("file_path");
				string strFullName = strFilePath+"\\"+strFileName;
				FileInfo fiCurrFile = new FileInfo(strFullName);
				long lngFileSize = fiCurrFile.Length;
				DateTime dtModifyDate = fiCurrFile.LastWriteTime;


				// write status
				dlgStatus.AddStatusLine("Begin streaming file to server (" + FormatSize(lngFileSize) + ")", strFileName);

				// get a new version id
				string strSql = "select nextval('seq_hp_version_version_id'::regclass);";
				NpgsqlCommand cmdGetVersion = new NpgsqlCommand(strSql, connDb, t);
				int intVersionId = (int)(long)cmdGetVersion.ExecuteScalar();

				// prepare a database command to insert the version
				strSql = @"
					insert into hp_version (
						version_id,
						entry_id,
						file_size,
						file_modify_stamp,
						create_user,
						preview_image
					) values (
						:version_id,
						:entry_id,
						:file_size,
						:file_modify_stamp,
						:create_user,
						:preview_image
					);
				";
				NpgsqlCommand cmdInsertVersion = new NpgsqlCommand(strSql, connDb, t);
				cmdInsertVersion.Parameters.Add(new NpgsqlParameter("version_id", intVersionId));
				cmdInsertVersion.Parameters.Add(new NpgsqlParameter("entry_id", drCurrent.Field<int>("entry_id")));
				cmdInsertVersion.Parameters.Add(new NpgsqlParameter("file_size", lngFileSize));
				cmdInsertVersion.Parameters.Add(new NpgsqlParameter("file_modify_stamp", dtModifyDate.ToString()));
				cmdInsertVersion.Parameters.Add(new NpgsqlParameter("create_user", intMyUserId));
				cmdInsertVersion.Parameters.Add(new NpgsqlParameter("preview_image", GetLocalPreview(drCurrent)));

				// insert version
				try {
					cmdInsertVersion.ExecuteNonQuery();
				} catch (NpgsqlException ex) {
					// if unique key/index violation
					throw new System.Exception("A version of file "+strFileName+" already exists on the server.  Refresh your view.  "+ex.BaseMessage);
					//return;
				}

				// remove checked-out status
				strSql = @"
					update hp_entry
					set
						checkout_user=null,
						checkout_date=null,
						checkout_node=null
					where entry_id=:entry_id;
				";
				NpgsqlCommand cmdUpdateEntry = new NpgsqlCommand(strSql, connDb, t);
				cmdUpdateEntry.Parameters.Add(new NpgsqlParameter("entry_id", drCurrent.Field<int>("entry_id")));
				cmdUpdateEntry.ExecuteNonQuery();

				// name the file
				int intEntryId = drCurrent.Field<int>("entry_id");
				string strFileExt = drCurrent.Field<string>("file_ext");
				string strDavName = "/" + intEntryId.ToString() + "/" + intVersionId.ToString() + "." + strFileExt;

				// creating the directory shouldn't be necessary
				// upload the file
				connDav.Upload(strFullName, strDavName);
				if (connDav.StatusString != "2") blnFailed = true;

			}

			// commit to database and set files ReadOnly
			if (blnFailed == true) {
				t.Rollback();
				// TODO: figure out how to rollback WebDav changes
				throw new System.Exception("Operation failed. Rolling back the database");
			} else {
				t.Commit();

				// set the local files readonly
				for (int i = 0; i < intRowCount; i++) {

					DataRow drCurrent = dtItems.Rows[i];

					string strFileName = drCurrent.Field<string>("entry_name");
					string strFilePath = drCurrent.Field<string>("file_path");
					string strFullName = strFilePath+"\\"+strFileName;
					FileInfo fiCurrFile = new FileInfo(strFullName);
					dlgStatus.AddStatusLine("Setting file ReadOnly (" + (i+1).ToString() + " of " + intRowCount.ToString() + ")", strFileName);
					try {
						fiCurrFile.IsReadOnly = true;
					} catch (Exception ex) {
						dlgStatus.AddStatusLine("Failed to set file to readonly.", fiCurrFile.Name);
						dlgStatus.AddStatusLine("Error", ex.ToString());
					}

				} // end for

			} // end if


		}

		void CmsListUndoCheckoutClick(object sender, EventArgs e) {

			// get directory info
			int intDirId = (int)treeView1.SelectedNode.Tag;

			// get a data table of selected items
			DataTable dtSelected = dsList.Tables[0].Clone();
			ListView.SelectedListViewItemCollection lviSelection = listView1.SelectedItems;
			foreach (ListViewItem lviSelected in lviSelection) {
				string strFileName = (string)lviSelected.SubItems[0].Text;
				DataRow drSelected = dsList.Tables[0].Select("dir_id=" + intDirId + " and entry_name='" + strFileName+"'")[0];
				dtSelected.ImportRow(drSelected);
			}

			// create the status dialog
			dlgStatus = new StatusDialog();

			// launch the background thread
			BackgroundWorker worker = new BackgroundWorker();
			worker.WorkerSupportsCancellation = true;
			worker.DoWork += new DoWorkEventHandler(worker_ListUndoCheckout);
			worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
			dlgStatus.AddStatusLine("Undo Checkout", "Selected items: "+lviSelection.Count);
			worker.RunWorkerAsync(dtSelected);

			bool blnWorkCanceled = dlgStatus.ShowStatusDialog("Undo Checkout");
			if (blnWorkCanceled == true) {
				worker.CancelAsync();
			}

			ResetView(treeView1.SelectedNode.FullPath);

		}

		void worker_ListUndoCheckout(object sender, DoWorkEventArgs e) {

			BackgroundWorker myWorker = sender as BackgroundWorker;
			DataTable dtItems = (DataTable)e.Argument;
			int intRowCount = dtItems.Rows.Count;
			dlgStatus.AddStatusLine("info", "Starting worker");

			// start the database transaction
			t = connDb.BeginTransaction();
			//LargeObjectManager lbm = new LargeObjectManager(connDb);

			// prepare to get latest version file id
			string strSql;
			strSql = @"
					select blob_ref
					from hp_version
					where entry_id=:entry_id
					order by create_stamp
					limit 1;
				";
			NpgsqlCommand cmdGetId = new NpgsqlCommand(strSql, connDb, t);
			cmdGetId.Parameters.Add(new NpgsqlParameter("entry_id", NpgsqlTypes.NpgsqlDbType.Integer));
			cmdGetId.Prepare();

			// prepare to undo checkout info
			strSql = @"
				update hp_entry
				set
					checkout_user=null,
					checkout_date=null,
					checkout_node=null
				where entry_id=:entry_id;
			";
			NpgsqlCommand cmdUpdateEntry = new NpgsqlCommand(strSql, connDb, t);
			cmdUpdateEntry.Parameters.Add(new NpgsqlParameter("entry_id", NpgsqlTypes.NpgsqlDbType.Integer));
			cmdUpdateEntry.Prepare();

			for (int i = 0; i < intRowCount; i++) {


				if ((myWorker.CancellationPending == true)) {
					e.Cancel = true;
					break;
				}

				DataRow drCurrent = dtItems.Rows[i];
				string strFileName = drCurrent.Field<string>("entry_name");
				string strFilePath = drCurrent.Field<string>("file_path");
				string strFullName = strFilePath+"\\"+strFileName;
				FileInfo fiCurrFile = new FileInfo(strFullName);

				// report status
				dlgStatus.AddStatusLine("Testing file fitness (" + (i+1).ToString() + " of " + intRowCount.ToString() + ")", strFileName);

				// test for checked-out-by-me
				object oTest = drCurrent["checkout_user"];
				if ( (oTest == System.DBNull.Value) || (drCurrent.Field<int>("checkout_user") != intMyUserId) ) {
					// it is not checked out to me
					dlgStatus.AddStatusLine("You don't have this file checked out (" + (i+1).ToString() + " of " + intRowCount.ToString() + ")", strFileName);
					continue;
				}

				// test for local file existence
				if (File.Exists(strFullName)) {

					// test for newer version
					if ((DateTime)drCurrent["latest_stamp"] >= fiCurrFile.LastWriteTime) {
						// we have the latest version
						// undo the checkout info in the database
						dlgStatus.AddStatusLine("File is unmodified (" + (i+1).ToString() + " of " + intRowCount.ToString() + ")", strFileName);
						cmdUpdateEntry.Parameters["entry_id"].Value = (int)drCurrent["entry_id"];
						cmdUpdateEntry.ExecuteNonQuery();
						//  continue to the next file
						continue;
					}

				}

				// get the file oid
				cmdGetId.Parameters["entry_id"].Value = (int)drCurrent["entry_id"];
				object oTemp = cmdGetId.ExecuteScalar();
				int intFileId;
				if (oTemp != null) {
					intFileId = (int)(long)oTemp;
				} else {
					throw new System.Exception("Failed to get file ID: \"" + fiCurrFile.Name + "\"");
					//return;
				}

				// report status
				string strFileSize = drCurrent.Field<string>("str_latest_size");
				dlgStatus.AddStatusLine("Begin streaming file to client (" + strFileSize + ")", strFileName);

				// open the blob
				//LargeObject lo =  lbm.Open(intFileId,LargeObjectManager.READ);
				//lo =  lbm.Open(intFileId,LargeObjectManager.READ);

				// acquire and lock the file stream
				//FileStream fs = fiCurrFile.OpenWrite();
				//try {
				//	fs.Lock(0,fs.Length);
				//} catch {
				//	throw new System.Exception("The file \"" + fiCurrFile.Name + "\" has been locked by another process.  Release it before committing it.");
				//	//return;
				//}

				// stream the blob into the file
				//byte[] buf = new byte[lo.Size()];
				//buf = lo.Read(lo.Size());
				//fs.Write(buf, 0, (int)lo.Size());
				//fs.Flush();
				//fs.Close();
				//lo.Close();



				// set the file readonly
				fiCurrFile.IsReadOnly = true;

				// report status
				dlgStatus.AddStatusLine("File transfer complete", strFileName);

				// undo the checkout info in the database
				cmdUpdateEntry.Parameters["entry_id"].Value = (int)drCurrent["entry_id"];
				cmdUpdateEntry.ExecuteNonQuery();

			}

			// commit to database
			t.Commit();

		}

		#endregion


		#region tab page actions

		void ListView1SelectedIndexChanged(object sender, EventArgs e) {

			if (listView1.SelectedItems.Count != 1 ) {
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

		#endregion


		void CmdManageFileTypesClick(object sender, EventArgs e)
		{
			// load file type manager dialog
			ftmStart.ShowDialog();
		}




		void MainFormFormClosed(object sender, FormClosedEventArgs e) {
				Properties.Settings.Default.usetWindowState = this.WindowState;
		}



	}
}
