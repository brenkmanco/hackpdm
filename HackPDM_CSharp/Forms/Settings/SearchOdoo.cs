using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HackPDM.ClientUtils;

using OdooRpcCs;

namespace HackPDM.Forms.Settings
{
	public partial class SearchOdoo : Form
	{
		readonly Dictionary<string, int> SearchWidths = new()
		{
			{HackFileManager.NameConfig["SearchID"], 10},
			{HackFileManager.NameConfig["SearchName"], 25},
			{HackFileManager.NameConfig["SearchDirectory"], 0},
		};
		// will do percentages instead of px length
		// 0 means it'll use the left over percentage
		readonly Dictionary<string, int> SearchPropWidths = new()
		{
			{HackFileManager.NameConfig["SearchPropName"], 30},
			{HackFileManager.NameConfig["SearchPropEqual"], 15},
			{HackFileManager.NameConfig["SearchPropValue"], 0},
		};

		private HackFileManager hackman;
		private TreeView OdooDirectoryTree;
		private ListView OdooEntryList;

		public SearchOdoo()
		{
			InitializeComponent();
		}
		public SearchOdoo(in HackFileManager hackman) : this()
		{
			this.hackman = hackman;
			this.OdooDirectoryTree = hackman.GetOdooDirectoryTree();
			this.OdooEntryList = hackman.GetOdooEntryList();

			SetPropertyDropdown();
			SetPropertyEqualDropdown();
		}

		private void SetPropertyDropdown()
		{
			foreach(var values in OdooDefaults.IDToProp)
			{
				OdooSearchProperty.Items.Add(
					new LItem()
					{
						Name = values.Value.name,
						ID = values.Key,
						IsTextOrDate = values.Value.prop_type == "text",
					}
				);
			}
		}
		private void SetPropertyEqualDropdown()
		{
			foreach(Operators value in Enum.GetValues(typeof(Operators)))
			{
				string op = value.OperatorConversion();
				OdooSearchPropEqual.Items.Add( op );
				OdooSearchComparer.Items.Add( op );
			}
			OdooSearchPropEqual.SelectedItem = "=";
			OdooSearchComparer.SelectedItem = "ilike";
		}
		private async void worker_Search(object sender, DoWorkEventArgs e)
		{
			string fileName = FileNameTextbox.Text;
			bool odooCheckedOutMe = OdooCheckedMe.Checked;
			bool deletedRemotely = OdooDeletedIsLocal.Checked;
			bool localOnly = OdooLocalOnly.Checked;
			var propItems = OdooSearchPropList.Items;

			if (!int.TryParse(OdooMaxRes.Text, out int maxResults))
			{
				MessageBox.Show("Invalid max results value. Please enter a valid number.");
				return;
			}

			ArrayList execParams = [];
			ArrayList searchDomain = [];
			string comparer = "";
			
			hackman.SafeInvoke(OdooSearchComparer, ()=>
			{
				comparer = (string)OdooSearchComparer.SelectedItem;
			});

			if (fileName.Length > 0)
			{
				searchDomain.Add( new ArrayList { "name", comparer, fileName } );
			}

			ArrayList fields = ["id", "name", "directory_complete_name"];

			if (odooCheckedOutMe && !localOnly)
			{
				searchDomain.Add(new ArrayList {"checkout_user", "=", OdooDefaults.OdooID});	
			}

			if (deletedRemotely && !localOnly)
			{
				searchDomain.Add(new ArrayList {"deleted", "=", true});
			}


			ArrayList results;
			if (OdooSearchPropList.Enabled && OdooSearchPropList.Items?.Count > 0)
			{
				ArrayList[] arrs = await CompilePropertyParams();
				ConcurrentSet<int> candidates = FilterCandidates(arrs);

				searchDomain.Add(new ArrayList {"id", "in", candidates.ToArrayList()});
			}
			
			execParams =
            [
                searchDomain,
				fields,				
			];

			results = await OdooClient.BrowseAsync(HpEntry.GetHpModel(), execParams, 10000);
			

			if (localOnly)
			{
				DisplayLocal( results, fileName, maxResults, false );
			}
			else if (deletedRemotely)
			{
				DisplayLocal( results, fileName, maxResults, true );
			}
			else
			{
				if (results.Count < 1 )
				{
					MessageBox.Show( "No results found." );
					return;
				}

				DisplaySearch(results, maxResults);
			}
			hackman.SafeInvoke(OdooSearchResults, ()=>
			{
				OdooSearchResults.ListViewItemSorter = new ListNameCompare();
				OdooSearchResults.Sort();
			} );
			MessageBox.Show("Finished!");
		}
		private void DisplayLocal( ArrayList results, string filename, int limit=100, bool isNotOnlyLocal=false)
		{
			const string Empty = "-";
			HackFileManager.InitListViewPercentage( OdooSearchResults, SearchWidths );
			DirectoryInfo directoryInfo = new DirectoryInfo(HackDefaults.PWAPathAbsolute);
			FileInfo[] files = directoryInfo.EnumerateFiles($"*{filename}*", SearchOption.AllDirectories).ToArray();
			ListViewItem item;

			Dictionary<string, List<string>> hts = GetNamePathwaysDict(results);
			StringComparer compare = StringComparer.OrdinalIgnoreCase;
			int counter = 0;

			foreach (var file in files)
			{
				if ( counter >= limit )
					break;

				string odooPath = HpDirectory.WindowsToOdooPath(file.DirectoryName.Substring(HackDefaults.PWAPathAbsolute.Length - HackDefaults.PWAPathRelative.Length));

				if (isNotOnlyLocal ^ !(hts.TryGetValue( file.Name.ToLower(), out List<string> paths) && paths.Contains(odooPath)))
				{
					counter++;
					item = HackFileManager.EmptyListItem( OdooSearchResults );
					item.SubItems [ HackFileManager.NameConfig [ "SearchID" ] ].Text     = Empty;
					item.SubItems [ HackFileManager.NameConfig [ "SearchName" ] ].Text   = file.Name;
					item.SubItems [ HackFileManager.NameConfig [ "SearchDirectory" ] ].Text = file.DirectoryName;

					hackman.SafeInvoke(OdooSearchResults, ()=> OdooSearchResults.Items.Add( item ));
				}
			}
		}
		private void DisplaySearch(ArrayList list, int limit)
		{
			HackFileManager.InitListViewPercentage(OdooSearchResults, SearchWidths);
			ListViewItem item;

			Hashtable ht;
			int min = Math.Min( list.Count, limit );
			for ( int i = 0; i < min; i++ )
			{
				item = HackFileManager.EmptyListItem( OdooSearchResults );
				ht = (Hashtable)list [ i ];
				item.SubItems [ HackFileManager.NameConfig [ "SearchID" ] ].Text     = ht [ "id" ].ToString();
				item.SubItems [ HackFileManager.NameConfig [ "SearchName" ] ].Text   = ht [ "name" ].ToString();
				item.SubItems [ HackFileManager.NameConfig [ "SearchDirectory" ] ].Text = ht [ "directory_complete_name" ].ToString();

				hackman.SafeInvoke(OdooSearchResults, ()=>
				{
					OdooSearchResults.Items.Add(item);
				} );
			}
		}
		private Dictionary<string, List<string>> GetNamePathwaysDict(ArrayList result)
		{
			var dict = new Dictionary<string, List<string>>();
			foreach ( Hashtable ht in result )
			{
				string name = ((string)ht["name"]).ToLower();
				string path = (string)ht[ "directory_complete_name" ];
				if (dict.TryGetValue(name, out List<string> list))
				{
					list.Add(path);
				}
				else
				{
					dict.Add(name, [path]);
				}
			}
			return dict;
		}
		private async Task<ArrayList[]> CompilePropertyParams()
		{
			List<Task<ArrayList>> tasks = [];
			if (OdooSearchPropList.Items?.Count > 0)
			{
				ArrayList fields = ["entry_id"]; 
				for (int i = 0; i < OdooSearchPropList.Items.Count; i++)
				{
					Task<ArrayList> newTask = Task.Run( async ()=>
					{
						ArrayList arr1 = [];
						ListViewItem item = null;
					
						hackman.SafeInvoke(OdooSearchPropList, () =>
						{
							item = OdooSearchPropList.Items[i];
						});
						ListViewItem.ListViewSubItem subItem = item.SubItems[HackFileManager.NameConfig [ "SearchPropName" ]];
						LItem lItem = subItem.Tag as LItem;

						arr1.Add(new ArrayList{ "prop_id", "=", lItem.ID });
						arr1.Add(new ArrayList{ 
								"text_value", 
								item.SubItems[HackFileManager.NameConfig [ "SearchPropEqual" ]].Text, 
								item.SubItems[HackFileManager.NameConfig [ "SearchPropValue" ] ].Text});
						return await OdooClient.BrowseAsync(HpVersionProperty.GetHpModel(), [arr1, fields], 10000);
					});
					await newTask;
					tasks.Add(newTask);
				}
			}
			if (tasks.Count > 0)
			{
				return await Task.WhenAll(tasks);
			}
			return null;
		}
		private ConcurrentSet<int> FilterCandidates(ArrayList[] lists)
		{
			ConcurrentSet<int> candidates = [];
			
			for (int i = 0; i < lists.Length; i++) 
			{
				IEnumerable<int> version_ids = lists[i].Select<Hashtable, int>(item =>
				{
					return (int)(((ArrayList)item["entry_id"])[0]);
				} );

				if (i == 0) 
				{
					candidates = version_ids.ToConcurrentSet();
				}
				else 
				{
					candidates.IntersectWith(version_ids);
				}
			}
			return candidates;
		}

		#region Form Events
		private void OdooCancel_Click( object sender, EventArgs e )
		{
			this.Close();
		}
		private void OdooReset_Click( object sender, EventArgs e )
		{
			FileNameTextbox.Text = "";
			OdooSearchProperty.SelectedItem = null;
			OdooSearchPropValue.Text = "";
			OdooCheckedMe.Checked = false;
			OdooDeletedIsLocal.Checked = false;
			OdooLocalOnly.Checked = false;

			OdooMaxRes.Text = "100";
			OdooSearchResults.Clear();
			OdooSearchPropList.Clear();
			OdooSearchComparer.SelectedItem = null;
		}
		private void OdooSearch_Click( object sender, EventArgs e )
		{
			BackgroundWorker worker = new()
            {
                WorkerSupportsCancellation = true
            };
            worker.DoWork += new DoWorkEventHandler(worker_Search);
            worker.RunWorkerAsync();

            bool blnWorkCanceled = false;
            if (blnWorkCanceled) worker.CancelAsync();
		}

		private void OdooSearchResults_DoubleClick( object sender, EventArgs e )
		{
			if (OdooSearchResults.SelectedItems.Count > 0)
			{
				FindSearchSelection(OdooSearchResults.SelectedItems[0]);
			}
		}
		private void OdooLocalOnly_CheckedChanged( object sender, EventArgs e ) => ControlEnabler();
		private void OdooCheckedMe_CheckedChanged( object sender, EventArgs e ) => ControlEnabler();
		private void OdooDeletedIsLocal_CheckedChanged( object sender, EventArgs e ) => ControlEnabler();
		private void OdooPropAdd_Click( object sender, EventArgs e )
		{
			if (OdooSearchProperty.SelectedItem == null || OdooSearchPropEqual.SelectedItem == null)
			{
				MessageBox.Show("Add Property Name or Comparator");
				return;
			}
		
			if (OdooSearchPropList.Columns.Count < 1) 
				HackFileManager.InitListViewPercentage(OdooSearchPropList, SearchPropWidths);

			ListViewItem item = HackFileManager.EmptyListItem( OdooSearchPropList );

			LItem listItem = (LItem)OdooSearchProperty.SelectedItem;
			ListViewItem.ListViewSubItem subItem = item.SubItems[HackFileManager.NameConfig [ "SearchPropName" ] ];
			subItem.Tag = listItem;
			subItem.Text = listItem.Name;

			item.SubItems[HackFileManager.NameConfig [ "SearchPropEqual" ] ].Text = (string)OdooSearchPropEqual.SelectedItem;
			item.SubItems[HackFileManager.NameConfig [ "SearchPropValue" ] ].Text = OdooSearchPropValue.Text;
			
			OdooSearchPropList.Items.Add(item);
		}
		private void OdooPropertyReset_Click( object sender, EventArgs e )
		{
			OdooSearchPropList.Clear();
			//OdooSearchProperty.SelectedItem = null;
			//OdooSearchPropEqual.SelectedItem = null;
			//OdooSearchPropValue.Text = "";
		}
		private void OdooPropDelete_Click( object sender, EventArgs e )
		{
			if (OdooSearchPropList.SelectedItems?.Count > 0)
			{
				foreach (ListViewItem item in OdooSearchPropList.SelectedItems)
				{
					OdooSearchPropList.Items.Remove(item);
				}
			}
		}
		#endregion

		
		private async void FindSearchSelection(ListViewItem item)
		{
			if (item == null) return;

			// first select the treeview node
			// then select the listview item
			string directory = item.SubItems [ HackFileManager.NameConfig [ "SearchDirectory" ] ].Text;
			string fileName = item.SubItems [ HackFileManager.NameConfig [ "SearchName" ] ].Text;

			string[] paths = directory.Split(new[] { " / " }, StringSplitOptions.None);
			
			TreeNodeCollection nodes = null;
			TreeNode node = null;
			try
			{
				for (int i = 0; i < paths.Length; i++)
				{
					if (i == 0) nodes = OdooDirectoryTree.Nodes;
					else nodes = node.Nodes;
					
					bool wasFound = false;
					foreach (TreeNode n in nodes)
					{
						if ( n.Text == paths [ i ] )
						{
							wasFound = true;
							node = n;
							break;
						}
					}
					if (!wasFound) throw new ArgumentException();
				}
				OdooDirectoryTree.CollapseAll();
				hackman.lastSelectedNode = node;
				//hackman.lastSelectedNode.Expand();
				hackman.lastSelectedNode.EnsureVisible();
				OdooDirectoryTree.Select();
				
				while (!hackman.IsListLoaded)
				{
					await Task.Delay(100);
				}
				ListViewItem listItem = null;
				string index = HackFileManager.NameConfig [ "SearchName" ];
				foreach (ListViewItem lv in OdooEntryList.Items)
				{
					if (lv.SubItems[ index ].Text == fileName )
					{
						listItem = lv;
						break;
					}
				}
				if (listItem == null) throw new ArgumentException();
				
				listItem.Selected = true;
				listItem.Focused = true;
				OdooEntryList.FocusedItem = listItem;
				OdooEntryList.EnsureVisible(listItem.Index);
			}
			catch 
			{
				return;
			}
		}

		
		private void ControlEnabler()
		{
			if ( OdooLocalOnly.Checked )
			{
				OdooCheckedMe.Checked = false;
				OdooCheckedMe.Enabled = false;

				OdooDeletedIsLocal.Checked = false;
				OdooDeletedIsLocal.Enabled = false;
				OdooSearchPropList.Enabled = false;

				OdooSearchComparer.SelectedItem = OdooSearchComparer.Items[0];
				OdooSearchComparer.Enabled = false;

				OdooSearchProperty.Enabled = false;
				OdooSearchPropEqual.Enabled = false;
				OdooSearchPropValue.Enabled = false;
				OdooPropAdd.Enabled = false;
				OdooPropDelete.Enabled = false;
				OdooPropertyReset.Enabled = false;
			}
			else
			{
				OdooCheckedMe.Enabled = true;
				OdooDeletedIsLocal.Enabled = true;
				OdooSearchPropList.Enabled = true;
				OdooSearchComparer.Enabled = true;
				OdooSearchProperty.Enabled = true;
				OdooSearchPropEqual.Enabled = true;
				OdooSearchPropValue.Enabled = true;
				OdooPropAdd.Enabled = true;
				OdooPropDelete.Enabled = true;
				OdooPropertyReset.Enabled = true;
			}

			if ( OdooCheckedMe.Checked ||  OdooDeletedIsLocal.Checked ) 
			{
				OdooLocalOnly.Checked = false;
				OdooLocalOnly.Enabled = false;
			}
			else
			{
				OdooLocalOnly.Enabled = true;
			}
		}

		private async void CheckOutMenuItem_Click( object sender, EventArgs e )
		{
			if (OdooLocalOnly.Checked)
			{
				MessageBox.Show("Can't checkout local only entries");
				return;
			}
			await CheckOutItems(OdooSearchResults.SelectedItems);
		}
		private async void unCheckoutToolStripMenuItem_Click( object sender, EventArgs e )
		{
			if (OdooLocalOnly.Checked)
			{
				MessageBox.Show("Can't uncheckout local only entries");
				return;
			}
			await CheckOutItems(OdooSearchResults.SelectedItems, false);
		}
		private async Task CheckOutItems(IEnumerable items, bool willCheckout = true)
		{
			ArrayList ids = [];
			foreach (ListViewItem item in items)
			{
				ids.Add(int.Parse(item.SubItems[HackFileManager.NameConfig [ "SearchID" ]].Text));
			}
			HpEntry[] entries = HpEntry.GetRecordsByIDS(ids);
			foreach (HpEntry entry in entries)
			{
				if (willCheckout && entry.CanCheckOut())
				{
					await entry.CheckOut();
				}
				if (!willCheckout && entry.CanUnCheckOut())
				{
					await entry.UnCheckOut();
				}
			}
		}

		private void openToolStripMenuItem_Click( object sender, EventArgs e )
		{
			if (OdooLocalOnly.Checked)
			{
				foreach (ListViewItem item in OdooSearchResults.SelectedItems)
				{
					string path = item.SubItems[HackFileManager.NameConfig [ "SearchName" ]].Text;
					OpenLocalFile(path);
				}
			}
			else
			{
				foreach (ListViewItem item in OdooSearchResults.SelectedItems)
				{
					int id = int.Parse(item.SubItems[HackFileManager.NameConfig [ "SearchID" ]].Text);
					DownloadRemoteFile(id);
				}
			}
			
		}

		private void checkoutOpenToolStripMenuItem_Click( object sender, EventArgs e )
		{
			CheckOutMenuItem_Click(sender, e);
			openToolStripMenuItem_Click(sender, e);
		}

		private void OpenLocalFile( string path )
		{
			FileOperations.OpenFile( path );
		}
		private void DownloadRemoteFile( int entryID )
		{
			const string latest_version = "latest_version_id";
			HpVersion version = HpEntry.GetRelatedRecordByIDS<HpVersion>([entryID], latest_version, excludedFields: ["preview_image"]).First();

			if ( version == null )
				return;

			// download version data and place into temporary folder
			version.DownloadFile();
			FileOperations.OpenFile( Path.Combine(version.winPathway, version.name));
		}
		private void PreviewRemoteFile( int entryID )
		{
			const string latest_version = "latest_version_id";
			HpVersion version = HpEntry.GetRelatedRecordByIDS<HpVersion>([entryID], latest_version, excludedFields: ["preview_image"]).First();

			if ( version == null )
				return;

			// download version data and place into temporary folder
			version.DownloadFile(Properties.UserSettings.Default.TemporaryPath);
			FileOperations.OpenFile( Path.Combine( version.winPathway, version.name ) );
		}
	}


	public class ListNameCompare : IComparer, IComparer<ListViewItem>
	{
		public int Compare( object x, object y ) 
			=> Compare((ListViewItem)x, (ListViewItem)y);

		public int Compare( ListViewItem x, ListViewItem y ) 
		{
			var xText = x.SubItems [ HackFileManager.NameConfig [ "SearchName" ] ].Text;
			var yText = y.SubItems [ HackFileManager.NameConfig [ "SearchName" ] ].Text;
			return String.CompareOrdinal(xText, yText);
		}
	}
	public class LItem
	{
		public int ID { get; set; }
		public string Name { get; set; }
		public bool IsTextOrDate { get; set; }

		public override string ToString() => Name;
	}
	public class LEqualItem
	{
		public Operators Operators { get; set; }
		public override string ToString() => Operators.OperatorConversion();
	}
}
