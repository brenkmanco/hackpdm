using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Entity.Core.Common.CommandTrees.ExpressionBuilder;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using HackPDM.ClientUtils;
using HackPDM.Forms.Odoo;
using HackPDM.Forms.Settings;

using OClient = OdooRpcCs.OdooClient;

namespace HackPDM
{
	public partial class HackFileManager : Form
    {
		#region Declarations
		//internal static readonly DirectoryInfo directoryInfo = new(Properties.UserSettings.Default.ProjectDirectory);
		//internal static readonly string StatusIconPath = $"{directoryInfo.FullName}\\Icons\\StatusIcons";
		//internal static readonly string ExtensionIconPath = $"{directoryInfo.FullName}\\Icons\\ExtensionIcons";
		//internal static readonly string ResourcesPath = $"{directoryInfo.FullName}\\Resources";
		private const string EmptyPlaceholder = "-";
		// List Views Column Name and Widths
		internal static readonly Dictionary<string, string> NameConfig = new Dictionary<string, string>()
		{
			{"RowID", "ID"},
			{"RowName", "Name"},
			{"RowType", "Type"},
			{"RowSize", $"Size ({HackDefaults.MeasureFileSize})"},
			{"RowStatus", "Status"},
			{"RowCheckOut", "CheckOut"},
			{"RowCategory", "Category"},
			{"RowLocalDate", "Local Date"},
			{"RowRemoteDate", "Remote Date"},
			{"RowFullName", "FullName"},
			{"HistoryVersion", "Version"},
			{"HistoryModUser", "ModUser"},
			{"HistoryModDate", "ModDate"},
			{"HistorySize", "Size"},
			{"HistoryRelDate", "RelDate"},
			{"ParentVersion", "Version"},
			{"ParentName", "Name"},
			{"ParentBasePath", "Base Path"},
			{"ChildrenVersion", "Version"},
			{"ChildrenName", "Name"},
			{"ChildrenBasePath", "Base Path"},
			{"PropertiesVersion", "Version"},
			{"PropertiesConfiguration", "Configuration"},
			{"PropertiesName", "Name"},
			{"PropertiesProperty", "Property"},
			{"PropertiesType", "Type"},
			{"PropertiesValue", "Value"},
			{"VersionID", "ID"},
			{"VersionName", "Name"},
			{"VersionFileSize", "File Size"},
			{"VersionDirectoryID", "Directory ID"},
			{"VersionNodeID", "Node ID"},
			{"VersionEntryID", "Entry ID"},
			{"VersionAttachmentID", "Attachment ID"},
			{"VersionModifyDate", "Modify Date"},
			{"VersionChecksum", "Checksum"},
			{"VersionOdooCompletePath", "Odoo Complete Path"},
			{"SearchID", "ID"},
			{"SearchName", "Name"},
			{"SearchDirectory", "Directory"},
			{"SearchPropName", "Name"},
			{"SearchPropEqual", "Comparer"},
			{"SearchPropValue", "Value"},
			{"FileTypeExtension", "Extension"},
			{"FileTypeCategory", "Category"},
			{"FileTypeRegEx", "RegEx"},
			{"FileTypeDescription", "Description"},
			{"FileTypeEntryFilterID", "ID"},
			{"FileTypeEntryFilterProto", "Proto"},
			{"FileTypeEntryFilterRegEx", "RegEx"},
			{"FileTypeEntryFilterDescription", "Description"},
			{"FileTypeLocExt", "Extension"},
			{"FileTypeLocStatus", "Status"},
			{"FileTypeLocExample", "Example"},
			{"FileTypeLocDatExt", "Extension"},
			{"FileTypeLocDatReg", "RegEx"},
			{"FileTypeLocDatCat", "Category"},
			{"FileTypeLocDatDes", "Description"},
			{"FileTypeLocDatIco", "Icon"},
			{"FileTypeLocDatIcoCancel", "Remove Icon?"},
		};
		readonly Dictionary<string, int> RowWidths = new()
		{
			{NameConfig["RowID"], 75},
			{NameConfig["RowName"], 300},
			{NameConfig["RowType"], 120},
			{NameConfig["RowSize"], 100},
			{NameConfig["RowStatus"], 75},
			{NameConfig["RowCheckOut"], 120},
			{NameConfig["RowCategory"], 110},
			{NameConfig["RowLocalDate"], 150},
			{NameConfig["RowRemoteDate"], 150},
			{NameConfig["RowFullName"], 100}
		};
		readonly Dictionary<string, int> HistoryRows = new()
		{
			{NameConfig["HistoryVersion"], 50},
			{NameConfig["HistoryModUser"], 140},
			{NameConfig["HistoryModDate"], 140},
			{NameConfig["HistorySize"], 75},
			{NameConfig["HistoryRelDate"], 75},
		};
		readonly Dictionary<string, int> ParentRows = new()
		{
			{NameConfig["ParentVersion"], 50},
			{NameConfig["ParentName"], 400},
			{NameConfig["ParentBasePath"], 600},
		};
		readonly Dictionary<string, int> ChildrenRows = new()
		{
			{NameConfig["ChildrenVersion"], 50},
			{NameConfig["ChildrenName"], 400},
			{NameConfig["ChildrenBasePath"], 600},
		};
		readonly Dictionary<string, int> PropertiesRows = new()
		{
			{NameConfig["PropertiesVersion"], 50},
			{NameConfig["PropertiesConfiguration"], 100},
			{NameConfig["PropertiesName"], 100},
			{NameConfig["PropertiesProperty"], 50},
			{NameConfig["PropertiesType"], 75},
			{NameConfig["PropertiesValue"], 400},
		};
		readonly Dictionary<string, int> VersionInfoRows = new()
		{
			{NameConfig["VersionID"], 75},
			{NameConfig["VersionName"], 300},
			{NameConfig["VersionFileSize"], 100},
			{NameConfig["VersionDirectoryID"], 75},
			{NameConfig["VersionNodeID"], 75},
			{NameConfig["VersionEntryID"], 75},
			{NameConfig["VersionAttachmentID"], 75},
			{NameConfig["VersionModifyDate"], 120},
			{NameConfig["VersionChecksum"], 300},
			{NameConfig["VersionOdooCompletePath"], 300},
		};
        HpDirectory root;
        public static StatusDialog Dialog { get; set; }
        
        private delegate void BackgroundMethodDel(object sender, DoWorkEventArgs e);
        private delegate void BackgroundCompleteDel(object sender, RunWorkerCompletedEventArgs e);
        private static BackgroundWorker backgroundWorker = new()
		{
			WorkerSupportsCancellation = true
		};
        private static CancellationTokenSource cSource;
		private static Image previewImage = null;


        // Download Static Variables
        // set status lines in a queue for StatusDialog.AddStatusLines method
        private static ConcurrentQueue<string[]> queueAsyncStatus = new();

		public static int DownloadBatchSize 
        { 
            get
            {
                if (field == 0)
                {
                    field = Properties.UserSettings.Default.DownloadBatchSize;
                }
                return field;
            }
            set
            {
                field = value;
                Properties.UserSettings.Default.DownloadBatchSize = value;
                Properties.UserSettings.Default.Save();
            }
        }
        private static int skipCounter = 0;
		public static int SkipCounter => skipCounter;
		private static int processCounter = 0;
        private static int maxCount = 0;
		internal bool IsTreeLoaded{ get; set; } = false;
		internal bool IsListLoaded{ get; set; } = false;
        private bool isClosing = false;

		private static bool IsActive { get; set; } = false;
		public TreeNode lastSelectedNode { get; set; } = null;
		public string lastSelectedNodePath {get; set;} = null;
		#endregion

		#region TEST_VARIABLES
		#if DEBUG
			Stopwatch stopwatch;
		#endif
		#endregion

		#region Initializers
		static HackFileManager() {}
        public HackFileManager()
		{
			while (OdooDefaults.OdooID == 0)
			{
				List<string> errors = new();
				if (!OClient.CorrectOdooAddress())
				{
					errors.Add("invalid odoo address or unreachable host");
				} 
				else if (!OClient.CorrectOdooPort())
				{
					errors.Add("invalid odoo port or server is down");
				}
				else
				{
					errors.Add("invalid odoo credentials");
				}
				var pm = new ProfileManager(errors);
				var result = pm.ShowDialog();
				if (result is DialogResult.None or DialogResult.Cancel or DialogResult.Abort or DialogResult.No) 
				{
					return;
				}
			}
            InitializeComponent();
			previewImage = OdooEntryImage.Image;
			OdooDirectoryTree.LostFocus += TreeView_LostFocus;
			ResetListViews();

			this.WindowState = FormWindowState.Maximized;
            this.FormClosing += (s, e) => isClosing = true;
			
			this.Load += new EventHandler(FormLoaded);
			this.root = HpBaseModel<HpDirectory>.GetRecordByID(1);
        }
		
		#if SERVER
		private HackFileManager(bool withShell = false)
		{
			this.root = HpBaseModel<HpDirectory>.GetRecordByID(1);
		}
		public static HackFileManager HackServerInitializer(bool withShell = false)
			=> new HackFileManager(withShell);
		#endif



		private void FormLoaded(object sender, EventArgs e)
		{
			CreateTreeViewBackground();
			InitBackgroundWorker();
		}
		private void CreateTreeViewBackground()
        {
            BackgroundWorker worker = new()
            {
                WorkerSupportsCancellation = true
            };
            worker.DoWork += new DoWorkEventHandler(LoadOdooDirectoryTree);
            worker.RunWorkerAsync();

            bool blnWorkCanceled = false;
            if (blnWorkCanceled) worker.CancelAsync();
        }
        private void LoadOdooDirectoryTree(object sender, DoWorkEventArgs e)
        {
			IsTreeLoaded = false;

            CreateTreeHash(root);
            CreateLocalTree(OdooDirectoryTree);

			if (lastSelectedNode != null)
			{
				lastSelectedNode = OdooDirectoryTree.FindTreeNode(lastSelectedNodePath);
			}

			SafeInvoke(OdooDirectoryTree, () => 
			{
				OdooDirectoryTree.Sort();
				if (lastSelectedNode is not null) lastSelectedNode.EnsureVisible();
			});
			
			IsTreeLoaded = true;

            SafeInvoke(OdooEntryImage, () => 
			{
				OdooEntryImage.Image = null;
			});
        }
        private void InitBackgroundWorker()
        {
            cSource = new();
			backgroundWorker.DoWork += new DoWorkEventHandler( worker_ListItemChange );
		}
        private Dictionary<string, object> ConvertSubDirectories(Hashtable ht)
        {
            Dictionary<string, object> keyValues = new()
            {
                ["id"] = ht["id"],
                ["name"] = ht["name"],
            };

            
            Hashtable directories = (Hashtable)ht["directories"];

            Dictionary<string, IDictionary> children = [];
            foreach (DictionaryEntry value in directories)
            {
                if (value.Value is Hashtable childDirectory)
                {
                    children.Add((string)value.Key, ConvertSubDirectories(childDirectory));
                }
            }
            keyValues["directories"] = children;
            return keyValues;
        }

        // list controls
        // reset list items and columns
        private void InitListViewInternal(ListView list, Dictionary<string, int> rows)
			=> InitListView( list, rows );
		internal static void InitListView( ListView list, Dictionary<string, int> rows )
		{
            SafeInvokeGen(list, rows, (row) =>
            {
                list.Clear();
                foreach (KeyValuePair<string, int> item in row)
                {
                    list.Columns.Add(item.Key, item.Key, item.Value);
                }
			});
		}
		internal static void InitGridView( DataGridView gridView )
		{
			SafeInvoker(gridView, () =>
            {
				gridView.Columns.Clear();
                gridView.DataSource = null;
			});
		}
		internal static void InitListViewPercentage( ListView list, Dictionary<string, int> rows )
		{
			SafeInvokeGen(list, rows, (row) =>
            {
                list.Clear();
				List<ColumnHeader> offsets = new();
				int unUsedPercentage = 100;
                foreach (KeyValuePair<string, int> item in row)
                {
					if (item.Value == 0)
					{
						offsets.Add(list.Columns.Add(item.Key, item.Key));
					}
					else
					{
						int use = (int)( list.Size.Width * (item.Value / 100f) );
						list.Columns.Add(item.Key, item.Key, use );
						unUsedPercentage -= item.Value;
					}
                }
				int totalItems = offsets.Count;
				int distNum = unUsedPercentage / totalItems;
				foreach(var column in offsets)
				{
					if (distNum <= unUsedPercentage)
					{
						column.Width = (int)(list.Size.Width * (distNum / 100f));
						unUsedPercentage -= distNum;
					}
					else
					{
						column.Width = (int)(list.Size.Width * (unUsedPercentage / 100f));;
						unUsedPercentage = 0;
					}
				}
			});
		}
		internal ListViewItem EmptyListItemInternal(ListView list)
			=> EmptyListItem(list);
		internal static ListViewItem EmptyListItem(ListView list)
		{
            ListViewItem item = new();
            item.SubItems.Clear();
            SafeInvoker( list, () =>
			{
				for (int i = 0; i < list.Columns.Count; i++)
                {
                    if (item.SubItems[0].Name != "") item.SubItems.Add(string.Empty);
					item.SubItems [ item.SubItems.Count - 1 ].Name = list.Columns [ i ].Text;
				}
			} );
			return item;
		}
		internal static DataTable EmptyGridTable(DataGridView grid, Dictionary<string, DataColumnSettings> rows)
		{
			DataTable table = new();
            
            SafeInvokeGen(grid, rows, (row) =>
            {
				foreach (var r in row)
				{
					table.Columns.Add(r.Value.NewInstance());
				}
			} );
			return table;
		}

		private void ResetSubListViews()
		{
			InitListViewInternal( OdooHistory, HistoryRows );
			InitListViewInternal( OdooParents, ParentRows );
			InitListViewInternal( OdooChildren, ChildrenRows );
			InitListViewInternal( OdooProperties, PropertiesRows );
			InitListViewInternal( OdooVersionInfoList, VersionInfoRows );
		}

		private void ResetListViews()
		{
			InitListViewInternal( OdooEntryList, RowWidths );
			InitListViewInternal( OdooHistory, HistoryRows );
			InitListViewInternal( OdooParents, ParentRows );
			InitListViewInternal( OdooChildren, ChildrenRows );
			InitListViewInternal( OdooProperties, PropertiesRows );
			InitListViewInternal( OdooVersionInfoList, VersionInfoRows );
		}
		private void ClearEntryLists()
        {
            //OdooHistory.Clear();
			OdooHistory.Items?.Clear();
            //OdooParents.Clear();
			OdooParents.Items?.Clear();
            //OdooChildren.Clear();
			OdooChildren.Items?.Clear();
            //OdooProperties.Clear();
			OdooProperties.Items?.Clear();
        }
        public void RefreshEntries()
        {
            SafeInvoke(OdooEntryList, () => OdooEntryList.Refresh());
        }

		internal TreeView GetOdooDirectoryTree()
			=> OdooDirectoryTree;
		internal ListView GetOdooEntryList()
			=> OdooEntryList;
		#endregion

		#region TreeView functions
		// tree view directories
		private async Task TreeSelectItem( TreeNode node )
		{
			IsListLoaded = false;
			await Task.Run(()=>
			{
				while (!IsTreeLoaded)
				{
					Task.Delay(100);
				}
			});
			node.EnsureVisible();
			InitListViewInternal( OdooEntryList, RowWidths );

			if ( node?.Tag is int directoryID )
			{
				if ( directoryID == 0 )
				{
					// add file entries to folder
					AddLocalEntries( node );
					return;
				}
				Hashtable entries = HpDirectory.GetEntries(directoryID, IsActive);

				Dictionary<string, Task<HackFile>> hackFileMap = await GetFileMap(entries);
				AddRemoteEntries( entries, hackFileMap );
				AddLocalEntries( lastSelectedNode, hackFileMap );
				SafeInvoke(OdooEntryList, () => OdooEntryList.Sort());
			}
			IsListLoaded = true;
		}
		private void CreateTreeHash( HpDirectory directory )
		{
			AddDirectoriesToTree( OdooDirectoryTree, directory.GetSubdirectories( false ) );
		}
		private void CreateLocalTree( in TreeView treeView )
		{
			Dictionary<string, TreeNode> treeDict = Utils.ConvertTreeToDictionary(treeView);
			IEnumerable<string> pathways = Directory.EnumerateDirectories(HackDefaults.PWAPathAbsolute, "*", SearchOption.AllDirectories);
			pathways = Utils.FastSlice( pathways, HackDefaults.PWAPathAbsolute.Length, prependText: "root" );

			foreach ( string pathway in pathways )
			{
				string[] paths = pathway.Split('\\');
				(int, TreeNode) validIndexNode = Utils.LastValidTreeIndex(in pathway, in paths, treeDict);
				// the last valid index does not go to the end meaning it didn't find the
				// remaining paths
				if ( validIndexNode.Item1 != paths.Length - 1 )
				{
					AddLocalDirectories( validIndexNode.Item2, paths.AsSpan( validIndexNode.Item1 + 1 ), treeDict );
				}
			}
		}
		private void AddLocalDirectories( TreeNode node, Span<string> pathway, Dictionary<string, TreeNode> treeDict )
		{
			string[] paths = pathway.ToArray();
			SafeInvoke( OdooDirectoryTree, () =>
			{
				for ( int i = 0; i < paths.Length; i++ )
				{
					node = node.Nodes.Add( paths [ i ] );
					node.ImageIndex = 1;
					node.SelectedImageIndex = 1;
					node.Tag = 0;

					treeDict.Add( node.FullPath, node );
				}
			} );

		}
		private void AddDirectoriesToTree( TreeView tree, Hashtable entries )
		{
			SafeInvoke( tree, () => 
			{
				tree.Nodes.Clear();	
				RecurseAddNodesAsync( null, entries, 0 ).Wait();
			});
		}
		private async Task RecurseAddNodesAsync( TreeNode treeNode, Hashtable node, int depth )
		{
			// add container node (directory name)
			TreeNode treeNodeName = new((string)node["name"]);

			// if treeNode == null then it will be the root node
			if ( treeNode == null )
			{
				treeNode = treeNodeName;
				SafeInvoke( OdooDirectoryTree, () => 
				{
					OdooDirectoryTree.Nodes.Add( treeNode );
				});
			}
			else
			{
				treeNode.Nodes.Add( treeNodeName );
			}

			string path = HackDefaults.DefaultPath(treeNodeName.FullPath, true);
			if ( Directory.Exists( path ) )
			{
				treeNodeName.ImageIndex = 0;
				treeNodeName.SelectedImageIndex = 0;
			}
			else
			{
				treeNodeName.ImageIndex = 2;
				treeNodeName.SelectedImageIndex = 2;
			}

			treeNodeName.Tag = (int)node [ "id" ];

			// refresh to show active changes

			// add children
			foreach ( DictionaryEntry pair in (Hashtable)node [ "directories" ] )
			{
				if ( pair.Value is Hashtable childDirectory )
				{
					await RecurseAddNodesAsync( treeNodeName, childDirectory, depth + 1 );
				}
			}
		}
        public void RefreshTree()
        {
            SafeInvoke(OdooDirectoryTree, () => OdooDirectoryTree.Refresh());
        }
		public void RestartTree() => CreateTreeViewBackground();
		public async void RestartEntries() => SafeInvoke(OdooDirectoryTree, async () => await TreeSelectItem(lastSelectedNode));
		#endregion

		#region Tree Item Selection
        private void AddRemoteEntries(Hashtable entries, Dictionary<string, Task<HackFile>> hackFileMap)
        {
			#if DEBUG
			stopwatch = Stopwatch.StartNew();
			#endif
            foreach (DictionaryEntry pair in entries)
            {
                Hashtable table = (Hashtable)pair.Value;
                ListViewItem item = EmptyListItemInternal(OdooEntryList);
                item.SubItems[ NameConfig["RowID"] ].Text                   = ((int)table["id"]).ToString();

                //item.SubItems.Add(((int)table["id"]).ToString());
                item.SubItems [ NameConfig [ "RowName" ] ].Text             = pair.Key.ToString();

				string type = (string)table["type"];
				item.SubItems [ NameConfig [ "RowType" ] ].Text             = type;

				double size = (double)( Convert.ToDouble(table["size"]) * HackDefaults.ByteSizeMultiplier );
				item.SubItems [ NameConfig [ "RowSize" ] ].Text				= size.ToString("0.00");
				

				string checkout = (string)table["checkout"];
                checkout = checkout == "False:False" ? EmptyPlaceholder : checkout;
                item.SubItems [ NameConfig [ "RowCheckOut" ] ].Text         = checkout;

                // check if latest checksum
                string status = "";
                string fullName = (string)table["fullname"];
                HackFile hack = hackFileMap[fullName].Result;

				item.SubItems [ NameConfig [ "RowRemoteDate" ] ].Text = DateTime.TryParse((string)table["latest_date"], out DateTime remoteDate) && remoteDate != default ? remoteDate.ToShortDateString() : EmptyPlaceholder;
				item.SubItems [ NameConfig [ "RowLocalDate" ] ].Text = hack.ModifiedDate.Year != 1 ? hack.ModifiedDate.ToShortDateString() : EmptyPlaceholder;

				// remote only
				// local only
				// new remote version
				// checked out to me
				// checked out to other
				// ignore filter
				// no remote file type
				// local modification
				// deleted
				// destroyed

				if (table["deleted"] is bool deleted && !deleted)
				{
					if (checkout != EmptyPlaceholder)
					{
						// cm = checked out to me
						// co = checked out to other
						status = checkout == $"{OdooDefaults.OdooUser}:{OdooDefaults.OdooID}" ? "cm" : "co";
					}
					else
					{
						switch(table["latest_checksum"])
						{
							case bool:
							{
								status = "lo";
								break;
							}
							case string latestChecksum:
							{
								if (hack.SHA1Checksum == null) status = "ro";
								else if (hack.SHA1Checksum == latestChecksum) status = "ok";
								else
								{
									// either the local version is newer or the remote version is newer
									// because the checksums don't match
									if (remoteDate > hack.ModifiedDate)
									{
										status = "nv";
									}
									else
									{
										status = "lm";
									}
								}
								break;
							}
							default: status = "lo"; break;
						}
					}
				}
				else
				{
					status = "dt";
				}

				
				
				// get or add image key

				string strKey;
				if (status != "ok")	strKey = $"{type}.{status}";
				else strKey = type;
				
				if ( ilListIcons.Images [ strKey ] == null)
				{
					// image key not present in ilListIcons
					Image imgExt = ilListIcons.Images[type];
					if (imgExt == null)
					{
						if (OdooDefaults.ExtToType.TryGetValue( $".{type}", out var hpType ))
						{
							// get remote image
							byte[] imgBytes = FileOperations.ConvertFromBase64(hpType.icon);
							MemoryStream ms = new();
							ms.Write( imgBytes, 0, imgBytes.Length );
							imgExt = Image.FromStream( ms );
						}

						if (imgExt == null)
						{
							imgExt = ilListIcons.Images [ "default" ];
						}
						else
						{
							ilListIcons.Images.Add(type, imgExt);
						}
					}

					// get status image

					if (status == "ok")
					{
						if (imgExt is null)	strKey = "default";
					}
					else
					{
						Image imgStatus = ilListIcons.Images[status];

						// combine images
						if (imgExt is not null && imgStatus is not null)
						{
							ilListIcons.Images.Add(strKey, ImageUtils.ImageOverlay( imgExt, imgStatus ));
						}
						else
						{
							strKey = "default";
						}
					}
				}

				item.ImageKey = strKey;


				item.SubItems[ NameConfig [ "RowStatus" ] ].Text            = status;
                item.SubItems [ NameConfig [ "RowCategory" ] ].Text         = (string)table["category"];
                item.SubItems [ NameConfig [ "RowFullName" ] ].Text         = fullName;

				SafeInvoke(OdooEntryList, ()=> OdooEntryList.Items.Add(item));
            }
			#if DEBUG
			stopwatch.Stop();
			Console.WriteLine($"remote entries time: {stopwatch.Elapsed}");
			#endif
        }
        private void AddLocalEntries(TreeNode node, Dictionary<string, Task<HackFile>> hackFileMap = null)
        {
			#if DEBUG
			stopwatch = Stopwatch.StartNew();
			#endif

			string path = HackDefaults.DefaultPath(node.FullPath, true);
            if (!Directory.Exists(path)) return;

            HackFile[] files;

            bool hasEntries = hackFileMap != null;
            if (hasEntries) files = FileOperations.FilesInDirectory(path, hackFileMap); //, out Dictionary<string, Hashtable> conflictPaths);
            else files = FileOperations.FilesInDirectory(path);

            foreach (HackFile file in files)
            {
				string type = file.TypeExt.ToLower();
				if (!OdooDefaults.ExtToType.TryGetValue(type, out var hpType)) continue;
				type = type.Substring(1);

                ListViewItem item = EmptyListItemInternal(OdooEntryList);
				item.SubItems[ NameConfig["RowID"] ].Text = EmptyPlaceholder;
                item.SubItems[ NameConfig["RowName"] ].Text = file.Name;

				
				string status = "lo";
				item.SubItems[ NameConfig["RowType"] ].Text = type;

                double size =  (double)( file.FileSize * HackDefaults.ByteSizeMultiplier );
				item.SubItems[ NameConfig["RowSize"] ].Text = size.ToString("0.00");

				item.SubItems [ NameConfig [ "RowLocalDate" ] ].Text = file.ModifiedDate.ToShortDateString();
				item.SubItems [ NameConfig [ "RowRemoteDate" ] ].Text = EmptyPlaceholder;
				item.SubItems[ NameConfig["RowStatus"] ].Text = "lo";


				// get or add image key
				string strKey = $"{type}.lo";

				if ( ilListIcons.Images [ strKey ] == null )
				{
					// image key not present in ilListIcons
					Image imgExt = ilListIcons.Images[type];
					if ( imgExt == null )
					{

						// get remote image
						if (hpType.icon != null)
						{
							byte[] imgBytes = FileOperations.ConvertFromBase64(hpType.icon);
							MemoryStream ms = new();
							ms.Write( imgBytes, 0, imgBytes.Length );
							imgExt = Image.FromStream( ms );
						}
						

						//string extPath = Path.Combine(ExtensionIconPath, $"{type}.png");
						//if ( File.Exists( extPath ) )
						//{
						//	imgExt = Image.FromFile( extPath );
						//}
						
						if ( imgExt == null )
						{
							imgExt = ilListIcons.Images [ "default" ];
						}
						else
						{
							ilListIcons.Images.Add( type, imgExt );
						}
					}

					// get status image
					Image imgStatus = ilListIcons.Images[status];
					//if ( imgStatus == null )
					//{
					//	string statusPath = Path.Combine(StatusIconPath, $"{status}.png");
					//	if ( File.Exists( statusPath ) )
					//	{
					//		imgStatus = Image.FromFile( statusPath );
					//		ilListIcons.Images.Add( status, imgStatus );
					//	}
					//}

					// combine images
					if ( imgExt is not null && imgStatus is not null )
					{
						ilListIcons.Images.Add( strKey, ImageUtils.ImageOverlay( imgExt, imgStatus ) );
					}
					else
					{
						strKey = "default";
					}
				}

				item.ImageKey = strKey;


				item.SubItems[ NameConfig["RowCheckOut"] ].Text = EmptyPlaceholder;
                item.SubItems[ NameConfig["RowCategory"] ].Text = OdooDefaults.ExtToCat[$".{type}"].name;
                item.SubItems[ NameConfig["RowFullName"] ].Text = file.FullPath;

                //OdooEntryList.Items.Add(item);
                UpdateListAsync(OdooEntryList, item);
            }
			
			#if DEBUG
			stopwatch.Stop();
			Console.WriteLine($"local entries time: {stopwatch.Elapsed}");
			#endif
        }

		#endregion

		#region List Item Selection
		private async Task ProcessEntrySelectionAsync(ListViewItem item, CancellationToken token)
        {
			HpVersion[] versions = [];
			(ArrayList, ArrayList) versionRels;
			List<HpVersionProperty[]> versionProperties = [];
			(HpVersion[], HpVersion[]) versionsRelation = ([], []);

			bool success = int.TryParse(item.Text, out int ID);

			if ( !success )
				return;


			Task historyAndProperties = Task.Run(() =>
			{
                // get history list
                versions = GetVersionsForEntry(ID, ["preview_image", "file_contents"]);
			})
			.ContinueWith(task1 =>
			{
				if (versions != null && versions.Length > 0)
				{
					versionProperties = HpVersion.GetAllVersionProperties(versions.ToArrayListIDs());
				}
			});


			Task parentAndChild = Task.Run(() =>
			{
				int? entryID = (int?)HpEntry.GetFieldValue(ID, "latest_version_id");
				if (entryID != null)
				{
					versionRels = GetRelFromVersions([entryID]);
					versionsRelation = GetVersionsFromRelationship(versionRels);
				}
			});

			await Task.WhenAll( historyAndProperties, parentAndChild );
            token.ThrowIfCancellationRequested();
			object lockObject = new();
			lock ( lockObject )
			{
				UpdateTabPageText( OdooHistoryPage, $"History ({versions?.Length ?? 0})" );
				UpdateTabPageText( OdooParentsPage, $"Where Used ({versionsRelation.Item1?.Length ?? 0})" );
				UpdateTabPageText( OdooChildrenPage, $"Dependents ({versionsRelation.Item2?.Length ?? 0})" );

				PopulateHistory( in versions );
				PopulateProperties( in versionProperties );
				if ( versionsRelation.Item1 != null && versionsRelation.Item1.Length > 0 )
				{
					// Populating Where Used
					// HpVersion.SortReverseById(versionsRelation.Item1);
					PopulateParent( in versionsRelation.Item1 );
				}
				if ( versionsRelation.Item2 != null && versionsRelation.Item2.Length > 0 )
				{
					// Populating Dependency
				 	// HpVersion.SortReverseById(versionsRelation.Item2);
					PopulateChildren( in versionsRelation.Item2 );
				}
				if ( versions.Length == 1 )
				{
					PopulateVersionInfo( versions [ 0 ] );
				}
				else if ( versions.Length > 1 )
				{
					HpVersion latest = null;
					foreach ( HpVersion version in versions )
					{
						if ( latest == null || version.file_modify_stamp > latest.file_modify_stamp )
						{
							latest = version;
						}
					}
					if ( latest != null )
					{
						PopulateVersionInfo( latest );
						PreviewImage( latest.ID );
					}
				}
			}
		}
        private async void UpdateListAsync(ListView list, ListViewItem item)
        {
            await Task.Yield();
            SafeInvoke(list, () => list.Items.Add(item));
        }
        private HpVersion[] GetVersionsForEntry(int EntryID, string[] excludedFields = null)
        {
            HpVersion[] versions = [];
            ArrayList ids = [EntryID];
            ArrayList al = OClient.Read(HpEntry.GetHpModel(), ids, ["version_ids"], 10000);
            if (al != null && al.Count > 0)
            {
                Hashtable ht = (Hashtable)al[0];
                ArrayList result = (ArrayList)ht["version_ids"];
                if (excludedFields == null) excludedFields = ["preview_image", "file_contents"];
                versions = HpVersion.GetRecordsByIDS(result, excludedFields:excludedFields);
            }
            return versions;
        }
        private (ArrayList, ArrayList) GetRelFromVersions(ArrayList versionIDs, relationType relation = relationType.Both)
        {
            const string parent = "parent_ids", child = "child_ids";

            (ArrayList, ArrayList) versionRel = ([], []);

            //string field = isParent ? "parent_ids" : "child_ids";
            ArrayList fields = [];
            switch (relation)
            {
                case relationType.Parent: fields.Add(parent); break;
                case relationType.Child: fields.Add(child); break;
                default: fields.AddRange(new string[] { parent, child }); break;
            }

            ArrayList al = OClient.Read(HpVersion.GetHpModel(), versionIDs, fields, 10000);
            if (al == null && al.Count < 1) return (null, null);

            ArrayList parent_ids = Utils.GetResults(in al, parent);
            ArrayList child_ids = Utils.GetResults(in al, child);
            
            // get the HpVersionparent
            if (relation == relationType.Parent || relation == relationType.Both)
            {
                ArrayList temp = OClient.Read(HpVersionRelationship.GetHpModel(), parent_ids, ["parent_id"], 10000);

                versionRel.Item1 = Utils.GetResults(in temp, "parent_id", true);
            }
            if (relation == relationType.Child || relation == relationType.Both)
            {
                ArrayList temp = OClient.Read(HpVersionRelationship.GetHpModel(), child_ids, ["child_id"], 10000);
                versionRel.Item2 = Utils.GetResults(in temp, "child_id", true);
            }
            return versionRel;
        }
        private (HpVersion[], HpVersion[]) GetVersionsFromRelationship(in (ArrayList, ArrayList) versionRelationship)
        {
            
            HpVersion[] parents = null, children = null;

            if (versionRelationship.Item1 != null 
                && versionRelationship.Item1.Count > 0)
            {
                parents = HpVersion.GetRecordsByIDS(versionRelationship.Item1, excludedFields: ["preview_image", "node_id", "entry_id", "file_modify_stamp", "checksum", "file_contents"] );
            }
            if (versionRelationship.Item2 != null
                && versionRelationship.Item2.Count > 0)
            {
                children = HpVersion.GetRecordsByIDS(versionRelationship.Item2, excludedFields: ["preview_image", "node_id", "entry_id", "file_modify_stamp", "checksum", "file_contents"]);
            }

            
            
            return (parents, children);
        }
        private static async Task<Dictionary<string, Task<HackFile>>> GetFileMap(Hashtable entries)
        {
            // need to check local files
            List<Task<HackFile>> hackTasks = new(entries.Count);
            Dictionary<string, Task<HackFile>> hackFileMap = new(entries.Count);

            foreach (DictionaryEntry pair in entries)
            {
                Hashtable ht = (Hashtable)pair.Value;
                string filepath = (string)((Hashtable)pair.Value)["fullname"];

                Task<HackFile> hackTask = HackBaseFile
                    .GetHackFileAsync<HackFile>(HpDirectory
                        .ConvertToWindowsPath(filepath, true));

                hackFileMap.Add(filepath, hackTask);
                hackTasks.Add(hackTask);
            }
            await Task.WhenAll(hackTasks);
            //Task.WaitAll(hackTasks.ToArray());
            return hackFileMap;
        }


        private void PopulateProperties(in List<HpVersionProperty[]> allProperties)
        {
			//"Version", 50
			//"Configuration", 100
			//"Property", 140
			//"Value", 400
			//"Type", 140
			object lockObject = new();
			lock ( lockObject )
			{
				InitListViewInternal(OdooProperties, PropertiesRows);

                if (allProperties == null) return;
                SafeInvokeGeneric(OdooProperties, allProperties, (allp) =>
                {
                    foreach (HpVersionProperty[] versionProperties in allp)
                    {
                        if (versionProperties == null || versionProperties.Length == 0) continue;

                        foreach (HpVersionProperty versionProp in versionProperties)
                        {
                            if (versionProp == null || versionProp.ID == 0) continue;

						    ListViewItem item = EmptyListItemInternal(OdooProperties);

                            item.SubItems[ NameConfig [ "PropertiesVersion" ]].Text         = versionProp.version_id.ToString();
                            item.SubItems[ NameConfig [ "PropertiesConfiguration"] ].Text   = versionProp.sw_config_name ?? EmptyPlaceholder;
							item.SubItems[ NameConfig [ "PropertiesName" ] ].Text			= versionProp.prop_name ?? EmptyPlaceholder;
                            item.SubItems[ NameConfig [ "PropertiesProperty"] ].Text        = versionProp.prop_id.ToString();

                            string type = null;
                            string value = null;

                            if (versionProp.IsText(out string text))
                            {
                                type = "Text";
                                value = text;
                            }
                            else if (versionProp.IsNumber(out float number))
                            {
                                type = "Number";
                                value = number.ToString();
                            }
                            else if (versionProp.IsDate(out string date))
                            {
                                type = "Date";
                                value = date.ToString();
                            }
                            else
                            {
                                type = "YesNo";
                                versionProp.IsYesNo(out bool yesNo);
                                value = yesNo ? "Yes" : "No";
                            }
                            item.SubItems[ NameConfig [ "PropertiesValue" ] ].Text          = value;
                            item.SubItems[ NameConfig [ "PropertiesType" ] ].Text           = type;

                            OdooProperties.Items.Add(item);
                        }
                    }
                });
            }
        }
        private void PopulateChildren(in HpVersion[] versions)
        {
			// "Version", 50
			// "Name", 600
			object lockObject = new();
			lock ( lockObject )
			{
				InitListViewInternal(OdooChildren, ChildrenRows);

                if (versions == null) return;
                SafeInvokeGeneric(OdooChildren, versions, (v) =>
                {
                    foreach (HpVersion version in v)
                    {
                        ListViewItem item = EmptyListItemInternal(OdooChildren);
                        item.SubItems[ NameConfig [ "ChildrenVersion" ] ].Text      = version.ID.ToString();
                        item.SubItems[ NameConfig [ "ChildrenName" ] ].Text         = version.name;
						item.SubItems[ NameConfig [ "ChildrenBasePath" ] ].Text		= Path.Combine(/*HackDefaults.PWAPathAbsolute,*/ version.winPathway);
                        OdooChildren.Items.Add(item);
                    }
                });
            }
        }
        private void PopulateParent(in HpVersion[] versions)
        {
			// "Version", 50
			// "Name", 600
			object lockObject = new();
			lock ( lockObject )
			{
				InitListViewInternal( OdooParents, ParentRows);

                if (versions == null) return;
                SafeInvokeGeneric(OdooParents, versions, (v) =>
                {
                    foreach (HpVersion version in v)
                    {
					    ListViewItem item = EmptyListItemInternal(OdooParents);
					    item.SubItems [ NameConfig [ "ParentVersion" ] ].Text       = version.ID.ToString();
					    item.SubItems [ NameConfig [ "ParentName" ] ].Text          = version.name;
						item.SubItems [ NameConfig [ "ParentBasePath" ] ].Text		= version.winPathway;

					    OdooParents.Items.Add( item );
				    }
                });
            }
        }
        private void PopulateHistory(in HpVersion[] versions)
        {
			// "Version", 50
			// "ModUser", 140
            // "ModDate", 140
            // "Size", 75
            // "RelDate", 75
            object lockObject = new();
			lock (lockObject)
            {
                InitListViewInternal(OdooHistory, HistoryRows);

                if (versions == null) return;
                SafeInvokeGeneric(OdooHistory, versions, (v) =>
                {
                    foreach (HpVersion version in v)
                    {
                        ListViewItem item = EmptyListItemInternal(OdooHistory);

					    item.SubItems[ NameConfig [ "HistoryVersion" ] ].Text = version.ID.ToString();
                        item.SubItems[ NameConfig [ "HistoryModUser" ] ].Text = EmptyPlaceholder;
                        item.SubItems[ NameConfig [ "HistoryModDate" ] ].Text = version.file_modify_stamp?.ToShortDateString();
                        item.SubItems[ NameConfig [ "HistorySize" ] ].Text = version.file_size?.ToString();
                        item.SubItems[ NameConfig [ "HistoryRelDate" ] ].Text = EmptyPlaceholder;

                        OdooHistory.Items.Add(item);
                    }
                });
            }
        }
        private void PopulateVersionInfo(HpVersion version)
        {
			// int CheckoutColumnIndex = OdooEntryList.Columns["Checkout"].Index;
			// item.SubItems [ CheckoutColumnIndex ].Text == ""

			InitListViewInternal(OdooVersionInfoList, VersionInfoRows);
            
            if (version == null) return;
            SafeInvokeGeneric( OdooVersionInfoList, version, ( v ) =>
			{
				ListViewItem item = EmptyListItemInternal(OdooVersionInfoList);

                item.SubItems[NameConfig["VersionID"]].Text                     = version.ID.ToString();
				item.SubItems[NameConfig["VersionName"]].Text                   = version.name;
				item.SubItems[NameConfig["VersionChecksum"]].Text               = version.checksum;
				item.SubItems[NameConfig["VersionFileSize"]].Text               = version.file_size?.ToString();
				item.SubItems[NameConfig["VersionDirectoryID"]].Text            = version.dir_id?.ToString();
				item.SubItems[NameConfig["VersionNodeID"]].Text                 = version.node_id?.ToString();
				item.SubItems[NameConfig["VersionEntryID"]].Text                = version.entry_id?.ToString();
				item.SubItems[NameConfig["VersionAttachmentID"]].Text           = version.attachment_id?.ToString();
				item.SubItems[NameConfig["VersionModifyDate"]].Text             = version.file_modify_stamp?.ToShortDateString();
                string path;
                if (version.HashedValues != null && version.HashedValues.ContainsKey( "dir_id" ) )
				{
                    path = ( (ArrayList)version.HashedValues [ "dir_id" ] ) [ 1 ].ToString();
				}
				else
				{
					path = "Not Found";
				}
				item.SubItems [ NameConfig [ "VersionOdooCompletePath" ]].Text  = path;

				OdooVersionInfoList.Items.Add( item );
			} );
		}
        private void PreviewImage(int HpVersionID)
        {
            const string previewImage = "preview_image";
            //string previewImageB64 = null;

            if (HpVersionID != 0)
            {
                //ArrayList result = OClient.Read(HpVersion.GetHpModel(), [HpVersionID], [previewImage]);
                //Hashtable ht = (Hashtable)result[0];
                //object value = ht[previewImage];
                //previewImageB64 = value is string str ? str : null;

				HpVersion version = HpVersion.GetRecordsByIDS([HpVersionID], includedFields: [previewImage]).FirstOrDefault();
				PreviewImage(version);
            }
        }
		private void PreviewImage(HpVersion version)
		{
			if (version.preview_image is null or "") return;

			byte[] previewImageBytes = Convert.FromBase64String(version.preview_image);
			MemoryStream ms = new(previewImageBytes)
			{
				Position = 0
			};
			OdooEntryImage.Image = Image.FromStream( ms );
		}
		#endregion

		#region Background Worker functions
		// initialize background workers    
		// download latest version
		
		/// <summary>Handles the GetLatestAsync event of the worker control.</summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">The <see cref="DoWorkEventArgs" /> instance containing the event data.</param>
		private async void worker_GetLatestAsync( object sender, DoWorkEventArgs e )
		{
			BackgroundWorker myWorker = sender as BackgroundWorker;

			object lockObject = new();
			ArrayList entryIDs = (ArrayList)e.Argument;
			HpVersion[] versions;

			// add status lines for entry id and upcoming versions
			lock ( lockObject )
			{
				Dialog.AddStatusLine( "INFO", $"Found {entryIDs.Count} entries" );
				Dialog.AddStatusLine( "INFO", $"Retrieving all latest versions associated with entries" );
			}

			versions = GetLatestVersions( entryIDs, [ "preview_image", "entry_id", "node_id", "file_modify_stamp", "attachment_id", "file_contents" ] );

			IEnumerable<IEnumerable<HpVersion>> versionBatches = Utils.BatchList(versions, DownloadBatchSize);

			maxCount = versions.Length;
			skipCounter = 0;
			processCounter = 0;
			List<Task> tasks = [];

			foreach ( List<HpVersion> batch in versionBatches )
				tasks.Add( ProcessVersionBatchAsync( batch ) );

			await Task.WhenAll( tasks );
			// ensure that it does not move on until all files requested are downloaded
			Task.WaitAll( tasks.ToArray() );
			MessageBox.Show( "Completed" );
			RestartTree();
			RestartEntries();
		}

        /// <summary>background worker to create records within odoo that won't conflict with existing records</summary>
        /// <param name="sender">
        ///   <para>background worker</para>
        /// </param>
        /// <param name="e"></param>
        private async void worker_Commit(object sender, DoWorkEventArgs e)
        {
            
            BackgroundWorker myWorker = sender as BackgroundWorker;

            ValueTuple<HpEntry[], List<HackFile>> Arguments = (ValueTuple<HpEntry[], List<HackFile>>)e.Argument;
            // section for checking if the existing remote file already has a version with the same checksum 
            // or possibly an entry that has a newer version from that which is downloaded locally
            
			ConcurrentBag<HpEntry> entries = Arguments.Item1.ConvertToBag();
			ConcurrentSet<HackFile> hackFiles = Arguments.Item2;
            
            
            // testing filter hacks..
            entries = await FilterCommitEntries(entries);

            // section for checking if hack files have a checksum that matches the fullpath
            hackFiles = await FilterCommitHackFiles(hackFiles);


            while (hackFiles.TryTake(out HackFile result))
            {
                OdooDefaults.ConvertHackFile(result).Wait();
            }
			while (entries.TryTake(out HpEntry entry))
			{
				string entry_dir = HpDirectory.ConvertToWindowsPath(entry.HashedValues["directory_complete_name"] as string, false);
				HackFile hack = HackFile.GetFromPath(Path.Combine(HackDefaults.PWAPathAbsolute, entry_dir, entry.name));
				OdooDefaults.CreateNewVersion(hack, entry).Wait();
			}
			RestartTree();
			RestartEntries();
        }

		// checkout file
		private async void worker_CheckOut(object sender, DoWorkEventArgs e)
		{
			HpEntry[] entries = (HpEntry[])e.Argument;
			object lockObject = new();
			foreach ( HpEntry entry in entries )
			{
				lock(lockObject)
				{
					Dialog.AddStatusLine("INFO", $"Checking out {entry.name} ({entry.ID})" );
				}
				await CheckOutEntry( entry );
			}
			RestartEntries();
		}

		// uncheckout file
		private async void worker_UnCheckOut( object sender, DoWorkEventArgs e )
		{
			HpEntry[] entries = (HpEntry[])e.Argument;
			object lockObject = new();
			foreach ( HpEntry entry in entries )
			{
				lock ( lockObject )
				{
					Dialog.AddStatusLine( "INFO", $"UnChecking out {entry.name} ({entry.ID})" );
				}
				await UnCheckOutEntry( entry );
			}
			RestartEntries();
		}

		private async void worker_LogicalDelete( object sender, DoWorkEventArgs e )
		{
			HpEntry[] entries = (HpEntry[])e.Argument;
			object lockObject = new();
			foreach ( var entry in entries )
			{
				lock ( lockObject )
				{
					Dialog.AddStatusLine( "INFO", $"Setting InActive {entry.name}: {entry.ID}" );
				}
				await entry.LogicalDelete();
			}
		}
		private async void worker_LogicalUnDelete( object sender, DoWorkEventArgs e ) 
		{
			HpEntry[] entries = (HpEntry[])e.Argument;
			object lockObject = new();
			foreach ( var entry in entries )
			{
				lock ( lockObject )
				{
					Dialog.AddStatusLine( "INFO", $"Setting Active {entry.name}: {entry.ID}" );
				}
				await entry.LogicalUnDelete();
			}
		}

		// list item select
		private async void worker_ListItemChange(object sender, DoWorkEventArgs e)
        {
			try
            {
                ValueTuple<ListViewItemSelectionChangedEventArgs, CancellationToken> tuple = (ValueTuple<ListViewItemSelectionChangedEventArgs, CancellationToken>)e.Argument;
				await ProcessEntrySelectionAsync( tuple.Item1.Item, tuple.Item2 );
			}
			catch ( Exception ) { }
		}
		#endregion

		#region CheckOut Functions
		private IEnumerable<HpEntry> FilterCheckoutEntries( HpEntry[] entries )
		{
			foreach ( HpEntry entry in entries )
			{
				if ( entry.checkout_user == 0 )
				{
					yield return entry;
				}
			}
		}
		private IEnumerable<HpEntry> FilterUnCheckoutEntries( HpEntry[] entries )
		{
			foreach ( HpEntry entry in entries )
			{
				if ( entry.checkout_user != 0 )
				{
					yield return entry;
				}
			}
		}
		private async Task CheckOutEntry( HpEntry entry )
		{
			if ( entry == null )
				return;

			await entry.CheckOut();
		}
		private async Task UnCheckOutEntry( HpEntry entry )
		{
			if ( entry == null )
				return;

			await entry.UnCheckOut();
		}
		#endregion

		#region Commit Functions
        private async Task<ConcurrentBag<HpEntry>> FilterCommitEntries( ConcurrentBag<HpEntry> entries )
        {
            if (entries == null || entries.Count < 1) return null;

            string[] excludedFields = ["preview_image", "attachment_id", "file_modify_stamp", "file_size", "node_id", "file_contents"];
            ConcurrentBag<Task<HpEntry>> tasks = [];
            object lockObject = new();

            while (entries.TryTake(out HpEntry entry))
            {
                Task<HpEntry> entryTask = Task.Run(() =>
                {
                    // true means that this entry is checked out
                    if (entry.checkout_user != 0 && entry.checkout_user != OdooDefaults.OdooID)
                    {
                        lock (lockObject)
                        {
                            Dialog.AddStatusLine("INFO", $"Entry {entry.name} ({entry.ID}) is checked out to user id ({entry.checkout_user})");
                        }
                        return null;
                    }
                    // can eventually just change this to get the list of id's available instead
                    HpVersion[] entryVersions = GetVersionsForEntry(entry.ID, excludedFields);


                    // check if any of the versions checksums are local
					HpVersion temp = entryVersions.First();
                    if (HackFile.GetLocalVersion(entryVersions, out HackFile _))
                    {
                        lock (lockObject)
                        {
                            Dialog.AddStatusLine("INFO", $"Remote {temp.name} has matching local version");
                        }
                        
                        return null;
                    }
					FileInfo file = new(Path.Combine(HackDefaults.PWAPathAbsolute, temp.winPathway, temp.name));
					if (!file.Exists)
					{
						lock (lockObject)
                        {
                            Dialog.AddStatusLine("INFO", $"Remote {temp.name} has no local version");
                        }
                        
                        return null;
					}

                    lock (lockObject)
                    {
                        Dialog.AddStatusLine("INFO", $"Able to commit {entryVersions.First().name}");
                    }
                    return entry;
                });
                await entryTask;
                tasks.Add(entryTask);
            }
            await Task.WhenAll(tasks);
            return tasks.SkipSelect(
                taskPredicate =>
                {
                    if (taskPredicate.Result == null) return true;
                    return false;
                },
                taskSelect => taskSelect.Result).ConvertToBag();
        }
		private async Task<ConcurrentSet<HackFile>> FilterCommitHackFiles( ConcurrentSet<HackFile> hackFiles )
		{
			List<Task<HackFile>> tasks = [];
			object lockObject = new();

			//string[] filePaths = hackFiles.Select(hack => hack.FullPath).ToArray();

			HackFile[] files = await FileOperations.FilesNotInOdoo(hackFiles);
			return files;
		}
		#endregion

		#region Latest Functions
		private HpVersion [] GetLatestVersions(ArrayList entryIDs, string[] excludedFields = null)
        {
			if (excludedFields == null) excludedFields = ["preview_image", "file_contents"];
			return HpEntry.GetRelatedRecordByIDS<HpVersion>(entryIDs, "latest_version_id", excludedFields);
        }
        private async Task ProcessVersionBatchAsync(List<HpVersion> batchVersions)
        {
            object lockObject = new();
            ConcurrentBag<HpVersion> processVersions = [];
            ConcurrentBag<int> unprocessedVersions = [];
            List<Task> tasks = [];

            foreach (HpVersion version in batchVersions)
            {
                tasks.Add(
                    Task.Run(() =>
                    {
                        if (version.checksum == null || version.checksum.Length == 0 || version.checksum == "False") 
						{
							Interlocked.Increment(ref skipCounter);
							return null;
						}
                        if (FileOperations.SameChecksum(version, ChecksumType.SHA1))
                        {
                            //unprocessedVersions.Add(version.ID);
                            queueAsyncStatus.Enqueue(["INFO", $"Skipping download (Found): {version.name}"]);
                            Interlocked.Increment(ref skipCounter);
                            return null;
                        }
                        return version;
                    })
                    .ContinueWith((task) =>
                    {
                        if (task.Result == null) return;

                        string fileName = Path.Combine(task.Result.winPathway, task.Result.name);
                        processVersions.Add(task.Result);

                        queueAsyncStatus.Enqueue(["INFO", $"Downloading missing latest file: {fileName}"]);
                        Interlocked.Increment(ref processCounter);
                    })
                    .ContinueWith((task2) =>
                    {
                        lock (lockObject)
                        {
                            if (SkipCounter % 100 == 0 || SkipCounter == maxCount)
                            {
                                Dialog.AddStatusLines(queueAsyncStatus);
                            }
                            Dialog.SetProgressBar(skipCounter + processCounter, maxCount);
                        }
                    })
                );
            }
            // when all the tasks are completed for checking checksums start another task 
            // that then batch downloads those files to the correct folders.
            await Task.WhenAll(tasks)
                .ContinueWith(async (task) =>
                {
                    if (processVersions.Count > 0)
                    {
                        Task<int[]> finishSuccesses = Task.WhenAll(HpVersion.BatchDownloadFiles(processVersions.ToList()));
                        await finishSuccesses;
                        return finishSuccesses.Result[0];
                    }
                    return 0;
                });
        }
		private async void GetLatestFromTreeNode(bool withSubdirectories = false)
		{
			Dialog = new StatusDialog();
			object lockObject = new();


			TreeNode tnCurrent = lastSelectedNode;

			if ( tnCurrent == null )
			{
				MessageBox.Show( "current directory doesn't exist remotely" );
				return;
			}

			// directory only needs ID set to find that record's entries
			HpDirectory directory = new("temp");
			directory.ID = (int)tnCurrent.Tag;

			lock ( lockObject )
			{
				Dialog.AddStatusLine( "INFO", $"Retrieving all entries within directory ({directory.ID})" );
			}

			ArrayList entryIDs = directory.GetDirectoryEntryIDs( withSubdirectories, ShowInactive.Checked );


            HpEntry[] entries = HpEntry.GetRecordsByIDS(entryIDs, includedFields: ["latest_version_id"]);
            ArrayList newIds = await GetEntryList(entries.Select(e=>e.latest_version_id).ToArray());

            newIds.AddRange(entryIDs);
            newIds = newIds.ToHashSet<int>().ToArrayList();



            BackgroundWorker worker = new()
			{
				WorkerSupportsCancellation = true
			};
			//worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler((s, ev) => MessageBox.Show("Finished"));
			worker.DoWork += new DoWorkEventHandler( worker_GetLatestAsync );
			worker.RunWorkerAsync( newIds );

			bool blnWorkCanceled = Dialog.ShowStatusDialog("Get Latest");
			if ( blnWorkCanceled )
				worker.CancelAsync();
		}
		#endregion

		#region Form Event Handlers
		private void TreeView_LostFocus					( object sender, EventArgs e )
		{
			// Reselect the last selected node when the TreeView loses focus
			if ( lastSelectedNode != null )
			{
				OdooDirectoryTree.SelectedNode = lastSelectedNode;
			}
		}
		private async void OdooDirectoryTree_AfterSelect( object sender, TreeViewEventArgs e ) 
		{
			// Store the currently selected node
			lastSelectedNode = e.Node;
			lastSelectedNodePath = e.Node.FullPath;
			await TreeSelectItem( lastSelectedNode );
		}
		private void OdooEntryList_ItemSelectionChanged	( object sender, ListViewItemSelectionChangedEventArgs e )
		{
			if ( OdooEntryList.SelectedItems.Count > 1 )
				return;
			ClearEntryLists();
			if ( OdooEntryList.SelectedItems.Count == 0 )
				return;

			OdooEntryImage.Image = null;

			// TODO: Fix: if an item is selected while another item is still processing 
			// it runs into an error with item in Populate functions being null
			// or not processing the new item


			if ( backgroundWorker.IsBusy )
			{
				cSource.Cancel();
				backgroundWorker.CancelAsync();
			}
			else
			{
				cSource = new();
				backgroundWorker.RunWorkerAsync( (e, cSource.Token) );
			}
		}
		private void GetLatestStrip_Click				( object sender, EventArgs e )
			=> GetLatestFromTreeNode(true);
		private void allDirectoriesToolStripMenuItem_Click( object sender, EventArgs e ) 
			=> GetLatestStrip_Click(sender, e);
		private void topDirectoryToolStripMenuItem_Click( object sender, EventArgs e )
			=> GetLatestFromTreeNode(false);
		private async void GetLatestEntryStrip_Click			( object sender, EventArgs e )
		{
			Dialog = new StatusDialog();
			var entryItem = OdooEntryList.SelectedItems;
			
			//ArrayList entryIDs = new();
			//HashSet<int> entryIDs = new HashSet<int>();
			
			ArrayList entryIDs = new ArrayList();
			List<HpEntry> entries = new List<HpEntry>();

			foreach ( ListViewItem item in entryItem )
			{
				if ( int.TryParse( item.Text, out int ID ) )
				{
					//Hashtable ht = await GetEntryList(ID);
					entryIDs.Add(ID);
					entries.Add(HpEntry.GetRecordsByIDS([ID], includedFields:["latest_version_id"]).First());
				}
			}
			
			
			ArrayList newIds = await GetEntryList(entries.Select(e=>e.latest_version_id).ToArray());

			newIds.AddRange(entryIDs);
			newIds = newIds.ToHashSet<int>().ToArrayList();
			

			BackgroundWorker worker = new()
			{
				WorkerSupportsCancellation = true
			};
			worker.DoWork += new DoWorkEventHandler( worker_GetLatestAsync );
			worker.RunWorkerAsync( newIds );

			bool blnWorkCanceled = Dialog.ShowStatusDialog("Get Latest");
			if ( blnWorkCanceled )
				worker.CancelAsync();
		}
		
		private async Task RecurseAddDependentIds( HpVersion version, ConcurrentSet<int> entryIDs )
		{
			//entryIDs.Add( (int)version.entry_id  );
			//await Task.Run(async ()=>
			//{
			//	HpVersion[] childVersions = HpVersion.GetChildren(version.ID);

			//	if (childVersions is not null) 
			//	{
			//		foreach( HpVersion v in childVersions)
			//		{
			//			await RecurseAddDependentIds(v, entryIDs);
			//		}
			//	}
			//} );

		}

		private async Task<ArrayList> GetEntryList(int[] entry_ids)
		{
			ArrayList arr = await OClient.CommandAsync<ArrayList>(HpVersion.GetHpModel(), "get_recursive_dependency_entries", entry_ids.ToArrayList(), 50000);
			return arr;
		}

		private void CommitTreeStrip_Click				( object sender, EventArgs e )
		{
			Dialog = new StatusDialog();
			ArrayList entryIDs = HpDirectory.GetDirectoryEntryIDs((int)lastSelectedNode.Tag, true);
			var directory = lastSelectedNode.FullPath;

			List<HackFile> hackFiles = [];
			string pathway = lastSelectedNodePath.Length < 5 ? HackDefaults.PWAPathAbsolute : Path.Combine(HackDefaults.PWAPathAbsolute, lastSelectedNodePath.Substring(5));
			IEnumerable<string> files = Directory.EnumerateFiles(pathway, "*", SearchOption.AllDirectories);
			foreach ( string item in files )
			{
				HackFile hack = HackFile.GetFromPath(item, FileOperations.GetRelativePath(item));
				if ( hack != null )
					hackFiles.Add( hack );
			}

			HpEntry[] entries = HpEntry.GetRecordsByIDS(entryIDs, excludedFields:["type_id", "cat_id", "checkout_node"], insertFields:["directory_complete_name"]);

			object arguments = (entries, hackFiles);

			BackgroundWorker worker = new()
			{
				WorkerSupportsCancellation = true
			};

			worker.DoWork += new DoWorkEventHandler( worker_Commit );
			worker.RunWorkerAsync( arguments );

			bool blnWorkCanceled = Dialog.ShowStatusDialog("Commit Files");
			if ( blnWorkCanceled )
				worker.CancelAsync();
		}
		private void CommitEntryStrip_Click				( object sender, EventArgs e )
		{
			Dialog = new StatusDialog();

			var entryItem = OdooEntryList.SelectedItems;
			var directory = lastSelectedNode.FullPath;

			ArrayList entryIDs = new(entryItem.Count);
			List<HackFile> hackFiles = [];
			int FullNameColumnIndex = OdooEntryList.Columns["FullName"].Index;

			foreach ( ListViewItem item in entryItem )
			{
				if ( int.TryParse( item.Text, out int ID ) )
				{
					entryIDs.Add( ID );
				}
				else
				{
					if ( item.Text == "-" )
					{
						//hackFile.Add()
						HackFile hack = HackFile.GetFromPath(item.SubItems[FullNameColumnIndex].Text, directory);
						if ( hack != null )
							hackFiles.Add( hack );
					}
				}
			}

			HpEntry[] entries = HpEntry.GetRecordsByIDS(entryIDs, excludedFields:["type_id", "cat_id", "checkout_node"], insertFields:["directory_complete_name"]);

			object arguments = (entries, hackFiles);

			BackgroundWorker worker = new()
			{
				WorkerSupportsCancellation = true
			};
			//worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler((s, ev) => MessageBox.Show("Finished"));
			worker.DoWork += new DoWorkEventHandler( worker_Commit );
			worker.RunWorkerAsync( arguments );

			bool blnWorkCanceled = Dialog.ShowStatusDialog("Commit Files");
			if ( blnWorkCanceled )
				worker.CancelAsync();
		}
		private void CheckoutTreeStrip_Click			( object sender, EventArgs e )
		{
			Dialog = new StatusDialog();
			ArrayList entryIDs = HpDirectory.GetDirectoryEntryIDs((int)lastSelectedNode.Tag, true);

			HpEntry[] entries = HpEntry.GetRecordsByIDS(entryIDs, excludedFields:["type_id", "cat_id", "checkout_node"]);


			// filter out entries that are already checked out
			entries = FilterCheckoutEntries(entries).ToArray();

			object arguments = entries;

			BackgroundWorker worker = new()
			{
				WorkerSupportsCancellation = true
			};

			worker.DoWork += new DoWorkEventHandler( worker_CheckOut );
			worker.RunWorkerAsync( arguments );

			bool blnWorkCanceled = Dialog.ShowStatusDialog("Checkout Files");
			if ( blnWorkCanceled )
				worker.CancelAsync();
		}
		private void CheckoutEntryStrip_Click			( object sender, EventArgs e )
		{
			var entryItem = OdooEntryList.SelectedItems;
			var directory = lastSelectedNode.FullPath;

			ArrayList entryIDs = new(entryItem.Count);

			int FullNameColumnIndex = OdooEntryList.Columns["FullName"].Index;
			int CheckoutColumnIndex = OdooEntryList.Columns["CheckOut"].Index;

			foreach ( ListViewItem item in entryItem )
			{
				if ( int.TryParse( item.Text, out int ID ) )
				{
					if ( item.SubItems [ CheckoutColumnIndex ].Text == EmptyPlaceholder )
					{
						entryIDs.Add( ID );
					}
				}
			}

			if (entryIDs.Count < 1) return;
			HpEntry[] entries = HpEntry.GetRecordsByIDS(entryIDs, excludedFields:["type_id", "cat_id", "checkout_node"]);

			if (entries is null || entries.Length < 1) return;
			Dialog = new StatusDialog();

			object arguments = entries;

			BackgroundWorker worker = new()
			{
				WorkerSupportsCancellation = true
			};
			//worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler((s, ev) => MessageBox.Show("Finished"));
			worker.DoWork += new DoWorkEventHandler( worker_CheckOut );
			worker.RunWorkerAsync( arguments );

			bool blnWorkCanceled = Dialog.ShowStatusDialog("Checkout Files");
			if ( blnWorkCanceled )
				worker.CancelAsync();
		}
		private async void CheckedChange_Event( object sender, EventArgs e )
		{
			IsActive = ShowInactive.Checked;
			await TreeSelectItem( lastSelectedNode );
		}
		private void LogicalDeleteEntryStrip_Click		( object sender, EventArgs e )
		{
			Dialog = new StatusDialog();

			var entryItem = OdooEntryList.SelectedItems;
			//var directory = HackDefaults.DefaultPath(lastSelectedNode.FullPath, true);

			ArrayList entryIDs = [];
			foreach ( ListViewItem item in entryItem )
			{
				if ( int.TryParse( item.Text, out int ID ) )
				{
					entryIDs.Add(ID);
				}
			}
			HpEntry[] entries = HpEntry.GetRecordsByIDS(entryIDs, excludedFields:["type_id", "cat_id", "checkout_node"]);

			object arguments = entries;

			BackgroundWorker worker = new()
			{
				WorkerSupportsCancellation = true
			};
			//worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler((s, ev) => MessageBox.Show("Finished"));
			worker.DoWork += new DoWorkEventHandler( worker_LogicalDelete );
			worker.RunWorkerAsync( arguments );

			bool blnWorkCanceled = Dialog.ShowStatusDialog("Logically Delete Files");
			if ( blnWorkCanceled )
				worker.CancelAsync();
		}
		private void LogicalDeleteTreeStrip_Click		( object sender, EventArgs e )
		{
			Dialog = new StatusDialog();
			
			ArrayList entryIDs = HpDirectory.GetDirectoryEntryIDs((int)lastSelectedNode.Tag, true);

			HpEntry[] entries = HpEntry.GetRecordsByIDS(entryIDs, excludedFields:["type_id", "cat_id", "checkout_node"]);

			object arguments = entries;

			BackgroundWorker worker = new()
			{
				WorkerSupportsCancellation = true
			};
			//worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler((s, ev) => MessageBox.Show("Finished"));
			worker.DoWork += new DoWorkEventHandler( worker_LogicalDelete );
			worker.RunWorkerAsync( arguments );

			bool blnWorkCanceled = Dialog.ShowStatusDialog("Logically Delete Files");
			if ( blnWorkCanceled )
				worker.CancelAsync();

		}

		private void unDeleteToolStripMenuItem_Click( object sender, EventArgs e )
		{
			
		}
		private void unDeleteToolStripMenuItem1_Click( object sender, EventArgs e ) => UnDelete(false);
		private void allDirectoriesToolStripMenuItem1_Click( object sender, EventArgs e ) => MessageBox.Show("Not Implemented Yet");//UnDelete(true);
		private void topDirectoryToolStripMenuItem1_Click( object sender, EventArgs e ) => UnDelete(false);



		private void UnDelete(bool withSubdirectories = false)
		{
			Dialog = new StatusDialog();
			
			HpEntry[] entries = HpEntry.GetRecordsByIDS(null, searchFilters: [new ArrayList(){"deleted", "=", true}, new ArrayList(){"dir_id", "=", (int)lastSelectedNode.Tag}], excludedFields:["type_id", "cat_id", "checkout_node"]);
			object arguments = entries;

			BackgroundWorker worker = new()
			{
				WorkerSupportsCancellation = true
			};
			worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler( ( s, ev ) => MessageBox.Show( "Finished" ) );
			worker.DoWork += new DoWorkEventHandler( worker_LogicalUnDelete );
			worker.RunWorkerAsync( arguments );

			bool blnWorkCanceled = Dialog.ShowStatusDialog("Logically UnDelete Files");
			if ( blnWorkCanceled )
				worker.CancelAsync();
		}

		private void UnCheckoutTreeStrip_Click			( object sender, EventArgs e )
		{
			Dialog = new StatusDialog();
			ArrayList entryIDs = HpDirectory.GetDirectoryEntryIDs((int)lastSelectedNode.Tag, true);

			HpEntry[] entries = HpEntry.GetRecordsByIDS(entryIDs, excludedFields:["type_id", "cat_id", "checkout_node"]);


			// filter out entries that are already checked out
			entries = FilterUnCheckoutEntries( entries ).ToArray();

			object arguments = entries;

			BackgroundWorker worker = new()
			{
				WorkerSupportsCancellation = true
			};

			worker.DoWork += new DoWorkEventHandler( worker_UnCheckOut );
			worker.RunWorkerAsync( arguments );

			bool blnWorkCanceled = Dialog.ShowStatusDialog("UnCheckout Files");
			if ( blnWorkCanceled )
				worker.CancelAsync();
		}
		private void UnCheckoutEntryStrip_Click			( object sender, EventArgs e )
		{
			var entryItem = OdooEntryList.SelectedItems;
			var directory = lastSelectedNode.FullPath;

			ArrayList entryIDs = new(entryItem.Count);

			int FullNameColumnIndex = OdooEntryList.Columns["FullName"].Index;
			int CheckoutColumnIndex = OdooEntryList.Columns["CheckOut"].Index;

			foreach ( ListViewItem item in entryItem )
			{
				if ( int.TryParse( item.Text, out int ID ) )
				{
					if ( item.SubItems [ CheckoutColumnIndex ].Text != EmptyPlaceholder )
					{
						entryIDs.Add( ID );
					}
				}
			}

			if ( entryIDs.Count < 1 )
				return;
			HpEntry[] entries = HpEntry.GetRecordsByIDS(entryIDs, excludedFields:["type_id", "cat_id", "checkout_node"]);

			if ( entries is null || entries.Length < 1 )
				return;
			Dialog = new StatusDialog();

			object arguments = entries;

			BackgroundWorker worker = new()
			{
				WorkerSupportsCancellation = true
			};
			//worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler((s, ev) => MessageBox.Show("Finished"));
			worker.DoWork += new DoWorkEventHandler( worker_UnCheckOut );
			worker.RunWorkerAsync( arguments );

			bool blnWorkCanceled = Dialog.ShowStatusDialog("UnCheckout Files");
			if ( blnWorkCanceled )
				worker.CancelAsync();
		}
		private void OdooModelViewer_Click				( object sender, EventArgs e )
		{
			new OdooViewer().Show();
		}
		private void OdooSearchDropdown_Click			( object sender, EventArgs e )
		{
			SearchOdoo searchForm = new(this);
			searchForm.Show();
		}
		private void OdooRefreshDropdown_Click			( object sender, EventArgs e )
		{
			SafeInvoke(OdooEntryImage, () => 
			{
				OdooEntryImage.Image = previewImage;
			});
			RestartTree();
			RestartEntries();
		}
		private void OpenEntryStrip_Click				( object sender, EventArgs e )
		{
			// open local if lm, co
			// open remote if ro, dt
			foreach (ListViewItem viewItem in OdooEntryList.SelectedItems)
			{
				string path = viewItem.SubItems[NameConfig["RowFullName"]].Text;
				string IDStr = viewItem.SubItems[NameConfig["RowID"]].Text;
				if (IDStr == EmptyPlaceholder)
				{
					OpenLocalFile(path);
					continue;
				}
				string status = viewItem.SubItems[NameConfig["RowStatus"]].Text;
				switch (status) 
				{
					case "ro":
					case "nv":
					{
						if (int.TryParse(IDStr, out var id))
						{
							OpenRemoteFile(id);
						}
						continue;
					}

					case "lm":
					case "ok":
					case "co":
					case "ft":
					case "if":
					case "cm":
					{
						OpenLocalFile(HpDirectory.ConvertToWindowsPath(path, true));
						continue;
					}

					default:
						continue;
				}

			}
		}
		private void OpenLatestRemoteStrip_Click		( object sender, EventArgs e )
		{
			StringBuilder errors = new();
			foreach (ListViewItem viewItem in OdooEntryList.SelectedItems)
			{
				if (viewItem.SubItems[NameConfig["RowID"]].Text == EmptyPlaceholder)
				{
					errors.AppendLine($"can't open local only file remotely {viewItem.SubItems[NameConfig["RowName"]].Text}");
					continue;
				}
				string path = viewItem.SubItems[NameConfig["RowFullName"]].Text;
				string IDStr = viewItem.SubItems[NameConfig["RowID"]].Text;
				string status = viewItem.SubItems[NameConfig["RowStatus"]].Text;
				
				switch (status) 
				{
					case "ro":
					case "nv":
					case "lm":
					case "ok":
					case "co":
					case "ft":
					case "if":
					case "cm":
					{
						if (int.TryParse(IDStr, out var id))
						{
							OpenRemoteFile(id);
						}
						continue;
					}

					default:
					{
						errors.AppendLine($"can't open local only file remotely {viewItem.SubItems[NameConfig["RowName"]].Text}");
						continue;
					}
				}
			}
			if (errors.Length > 0) MessageBox.Show(errors.ToString());
		}
		private void OpenLatestLocalStrip_Click			( object sender, EventArgs e )
		{
			StringBuilder errors = new();
			foreach (ListViewItem viewItem in OdooEntryList.SelectedItems)
			{
				string path = viewItem.SubItems[NameConfig["RowFullName"]].Text;
				string IDStr = viewItem.SubItems[NameConfig["RowID"]].Text;

				if (IDStr == EmptyPlaceholder)
				{
					OpenLocalFile(path);
					continue;
				}

				string status = viewItem.SubItems[NameConfig["RowStatus"]].Text;
				
				switch (status) 
				{
					case "nv":
					case "lm":
					case "ok":
					case "co":
					case "ft":
					case "if":
					case "cm":
					{
						OpenLocalFile(HpDirectory.ConvertToWindowsPath(path, true));
						continue;
					}

					case "ro":
					default:
					{
						errors.AppendLine($"can't open remote only file locally {viewItem.SubItems[NameConfig["RowName"]].Text}");
						continue;
					}
				}
			}
			if (errors.Length > 0) MessageBox.Show(errors.ToString());
		}
		private void fileDirectoryToolStripMenuItem_Click( object sender, EventArgs e )
		{
			foreach(ListViewItem item in OdooEntryList.SelectedItems) 
			{
				string path = item.SubItems[ NameConfig [ "RowFullName" ] ].Text;
				string id = item.SubItems[ NameConfig [ "RowID" ] ].Text;

				try
				{
					// remote file path
					if (int.TryParse(id, out int entryID))
					{
						path = HpDirectory.ConvertToWindowsPath(path, true);
					}
					FileInfo file = new FileInfo(path);
					if (!file.Exists) continue;

					FileOperations.OpenFolder(file.DirectoryName);
				}
				catch
				{
					continue;
				}
				//FileOperations.OpenFile(  );
			}
		}
		private void OdooManageTypesDropdown_Click		( object sender, EventArgs e )
			=> new OdooFileTypeManager( this ).Show();
		private void OdooHistory_ItemSelectionChanged	( object sender, ListViewItemSelectionChangedEventArgs e )
			=> PreviewImageSelection( e.Item, "HistoryVersion" );
		private void OdooParents_ItemSelectionChanged	( object sender, ListViewItemSelectionChangedEventArgs e )
			=> PreviewImageSelection( e.Item, "ParentVersion" );
		private void OdooChildren_ItemSelectionChanged	( object sender, ListViewItemSelectionChangedEventArgs e )
			=> PreviewImageSelection( e.Item, "ChildrenVersion" );
		private async void OdooParents_DoubleClick		( object sender, EventArgs e )
		{
			ListViewItem item = OdooParents.SelectedItems?[0];
			if (item == null) return;

			string pwaPath = item.SubItems[ NameConfig[ "ParentBasePath" ] ].Text;
			string fileName = item.SubItems[ NameConfig[ "ParentName" ] ].Text;
			await FindSearchSelectionAsync(pwaPath, fileName);
		}
		private async void OdooChildren_DoubleClick		( object sender, EventArgs e )
		{
			ListViewItem item = OdooChildren.SelectedItems?[0];
			if (item == null) return;

			string pwaPath = item.SubItems[ NameConfig[ "ChildrenBasePath" ] ].Text;
			string fileName = item.SubItems[ NameConfig[ "ChildrenName" ] ].Text;
			await FindSearchSelectionAsync(pwaPath, fileName);
		}
		private void downloadToolStripMenuItem_Click	( object sender, EventArgs e )
		{
			var version = GetVersionFromHistory();
			FileInfo file = new(Path.Combine(version.winPathway, version.name));
			if (FileOperations.SameChecksum( file, version.checksum ))
			{
				if (file.Exists)
				{
					var response = MessageBox.Show("File exists as a different version.\n" +
					"Retry:\tDownload in the Temporary Folder\n" +
					"Ignore:\tOverwrite the current version\n" +
					"Abort:\tCancel download", "File Version Conflict", MessageBoxButtons.AbortRetryIgnore, MessageBoxIcon.Warning);

					if (response == DialogResult.Ignore) version.DownloadFile(version.winPathway);
					else if (response == DialogResult.Retry) version.DownloadFile(Path.GetTempPath());
				}
			}
			else
			{
				version.DownloadFile(version.winPathway);
			}
		}
		private void toTemporaryToolStripMenuItem_Click	( object sender, EventArgs e )
			=> DownloadHistory(true);
		private void overwriteCurrentToolStripMenuItem1_Click	( object sender, EventArgs e )
			=> DownloadHistory(false);
		private void overwriteAndOpenToolStripMenuItem_Click	( object sender, EventArgs e )
			=> DownloadOpen(false);
		private void temporaryAndOpenToolStripMenuItem_Click	( object sender, EventArgs e )
			=> DownloadOpen(true);
		private void toCurrentToolStripMenuItem_Click	( object sender, EventArgs e )
			=> LocalMoveEntry(false);
		private void toTemporaryToolStripMenuItem1_Click( object sender, EventArgs e )
			=> LocalMoveEntry(true);
		private async void OdooEntryList_DragDrop				( object sender, DragEventArgs e )
		{
			if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
			string[] fileDrop = e.Data.GetData(DataFormats.FileDrop) as string[];
			if (fileDrop is null or {Length: < 1}) return;
			
			Dialog = new StatusDialog();

			var directory = lastSelectedNode.FullPath;
			var winDirect = Path.Combine(HackDefaults.PWAPathAbsolute, directory.Substring(5));
			List<HackFile> hackFiles = [];

			foreach ( var path in fileDrop)
			{
				//if (!HackDefaults.PWAPathAbsolute.StartsWith(path)) continue;
				FileInfo file = new FileInfo(path);
				if (!file.Exists) continue;
				file = file.CopyFile(winDirect);

				HackFile hack = await HackFile.GetFromFileInfo(file);
				string newDirectory = path.Substring(HackDefaults.PWAPathAbsolute.Length);
				hack.RelativePath = newDirectory;
				if ( hack != null )
					hackFiles.Add( hack );	
			}

			if (hackFiles.Count < 1) return;
			object arguments = (new HpEntry[0], hackFiles);

			BackgroundWorker worker = new()
			{
				WorkerSupportsCancellation = true
			};
			//worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler((s, ev) => MessageBox.Show("Finished"));
			worker.DoWork += new DoWorkEventHandler( worker_Commit );
			worker.RunWorkerAsync( arguments );

			bool blnWorkCanceled = Dialog.ShowStatusDialog("Commit Files");
			if ( blnWorkCanceled )
				worker.CancelAsync();
		}
		private void OdooEntryList_DragEnter( object sender, DragEventArgs e )
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				e.Effect = DragDropEffects.Copy;
				StartOverlay(e);
			}
			else
			{
				e.Effect = DragDropEffects.None;
			}
		}
		private void OdooEntryList_DragLeave( object sender, EventArgs e )
		{
			EndOverlay();	
		}
		private void OdooEntryList_DragOver( object sender, DragEventArgs e )
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				UpdateOverlay(e);	
			}
		}

		private void permanentDeleteToolStripMenuItem_Click( object sender, EventArgs e )
		{
		#if DEBUG
			Dialog = new StatusDialog();

			var entryItem = OdooEntryList.SelectedItems;
			var directory = lastSelectedNode.FullPath;

			ArrayList entryIDs = new(entryItem.Count);

			foreach ( ListViewItem item in entryItem )
			{
				if ( int.TryParse( item.Text, out int ID ) )
				{
					entryIDs.Add( ID );
				}
			}

			HpEntry[] entries = HpEntry.GetRecordsByIDS(entryIDs, excludedFields:["type_id", "cat_id", "checkout_node"]);

			object arguments = entries;
			BackgroundWorker worker = new()
			{
				WorkerSupportsCancellation = true
			};
			//worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler((s, ev) => MessageBox.Show("Finished"));
			worker.DoWork += new DoWorkEventHandler( worker_PermDelete );
			worker.RunWorkerAsync( arguments );

			bool blnWorkCanceled = Dialog.ShowStatusDialog("Permanently Delete Files");
			if ( blnWorkCanceled )
				worker.CancelAsync();
		#endif
		}
		// tree
		private void perminentDeleteToolStripMenuItem_Click( object sender, EventArgs e )
		{
		#if DEBUG
			
		#endif
		}
		private void openDirectoryToolStripMenuItem_Click( object sender, EventArgs e )
		{
			string pathway = lastSelectedNodePath.Length < 5 ? HackDefaults.PWAPathAbsolute : Path.Combine(HackDefaults.PWAPathAbsolute, lastSelectedNodePath.Substring(5));
			if (Directory.Exists( pathway ) )
			{
				Process.Start( "explorer.exe", pathway );
			}
		}

		private void OdooCMSTree_Opening( object sender, CancelEventArgs e )
		{
			string pathway = lastSelectedNodePath.Length < 5 ? HackDefaults.PWAPathAbsolute : Path.Combine(HackDefaults.PWAPathAbsolute, lastSelectedNodePath.Substring(5));
			if (Directory.Exists( pathway ) ) 
			{
				openDirectoryToolStripMenuItem.Enabled = true;
				localDeleteToolStripMenuItem.Enabled = true;
			}
			else 
			{
				openDirectoryToolStripMenuItem.Enabled = false;
				localDeleteToolStripMenuItem.Enabled = false;
			}
		}
		// tree
		private void localDeleteToolStripMenuItem_Click( object sender, EventArgs e )
		{
			string pathway = lastSelectedNodePath.Length < 5 ? HackDefaults.PWAPathAbsolute : Path.Combine(HackDefaults.PWAPathAbsolute, lastSelectedNodePath.Substring(5));
			DirectoryInfo directory = new( pathway );
			if ( directory.Exists ) 
			{
				if (MessageBox.Show( $"Are you sure you want to delete this directory and ({directory.EnumerateFiles().Count()}) files inside?", 
					"Delete Directory", 
					MessageBoxButtons.YesNoCancel, 
					MessageBoxIcon.Warning ) == DialogResult.Yes)
				{
					directory.Delete( true );
				}
			}
		}
		// entry
		private void localDeleteToolStripMenuItem1_Click( object sender, EventArgs e )
		{
			string pathway = lastSelectedNodePath.Length < 5 ? HackDefaults.PWAPathAbsolute : Path.Combine(HackDefaults.PWAPathAbsolute, lastSelectedNodePath.Substring(5));
			DirectoryInfo directory = new( pathway );
			if ( !directory.Exists ) return;

			var sb = new StringBuilder();
			var files = new List<FileInfo>();

			OdooEntryList.SelectedItems.Cast<ListViewItem>().ToList().ForEach( item =>
			{
				string filepath = Path.Combine(pathway, item.SubItems[ NameConfig["RowName"] ].Text);
				FileInfo file = new( filepath );
				if ( file.Exists )
				{
					sb.AppendLine( file.FullName );
					files.Add( file );
				}
			} );
			bool tooMany = files.Count > 10;
			string message = tooMany ? $"Are you sure you want to delete ({files.Count}) files?" : $"Are you sure you want to delete these files?\nfiles:\n{sb.ToString()}";
			if (MessageBox.Show( message , 
					"Delete Directory", 
					MessageBoxButtons.YesNoCancel, 
					MessageBoxIcon.Warning ) == DialogResult.Yes)
			{
				files.ForEach( f => f.Delete() );
			}
			RestartEntries();
		}

		Point prevOverlayMousePos = new(0, 0);
		private void StartOverlay(DragEventArgs e)
		{
			// start overlay graphic
			
			FileDragGraphics(OdooEntryList, e);
		}
		private void UpdateOverlay(DragEventArgs e)
		{
			// update overlay graphic
			// FileDragGraphics(OdooEntryList, e);
		}
		private void EndOverlay()
		{
			// set back to normal graphics
			OdooEntryList.Invalidate();
		}
		private void FileDragGraphics(Control control, DragEventArgs e)
		{
			string[] files = e.Data.GetData(DataFormats.FileDrop) as string[] ?? new string[0];
			if (files.Length < 1) return;
			List<FileInfo> fileInfos = files.Select(f => new FileInfo(f)).ToList();

			// get graphics reset
			Graphics g = control.CreateGraphics();
			g.Clear(OdooEntryList.BackColor);

			// add the size of the radial gradient
			Rectangle controlSize = control.ClientRectangle;
			float HYPOT = controlSize.Size.Width*2;
			PointF midPoint = new((controlSize.Width/2) + controlSize.X, (controlSize.Height/2) + controlSize.Y);
			Rectangle sizeBox = new(0, 0, controlSize.Width, controlSize.Height);
			
			// create graphics path for radial gradient
			PointF scalePoint = ScalePoint(new PointF(e.X, e.Y), midPoint, HYPOT);
		
			using (var gPathBrush = new LinearGradientBrush(midPoint, scalePoint, Color.AliceBlue, Color.Coral))
			{
				gPathBrush.LinearColors		= [Color.AliceBlue, Color.Azure, Color.DarkSlateBlue, Color.Coral];
				g.FillRectangle(gPathBrush, controlSize);
			}
			

			// create back color 
			Font font = new(FontFamily.GenericSansSerif, 55f, GraphicsUnit.Pixel);
			Font fontValid = new(FontFamily.GenericSansSerif, 15f, GraphicsUnit.Pixel);
			Font fontInvalid = new(FontFamily.GenericSansSerif, 15f, FontStyle.Strikeout, GraphicsUnit.Pixel);

			SizeF offSet = new(controlSize.Width / 5f, controlSize.Height / 5f);
			const float imgRadius = 25f;

			RectangleF imageLayout = new(
				midPoint.X - imgRadius,
				midPoint.Y - imgRadius,
				imgRadius*2,
				imgRadius*2
			);
			RectangleF layout = new(
				imageLayout.X - 50, 
				imageLayout.Y - 50,
				400,
				100
			);
			Rectangle layoutPixel = new(
				(int)layout.X,
				(int)layout.Y,
				(int)layout.Width,
				(int)layout.Height
			);
			//Rectangle dot = new(Convert.ToInt32(midPoint.X), Convert.ToInt32(midPoint.Y), 5, 5);
			Pen pen = new Pen(new SolidBrush(Color.FromArgb(100, Color.Black)));


			//g.DrawRectangle(pen, layoutPixel);
			Image def = ilListIcons.Images["default"];
			g.DrawImage(def, imageLayout);
			RectangleF startRect = new(32, 50, controlSize.Width * 0.4f, 32f);
			using ( var brush = new SolidBrush( Color.Black ) )
			using ( var brushInvalid = new SolidBrush( Color.Crimson ) )
			{
				g.DrawString( $"{files.Length} Files", font, brush, layout );

				foreach ( var file in fileInfos )
				{
					if ( !file.Exists )
						continue;

					Image img = null; 

					if ( !OdooDefaults.ExtToType.ContainsKey( file.Extension ) )
					{
						img = ilListIcons.Images["delete_image_button"];
						
						g.DrawString( file.FullName, fontInvalid, brushInvalid, startRect );
					}
					else
					{
						img = ilListIcons.Images[file.Extension.Substring(1)];
						if ( img == null )
							img = def;
						
						g.DrawString( file.FullName, fontValid, brush, startRect );
					}
					g.DrawImage(img, 0, startRect.Y, 32, 32 );
					startRect.Y += 32;
				}
			}

			//g.DrawEllipse(pen, dot);
			//dot.X = Convert.ToInt32(scalePoint.X);
			//dot.Y = Convert.ToInt32(scalePoint.Y);
			//g.DrawEllipse(pen, dot);
		}
		private PointF ScalePoint(PointF p1, PointF p2, double desiredDistance )
		{
			PointF p3 = new(
				p2.X - p1.X,
				p2.Y - p1.Y
			);

			double currentDist = Math.Sqrt(p3.X * p3.X + p3.Y * p3.Y);
			double scaleFactor = desiredDistance / currentDist;
			p3.X = p2.X - Convert.ToSingle(scaleFactor) * p3.X;
			p3.Y = p2.Y - Convert.ToSingle(scaleFactor) * p3.Y;

			return p3;
		}
		#endregion

		#region Form Helper Functions
		// form safe invoke
		private delegate void UpdateTabPageTextDel(TabPage page, string text);
        private delegate void SafeInvokeDelGeneric<T>(Control c, T data, Action<T> action);
        private delegate void SafeInvokeDel(Control c, Action action);
        private void UpdateTabPageText(TabPage page, string text)
        {
            if (page.InvokeRequired)
            {
                page.Invoke(new UpdateTabPageTextDel(UpdateTabPageText), page, text);
            }
            else
                page.Text = text;
        }
        internal void InvokeControls<T>(params (Control control, T data, Action<T> action)[] values)
        {
            foreach (var value in values)
            {
                SafeInvokeGeneric<T>(value.control, value.data, value.action);
            }
        }
        internal void SafeInvokeGeneric<T>(Control control, T data, Action<T> action)
			=> SafeInvokeGen( control, data, action );
		internal static void SafeInvokeGen<T>( Control control, T data, Action<T> action )
		{
            if (control.InvokeRequired)
                control.Invoke(new SafeInvokeDelGeneric<T>(SafeInvokeGen), [control, data, action]);
            else
                action.Invoke(data);
		}
		internal void SafeInvoke(Control control, Action action)
			=> SafeInvoker( control, action );
		internal static void SafeInvoker( Control control, Action action )
		{			
            if (control.InvokeRequired)
                control.Invoke(new SafeInvokeDel(SafeInvoker), [control, action]);     
            else
                action.Invoke();
		}
		internal ImageList GetImageList() => ilListIcons;
		private void OpenLocalFile( string path )
		{
			FileOperations.OpenFile( path );
		}
		private void OpenRemoteFile( int entryID )
		{
			const string latest_version = "latest_version_id";
			HpVersion version = HpEntry.GetRelatedRecordByIDS<HpVersion>(new ArrayList() { entryID }, latest_version, excludedFields: ["preview_image"]).First();
			if ( version == null )
				return;
			
			// download version data and place into temporary folder
			version.DownloadFile( Path.GetTempPath() );
			FileOperations.OpenFile( Path.Combine( version.winPathway, version.name ) );
		}
		private void PreviewImageSelection(ListViewItem item, string nameConfigID)
		{
			string textID = item.SubItems[NameConfig[nameConfigID]].Text;
			if ( int.TryParse( textID, out int result ) )
			{
				PreviewImage( result );
			}
		}
		public async Task FindSearchSelectionAsync(string pwaPath, string fileName, string delimiter = "\\")
		{
			// first select the treeview node
			// then select the listview item
			string[] paths = pwaPath.Split([delimiter], StringSplitOptions.None);
			
			TreeNodeCollection nodes = OdooDirectoryTree.Nodes;
			TreeNode node = nodes[0];

			try
			{
				for (int i = 0; i < paths.Length; i++)
				{
					nodes = node.Nodes;
					
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
				lastSelectedNode = node;
				lastSelectedNode.EnsureVisible();
				OdooDirectoryTree.Select();
				
				while (!IsListLoaded)
				{
					await Task.Delay(100);
				}
				ListViewItem listItem = null;
				string index = NameConfig [ "SearchName" ];
				foreach (ListViewItem lv in OdooEntryList.Items)
				{
					if (lv.SubItems[ index ].Text == fileName)
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

		private void DownloadOpen(bool toTemp = false)
		{
			var version = DownloadHistory(toTemp);
			if ( version == null )
				return;

			OpenLocalFile( Path.Combine( version.winPathway, version.name ) );
		}
		private void LocalMoveEntry(bool toTemp=false)
		{
			var version = GetVersionFromHistory();
			if ( version == null ) return;

			string tempFilePath = Path.Combine(Properties.UserSettings.Default.TemporaryPath, version.name);
			string mainFilePath = Path.Combine(version.winPathway, version.name);

			FileInfo fileFrom	= new FileInfo( !toTemp ? tempFilePath : mainFilePath );
			FileInfo fileTo	= new FileInfo( toTemp ? tempFilePath : mainFilePath  );

			string message = "";
			string caption = "";
			string boolReplace = toTemp ? "temporary" : "current";

			var icon = MessageBoxIcon.None;
			// if the file doesn't exist in temporary folder, download it an place it in current path.
			if (fileFrom.Exists)
			{
				if (fileTo.Exists) 
				{
					message = $"Would you like to move this version to {boolReplace} and overwrite that version?";
					caption = "Move & Overwrite";
					icon	= MessageBoxIcon.Warning;
				}
				else
				{
					// temporary version file and current version file don't exist
					message = $"Would you like to move this version to {boolReplace}?";
					caption = "Move";
					icon	= MessageBoxIcon.Question;
				}
				// temporary version file doesn't exist but does exist in current
				if (DialogResult.Yes == MessageBox.Show(message, caption, MessageBoxButtons.YesNoCancel, icon: icon))
				{
					fileFrom.MoveFile(fileTo.DirectoryName);
				}
			}
			else
			{
				if (fileTo.Exists) 
				{
					message = $"file doesn't exist in {fileFrom.DirectoryName}.\nWould you like to download this version to {boolReplace} and overwrite that version?";
					caption = "Download & Overwrite";
					icon	= MessageBoxIcon.Warning;
				}
				else
				{
					// temporary version file and current version file don't exist
					message = $"file doesn't exist in {fileFrom.DirectoryName}.\nWould you like to download this version to {boolReplace}?";
					caption = "Download";
					icon	= MessageBoxIcon.Question;
				}
				// temporary version file doesn't exist but does exist in current
				if (DialogResult.Yes == MessageBox.Show(message, caption, MessageBoxButtons.YesNoCancel, icon: icon))
				{
					version.DownloadFile(fileTo.DirectoryName);
				}
			}
			RestartEntries();
		}
		private HpVersion DownloadHistory(bool toTemp = false)
		{
			var version = GetVersionFromHistory();
			if (version is null) return null;

			if ( toTemp )
				version.DownloadFile( Properties.UserSettings.Default.TemporaryPath );
			else
				version.DownloadFile( version.winPathway );

			return version;
		}
		private HpVersion GetVersionFromHistory()
		{
			if ( OdooHistory.SelectedItems.Count < 1 )
				return null;

			ListViewItem item = OdooHistory.SelectedItems[0];
			string IDstr = item.SubItems[ NameConfig["HistoryVersion"] ].Text;
			if ( int.TryParse( IDstr, out int ID ) )
			{
				var version = HpVersion.GetRecordByID(ID, HpVersion.UsualExcludedFields);
				version.winPathway = Path.Combine(HackDefaults.PWAPathAbsolute, version.winPathway);
				return version;
			}
			return null;
		}


		#endregion

		// need to delete pwa\
		// .\GitInfo
		// .\HackPDM_CSharp.csproj
		// .\HackPDM_CSharp.sln

		// list


		private void worker_PermDelete( object sender, DoWorkEventArgs e ) 
		{
			HpEntry[] entries = e.Argument as HpEntry[];
			ArrayList ids = entries.Select(e=>e.ID).ToArrayList();

			// first delete all versions associated with entry
			bool vDeleted = DeleteVersions(ids);
			if (!vDeleted)
			{
				MessageBox.Show("Was unable to delete versions", "Error", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
				return;
			}

			// second delete the entry
			bool eDeleted = DeleteEntry(ids);
			if (!eDeleted)
			{
				MessageBox.Show("Able to delete versions but was unable to delete entries", "Error", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
				return;
			}
		}
		private bool DeleteEntry( ArrayList ids )
			=> OClient.Delete(HpEntry.GetHpModel(), new ArrayList() {new ArrayList(){"id", "in", ids}});
		private bool DeleteVersions( ArrayList ids )
		{
			HpVersion[] versions = HpEntry.GetRelatedRecordByIDS<HpVersion>(ids, "version_ids", includedFields:["ID"]);
			ArrayList vIds = versions.Select(v => v.ID).ToArrayList();
			return OClient.Delete(HpVersion.GetHpModel(), new ArrayList() {new ArrayList(){"id", "in", vIds}});
		}

	}
}