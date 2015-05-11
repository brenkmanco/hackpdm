using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;

namespace HackPDM
{
    public partial class SearchDialog : Form
    {
        private NpgsqlConnection connDb;

        private string strFilePath;
        int intMyUserId;

        Action<string> MainFormCallbackFunc;

        private SortedDictionary<string, string> PropTypeMap;


        public SearchDialog(NpgsqlConnection dbConn, string FilePath, int UserId, Action<string> callbackfunc)
        {
            InitializeComponent();

            // Add an event handler for the item-selection:
            lvSearchResults.MouseDoubleClick += new MouseEventHandler(lvSearchResults_DoubleClick);
       //     this.Load += new EventHandler(SearchDialog_)

            connDb = dbConn;
            strFilePath = FilePath;
            intMyUserId = UserId;
            MainFormCallbackFunc = callbackfunc;

            // Get the list of available properties from the database:
            // Initialize the SQL command:
            string strcomm = @"
                select
                    prop_name, prop_type
                from hp_property
                order by prop_name;
            ";

            // Send the command:
            PropTypeMap = new SortedDictionary<string,string>();
            NpgsqlCommand command = new NpgsqlCommand(strcomm, connDb);
            try
            {
                NpgsqlDataReader dr = command.ExecuteReader();
                while (dr.Read())
                {
                    // Add the property to the drop-down box:
                    cboProperty.Items.Add(dr["prop_name"]);

                    // Add the property to the PropTypeMap:
                    PropTypeMap.Add(dr["prop_name"].ToString(), dr["prop_type"].ToString());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed getting properties." + System.Environment.NewLine + ex.Message,
                    "Data Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }


        private void btnSearch_Click(object sender, EventArgs e)
        {
            // Discover all of the local files:
            //List<string> alllocalfilepaths = new List<string>();
            //Utils.GetAllFilesInDir(strFilePath, ref alllocalfilepaths);

            // Get the parameters to search for:
            string sqlquerystr = @"";
            string proptype = "";
            if (txtFilename.TextLength > 0 && cboProperty.Text == "")
            {
                // Search for a file with the given name portion:
                string filenameContains = txtFilename.Text;
                string chkbxInfo = GetCheckboxInfo();

                sqlquerystr = @"SELECT * FROM hp_entry AS e LEFT JOIN view_dir_tree AS t ON t.dir_id=e.dir_id WHERE (UPPER(e.entry_name) LIKE '%" + filenameContains.ToUpper() + "%' " + chkbxInfo + ");";
            }
            else if (txtFilename.TextLength > 0 && cboProperty.Text != "")
            {
                // Search for a file with the given name portion and the given parameter setting:
                string filenameContains = txtFilename.Text;

                // Get the data about properties:
                string propContains = GetPropertyInfo();
                if (propContains == "")
                    return;
                string chkbxInfo = GetCheckboxInfo();
                proptype = PropTypeMap[cboProperty.Text] + "_value";

                sqlquerystr = @"SELECT * FROM hp_entry AS e LEFT JOIN view_dir_tree AS t ON t.dir_id=e.dir_id INNER JOIN hp_version AS v ON v.entry_id=e.entry_id INNER JOIN hp_version_property AS vp ON vp.version_id=v.version_id WHERE (UPPER(e.entry_name) LIKE '%" + filenameContains.ToUpper() + "%' AND " + propContains + chkbxInfo + ");";
            }
            else if (txtFilename.TextLength == 0 && cboProperty.Text != "")
            {
                // No filename given.  Search using only property information:
                string propContains = GetPropertyInfo();
                if (propContains == "")
                    return;
                string chkbxInfo = GetCheckboxInfo();
                proptype = PropTypeMap[cboProperty.Text] + "_value";

                sqlquerystr = @"SELECT * FROM hp_entry AS e LEFT JOIN view_dir_tree AS t ON t.dir_id=e.dir_id INNER JOIN hp_version AS v ON v.entry_id=e.entry_id INNER JOIN hp_version_property AS vp ON vp.version_id=v.version_id WHERE (" + propContains + chkbxInfo + ");";
            }

            // Perform the search:
            NpgsqlCommand command = new NpgsqlCommand(sqlquerystr, connDb);

            // Update the UI:
            lvSearchResults.Clear();
            lvSearchResults.View = View.Details;
            lvSearchResults.Columns.Add("File Name", -2);
            lvSearchResults.Columns.Add("File Path", -2);
            if (proptype != "")
                lvSearchResults.Columns.Add(proptype, -2);
            try
            {
                NpgsqlDataReader dr = command.ExecuteReader();
                while (dr.Read())
                {
                    string filename = dr["entry_name"].ToString();
                    string path = dr["rel_path"].ToString();

                    // Add the file to the list view:
                    if (proptype == "")
                        lvSearchResults.Items.Add(filename).SubItems.Add(Utils.GetAbsolutePath(strFilePath, path));
                    else
                        lvSearchResults.Items.Add(filename).SubItems.AddRange(new string[] { Utils.GetAbsolutePath(strFilePath, path), dr[proptype].ToString() });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed updating search results list." + System.Environment.NewLine + ex.Message,
                    "Data Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            // Set the width of the columns:
            lvSearchResults.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
            lvSearchResults.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }


        private void lvSearchResults_DoubleClick(object sender, MouseEventArgs e)
        {
            // Check to see if a valid item was selected:
            ListViewHitTestInfo info = lvSearchResults.HitTest(e.X, e.Y);
            ListViewItem item = info.Item;

            if (item != null)
            {
                // Get the full filepath of the selected item, and send it to the tree (on the main form):
                ListViewItem.ListViewSubItem pathitem;
                if (item.SubItems.Count > 0)
                {
                    // This is the basename item:
                    pathitem = item.SubItems[1];
                }
                else
                {
                    // Not a valid double-click:
                    return;
                }

                MainFormCallbackFunc(pathitem.Text + "/" + item.Text);

                this.Close();
            }
        }


        private string GetPropertyInfo()
        {
            string propContains = "vp." + PropTypeMap[cboProperty.Text] + "_value";
            if (txtProperty.TextLength == 0)
            {
                // Searching for files with any value of the given property:
                propContains += " IS NOT NULL";
            }
            else if (PropTypeMap[cboProperty.Text] == "text")
            {
                // Searching for files with the given property value that contains in the given text property.

                // Ask for case-insensitive search:
                propContains = "UPPER(vp.text_value)";
                propContains += " LIKE '%" + txtProperty.Text.ToUpper() + "%'";
            }
            else if (PropTypeMap[cboProperty.Text] == "date")
            {
                DateTime date;
                if (DateTime.TryParse(txtProperty.Text, out date))
                {
                    // Searching with a date property criteria:
                    propContains += "=" + date.ToString();
                }
                else
                {
                    MessageBox.Show("Failed parsing date entry." + System.Environment.NewLine + "Please check the format and try again.",
                        "Parsing Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return "";
                }
            }
            else if (PropTypeMap[cboProperty.Text] == "number")
            {
                double d;
                if (double.TryParse(txtProperty.Text, out d))
                {
                    // Searching with a number property criteria:
                    propContains += "=" + d.ToString();
                }
                else
                {
                    MessageBox.Show("Failed parsing number entry." + System.Environment.NewLine + "Please check the format and try again.",
                        "Parsing Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return "";
                }
            }
            else if (PropTypeMap[cboProperty.Text] == "yesno")
            {
                bool b;
                if (bool.TryParse(txtProperty.Text, out b))
                {
                    // Searching with a yes/no property criteria:
                    propContains += "=" + b.ToString().ToLower();
                }
                else
                {
                    MessageBox.Show("Failed parsing boolean (yes/no) entry." + System.Environment.NewLine + "Please check the format and try again.",
                        "Parsing Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return "";
                }
            }

            return propContains;
        }


        private string GetCheckboxInfo()
        {
            string info = @"";

            // Check the checkboxes:
            if (cbxCheckedMe.Checked)
            {
                // Searching with a "Checked-out to me" criteria:
                info += " AND e.checkout_user=" + intMyUserId.ToString();
            }

            if (cbxDeletedLocal.Checked)
            {
                // Searching for any files that are marked as "deleted" on the server, but that still exist locally:
                //  Get the list of local files:
                List<string> localfiles = new List<string>();
                Utils.GetAllFilesInDir(strFilePath, ref localfiles);

                // Add that to the SQL string:
                info += " AND e.destroyed=true AND CONCAT(t.rel_path, '/', e.entry_name) IN (";
                foreach (string s in localfiles)
                {
                    if (s != localfiles[0])
                        info += ",";
                    info += "'" + s.Substring(strFilePath.Length, s.Length - strFilePath.Length).Replace("\\", "/") + "'";
                }
                info += ")";
            }



            return info;
        }


        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }


        private void goToToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }
    }
}
