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
		private DataSet dsTypes = new DataSet();
		private DataTable dtRemoteTypes;
		private string strFilePath;

		private ListViewColumnSorter lvwColumnSorter;

		BindingSource bsFilters = new BindingSource();
		BindingSource bsTypes = new BindingSource();

		/// <summary>
		/// Return File Ignore Filter DataTable
		/// </summary>
		public DataTable IgnoreFilters
		{
			get { return dsFilters.Tables[0]; }
		}

		/// <summary>
		/// Return File Types DataTable
		/// </summary>
		public DataTable FileTypes
		{
			get { return dtRemoteTypes; }
		}



		#endregion


		public FileTypeManager(NpgsqlConnection dbConn, string FilePath, string TreePath)
		{
			//
			// The InitializeComponent() call is required for Windows Forms designer support.
			//
			InitializeComponent();

			// setup listview column sorting
			lvwColumnSorter = new ListViewColumnSorter();
			this.lvTypes.ListViewItemSorter = lvwColumnSorter;

			connDb = dbConn;
			strFilePath = FilePath;

			LoadFilters();
			LoadRemoteTypes();
			
		}

		void lvTypesColumnClick(object sender, ColumnClickEventArgs e)
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
			this.lvTypes.Sort();

		}


		#region Utility Methods

		private void LoadFilters() {

			// clear datagridview
			dgFilters.DataSource = null;
			dgFilters.Refresh();

			// clear dataset
			dsFilters = new DataSet();

			//
			// get remote filters
			//
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

		}


		private void LoadRemoteTypes()
		{


			//
			// get remote file types
			//
			// initialize sql command for remote type list
			string strSql = @"
				select
					t.type_id,
					t.file_ext,
					t.default_cat,
					c.cat_name,
					t.icon,
					t.type_regex,
					t.description,
					false as is_local,
					true as is_remote
				from hp_type as t
				left join hp_category as c on c.cat_id=t.default_cat
				order by t.file_ext;
			";

			// put the remote list in the DataSet
			NpgsqlDataAdapter daTemp = new NpgsqlDataAdapter(strSql, connDb);
			daTemp.Fill(dsTypes);

			// if there are no remote types
			if (dsTypes.Tables.Count == 0)
			{
				// make an empty DataTable
				dsTypes.Tables.Add(CreateTypeTable());
			}

			// copy to remote only datatable
			// are we doing this because we can't bind a Regex object to a field in the datagrid?
			dtRemoteTypes = dsTypes.Tables[0].Copy();
			dtRemoteTypes.Columns.Add("regex", Type.GetType("System.Object"));
			foreach (DataRow dr in dtRemoteTypes.Rows)
			{
				dr["regex"] = new Regex(dr["type_regex"].ToString(), RegexOptions.Compiled);
			}

		}

		private void LoadLocalTypes()
		{

			//
			// get local file types
			//
			GetTypesRecursive(strFilePath);

			PopulateList();

		}

		private DataTable CreateTypeTable()
		{

			DataTable dtTypes = new DataTable();
			dtTypes.Columns.Add("type_id", Type.GetType("System.Int32"));
			dtTypes.Columns.Add("file_ext", Type.GetType("System.String"));
			dtTypes.Columns.Add("default_cat", Type.GetType("System.Int32"));
			dtTypes.Columns.Add("cat_name", Type.GetType("System.String"));
			dtTypes.Columns.Add("icon", typeof(Byte[]));
			dtTypes.Columns.Add("type_regex", Type.GetType("System.String"));
			dtTypes.Columns.Add("is_local", Type.GetType("System.Boolean"));
			dtTypes.Columns.Add("is_remote", Type.GetType("System.Boolean"));
			return dtTypes;

		}

		protected void GetTypesRecursive(string ParentDir)
		{

			// get the DataTable
			DataTable dt = dsTypes.Tables[0];

			// get local types
			foreach (string d in Directory.GetDirectories(ParentDir))
			{
				foreach (string f in Directory.GetFiles(d))
				{

					// get the file extension and attempt a simple match
					string strFileExt = GetFileExt(f);
					int intMatch = dt.Select("file_ext='" + strFileExt + "'").Length;

					// if this extension (as windows recognizes it) is not in the current list
					if (intMatch == 0)
					{

						// if this file name is not blocked
						if (!FileBlocked(f))
						{

							// if this file does not match a more complex remote file type
							int intTypeId = GetRemoteType(f);
							if (intTypeId == 0)
							{
								// insert a now type row, including icon
									// type_id
									// file_ext
									// default_cat
									// cat_name
									// icon
									// type_regex
									// is_local
									// is_remote
								dt.Rows.Add(null,
									GetFileExt(f),
									null,
									null,
									ImageToByteArray(ExtractIcon(f)),
									null,
									true,
									false);
							}
							else
							{
								// otherwise, mark this remote type as existing local
								DataRow dr = dt.Select("type_id='" + intTypeId + "'")[0];
								dr.SetField<bool>("is_local", true);
							}


						}

					}

				}

				GetTypesRecursive(d);

			}

		}

		protected string GetFileExt(string strFileName)
		{

			// parse file path for file extension
			//string[] strSplit = strFileName.Split('.');
			//int _maxIndex = strSplit.Length - 1;
			//return strSplit[_maxIndex];

			// use FileInfo to get file extension
			FileInfo fiCurrFile = new FileInfo(strFileName);
			return fiCurrFile.Extension.Substring(1, fiCurrFile.Extension.Length - 1).ToLower();

		}

		protected int GetRemoteType(string strFileName)
		{

			// get short file name
			FileInfo fiCurrFile = new FileInfo(strFileName);
			string strShortName = fiCurrFile.Name;

			// use a regular expression to match file names with remote extensions
			foreach (DataRow dr in dtRemoteTypes.Rows)
			{
				Regex rxExt = new Regex(dr["type_regex"].ToString(), RegexOptions.IgnoreCase);
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

		protected void InitTypesList()
		{

			//init ListView control
			lvTypes.Clear();

			// configure sorting
			//listView1.Sorting = SortOrder.None;
			//listView1.ColumnClick += new ColumnClickEventHandler(lv1ColumnClick);

			//create columns for ListView
			lvTypes.Columns.Add("Extension", 100, System.Windows.Forms.HorizontalAlignment.Left);
			lvTypes.Columns.Add("Category", 250, System.Windows.Forms.HorizontalAlignment.Left);
			lvTypes.Columns.Add("Status", 75, System.Windows.Forms.HorizontalAlignment.Left);
			lvTypes.Columns.Add("Action", 75, System.Windows.Forms.HorizontalAlignment.Left);

		}

		private void PopulateList()
		{

			foreach (DataRow row in dsTypes.Tables[0].Rows)
			{

				// insert data
				string[] lvData = new string[3];
				lvData[0] = row.Field<string>("file_ext");
				lvData[1] = row.Field<string>("cat_name");

				if ((bool)row["is_local"] == true)
				{
					if ((bool)row["is_remote"] == false)
					{
						lvData[2] = "Local";
					}
					else
					{
						lvData[2] = "Both";
					}
				}
				else
				{
					lvData[2] = "Remote";
				}

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
				lvTypes.Items.Add(lvItem);

			}

		}

		#endregion


		private void btnRefreshTypes_Click(object sender, EventArgs e)
		{

			// Set cursor as hourglass
			Cursor.Current = Cursors.WaitCursor;

			// clear objects
			dsTypes = new DataSet();
			InitTypesList();

			// process file types
			LoadRemoteTypes();
			LoadLocalTypes();

			// bind types table to databinding
			bsTypes.DataSource = dsTypes.Tables[0];

			// Set cursor as default arrow
			Cursor.Current = Cursors.Default;

		}

		private void lvTypes_SelectedIndexChanged(object sender, EventArgs e)
		{
			string strFileExt = lvTypes.Text;
			SelectRecord(strFileExt);
		}


		void SelectRecord(string strFileExt)
		{
			int foundIndex = bsTypes.Find("file_ext", strFileExt);
			bsTypes.Position = foundIndex;
		}

		private void btnTypesNew_Click(object sender, EventArgs e)
		{
			dsTypes.Tables[0].Rows.Add(
				null,
				"",
				null,
				null,
				null,
				null,
				null,
				false,
				false);

			bsTypes.MoveLast();

		}

		private void btnRefreshFilters_Click(object sender, EventArgs e)
		{

			// Set cursor as hourglass
			Cursor.Current = Cursors.WaitCursor;

			// load the file ignore filters from the database
			LoadFilters();

			// bind datatable to datagridview
			dgFilters.DataSource = dsFilters.Tables[0];

			// set filter_id column readonly
			dgFilters.Columns["filter_id"].ReadOnly = true;
			dgFilters.Columns[0].DefaultCellStyle.BackColor = Color.LightGray;

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

		}


	}
}
