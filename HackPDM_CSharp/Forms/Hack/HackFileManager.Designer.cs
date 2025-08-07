namespace HackPDM
{
    partial class HackFileManager
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(HackFileManager));
            this.OdooDirectoryTree = new System.Windows.Forms.TreeView();
            this.TreeContextDirectory = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.TreeGetLatest = new System.Windows.Forms.ToolStripMenuItem();
            this.TreeGetLatestTop = new System.Windows.Forms.ToolStripMenuItem();
            this.TreeGetLatestAll = new System.Windows.Forms.ToolStripMenuItem();
            this.TreeCheckout = new System.Windows.Forms.ToolStripMenuItem();
            this.TreeCommit = new System.Windows.Forms.ToolStripMenuItem();
            this.TreeUndoCheckout = new System.Windows.Forms.ToolStripMenuItem();
            this.TreeAnalyze = new System.Windows.Forms.ToolStripMenuItem();
            this.TreeDelete = new System.Windows.Forms.ToolStripMenuItem();
            this.TreeLogicalDelete = new System.Windows.Forms.ToolStripMenuItem();
            this.TreePermanentDelete = new System.Windows.Forms.ToolStripMenuItem();
            this.TreeRestore = new System.Windows.Forms.ToolStripMenuItem();
            this.TreeRestoreTop = new System.Windows.Forms.ToolStripMenuItem();
            this.TreeRestoreAll = new System.Windows.Forms.ToolStripMenuItem();
            this.TreeLocalDelete = new System.Windows.Forms.ToolStripMenuItem();
            this.TreeOpenDirectory = new System.Windows.Forms.ToolStripMenuItem();
            this.TreeIcons = new System.Windows.Forms.ImageList(this.components);
            this.OdooEntryList = new System.Windows.Forms.ListView();
            this.ListContextEntry = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.ListGetLatest = new System.Windows.Forms.ToolStripMenuItem();
            this.ListCheckout = new System.Windows.Forms.ToolStripMenuItem();
            this.ListCommit = new System.Windows.Forms.ToolStripMenuItem();
            this.ListUndoCheckout = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem6 = new System.Windows.Forms.ToolStripMenuItem();
            this.logicalDeleteToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.unDeleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.permanentDeleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.localDeleteToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.OpenEntryStrip = new System.Windows.Forms.ToolStripMenuItem();
            this.OpenLatestRemoteStrip = new System.Windows.Forms.ToolStripMenuItem();
            this.OpenLatestLocalStrip = new System.Windows.Forms.ToolStripMenuItem();
            this.fileDirectoryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ListIcons = new System.Windows.Forms.ImageList(this.components);
            this.VersionTabs = new System.Windows.Forms.TabControl();
            this.OdooHistoryPage = new System.Windows.Forms.TabPage();
            this.OdooHistory = new System.Windows.Forms.ListView();
            this.OdooVersionHistoryMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.downloadToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toTemporaryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.overwriteCurrentToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.overwriteAndOpenToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.temporaryAndOpenToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.moveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toCurrentToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toTemporaryToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.OdooParentsPage = new System.Windows.Forms.TabPage();
            this.OdooParents = new System.Windows.Forms.ListView();
            this.OdooChildrenPage = new System.Windows.Forms.TabPage();
            this.OdooChildren = new System.Windows.Forms.ListView();
            this.OdooPropertiesPage = new System.Windows.Forms.TabPage();
            this.OdooProperties = new System.Windows.Forms.ListView();
            this.OdooVersionPage = new System.Windows.Forms.TabPage();
            this.OdooVersionInfoList = new System.Windows.Forms.ListView();
            this.panel1 = new System.Windows.Forms.Panel();
            this.ShowInactive = new System.Windows.Forms.CheckBox();
            this.MoreTools = new System.Windows.Forms.ToolStrip();
            this.ListTabs = new System.Windows.Forms.TabControl();
            this.EntriesTab = new System.Windows.Forms.TabPage();
            this.Changes = new System.Windows.Forms.TabPage();
            this.HackChangesList = new System.Windows.Forms.ListView();
            this.AdditionalTools = new System.Windows.Forms.ToolStripDropDownButton();
            this.OdooRefreshDropdown = new System.Windows.Forms.ToolStripMenuItem();
            this.OdooSearchDropdown = new System.Windows.Forms.ToolStripMenuItem();
            this.OdooManageTypesDropdown = new System.Windows.Forms.ToolStripMenuItem();
            this.OdooEntryImage = new System.Windows.Forms.PictureBox();
            this.TreeContextDirectory.SuspendLayout();
            this.ListContextEntry.SuspendLayout();
            this.VersionTabs.SuspendLayout();
            this.OdooHistoryPage.SuspendLayout();
            this.OdooVersionHistoryMenu.SuspendLayout();
            this.OdooParentsPage.SuspendLayout();
            this.OdooChildrenPage.SuspendLayout();
            this.OdooPropertiesPage.SuspendLayout();
            this.OdooVersionPage.SuspendLayout();
            this.panel1.SuspendLayout();
            this.MoreTools.SuspendLayout();
            this.ListTabs.SuspendLayout();
            this.EntriesTab.SuspendLayout();
            this.Changes.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.OdooEntryImage)).BeginInit();
            this.SuspendLayout();
            // 
            // OdooDirectoryTree
            // 
            this.OdooDirectoryTree.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.OdooDirectoryTree.ContextMenuStrip = this.TreeContextDirectory;
            this.OdooDirectoryTree.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.OdooDirectoryTree.ImageIndex = 0;
            this.OdooDirectoryTree.ImageList = this.TreeIcons;
            this.OdooDirectoryTree.Location = new System.Drawing.Point(12, 13);
            this.OdooDirectoryTree.Name = "OdooDirectoryTree";
            this.OdooDirectoryTree.SelectedImageIndex = 0;
            this.OdooDirectoryTree.Size = new System.Drawing.Size(321, 479);
            this.OdooDirectoryTree.TabIndex = 0;
            this.OdooDirectoryTree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.OdooDirectoryTree_AfterSelect);
            // 
            // TreeContextDirectory
            // 
            this.TreeContextDirectory.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.TreeGetLatest,
            this.TreeCheckout,
            this.TreeCommit,
            this.TreeUndoCheckout,
            this.TreeAnalyze,
            this.TreeDelete,
            this.TreeOpenDirectory});
            this.TreeContextDirectory.Name = "contextMenuStrip1";
            this.TreeContextDirectory.Size = new System.Drawing.Size(158, 158);
            this.TreeContextDirectory.Opening += new System.ComponentModel.CancelEventHandler(this.OdooCMSTree_Opening);
            // 
            // TreeGetLatest
            // 
            this.TreeGetLatest.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.TreeGetLatestTop,
            this.TreeGetLatestAll});
            this.TreeGetLatest.Name = "TreeGetLatest";
            this.TreeGetLatest.Size = new System.Drawing.Size(157, 22);
            this.TreeGetLatest.Text = "Get Latest";
            this.TreeGetLatest.Click += new System.EventHandler(this.Tree_Click_GetLatest);
            // 
            // TreeGetLatestTop
            // 
            this.TreeGetLatestTop.Name = "TreeGetLatestTop";
            this.TreeGetLatestTop.Size = new System.Drawing.Size(147, 22);
            this.TreeGetLatestTop.Text = "Top Directory";
            this.TreeGetLatestTop.Click += new System.EventHandler(this.Tree_Click_GetLatestTop);
            // 
            // TreeGetLatestAll
            // 
            this.TreeGetLatestAll.Name = "TreeGetLatestAll";
            this.TreeGetLatestAll.Size = new System.Drawing.Size(147, 22);
            this.TreeGetLatestAll.Text = "All Directories";
            this.TreeGetLatestAll.Click += new System.EventHandler(this.Tree_Click_GetLatestAll);
            // 
            // TreeCheckout
            // 
            this.TreeCheckout.Name = "TreeCheckout";
            this.TreeCheckout.Size = new System.Drawing.Size(157, 22);
            this.TreeCheckout.Text = "Checkout";
            this.TreeCheckout.Click += new System.EventHandler(this.Tree_Click_Checkout);
            // 
            // TreeCommit
            // 
            this.TreeCommit.Name = "TreeCommit";
            this.TreeCommit.Size = new System.Drawing.Size(157, 22);
            this.TreeCommit.Text = "Commit";
            this.TreeCommit.Click += new System.EventHandler(this.Tree_Click_Commit);
            // 
            // TreeUndoCheckout
            // 
            this.TreeUndoCheckout.Name = "TreeUndoCheckout";
            this.TreeUndoCheckout.Size = new System.Drawing.Size(157, 22);
            this.TreeUndoCheckout.Text = "Undo Checkout";
            this.TreeUndoCheckout.Click += new System.EventHandler(this.Tree_Click_UndoCheckout);
            // 
            // TreeAnalyze
            // 
            this.TreeAnalyze.Name = "TreeAnalyze";
            this.TreeAnalyze.Size = new System.Drawing.Size(157, 22);
            this.TreeAnalyze.Text = "Analyze";
            this.TreeAnalyze.Click += new System.EventHandler(this.TreeAnalyze_Click);
            // 
            // TreeDelete
            // 
            this.TreeDelete.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.TreeLogicalDelete,
            this.TreePermanentDelete,
            this.TreeRestore,
            this.TreeLocalDelete});
            this.TreeDelete.Name = "TreeDelete";
            this.TreeDelete.Size = new System.Drawing.Size(157, 22);
            this.TreeDelete.Text = "Delete";
            this.TreeDelete.Click += new System.EventHandler(this.TreeDelete_Click);
            // 
            // TreeLogicalDelete
            // 
            this.TreeLogicalDelete.Name = "TreeLogicalDelete";
            this.TreeLogicalDelete.Size = new System.Drawing.Size(168, 22);
            this.TreeLogicalDelete.Text = "Logical Delete";
            this.TreeLogicalDelete.Click += new System.EventHandler(this.Tree_Click_LogicalDelete);
            // 
            // TreePermanentDelete
            // 
            this.TreePermanentDelete.Name = "TreePermanentDelete";
            this.TreePermanentDelete.Size = new System.Drawing.Size(168, 22);
            this.TreePermanentDelete.Text = "Permanent Delete";
            this.TreePermanentDelete.Click += new System.EventHandler(this.Tree_Click_PermanentDelete);
            // 
            // TreeRestore
            // 
            this.TreeRestore.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.TreeRestoreTop,
            this.TreeRestoreAll});
            this.TreeRestore.Name = "TreeRestore";
            this.TreeRestore.Size = new System.Drawing.Size(168, 22);
            this.TreeRestore.Text = "UnDelete";
            this.TreeRestore.Click += new System.EventHandler(this.Tree_Click_Restore);
            // 
            // TreeRestoreTop
            // 
            this.TreeRestoreTop.Name = "TreeRestoreTop";
            this.TreeRestoreTop.Size = new System.Drawing.Size(147, 22);
            this.TreeRestoreTop.Text = "Top Directory";
            this.TreeRestoreTop.Click += new System.EventHandler(this.Tree_Click_RestoreTop);
            // 
            // TreeRestoreAll
            // 
            this.TreeRestoreAll.Name = "TreeRestoreAll";
            this.TreeRestoreAll.Size = new System.Drawing.Size(147, 22);
            this.TreeRestoreAll.Text = "All Directories";
            this.TreeRestoreAll.Click += new System.EventHandler(this.Tree_Click_RestoreAll);
            // 
            // TreeLocalDelete
            // 
            this.TreeLocalDelete.Name = "TreeLocalDelete";
            this.TreeLocalDelete.Size = new System.Drawing.Size(168, 22);
            this.TreeLocalDelete.Text = "Local Delete";
            this.TreeLocalDelete.Click += new System.EventHandler(this.Tree_Click_LocalDelete);
            // 
            // TreeOpenDirectory
            // 
            this.TreeOpenDirectory.Name = "TreeOpenDirectory";
            this.TreeOpenDirectory.Size = new System.Drawing.Size(157, 22);
            this.TreeOpenDirectory.Text = "Open Directory";
            this.TreeOpenDirectory.Click += new System.EventHandler(this.Tree_Click_OpenDirectory);
            // 
            // TreeIcons
            // 
            this.TreeIcons.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("TreeIcons.ImageStream")));
            this.TreeIcons.TransparentColor = System.Drawing.Color.Transparent;
            this.TreeIcons.Images.SetKeyName(0, "simple-folder-icon_32.gif");
            this.TreeIcons.Images.SetKeyName(1, "folder-icon_localonly_32.gif");
            this.TreeIcons.Images.SetKeyName(2, "folder-icon_remoteonly_32.gif");
            this.TreeIcons.Images.SetKeyName(3, "folder-icon_checkedme_32.gif");
            this.TreeIcons.Images.SetKeyName(4, "folder-icon_checkedother_32.gif");
            this.TreeIcons.Images.SetKeyName(5, "folder-icon_deleted_32.gif");
            // 
            // OdooEntryList
            // 
            this.OdooEntryList.AllowColumnReorder = true;
            this.OdooEntryList.AllowDrop = true;
            this.OdooEntryList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.OdooEntryList.ContextMenuStrip = this.ListContextEntry;
            this.OdooEntryList.FullRowSelect = true;
            this.OdooEntryList.GridLines = true;
            this.OdooEntryList.HideSelection = false;
            this.OdooEntryList.Location = new System.Drawing.Point(0, 0);
            this.OdooEntryList.Name = "OdooEntryList";
            this.OdooEntryList.Size = new System.Drawing.Size(1108, 470);
            this.OdooEntryList.SmallImageList = this.ListIcons;
            this.OdooEntryList.Sorting = System.Windows.Forms.SortOrder.Ascending;
            this.OdooEntryList.TabIndex = 1;
            this.OdooEntryList.UseCompatibleStateImageBehavior = false;
            this.OdooEntryList.View = System.Windows.Forms.View.Details;
            this.OdooEntryList.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.List_ColumnClick);
            this.OdooEntryList.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.OdooEntryList_ItemSelectionChanged);
            this.OdooEntryList.SelectedIndexChanged += new System.EventHandler(this.OdooEntryList_SelectedIndexChanged);
            this.OdooEntryList.DragDrop += new System.Windows.Forms.DragEventHandler(this.List_DragDrop);
            this.OdooEntryList.DragEnter += new System.Windows.Forms.DragEventHandler(this.List_DragEnter);
            this.OdooEntryList.DragOver += new System.Windows.Forms.DragEventHandler(this.List_DragOver);
            this.OdooEntryList.DragLeave += new System.EventHandler(this.List_DragLeave);
            // 
            // ListContextEntry
            // 
            this.ListContextEntry.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ListGetLatest,
            this.ListCheckout,
            this.ListCommit,
            this.ListUndoCheckout,
            this.toolStripMenuItem6,
            this.OpenEntryStrip});
            this.ListContextEntry.Name = "OdooCMSList";
            this.ListContextEntry.Size = new System.Drawing.Size(158, 136);
            this.ListContextEntry.Opening += new System.ComponentModel.CancelEventHandler(this.ListContextEntry_Opening);
            // 
            // ListGetLatest
            // 
            this.ListGetLatest.Name = "ListGetLatest";
            this.ListGetLatest.Size = new System.Drawing.Size(157, 22);
            this.ListGetLatest.Text = "Get Latest";
            this.ListGetLatest.ToolTipText = "Checks to see if you have the same checksum as any of the version records and if " +
    "not then it will try to download it to the specific local directory";
            this.ListGetLatest.Click += new System.EventHandler(this.List_Click_GetLatest);
            // 
            // ListCheckout
            // 
            this.ListCheckout.Name = "ListCheckout";
            this.ListCheckout.Size = new System.Drawing.Size(157, 22);
            this.ListCheckout.Text = "Checkout";
            this.ListCheckout.Click += new System.EventHandler(this.List_Click_Checkout);
            // 
            // ListCommit
            // 
            this.ListCommit.Name = "ListCommit";
            this.ListCommit.Size = new System.Drawing.Size(157, 22);
            this.ListCommit.Text = "Commit";
            this.ListCommit.ToolTipText = "Checks to see if the version has the same checksum in the versions records and if" +
    " not then it will try to commit the file to Odoo to store.";
            this.ListCommit.Click += new System.EventHandler(this.List_Click_Commit);
            // 
            // ListUndoCheckout
            // 
            this.ListUndoCheckout.Name = "ListUndoCheckout";
            this.ListUndoCheckout.Size = new System.Drawing.Size(157, 22);
            this.ListUndoCheckout.Text = "Undo Checkout";
            this.ListUndoCheckout.Click += new System.EventHandler(this.List_Click_UndoCheckout);
            // 
            // toolStripMenuItem6
            // 
            this.toolStripMenuItem6.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.logicalDeleteToolStripMenuItem1,
            this.unDeleteToolStripMenuItem,
            this.permanentDeleteToolStripMenuItem,
            this.localDeleteToolStripMenuItem1});
            this.toolStripMenuItem6.Name = "toolStripMenuItem6";
            this.toolStripMenuItem6.Size = new System.Drawing.Size(157, 22);
            this.toolStripMenuItem6.Text = "Delete";
            this.toolStripMenuItem6.Click += new System.EventHandler(this.toolStripMenuItem6_Click);
            // 
            // logicalDeleteToolStripMenuItem1
            // 
            this.logicalDeleteToolStripMenuItem1.Name = "logicalDeleteToolStripMenuItem1";
            this.logicalDeleteToolStripMenuItem1.Size = new System.Drawing.Size(168, 22);
            this.logicalDeleteToolStripMenuItem1.Text = "Logical Delete";
            this.logicalDeleteToolStripMenuItem1.ToolTipText = "sets the Entry to inactive in Odoo";
            this.logicalDeleteToolStripMenuItem1.Click += new System.EventHandler(this.List_Click_LogicalDelete);
            // 
            // unDeleteToolStripMenuItem
            // 
            this.unDeleteToolStripMenuItem.Name = "unDeleteToolStripMenuItem";
            this.unDeleteToolStripMenuItem.Size = new System.Drawing.Size(168, 22);
            this.unDeleteToolStripMenuItem.Text = "UnDelete";
            this.unDeleteToolStripMenuItem.Click += new System.EventHandler(this.List_Click_Restore);
            // 
            // permanentDeleteToolStripMenuItem
            // 
            this.permanentDeleteToolStripMenuItem.Name = "permanentDeleteToolStripMenuItem";
            this.permanentDeleteToolStripMenuItem.Size = new System.Drawing.Size(168, 22);
            this.permanentDeleteToolStripMenuItem.Text = "Permanent Delete";
            this.permanentDeleteToolStripMenuItem.Click += new System.EventHandler(this.List_Click_PermanentDelete);
            // 
            // localDeleteToolStripMenuItem1
            // 
            this.localDeleteToolStripMenuItem1.Name = "localDeleteToolStripMenuItem1";
            this.localDeleteToolStripMenuItem1.Size = new System.Drawing.Size(168, 22);
            this.localDeleteToolStripMenuItem1.Text = "Local Delete";
            this.localDeleteToolStripMenuItem1.Click += new System.EventHandler(this.List_Click_LocalDelete);
            // 
            // OpenEntryStrip
            // 
            this.OpenEntryStrip.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.OpenLatestRemoteStrip,
            this.OpenLatestLocalStrip,
            this.fileDirectoryToolStripMenuItem});
            this.OpenEntryStrip.Name = "OpenEntryStrip";
            this.OpenEntryStrip.Size = new System.Drawing.Size(157, 22);
            this.OpenEntryStrip.Text = "Open";
            this.OpenEntryStrip.Click += new System.EventHandler(this.List_Click_Open);
            // 
            // OpenLatestRemoteStrip
            // 
            this.OpenLatestRemoteStrip.AutoToolTip = true;
            this.OpenLatestRemoteStrip.Name = "OpenLatestRemoteStrip";
            this.OpenLatestRemoteStrip.Size = new System.Drawing.Size(193, 22);
            this.OpenLatestRemoteStrip.Text = "Preview Latest Remote";
            this.OpenLatestRemoteStrip.ToolTipText = "Downloads the version file and puts it in the temporary folder path";
            this.OpenLatestRemoteStrip.Click += new System.EventHandler(this.List_Click_OpenLatestRemote);
            // 
            // OpenLatestLocalStrip
            // 
            this.OpenLatestLocalStrip.Name = "OpenLatestLocalStrip";
            this.OpenLatestLocalStrip.Size = new System.Drawing.Size(193, 22);
            this.OpenLatestLocalStrip.Text = "Latest Local";
            this.OpenLatestLocalStrip.ToolTipText = "Opens the latest local file";
            this.OpenLatestLocalStrip.Click += new System.EventHandler(this.List_Click_OpenLatestLocal);
            // 
            // fileDirectoryToolStripMenuItem
            // 
            this.fileDirectoryToolStripMenuItem.Name = "fileDirectoryToolStripMenuItem";
            this.fileDirectoryToolStripMenuItem.Size = new System.Drawing.Size(193, 22);
            this.fileDirectoryToolStripMenuItem.Text = "File Directory";
            this.fileDirectoryToolStripMenuItem.ToolTipText = "Open file explorer to the parent folder";
            this.fileDirectoryToolStripMenuItem.Click += new System.EventHandler(this.List_Click_OpenDirectory);
            // 
            // ListIcons
            // 
            this.ListIcons.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("ListIcons.ImageStream")));
            this.ListIcons.TransparentColor = System.Drawing.Color.Transparent;
            this.ListIcons.Images.SetKeyName(0, "cm");
            this.ListIcons.Images.SetKeyName(1, "co");
            this.ListIcons.Images.SetKeyName(2, "lo");
            this.ListIcons.Images.SetKeyName(3, "ro");
            this.ListIcons.Images.SetKeyName(4, "ft");
            this.ListIcons.Images.SetKeyName(5, "nv");
            this.ListIcons.Images.SetKeyName(6, "lm");
            this.ListIcons.Images.SetKeyName(7, "dt");
            this.ListIcons.Images.SetKeyName(8, "if");
            this.ListIcons.Images.SetKeyName(9, "ds");
            this.ListIcons.Images.SetKeyName(10, "default");
            this.ListIcons.Images.SetKeyName(11, "3mf");
            this.ListIcons.Images.SetKeyName(12, "ai");
            this.ListIcons.Images.SetKeyName(13, "asmdot");
            this.ListIcons.Images.SetKeyName(14, "asmprp");
            this.ListIcons.Images.SetKeyName(15, "avi");
            this.ListIcons.Images.SetKeyName(16, "bas");
            this.ListIcons.Images.SetKeyName(17, "bat");
            this.ListIcons.Images.SetKeyName(18, "bmp");
            this.ListIcons.Images.SetKeyName(19, "btl");
            this.ListIcons.Images.SetKeyName(20, "cnc");
            this.ListIcons.Images.SetKeyName(21, "cs");
            this.ListIcons.Images.SetKeyName(22, "csproj");
            this.ListIcons.Images.SetKeyName(23, "csv");
            this.ListIcons.Images.SetKeyName(24, "cwr");
            this.ListIcons.Images.SetKeyName(25, "dat");
            this.ListIcons.Images.SetKeyName(26, "db");
            this.ListIcons.Images.SetKeyName(27, "dic");
            this.ListIcons.Images.SetKeyName(28, "doc");
            this.ListIcons.Images.SetKeyName(29, "docx");
            this.ListIcons.Images.SetKeyName(30, "dot");
            this.ListIcons.Images.SetKeyName(31, "drwdot");
            this.ListIcons.Images.SetKeyName(32, "dwg");
            this.ListIcons.Images.SetKeyName(33, "dxf");
            this.ListIcons.Images.SetKeyName(34, "edrw");
            this.ListIcons.Images.SetKeyName(35, "eps");
            this.ListIcons.Images.SetKeyName(36, "gcode");
            this.ListIcons.Images.SetKeyName(37, "gif");
            this.ListIcons.Images.SetKeyName(38, "gz");
            this.ListIcons.Images.SetKeyName(39, "htm");
            this.ListIcons.Images.SetKeyName(40, "igs");
            this.ListIcons.Images.SetKeyName(41, "indd");
            this.ListIcons.Images.SetKeyName(42, "index");
            this.ListIcons.Images.SetKeyName(43, "jpg");
            this.ListIcons.Images.SetKeyName(44, "ldb");
            this.ListIcons.Images.SetKeyName(45, "log");
            this.ListIcons.Images.SetKeyName(46, "m");
            this.ListIcons.Images.SetKeyName(47, "mdb");
            this.ListIcons.Images.SetKeyName(48, "ods");
            this.ListIcons.Images.SetKeyName(49, "odt");
            this.ListIcons.Images.SetKeyName(50, "pdf");
            this.ListIcons.Images.SetKeyName(51, "png");
            this.ListIcons.Images.SetKeyName(52, "propdesc");
            this.ListIcons.Images.SetKeyName(53, "prtdot");
            this.ListIcons.Images.SetKeyName(54, "prtprp");
            this.ListIcons.Images.SetKeyName(55, "ps");
            this.ListIcons.Images.SetKeyName(56, "resx");
            this.ListIcons.Images.SetKeyName(57, "rpt");
            this.ListIcons.Images.SetKeyName(58, "settings");
            this.ListIcons.Images.SetKeyName(59, "sla");
            this.ListIcons.Images.SetKeyName(60, "sldasm");
            this.ListIcons.Images.SetKeyName(61, "sldblk");
            this.ListIcons.Images.SetKeyName(62, "sldbomtbt");
            this.ListIcons.Images.SetKeyName(63, "slddrt");
            this.ListIcons.Images.SetKeyName(64, "slddrw");
            this.ListIcons.Images.SetKeyName(65, "sldedb");
            this.ListIcons.Images.SetKeyName(66, "sldedbold");
            this.ListIcons.Images.SetKeyName(67, "sldgtolfvt");
            this.ListIcons.Images.SetKeyName(68, "sldholtbt");
            this.ListIcons.Images.SetKeyName(69, "sldlfp");
            this.ListIcons.Images.SetKeyName(70, "sldmtnfvt");
            this.ListIcons.Images.SetKeyName(71, "sldprt");
            this.ListIcons.Images.SetKeyName(72, "sldpuntbt");
            this.ListIcons.Images.SetKeyName(73, "sldrevtbt");
            this.ListIcons.Images.SetKeyName(74, "sldsffvt");
            this.ListIcons.Images.SetKeyName(75, "sldtbt");
            this.ListIcons.Images.SetKeyName(76, "sldweldfvt");
            this.ListIcons.Images.SetKeyName(77, "sldwldtbt");
            this.ListIcons.Images.SetKeyName(78, "sln");
            this.ListIcons.Images.SetKeyName(79, "sqy");
            this.ListIcons.Images.SetKeyName(80, "suo");
            this.ListIcons.Images.SetKeyName(81, "svg");
            this.ListIcons.Images.SetKeyName(82, "swp");
            this.ListIcons.Images.SetKeyName(83, "sym");
            this.ListIcons.Images.SetKeyName(84, "tbox");
            this.ListIcons.Images.SetKeyName(85, "tif");
            this.ListIcons.Images.SetKeyName(86, "ttf");
            this.ListIcons.Images.SetKeyName(87, "txt");
            this.ListIcons.Images.SetKeyName(88, "wxm");
            this.ListIcons.Images.SetKeyName(89, "wxmx");
            this.ListIcons.Images.SetKeyName(90, "x_b");
            this.ListIcons.Images.SetKeyName(91, "x_t");
            this.ListIcons.Images.SetKeyName(92, "xls");
            this.ListIcons.Images.SetKeyName(93, "xlsx");
            this.ListIcons.Images.SetKeyName(94, "xml");
            this.ListIcons.Images.SetKeyName(95, "zip");
            this.ListIcons.Images.SetKeyName(96, "delete_image_button");
            // 
            // VersionTabs
            // 
            this.VersionTabs.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.VersionTabs.Controls.Add(this.OdooHistoryPage);
            this.VersionTabs.Controls.Add(this.OdooParentsPage);
            this.VersionTabs.Controls.Add(this.OdooChildrenPage);
            this.VersionTabs.Controls.Add(this.OdooPropertiesPage);
            this.VersionTabs.Controls.Add(this.OdooVersionPage);
            this.VersionTabs.Location = new System.Drawing.Point(13, 498);
            this.VersionTabs.Name = "VersionTabs";
            this.VersionTabs.SelectedIndex = 0;
            this.VersionTabs.Size = new System.Drawing.Size(1191, 297);
            this.VersionTabs.TabIndex = 2;
            this.VersionTabs.SelectedIndexChanged += new System.EventHandler(this.VersionTabs_SelectedIndexChanged);
            // 
            // OdooHistoryPage
            // 
            this.OdooHistoryPage.Controls.Add(this.OdooHistory);
            this.OdooHistoryPage.Location = new System.Drawing.Point(4, 22);
            this.OdooHistoryPage.Name = "OdooHistoryPage";
            this.OdooHistoryPage.Padding = new System.Windows.Forms.Padding(3);
            this.OdooHistoryPage.Size = new System.Drawing.Size(1183, 271);
            this.OdooHistoryPage.TabIndex = 0;
            this.OdooHistoryPage.Text = "History (0)";
            this.OdooHistoryPage.UseVisualStyleBackColor = true;
            this.OdooHistoryPage.Click += new System.EventHandler(this.OdooHistoryPage_Click);
            // 
            // OdooHistory
            // 
            this.OdooHistory.ContextMenuStrip = this.OdooVersionHistoryMenu;
            this.OdooHistory.Dock = System.Windows.Forms.DockStyle.Fill;
            this.OdooHistory.FullRowSelect = true;
            this.OdooHistory.HideSelection = false;
            this.OdooHistory.Location = new System.Drawing.Point(3, 3);
            this.OdooHistory.Name = "OdooHistory";
            this.OdooHistory.Size = new System.Drawing.Size(1177, 265);
            this.OdooHistory.TabIndex = 1;
            this.OdooHistory.UseCompatibleStateImageBehavior = false;
            this.OdooHistory.View = System.Windows.Forms.View.Details;
            this.OdooHistory.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.OdooHistory_ItemSelectionChanged);
            this.OdooHistory.SelectedIndexChanged += new System.EventHandler(this.OdooHistory_SelectedIndexChanged);
            this.OdooHistory.DoubleClick += new System.EventHandler(this.History_DoubleClick);
            // 
            // OdooVersionHistoryMenu
            // 
            this.OdooVersionHistoryMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.downloadToolStripMenuItem,
            this.openToolStripMenuItem,
            this.moveToolStripMenuItem});
            this.OdooVersionHistoryMenu.Name = "OdooVersionHistoryMenu";
            this.OdooVersionHistoryMenu.Size = new System.Drawing.Size(129, 70);
            this.OdooVersionHistoryMenu.Opening += new System.ComponentModel.CancelEventHandler(this.OdooVersionHistoryMenu_Opening);
            // 
            // downloadToolStripMenuItem
            // 
            this.downloadToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toTemporaryToolStripMenuItem,
            this.overwriteCurrentToolStripMenuItem1});
            this.downloadToolStripMenuItem.Name = "downloadToolStripMenuItem";
            this.downloadToolStripMenuItem.Size = new System.Drawing.Size(128, 22);
            this.downloadToolStripMenuItem.Text = "Download";
            this.downloadToolStripMenuItem.Click += new System.EventHandler(this.History_Click_Download);
            // 
            // toTemporaryToolStripMenuItem
            // 
            this.toTemporaryToolStripMenuItem.Name = "toTemporaryToolStripMenuItem";
            this.toTemporaryToolStripMenuItem.Size = new System.Drawing.Size(168, 22);
            this.toTemporaryToolStripMenuItem.Text = "To Temporary";
            this.toTemporaryToolStripMenuItem.Click += new System.EventHandler(this.History_Click_TemporaryDownload);
            // 
            // overwriteCurrentToolStripMenuItem1
            // 
            this.overwriteCurrentToolStripMenuItem1.Name = "overwriteCurrentToolStripMenuItem1";
            this.overwriteCurrentToolStripMenuItem1.Size = new System.Drawing.Size(168, 22);
            this.overwriteCurrentToolStripMenuItem1.Text = "Overwrite Current";
            this.overwriteCurrentToolStripMenuItem1.Click += new System.EventHandler(this.History_Click_OverwriteDownload);
            // 
            // openToolStripMenuItem
            // 
            this.openToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.overwriteAndOpenToolStripMenuItem,
            this.temporaryAndOpenToolStripMenuItem});
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.Size = new System.Drawing.Size(128, 22);
            this.openToolStripMenuItem.Text = "Open";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.History_Click_Open);
            // 
            // overwriteAndOpenToolStripMenuItem
            // 
            this.overwriteAndOpenToolStripMenuItem.Name = "overwriteAndOpenToolStripMenuItem";
            this.overwriteAndOpenToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.overwriteAndOpenToolStripMenuItem.Text = "Overwrite and open";
            this.overwriteAndOpenToolStripMenuItem.Click += new System.EventHandler(this.History_Click_OverwriteOpen);
            // 
            // temporaryAndOpenToolStripMenuItem
            // 
            this.temporaryAndOpenToolStripMenuItem.Name = "temporaryAndOpenToolStripMenuItem";
            this.temporaryAndOpenToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.temporaryAndOpenToolStripMenuItem.Text = "Temporary and open";
            this.temporaryAndOpenToolStripMenuItem.Click += new System.EventHandler(this.History_Click_TemporaryOpen);
            // 
            // moveToolStripMenuItem
            // 
            this.moveToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toCurrentToolStripMenuItem,
            this.toTemporaryToolStripMenuItem1});
            this.moveToolStripMenuItem.Name = "moveToolStripMenuItem";
            this.moveToolStripMenuItem.Size = new System.Drawing.Size(128, 22);
            this.moveToolStripMenuItem.Text = "Move";
            this.moveToolStripMenuItem.Click += new System.EventHandler(this.moveToolStripMenuItem_Click);
            // 
            // toCurrentToolStripMenuItem
            // 
            this.toCurrentToolStripMenuItem.Name = "toCurrentToolStripMenuItem";
            this.toCurrentToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
            this.toCurrentToolStripMenuItem.Text = "To Current";
            this.toCurrentToolStripMenuItem.Click += new System.EventHandler(this.History_Click_OverwriteMove);
            // 
            // toTemporaryToolStripMenuItem1
            // 
            this.toTemporaryToolStripMenuItem1.Name = "toTemporaryToolStripMenuItem1";
            this.toTemporaryToolStripMenuItem1.Size = new System.Drawing.Size(147, 22);
            this.toTemporaryToolStripMenuItem1.Text = "To Temporary";
            this.toTemporaryToolStripMenuItem1.Click += new System.EventHandler(this.History_Click_TemporaryMove);
            // 
            // OdooParentsPage
            // 
            this.OdooParentsPage.Controls.Add(this.OdooParents);
            this.OdooParentsPage.Location = new System.Drawing.Point(4, 22);
            this.OdooParentsPage.Name = "OdooParentsPage";
            this.OdooParentsPage.Padding = new System.Windows.Forms.Padding(3);
            this.OdooParentsPage.Size = new System.Drawing.Size(1183, 271);
            this.OdooParentsPage.TabIndex = 1;
            this.OdooParentsPage.Text = "Where Used (0)";
            this.OdooParentsPage.UseVisualStyleBackColor = true;
            this.OdooParentsPage.Click += new System.EventHandler(this.OdooParentsPage_Click);
            // 
            // OdooParents
            // 
            this.OdooParents.Dock = System.Windows.Forms.DockStyle.Fill;
            this.OdooParents.FullRowSelect = true;
            this.OdooParents.HideSelection = false;
            this.OdooParents.Location = new System.Drawing.Point(3, 3);
            this.OdooParents.Name = "OdooParents";
            this.OdooParents.Size = new System.Drawing.Size(1177, 265);
            this.OdooParents.TabIndex = 1;
            this.OdooParents.UseCompatibleStateImageBehavior = false;
            this.OdooParents.View = System.Windows.Forms.View.Details;
            this.OdooParents.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.OdooParents_ItemSelectionChanged);
            this.OdooParents.SelectedIndexChanged += new System.EventHandler(this.OdooParents_SelectedIndexChanged);
            this.OdooParents.DoubleClick += new System.EventHandler(this.OdooParents_DoubleClick);
            // 
            // OdooChildrenPage
            // 
            this.OdooChildrenPage.Controls.Add(this.OdooChildren);
            this.OdooChildrenPage.Location = new System.Drawing.Point(4, 22);
            this.OdooChildrenPage.Name = "OdooChildrenPage";
            this.OdooChildrenPage.Size = new System.Drawing.Size(1183, 271);
            this.OdooChildrenPage.TabIndex = 2;
            this.OdooChildrenPage.Text = "Dependents (0)";
            this.OdooChildrenPage.UseVisualStyleBackColor = true;
            this.OdooChildrenPage.Click += new System.EventHandler(this.OdooChildrenPage_Click);
            // 
            // OdooChildren
            // 
            this.OdooChildren.Dock = System.Windows.Forms.DockStyle.Fill;
            this.OdooChildren.FullRowSelect = true;
            this.OdooChildren.HideSelection = false;
            this.OdooChildren.Location = new System.Drawing.Point(0, 0);
            this.OdooChildren.Name = "OdooChildren";
            this.OdooChildren.Size = new System.Drawing.Size(1183, 271);
            this.OdooChildren.TabIndex = 1;
            this.OdooChildren.UseCompatibleStateImageBehavior = false;
            this.OdooChildren.View = System.Windows.Forms.View.Details;
            this.OdooChildren.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.OdooChildren_ItemSelectionChanged);
            this.OdooChildren.SelectedIndexChanged += new System.EventHandler(this.OdooChildren_SelectedIndexChanged);
            this.OdooChildren.DoubleClick += new System.EventHandler(this.OdooChildren_DoubleClick);
            // 
            // OdooPropertiesPage
            // 
            this.OdooPropertiesPage.Controls.Add(this.OdooProperties);
            this.OdooPropertiesPage.Location = new System.Drawing.Point(4, 22);
            this.OdooPropertiesPage.Name = "OdooPropertiesPage";
            this.OdooPropertiesPage.Size = new System.Drawing.Size(1183, 271);
            this.OdooPropertiesPage.TabIndex = 3;
            this.OdooPropertiesPage.Text = "Properties";
            this.OdooPropertiesPage.UseVisualStyleBackColor = true;
            this.OdooPropertiesPage.Click += new System.EventHandler(this.OdooPropertiesPage_Click);
            // 
            // OdooProperties
            // 
            this.OdooProperties.Dock = System.Windows.Forms.DockStyle.Fill;
            this.OdooProperties.FullRowSelect = true;
            this.OdooProperties.HideSelection = false;
            this.OdooProperties.Location = new System.Drawing.Point(0, 0);
            this.OdooProperties.Name = "OdooProperties";
            this.OdooProperties.Size = new System.Drawing.Size(1183, 271);
            this.OdooProperties.TabIndex = 1;
            this.OdooProperties.UseCompatibleStateImageBehavior = false;
            this.OdooProperties.View = System.Windows.Forms.View.Details;
            this.OdooProperties.SelectedIndexChanged += new System.EventHandler(this.OdooProperties_SelectedIndexChanged);
            // 
            // OdooVersionPage
            // 
            this.OdooVersionPage.Controls.Add(this.OdooVersionInfoList);
            this.OdooVersionPage.Location = new System.Drawing.Point(4, 22);
            this.OdooVersionPage.Name = "OdooVersionPage";
            this.OdooVersionPage.Size = new System.Drawing.Size(1183, 271);
            this.OdooVersionPage.TabIndex = 4;
            this.OdooVersionPage.Text = "Info";
            this.OdooVersionPage.UseVisualStyleBackColor = true;
            this.OdooVersionPage.Click += new System.EventHandler(this.OdooVersionPage_Click);
            // 
            // OdooVersionInfoList
            // 
            this.OdooVersionInfoList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.OdooVersionInfoList.FullRowSelect = true;
            this.OdooVersionInfoList.HideSelection = false;
            this.OdooVersionInfoList.Location = new System.Drawing.Point(0, 0);
            this.OdooVersionInfoList.Name = "OdooVersionInfoList";
            this.OdooVersionInfoList.Size = new System.Drawing.Size(1183, 271);
            this.OdooVersionInfoList.TabIndex = 0;
            this.OdooVersionInfoList.UseCompatibleStateImageBehavior = false;
            this.OdooVersionInfoList.View = System.Windows.Forms.View.Details;
            this.OdooVersionInfoList.SelectedIndexChanged += new System.EventHandler(this.OdooVersionInfoList_SelectedIndexChanged);
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.Controls.Add(this.OdooEntryImage);
            this.panel1.Location = new System.Drawing.Point(1210, 517);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(256, 274);
            this.panel1.TabIndex = 3;
            this.panel1.Paint += new System.Windows.Forms.PaintEventHandler(this.panel1_Paint);
            // 
            // ShowInactive
            // 
            this.ShowInactive.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.ShowInactive.AutoSize = true;
            this.ShowInactive.Location = new System.Drawing.Point(1104, 497);
            this.ShowInactive.Name = "ShowInactive";
            this.ShowInactive.Size = new System.Drawing.Size(93, 17);
            this.ShowInactive.TabIndex = 5;
            this.ShowInactive.Text = "Show Deleted";
            this.ShowInactive.UseVisualStyleBackColor = true;
            this.ShowInactive.CheckedChanged += new System.EventHandler(this.CheckedChange_Event);
            // 
            // MoreTools
            // 
            this.MoreTools.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.MoreTools.Dock = System.Windows.Forms.DockStyle.None;
            this.MoreTools.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.AdditionalTools});
            this.MoreTools.Location = new System.Drawing.Point(840, 492);
            this.MoreTools.Name = "MoreTools";
            this.MoreTools.Size = new System.Drawing.Size(41, 25);
            this.MoreTools.TabIndex = 0;
            this.MoreTools.Text = "toolStrip1";
            this.MoreTools.ItemClicked += new System.Windows.Forms.ToolStripItemClickedEventHandler(this.MoreTools_ItemClicked);
            // 
            // ListTabs
            // 
            this.ListTabs.Controls.Add(this.EntriesTab);
            this.ListTabs.Controls.Add(this.Changes);
            this.ListTabs.Location = new System.Drawing.Point(350, 0);
            this.ListTabs.Name = "ListTabs";
            this.ListTabs.SelectedIndex = 0;
            this.ListTabs.Size = new System.Drawing.Size(1116, 492);
            this.ListTabs.TabIndex = 6;
            this.ListTabs.SelectedIndexChanged += new System.EventHandler(this.ListTabs_SelectedIndexChanged);
            // 
            // EntriesTab
            // 
            this.EntriesTab.Controls.Add(this.OdooEntryList);
            this.EntriesTab.Location = new System.Drawing.Point(4, 22);
            this.EntriesTab.Name = "EntriesTab";
            this.EntriesTab.Padding = new System.Windows.Forms.Padding(3);
            this.EntriesTab.Size = new System.Drawing.Size(1108, 466);
            this.EntriesTab.TabIndex = 0;
            this.EntriesTab.Text = "Entries";
            this.EntriesTab.UseVisualStyleBackColor = true;
            this.EntriesTab.Click += new System.EventHandler(this.EntriesTab_Click);
            // 
            // Changes
            // 
            this.Changes.Controls.Add(this.HackChangesList);
            this.Changes.Location = new System.Drawing.Point(4, 22);
            this.Changes.Name = "Changes";
            this.Changes.Padding = new System.Windows.Forms.Padding(3);
            this.Changes.Size = new System.Drawing.Size(1108, 466);
            this.Changes.TabIndex = 1;
            this.Changes.Text = "Changes";
            this.Changes.UseVisualStyleBackColor = true;
            this.Changes.Click += new System.EventHandler(this.Changes_Click);
            // 
            // HackChangesList
            // 
            this.HackChangesList.AllowColumnReorder = true;
            this.HackChangesList.AllowDrop = true;
            this.HackChangesList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.HackChangesList.ContextMenuStrip = this.ListContextEntry;
            this.HackChangesList.FullRowSelect = true;
            this.HackChangesList.GridLines = true;
            this.HackChangesList.HideSelection = false;
            this.HackChangesList.Location = new System.Drawing.Point(0, -2);
            this.HackChangesList.Name = "HackChangesList";
            this.HackChangesList.Size = new System.Drawing.Size(928, 470);
            this.HackChangesList.SmallImageList = this.ListIcons;
            this.HackChangesList.Sorting = System.Windows.Forms.SortOrder.Ascending;
            this.HackChangesList.TabIndex = 2;
            this.HackChangesList.UseCompatibleStateImageBehavior = false;
            this.HackChangesList.View = System.Windows.Forms.View.Details;
            this.HackChangesList.SelectedIndexChanged += new System.EventHandler(this.HackChangesList_SelectedIndexChanged);
            // 
            // AdditionalTools
            // 
            this.AdditionalTools.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.AdditionalTools.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.OdooRefreshDropdown,
            this.OdooSearchDropdown,
            this.OdooManageTypesDropdown});
            this.AdditionalTools.Image = ((System.Drawing.Image)(resources.GetObject("AdditionalTools.Image")));
            this.AdditionalTools.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.AdditionalTools.Name = "AdditionalTools";
            this.AdditionalTools.Size = new System.Drawing.Size(29, 22);
            this.AdditionalTools.Text = "AdditionalTools";
            this.AdditionalTools.Click += new System.EventHandler(this.AdditionalTools_Click);
            // 
            // OdooRefreshDropdown
            // 
            this.OdooRefreshDropdown.Name = "OdooRefreshDropdown";
            this.OdooRefreshDropdown.Size = new System.Drawing.Size(171, 22);
            this.OdooRefreshDropdown.Text = "Refresh View";
            this.OdooRefreshDropdown.Click += new System.EventHandler(this.AdditionalTools_Click_Refresh);
            // 
            // OdooSearchDropdown
            // 
            this.OdooSearchDropdown.Name = "OdooSearchDropdown";
            this.OdooSearchDropdown.Size = new System.Drawing.Size(171, 22);
            this.OdooSearchDropdown.Text = "Search";
            this.OdooSearchDropdown.Click += new System.EventHandler(this.AdditionalTools_Click_Search);
            // 
            // OdooManageTypesDropdown
            // 
            this.OdooManageTypesDropdown.Name = "OdooManageTypesDropdown";
            this.OdooManageTypesDropdown.Size = new System.Drawing.Size(171, 22);
            this.OdooManageTypesDropdown.Text = "Manage File Types";
            this.OdooManageTypesDropdown.Click += new System.EventHandler(this.AdditionalTools_Click_ManageTypes);
            // 
            // OdooEntryImage
            // 
            this.OdooEntryImage.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.OdooEntryImage.Image = ((System.Drawing.Image)(resources.GetObject("OdooEntryImage.Image")));
            this.OdooEntryImage.InitialImage = ((System.Drawing.Image)(resources.GetObject("OdooEntryImage.InitialImage")));
            this.OdooEntryImage.Location = new System.Drawing.Point(0, 0);
            this.OdooEntryImage.MinimumSize = new System.Drawing.Size(256, 256);
            this.OdooEntryImage.Name = "OdooEntryImage";
            this.OdooEntryImage.Size = new System.Drawing.Size(256, 274);
            this.OdooEntryImage.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.OdooEntryImage.TabIndex = 2;
            this.OdooEntryImage.TabStop = false;
            this.OdooEntryImage.Click += new System.EventHandler(this.OdooEntryImage_Click);
            // 
            // HackFileManager
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(1478, 807);
            this.Controls.Add(this.ListTabs);
            this.Controls.Add(this.MoreTools);
            this.Controls.Add(this.ShowInactive);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.VersionTabs);
            this.Controls.Add(this.OdooDirectoryTree);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "HackFileManager";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.TreeContextDirectory.ResumeLayout(false);
            this.ListContextEntry.ResumeLayout(false);
            this.VersionTabs.ResumeLayout(false);
            this.OdooHistoryPage.ResumeLayout(false);
            this.OdooVersionHistoryMenu.ResumeLayout(false);
            this.OdooParentsPage.ResumeLayout(false);
            this.OdooChildrenPage.ResumeLayout(false);
            this.OdooPropertiesPage.ResumeLayout(false);
            this.OdooVersionPage.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.MoreTools.ResumeLayout(false);
            this.MoreTools.PerformLayout();
            this.ListTabs.ResumeLayout(false);
            this.EntriesTab.ResumeLayout(false);
            this.Changes.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.OdooEntryImage)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolStripMenuItem TreeGetLatest;
		private System.Windows.Forms.ToolStripMenuItem TreeGetLatestTop;
		private System.Windows.Forms.ToolStripMenuItem TreeGetLatestAll;
        private System.Windows.Forms.TreeView OdooDirectoryTree;
        private System.Windows.Forms.ListView OdooEntryList;
        private System.Windows.Forms.TabControl VersionTabs;
        private System.Windows.Forms.TabPage OdooHistoryPage;
        private System.Windows.Forms.TabPage OdooParentsPage;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.ContextMenuStrip TreeContextDirectory;
        private System.Windows.Forms.ToolStripMenuItem TreeCheckout;
        private System.Windows.Forms.ToolStripMenuItem TreeCommit;
        private System.Windows.Forms.ToolStripMenuItem TreeUndoCheckout;
        private System.Windows.Forms.ToolStripMenuItem TreeAnalyze;
        private System.Windows.Forms.ToolStripMenuItem TreeDelete;
        private System.Windows.Forms.ToolStripMenuItem TreeLogicalDelete;
        private System.Windows.Forms.ToolStripMenuItem TreePermanentDelete;
        private System.Windows.Forms.ContextMenuStrip ListContextEntry;
        private System.Windows.Forms.ToolStripMenuItem ListGetLatest;
        private System.Windows.Forms.ToolStripMenuItem ListCheckout;
        private System.Windows.Forms.ToolStripMenuItem ListCommit;
        private System.Windows.Forms.ToolStripMenuItem ListUndoCheckout;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem6;
        private System.Windows.Forms.ToolStripMenuItem OpenEntryStrip;
        private System.Windows.Forms.ToolStripMenuItem logicalDeleteToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem unDeleteToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem permanentDeleteToolStripMenuItem;
        private System.Windows.Forms.ImageList TreeIcons;
        private System.Windows.Forms.ImageList ListIcons;
        private System.Windows.Forms.PictureBox OdooEntryImage;
        private System.Windows.Forms.TabPage OdooChildrenPage;
        private System.Windows.Forms.TabPage OdooPropertiesPage;
        private System.Windows.Forms.ListView OdooHistory;
        private System.Windows.Forms.ListView OdooParents;
        private System.Windows.Forms.ListView OdooChildren;
        private System.Windows.Forms.ListView OdooProperties;
		private System.Windows.Forms.TabPage OdooVersionPage;
		private System.Windows.Forms.ListView OdooVersionInfoList;
		private System.Windows.Forms.CheckBox ShowInactive;
		private System.Windows.Forms.ToolStrip MoreTools;
		private System.Windows.Forms.ToolStripDropDownButton AdditionalTools;
		private System.Windows.Forms.ToolStripMenuItem OdooRefreshDropdown;
		private System.Windows.Forms.ToolStripMenuItem OdooSearchDropdown;
		private System.Windows.Forms.ToolStripMenuItem OdooManageTypesDropdown;
		private System.Windows.Forms.ToolStripMenuItem OpenLatestRemoteStrip;
		private System.Windows.Forms.ToolStripMenuItem OpenLatestLocalStrip;
		private System.Windows.Forms.ToolStripMenuItem fileDirectoryToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem TreeRestore;
		private System.Windows.Forms.ToolStripMenuItem TreeRestoreTop;
		private System.Windows.Forms.ToolStripMenuItem TreeRestoreAll;
		private System.Windows.Forms.ContextMenuStrip OdooVersionHistoryMenu;
		private System.Windows.Forms.ToolStripMenuItem downloadToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem toTemporaryToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem overwriteCurrentToolStripMenuItem1;
		private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem overwriteAndOpenToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem temporaryAndOpenToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem moveToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem toCurrentToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem toTemporaryToolStripMenuItem1;
		private System.Windows.Forms.ToolStripMenuItem TreeOpenDirectory;
		private System.Windows.Forms.ToolStripMenuItem TreeLocalDelete;
		private System.Windows.Forms.ToolStripMenuItem localDeleteToolStripMenuItem1;
        private System.Windows.Forms.TabControl ListTabs;
        private System.Windows.Forms.TabPage EntriesTab;
        private System.Windows.Forms.TabPage Changes;
        private System.Windows.Forms.ListView HackChangesList;
    }
}