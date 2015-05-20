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
        Action<List<string> > StoreParamsFunc;

        private SortedDictionary<string, string> PropTypeMap;


        public SearchDialog(NpgsqlConnection dbConn, string FilePath, int UserId, Action<string> callbackfunc, Action<List<string> > storeparamsfunc, string FileContainsText,
            string PropDropDownText, string PropContainsText, string CheckedOutMeBox, string DeletedLocalBox, string LocalOnlyBox)
        {
            InitializeComponent();

            // Add an event handler for the item-selection:
            lvSearchResults.MouseDoubleClick += new MouseEventHandler(lvSearchResults_DoubleClick);
       //     this.Load += new EventHandler(SearchDialog_)

            connDb = dbConn;
            strFilePath = FilePath;
            intMyUserId = UserId;
            MainFormCallbackFunc = callbackfunc;
            StoreParamsFunc = storeparamsfunc;

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

            // Get any parameters that were used in the last search:
            txtFilename.Text = FileContainsText;
            cboProperty.Text = PropDropDownText;
            txtProperty.Text = PropContainsText;
            if (CheckedOutMeBox == "1")
                cbxCheckedMe.Checked = true;
            else
                cbxCheckedMe.Checked = false;

            if (DeletedLocalBox == "1")
                cbxDeletedLocal.Checked = true;
            else
                cbxDeletedLocal.Checked = false;

            if (LocalOnlyBox == "1")
                cbxLocalOnly.Checked = true;
            else
                cbxLocalOnly.Checked = false;


            if (cboProperty.Text != "" && PropTypeMap.ContainsKey(cboProperty.Text))
                txtProperty.Enabled = true;
            else
                txtProperty.Enabled = false;
        }

        ~SearchDialog()
        {
            StoreSearchParams();
        }

        private void StoreSearchParams()
        {
            List<string> paramlist = new List<string>();
            paramlist.Add(txtFilename.Text);
            paramlist.Add(cboProperty.Text);
            paramlist.Add(txtProperty.Text);
            if (cbxCheckedMe.Checked)
                paramlist.Add("1");
            else
                paramlist.Add("0");

            if (cbxDeletedLocal.Checked)
                paramlist.Add("1");
            else
                paramlist.Add("0");

            if (cbxLocalOnly.Checked)
                paramlist.Add("1");
            else
                paramlist.Add("0");

            StoreParamsFunc(paramlist);
        }


        private void btnSearch_Click(object sender, EventArgs e)
        {
            int MAXCOUNT = 100;

            string sqlquerystr = @"";
            if (cbxLocalOnly.Checked)
            {
                // Ignore all other search parameters, and return all files that exist only locally:
                List<string> localfiles = new List<string>();
                Utils.GetAllFilesInDir(strFilePath, ref localfiles);

                // Add these to the SQL string:
                string tmpstr = "";
                sqlquerystr = @"SELECT * FROM (";
                foreach (string s in localfiles)
                {
                    if (s != localfiles[0])
                        sqlquerystr += @"UNION";
                    tmpstr = Utils.GetParentDirectory(s);
                    sqlquerystr += @" SELECT '" + tmpstr.Substring(strFilePath.Length, tmpstr.Length - strFilePath.Length).Replace("\\", "/") + @"' AS rel_path,'" + Utils.GetBaseName(s) + @"' AS entry_name ";
                }
                sqlquerystr += @") AS l LEFT JOIN view_dir_tree AS t ON t.dir_name=l.rel_path LEFT JOIN hp_entry AS e ON l.entry_name=e.entry_name WHERE e.entry_id IS NULL LIMIT " + MAXCOUNT.ToString() + @";";
            }
            else
            {
                sqlquerystr = @"
                    SELECT DISTINCT
                        e.entry_name,
                        t.rel_path
                    FROM hp_entry AS e
                    LEFT JOIN view_dir_tree AS t ON t.dir_id=e.dir_id
                    FULL OUTER JOIN hp_version AS v ON v.entry_id=e.entry_id
                    FULL OUTER JOIN hp_version_property AS p ON p.version_id=v.version_id
                    WHERE 1=1";

                // Add the parameters from the search criteria:
                if (txtFilename.TextLength > 0)
                {
                    sqlquerystr += @"
                         AND UPPER(e.entry_name) LIKE '%" + txtFilename.Text.ToUpper() + "%'";
                }

                if (cboProperty.Text != "")
                {
                    string propcontains = GetPropertyInfo();
                    if (propcontains == "")
                        return;

                    sqlquerystr += @"
                         AND " + propcontains;
                }
                else
                {
                    // No property was specified, so ensure that the property value criteria box is empty:
                    txtProperty.Clear();
                }

                if (cbxCheckedMe.Checked)
                {
                    sqlquerystr += @"
                         AND e.checkout_user=" + intMyUserId.ToString();
                }

                if (cbxDeletedLocal.Checked)
                {
                    // Searching for any files that are marked as "deleted" on the server, but that still exist locally:
                    List<string> localfiles = new List<string>();
                    Utils.GetAllFilesInDir(strFilePath, ref localfiles);

                    // Add that to the SQL string:
                    sqlquerystr += @" AND e.destroyed=true AND CONCAT(t.rel_path, '/', e.entry_name) IN (";
                    foreach (string s in localfiles)
                    {
                        if (s != localfiles[0])
                            sqlquerystr += @",";
                        sqlquerystr += @"'" + s.Substring(strFilePath.Length, s.Length - strFilePath.Length).Replace("\\", "/") + @"'";
                    }
                    sqlquerystr += @")";
                }

                sqlquerystr += @" LIMIT " + MAXCOUNT.ToString() + @";";
            }


            // Perform the search:
            NpgsqlCommand command = new NpgsqlCommand(sqlquerystr, connDb);

            // Update the UI:
            lvSearchResults.Clear();
            lvSearchResults.View = View.Details;
            lvSearchResults.Columns.Add("File Name", -2);
            lvSearchResults.Columns.Add("File Path", -2);

            try
            {
                NpgsqlDataReader dr = command.ExecuteReader();
                string filename = "";
                string path = "";
                int counter = 0;
                while (dr.Read())
                {
                    try
                    {
                        filename = dr["entry_name"].ToString();
                        path = dr["rel_path"].ToString();
                        if (filename.Length == 0)
                        {
                            // Nothing useful was returned:
                            dr.Close();
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed reading query results." + System.Environment.NewLine + ex.Message, "Query Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        dr.Close();
                        break;
                    }

                    // Add the file to the list view:
                    lvSearchResults.Items.Add(filename).SubItems.Add(Utils.GetAbsolutePath(strFilePath, path));

                    counter++;
                    if (counter >= MAXCOUNT)
                    {
                        // Inform the user that there were too many results returned to display all of them:
                        lvSearchResults.Items.Add("...< ONLY THE FIRST " + MAXCOUNT.ToString() + " ITEMS ARE SHOWN >...");
                        break;
                    }
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

            StoreSearchParams();
        }

        
        private void btnReset_Click(object sender, EventArgs e)
        {
            txtFilename.Clear();
            cboProperty.Text = "";
            txtProperty.Clear();
            cbxCheckedMe.Checked = false;
            cbxDeletedLocal.Checked = false;
            cbxLocalOnly.Checked = false;
        }


        private void lvSearchResults_DoubleClick(object sender, MouseEventArgs e)
        {
            StoreSearchParams();

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
            // Make sure that the selected property type is valid:
            if (PropTypeMap.ContainsKey(cboProperty.Text) == false)
            {
                MessageBox.Show("Invalid Property type \"" + cboProperty.Text + "\" selected." + System.Environment.NewLine + "Please select a valid Property type.",
                        "Input Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                return "";
            }

            string propContains = "p." + PropTypeMap[cboProperty.Text] + "_value";
            if (txtProperty.TextLength == 0)
            {
                // Searching for files with any value of the given property:
                propContains += " IS NOT NULL";
            }
            else if (PropTypeMap[cboProperty.Text] == "text")
            {
                // Searching for files with the given property value that contains in the given text property.

                // Ask for case-insensitive search:
                propContains = "UPPER(p.text_value)";
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


        private void btnCancel_Click(object sender, EventArgs e)
        {
            StoreSearchParams();
            this.Close();
        }


        private void chkbxDeletedRemote_OnChanged(object sender, EventArgs e)
        {
            if (cbxDeletedLocal.Checked)
            {
                // Uncheck the "Local Files Only" check box:
                cbxLocalOnly.Checked = false;
            }
        }

        private void chkbxLocalOnly_OnChanged(object sender, EventArgs e)
        {
            if (cbxLocalOnly.Checked)
            {
                // Uncheck the "Deleted Existing Locally" check box:
           //     cbxDeletedLocal.Checked = false;
           //     cbxCheckedMe.Checked = false;

                // Make all other parameters disabled:
                txtFilename.Enabled = false;
                cboProperty.Enabled = false;
                txtProperty.Enabled = false;
                cbxDeletedLocal.Enabled = false;
                cbxCheckedMe.Enabled = false;
            }
            else
            {
                // Make all other parameters enabled:
                txtFilename.Enabled = true;
                cboProperty.Enabled = true;
                txtProperty.Enabled = true;
                cbxDeletedLocal.Enabled = true;
                cbxCheckedMe.Enabled = true;
            }
        }


        private void cmboProperty_OnChanged(object sender, EventArgs e)
        {
            if (cboProperty.Text != "" && PropTypeMap.ContainsKey(cboProperty.Text))
                txtProperty.Enabled = true;
            else
                txtProperty.Enabled = false;
        }


        private void goToToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }
    }
}
