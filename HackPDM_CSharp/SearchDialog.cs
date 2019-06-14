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
using System.IO;
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

        private SortedDictionary<string, Tuple<string, string> > PropTypeMap;


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
                    prop_name, prop_type, prop_id
                from hp_property
                order by prop_name;
            ";

            // TODO: re-use the property map from MainForm: LoadPropertyMaps()
            // Send the command:
            PropTypeMap = new SortedDictionary<string, Tuple<string, string> >();
            NpgsqlCommand command = new NpgsqlCommand(strcomm, connDb);
            try
            {
                NpgsqlDataReader dr = command.ExecuteReader();
                while (dr.Read())
                {
                    // Add the property to the drop-down box:
                    cboProperty.Items.Add(dr["prop_name"]);

                    // Add the property to the PropTypeMap:
                    var t = new Tuple<string, string>(dr["prop_type"].ToString(), dr["prop_id"].ToString());
                    PropTypeMap.Add(dr["prop_name"].ToString(), t);
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
            // get max record count from text box
            int MAXCOUNT = 0;
            if (!int.TryParse(txtMaxRes.Text, out MAXCOUNT))
            {
                MessageBox.Show("Max Results must be an integer");
                return;
            }
            else if (MAXCOUNT<1)
            {
                MessageBox.Show("Max Results must be a positive integer");
                return;
            }
            DataTable dtSearchResults = new DataTable();
            dtSearchResults.Columns.Add("rel_path", Type.GetType("System.String"));
            dtSearchResults.Columns.Add("entry_name", Type.GetType("System.String"));

            bool blnFailed = false;

            if (cbxLocalOnly.Checked)
            {
                // Ignore all other search parameters, and return all files that exist only locally
                blnFailed = GetResults_LocalOnly(MAXCOUNT, ref dtSearchResults);
            }
            else if (cbxDeletedLocal.Checked)
            {
                // Ignore all other search parameters, and return all files that exist locally and are deleted on the server
                blnFailed = GetResults_LocalDeleted(MAXCOUNT, ref dtSearchResults);
            }
            else
            {
                blnFailed = GetResults_General(MAXCOUNT, ref dtSearchResults);
            }

            if (blnFailed)
            {
                // Don't do anything else:
                return;
            }

            // Update the UI:
            lvSearchResults.Clear();
            lvSearchResults.View = View.Details;
            lvSearchResults.Columns.Add("File Name", -2);
            lvSearchResults.Columns.Add("File Path", -2);

            string filename = "";
            string path = "";
            int counter = 0;
            foreach (DataRow dr in dtSearchResults.Rows)
            {
                filename = dr.Field<string>("entry_name");
                path = dr.Field<string>("rel_path");

                // Add the file to the list view:
                lvSearchResults.Items.Add(filename).SubItems.Add(path);

                counter++;
                if (counter >= MAXCOUNT)
                {
                    // Inform the user that there were too many results returned to display all of them:
                    lvSearchResults.Items.Add("...< ONLY THE FIRST " + MAXCOUNT.ToString() + " ITEMS ARE SHOWN >...");
                }

            }

            // Set the width of the columns:
            lvSearchResults.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
            lvSearchResults.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);

            StoreSearchParams();
        }

        private bool GetResults_LocalOnly(int MAXCOUNT, ref DataTable dtSearchResults)
        {

            // get all local files
            string[] strLocals = Directory.GetFiles(strFilePath, "*", SearchOption.AllDirectories);
            List<string> allLocal = new List<string>(strLocals);

            // get all remote files
            string strGetAllEntries = @"
                select
                    t.rel_path,
                    e.entry_name
                from hp_entry as e
                left join view_dir_tree as t on t.dir_id=e.dir_id";

            try
            {
                using (NpgsqlCommand cmdAllRemote = new NpgsqlCommand(strGetAllEntries, connDb))
                {
                    using (NpgsqlDataReader drAllRemote = cmdAllRemote.ExecuteReader())
                    {
                        while (drAllRemote.Read())
                        {
                            // get local absolute path from remote relative path
                            string relPath = drAllRemote.GetString(0);
                            string entryName = drAllRemote.GetString(1);
                            string absPathName = strFilePath + relPath.Replace("/", "\\") + "\\" + entryName;
                            // remove the local file from the list if it exists remotely
                            allLocal.Remove(absPathName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed retrieving search results from the server." + System.Environment.NewLine + ex.Message,
                    "Data Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return true;
            }

            int resCount = 1;
            foreach (string s in allLocal)
            {
                if (resCount > MAXCOUNT)
                {
                    return false;
                }
                if (s.Contains("\\~"))
                {
                    continue;
                }
                // get relative path, formatted to match server paths
                string strRelPathForm = Utils.GetParentDirectory(s);
                strRelPathForm = strRelPathForm.Substring(strFilePath.Length, strRelPathForm.Length - strFilePath.Length);
                strRelPathForm = strRelPathForm.Replace("\\", "/");
                strRelPathForm = strRelPathForm.Replace("'", "''");
                if (strRelPathForm == "")
                {
                    strRelPathForm = "/";
                }
                // and add it to the results table
                dtSearchResults.Rows.Add(new object[] { strRelPathForm, Utils.GetBaseName(s) });
                resCount++;
            }

            return false;

        }

        private bool GetResults_LocalDeleted(int MAXCOUNT, ref DataTable dtSearchResults)
        {

            // get all deleted files from the server
            string strGetAllDeleted = @"
                    SELECT DISTINCT
                        e.entry_name,
                        t.rel_path
                    FROM hp_entry AS e
                    LEFT JOIN view_dir_tree AS t ON t.dir_id=e.dir_id
                    WHERE e.active=false;
                ";

            // execute and retrieve data
            DataTable dtDeleted = new DataTable();
            NpgsqlCommand cmdGetResults = new NpgsqlCommand(strGetAllDeleted, connDb);
            NpgsqlDataAdapter daGetResults = new NpgsqlDataAdapter(cmdGetResults);
            try
            {
                daGetResults.Fill(dtDeleted);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed retrieving search results from the server." + System.Environment.NewLine + ex.Message,
                    "Data Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return true;
            }

            // get all local files
            string[] strLocals = Directory.GetFiles(strFilePath, "*", SearchOption.AllDirectories);


            // loop through all local files, comparing against server delete list
            int intCounter = 0;
            foreach (string s in strLocals)
            {
                // get relative path, formatted to match server paths
                string strRelPathForm = Utils.GetParentDirectory(s);
                strRelPathForm = strRelPathForm.Substring(strFilePath.Length, strRelPathForm.Length - strFilePath.Length);
                strRelPathForm = strRelPathForm.Replace("\\", "/");
                strRelPathForm = strRelPathForm.Replace("'", "''");

                // get matching deletion
                DataRow[] drMatches = dtDeleted.Select(String.Format("rel_path='{0}' and entry_name='{1}'", strRelPathForm, Utils.GetBaseName(s)));

                if (drMatches.Length > 0)
                {
                    // append to results table
                    dtSearchResults.ImportRow(drMatches[0]);
                    intCounter++;
                    if (intCounter > MAXCOUNT)
                    {
                        break;
                    }
                }
            }

            return false;

        }

        private bool GetResults_General(int MAXCOUNT, ref DataTable dtSearchResults)
        {

            string strGetResults = @"
                    SELECT DISTINCT
                        e.entry_name,
                        t.rel_path
                    FROM hp_entry AS e
                    LEFT JOIN view_dir_tree AS t ON t.dir_id=e.dir_id
                    FULL OUTER JOIN hp_version AS v ON v.entry_id=e.entry_id
                    FULL OUTER JOIN hp_version_property AS p ON p.version_id=v.version_id
                    WHERE 1=1
                ";

            // Add file name contains criteria
            if (txtFilename.TextLength > 0)
            {
                strGetResults += String.Format("\nAND e.entry_name ILIKE '%{0}%'", txtFilename.Text.Replace("'", "''"));
            }
            else
            {
                strGetResults += "\nAND e.entry_name IS NOT NULL";
            }

            // Add property contains criteria
            if (cboProperty.Text != "")
            {
                string propcontains = GetPropertyInfo();
                if (propcontains == "")
                    return true;
                strGetResults += String.Format("\nAND {0}", propcontains);
            }
            else
            {
                // No property was specified, so ensure that the property value criteria box is empty:
                txtProperty.Clear();
            }

            if (cbxCheckedMe.Checked)
            {
                strGetResults += String.Format("\nAND e.checkout_user={0}", intMyUserId.ToString());
            }


            strGetResults += String.Format("\nLIMIT {0};", MAXCOUNT.ToString());

            // execute and retrieve data
            NpgsqlCommand cmdGetResults = new NpgsqlCommand(strGetResults, connDb);
            NpgsqlDataAdapter daGetResults = new NpgsqlDataAdapter(cmdGetResults);
            try
            {
                daGetResults.Fill(dtSearchResults);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed retrieving search results from the server." + System.Environment.NewLine + ex.Message,
                    "Data Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return true;
            }

            return false;

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

            string propContains = @"p.prop_id=" + PropTypeMap[cboProperty.Text].Item2;
            if (txtProperty.TextLength == 0)
            {
                // Searching for files with any value of the given property:
                propContains += @"
                        AND p." + PropTypeMap[cboProperty.Text].Item1 + @"_value IS NOT NULL";
            }
            else if (PropTypeMap[cboProperty.Text].Item1 == "text")
            {
                // Searching for files with the given property value that contains in the given text property.

                // Ask for case-insensitive search:
                propContains += "\nAND p.text_value ILIKE '%" + txtProperty.Text + "%'";
            }
            else if (PropTypeMap[cboProperty.Text].Item1 == "date")
            {
                DateTime date;
                if (DateTime.TryParse(txtProperty.Text, out date))
                {
                    // Searching with a date property criteria:
                    propContains += @"
                        AND p.date_value=" + date.ToString();
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
            else if (PropTypeMap[cboProperty.Text].Item1 == "number")
            {
                double d;
                if (double.TryParse(txtProperty.Text, out d))
                {
                    // Searching with a number property criteria:
                    propContains += @"
                        AND p.number_value=" + d.ToString();
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
            else if (PropTypeMap[cboProperty.Text].Item1 == "yesno")
            {
                bool b;
                if (bool.TryParse(txtProperty.Text, out b))
                {
                    // Searching with a yes/no property criteria:
                    propContains += @"
                        AND p.yesno_value=" + b.ToString().ToLower();
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
