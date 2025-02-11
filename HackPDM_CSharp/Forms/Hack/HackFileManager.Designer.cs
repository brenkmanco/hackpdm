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
			this.OdooCMSTree = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.getLatestToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.topDirectoryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.allDirectoriesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
			this.commitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.undoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.analyzeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.deleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.logicalDeleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.perminentDeleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.ilTreeIcons = new System.Windows.Forms.ImageList(this.components);
			this.OdooEntryList = new System.Windows.Forms.ListView();
			this.OdooCMSList = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.toolStripMenuItem2 = new System.Windows.Forms.ToolStripMenuItem();
			this.CheckoutStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.CommitEntryStrip = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripMenuItem5 = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripMenuItem6 = new System.Windows.Forms.ToolStripMenuItem();
			this.logicalDeleteToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
			this.unDeleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.permanentDeleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.OpenEntryStrip = new System.Windows.Forms.ToolStripMenuItem();
			this.OpenLatestRemoteStrip = new System.Windows.Forms.ToolStripMenuItem();
			this.OpenLatestLocalStrip = new System.Windows.Forms.ToolStripMenuItem();
			this.fileDirectoryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.ilListIcons = new System.Windows.Forms.ImageList(this.components);
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.OdooHistoryPage = new System.Windows.Forms.TabPage();
			this.OdooHistory = new System.Windows.Forms.ListView();
			this.OdooParentsPage = new System.Windows.Forms.TabPage();
			this.OdooParents = new System.Windows.Forms.ListView();
			this.OdooChildrenPage = new System.Windows.Forms.TabPage();
			this.OdooChildren = new System.Windows.Forms.ListView();
			this.OdooPropertiesPage = new System.Windows.Forms.TabPage();
			this.OdooProperties = new System.Windows.Forms.ListView();
			this.OdooVersionPage = new System.Windows.Forms.TabPage();
			this.OdooVersionInfoList = new System.Windows.Forms.ListView();
			this.panel1 = new System.Windows.Forms.Panel();
			this.OdooEntryImage = new System.Windows.Forms.PictureBox();
			this.OdooModelViewer = new System.Windows.Forms.Button();
			this.ShowInactive = new System.Windows.Forms.CheckBox();
			this.toolStrip1 = new System.Windows.Forms.ToolStrip();
			this.toolStripDropDownButton1 = new System.Windows.Forms.ToolStripDropDownButton();
			this.OdooRefreshDropdown = new System.Windows.Forms.ToolStripMenuItem();
			this.OdooSearchDropdown = new System.Windows.Forms.ToolStripMenuItem();
			this.OdooManageTypesDropdown = new System.Windows.Forms.ToolStripMenuItem();
			this.unDeleteToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
			this.topDirectoryToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
			this.allDirectoriesToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
			this.OdooCMSTree.SuspendLayout();
			this.OdooCMSList.SuspendLayout();
			this.tabControl1.SuspendLayout();
			this.OdooHistoryPage.SuspendLayout();
			this.OdooParentsPage.SuspendLayout();
			this.OdooChildrenPage.SuspendLayout();
			this.OdooPropertiesPage.SuspendLayout();
			this.OdooVersionPage.SuspendLayout();
			this.panel1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.OdooEntryImage)).BeginInit();
			this.toolStrip1.SuspendLayout();
			this.SuspendLayout();
			// 
			// OdooDirectoryTree
			// 
			this.OdooDirectoryTree.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
			this.OdooDirectoryTree.ContextMenuStrip = this.OdooCMSTree;
			this.OdooDirectoryTree.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.OdooDirectoryTree.ImageIndex = 0;
			this.OdooDirectoryTree.ImageList = this.ilTreeIcons;
			this.OdooDirectoryTree.Location = new System.Drawing.Point(13, 13);
			this.OdooDirectoryTree.Name = "OdooDirectoryTree";
			this.OdooDirectoryTree.SelectedImageIndex = 0;
			this.OdooDirectoryTree.Size = new System.Drawing.Size(321, 479);
			this.OdooDirectoryTree.TabIndex = 0;
			this.OdooDirectoryTree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.OdooDirectoryTree_AfterSelect);
			// 
			// OdooCMSTree
			// 
			this.OdooCMSTree.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.getLatestToolStripMenuItem,
            this.toolStripMenuItem1,
            this.commitToolStripMenuItem,
            this.undoToolStripMenuItem,
            this.analyzeToolStripMenuItem,
            this.deleteToolStripMenuItem});
			this.OdooCMSTree.Name = "contextMenuStrip1";
			this.OdooCMSTree.Size = new System.Drawing.Size(181, 158);
			// 
			// getLatestToolStripMenuItem
			// 
			this.getLatestToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.topDirectoryToolStripMenuItem,
            this.allDirectoriesToolStripMenuItem});
			this.getLatestToolStripMenuItem.Name = "getLatestToolStripMenuItem";
			this.getLatestToolStripMenuItem.Size = new System.Drawing.Size(157, 22);
			this.getLatestToolStripMenuItem.Text = "Get Latest";
			this.getLatestToolStripMenuItem.Click += new System.EventHandler(this.GetLatestStrip_Click);
			// 
			// topDirectoryToolStripMenuItem
			// 
			this.topDirectoryToolStripMenuItem.Name = "topDirectoryToolStripMenuItem";
			this.topDirectoryToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
			this.topDirectoryToolStripMenuItem.Text = "Top Directory";
			this.topDirectoryToolStripMenuItem.Click += new System.EventHandler(this.topDirectoryToolStripMenuItem_Click);
			// 
			// allDirectoriesToolStripMenuItem
			// 
			this.allDirectoriesToolStripMenuItem.Name = "allDirectoriesToolStripMenuItem";
			this.allDirectoriesToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
			this.allDirectoriesToolStripMenuItem.Text = "All Directories";
			this.allDirectoriesToolStripMenuItem.Click += new System.EventHandler(this.allDirectoriesToolStripMenuItem_Click);
			// 
			// toolStripMenuItem1
			// 
			this.toolStripMenuItem1.Name = "toolStripMenuItem1";
			this.toolStripMenuItem1.Size = new System.Drawing.Size(157, 22);
			this.toolStripMenuItem1.Text = "Checkout";
			this.toolStripMenuItem1.Click += new System.EventHandler(this.CheckoutTreeStrip_Click);
			// 
			// commitToolStripMenuItem
			// 
			this.commitToolStripMenuItem.Name = "commitToolStripMenuItem";
			this.commitToolStripMenuItem.Size = new System.Drawing.Size(157, 22);
			this.commitToolStripMenuItem.Text = "Commit";
			this.commitToolStripMenuItem.Click += new System.EventHandler(this.CommitTreeStrip_Click);
			// 
			// undoToolStripMenuItem
			// 
			this.undoToolStripMenuItem.Name = "undoToolStripMenuItem";
			this.undoToolStripMenuItem.Size = new System.Drawing.Size(157, 22);
			this.undoToolStripMenuItem.Text = "Undo Checkout";
			this.undoToolStripMenuItem.Click += new System.EventHandler(this.UnCheckoutTreeStrip_Click);
			// 
			// analyzeToolStripMenuItem
			// 
			this.analyzeToolStripMenuItem.Name = "analyzeToolStripMenuItem";
			this.analyzeToolStripMenuItem.Size = new System.Drawing.Size(157, 22);
			this.analyzeToolStripMenuItem.Text = "Analyze";
			// 
			// deleteToolStripMenuItem
			// 
			this.deleteToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.logicalDeleteToolStripMenuItem,
            this.perminentDeleteToolStripMenuItem,
            this.unDeleteToolStripMenuItem1});
			this.deleteToolStripMenuItem.Name = "deleteToolStripMenuItem";
			this.deleteToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
			this.deleteToolStripMenuItem.Text = "Delete";
			// 
			// logicalDeleteToolStripMenuItem
			// 
			this.logicalDeleteToolStripMenuItem.Name = "logicalDeleteToolStripMenuItem";
			this.logicalDeleteToolStripMenuItem.Size = new System.Drawing.Size(168, 22);
			this.logicalDeleteToolStripMenuItem.Text = "Logical Delete";
			this.logicalDeleteToolStripMenuItem.Click += new System.EventHandler(this.LogicalDeleteTreeStrip_Click);
			// 
			// perminentDeleteToolStripMenuItem
			// 
			this.perminentDeleteToolStripMenuItem.Name = "perminentDeleteToolStripMenuItem";
			this.perminentDeleteToolStripMenuItem.Size = new System.Drawing.Size(168, 22);
			this.perminentDeleteToolStripMenuItem.Text = "Permanent Delete";
			// 
			// ilTreeIcons
			// 
			this.ilTreeIcons.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("ilTreeIcons.ImageStream")));
			this.ilTreeIcons.TransparentColor = System.Drawing.Color.Transparent;
			this.ilTreeIcons.Images.SetKeyName(0, "simple-folder-icon_32.gif");
			this.ilTreeIcons.Images.SetKeyName(1, "folder-icon_localonly_32.gif");
			this.ilTreeIcons.Images.SetKeyName(2, "folder-icon_remoteonly_32.gif");
			this.ilTreeIcons.Images.SetKeyName(3, "folder-icon_checkedme_32.gif");
			this.ilTreeIcons.Images.SetKeyName(4, "folder-icon_checkedother_32.gif");
			this.ilTreeIcons.Images.SetKeyName(5, "folder-icon_deleted_32.gif");
			// 
			// OdooEntryList
			// 
			this.OdooEntryList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.OdooEntryList.ContextMenuStrip = this.OdooCMSList;
			this.OdooEntryList.FullRowSelect = true;
			this.OdooEntryList.HideSelection = false;
			this.OdooEntryList.Location = new System.Drawing.Point(350, 13);
			this.OdooEntryList.Name = "OdooEntryList";
			this.OdooEntryList.Size = new System.Drawing.Size(1116, 479);
			this.OdooEntryList.SmallImageList = this.ilListIcons;
			this.OdooEntryList.Sorting = System.Windows.Forms.SortOrder.Ascending;
			this.OdooEntryList.TabIndex = 1;
			this.OdooEntryList.UseCompatibleStateImageBehavior = false;
			this.OdooEntryList.View = System.Windows.Forms.View.Details;
			this.OdooEntryList.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.OdooEntryList_ItemSelectionChanged);
			// 
			// OdooCMSList
			// 
			this.OdooCMSList.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItem2,
            this.CheckoutStripMenuItem,
            this.CommitEntryStrip,
            this.toolStripMenuItem5,
            this.toolStripMenuItem6,
            this.OpenEntryStrip});
			this.OdooCMSList.Name = "contextMenuStrip1";
			this.OdooCMSList.Size = new System.Drawing.Size(158, 136);
			// 
			// toolStripMenuItem2
			// 
			this.toolStripMenuItem2.Name = "toolStripMenuItem2";
			this.toolStripMenuItem2.Size = new System.Drawing.Size(157, 22);
			this.toolStripMenuItem2.Text = "Get Latest";
			this.toolStripMenuItem2.ToolTipText = "Checks to see if you have the same checksum as any of the version records and if " +
    "not then it will try to download it to the specific local directory";
			this.toolStripMenuItem2.Click += new System.EventHandler(this.GetLatestEntryStrip_Click);
			// 
			// CheckoutStripMenuItem
			// 
			this.CheckoutStripMenuItem.Name = "CheckoutStripMenuItem";
			this.CheckoutStripMenuItem.Size = new System.Drawing.Size(157, 22);
			this.CheckoutStripMenuItem.Text = "Checkout";
			this.CheckoutStripMenuItem.Click += new System.EventHandler(this.CheckoutEntryStrip_Click);
			// 
			// CommitEntryStrip
			// 
			this.CommitEntryStrip.Name = "CommitEntryStrip";
			this.CommitEntryStrip.Size = new System.Drawing.Size(157, 22);
			this.CommitEntryStrip.Text = "Commit";
			this.CommitEntryStrip.ToolTipText = "Checks to see if the version has the same checksum in the versions records and if" +
    " not then it will try to commit the file to Odoo to store.";
			this.CommitEntryStrip.Click += new System.EventHandler(this.CommitEntryStrip_Click);
			// 
			// toolStripMenuItem5
			// 
			this.toolStripMenuItem5.Name = "toolStripMenuItem5";
			this.toolStripMenuItem5.Size = new System.Drawing.Size(157, 22);
			this.toolStripMenuItem5.Text = "Undo Checkout";
			this.toolStripMenuItem5.Click += new System.EventHandler(this.UnCheckoutEntryStrip_Click);
			// 
			// toolStripMenuItem6
			// 
			this.toolStripMenuItem6.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.logicalDeleteToolStripMenuItem1,
            this.unDeleteToolStripMenuItem,
            this.permanentDeleteToolStripMenuItem});
			this.toolStripMenuItem6.Name = "toolStripMenuItem6";
			this.toolStripMenuItem6.Size = new System.Drawing.Size(180, 22);
			this.toolStripMenuItem6.Text = "Delete";
			// 
			// logicalDeleteToolStripMenuItem1
			// 
			this.logicalDeleteToolStripMenuItem1.Name = "logicalDeleteToolStripMenuItem1";
			this.logicalDeleteToolStripMenuItem1.Size = new System.Drawing.Size(180, 22);
			this.logicalDeleteToolStripMenuItem1.Text = "Logical Delete";
			this.logicalDeleteToolStripMenuItem1.ToolTipText = "sets the Entry to inactive in Odoo";
			this.logicalDeleteToolStripMenuItem1.Click += new System.EventHandler(this.LogicalDeleteEntryStrip_Click);
			// 
			// unDeleteToolStripMenuItem
			// 
			this.unDeleteToolStripMenuItem.Name = "unDeleteToolStripMenuItem";
			this.unDeleteToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
			this.unDeleteToolStripMenuItem.Text = "UnDelete";
			this.unDeleteToolStripMenuItem.Click += new System.EventHandler(this.unDeleteToolStripMenuItem_Click);
			// 
			// permanentDeleteToolStripMenuItem
			// 
			this.permanentDeleteToolStripMenuItem.Name = "permanentDeleteToolStripMenuItem";
			this.permanentDeleteToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
			this.permanentDeleteToolStripMenuItem.Text = "Permanent Delete";
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
			this.OpenEntryStrip.Click += new System.EventHandler(this.OpenEntryStrip_Click);
			// 
			// OpenLatestRemoteStrip
			// 
			this.OpenLatestRemoteStrip.AutoToolTip = true;
			this.OpenLatestRemoteStrip.Name = "OpenLatestRemoteStrip";
			this.OpenLatestRemoteStrip.Size = new System.Drawing.Size(193, 22);
			this.OpenLatestRemoteStrip.Text = "Preview Latest Remote";
			this.OpenLatestRemoteStrip.ToolTipText = "Downloads the version file and puts it in the temporary folder path";
			this.OpenLatestRemoteStrip.Click += new System.EventHandler(this.OpenLatestRemoteStrip_Click);
			// 
			// OpenLatestLocalStrip
			// 
			this.OpenLatestLocalStrip.Name = "OpenLatestLocalStrip";
			this.OpenLatestLocalStrip.Size = new System.Drawing.Size(193, 22);
			this.OpenLatestLocalStrip.Text = "Latest Local";
			this.OpenLatestLocalStrip.ToolTipText = "Opens the latest local file";
			this.OpenLatestLocalStrip.Click += new System.EventHandler(this.OpenLatestLocalStrip_Click);
			// 
			// fileDirectoryToolStripMenuItem
			// 
			this.fileDirectoryToolStripMenuItem.Name = "fileDirectoryToolStripMenuItem";
			this.fileDirectoryToolStripMenuItem.Size = new System.Drawing.Size(193, 22);
			this.fileDirectoryToolStripMenuItem.Text = "File Directory";
			this.fileDirectoryToolStripMenuItem.ToolTipText = "Open file explorer to the parent folder";
			this.fileDirectoryToolStripMenuItem.Click += new System.EventHandler(this.fileDirectoryToolStripMenuItem_Click);
			// 
			// ilListIcons
			// 
			this.ilListIcons.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("ilListIcons.ImageStream")));
			this.ilListIcons.TransparentColor = System.Drawing.Color.Transparent;
			this.ilListIcons.Images.SetKeyName(0, "cm");
			this.ilListIcons.Images.SetKeyName(1, "co");
			this.ilListIcons.Images.SetKeyName(2, "lo");
			this.ilListIcons.Images.SetKeyName(3, "ro");
			this.ilListIcons.Images.SetKeyName(4, "ft");
			this.ilListIcons.Images.SetKeyName(5, "nv");
			this.ilListIcons.Images.SetKeyName(6, "lm");
			this.ilListIcons.Images.SetKeyName(7, "dt");
			this.ilListIcons.Images.SetKeyName(8, "if");
			this.ilListIcons.Images.SetKeyName(9, "ds");
			this.ilListIcons.Images.SetKeyName(10, "default");
			// 
			// tabControl1
			// 
			this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.tabControl1.Controls.Add(this.OdooHistoryPage);
			this.tabControl1.Controls.Add(this.OdooParentsPage);
			this.tabControl1.Controls.Add(this.OdooChildrenPage);
			this.tabControl1.Controls.Add(this.OdooPropertiesPage);
			this.tabControl1.Controls.Add(this.OdooVersionPage);
			this.tabControl1.Location = new System.Drawing.Point(13, 498);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(1191, 297);
			this.tabControl1.TabIndex = 2;
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
			// 
			// OdooHistory
			// 
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
			// 
			// panel1
			// 
			this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.panel1.Controls.Add(this.OdooEntryImage);
			this.panel1.Location = new System.Drawing.Point(1210, 517);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(256, 274);
			this.panel1.TabIndex = 3;
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
			// 
			// OdooModelViewer
			// 
			this.OdooModelViewer.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.OdooModelViewer.Location = new System.Drawing.Point(976, 494);
			this.OdooModelViewer.Name = "OdooModelViewer";
			this.OdooModelViewer.Size = new System.Drawing.Size(221, 23);
			this.OdooModelViewer.TabIndex = 4;
			this.OdooModelViewer.Text = "Odoo Model Viewer";
			this.OdooModelViewer.UseVisualStyleBackColor = true;
			this.OdooModelViewer.Click += new System.EventHandler(this.OdooModelViewer_Click);
			// 
			// ShowInactive
			// 
			this.ShowInactive.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.ShowInactive.AutoSize = true;
			this.ShowInactive.Location = new System.Drawing.Point(884, 497);
			this.ShowInactive.Name = "ShowInactive";
			this.ShowInactive.Size = new System.Drawing.Size(93, 17);
			this.ShowInactive.TabIndex = 5;
			this.ShowInactive.Text = "Show Deleted";
			this.ShowInactive.UseVisualStyleBackColor = true;
			this.ShowInactive.CheckedChanged += new System.EventHandler(this.CheckedChange_Event);
			// 
			// toolStrip1
			// 
			this.toolStrip1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.toolStrip1.Dock = System.Windows.Forms.DockStyle.None;
			this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripDropDownButton1});
			this.toolStrip1.Location = new System.Drawing.Point(840, 492);
			this.toolStrip1.Name = "toolStrip1";
			this.toolStrip1.Size = new System.Drawing.Size(41, 25);
			this.toolStrip1.TabIndex = 0;
			this.toolStrip1.Text = "toolStrip1";
			// 
			// toolStripDropDownButton1
			// 
			this.toolStripDropDownButton1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.toolStripDropDownButton1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.OdooRefreshDropdown,
            this.OdooSearchDropdown,
            this.OdooManageTypesDropdown});
			this.toolStripDropDownButton1.Image = ((System.Drawing.Image)(resources.GetObject("toolStripDropDownButton1.Image")));
			this.toolStripDropDownButton1.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.toolStripDropDownButton1.Name = "toolStripDropDownButton1";
			this.toolStripDropDownButton1.Size = new System.Drawing.Size(29, 22);
			this.toolStripDropDownButton1.Text = "toolStripDropDownButton1";
			// 
			// OdooRefreshDropdown
			// 
			this.OdooRefreshDropdown.Name = "OdooRefreshDropdown";
			this.OdooRefreshDropdown.Size = new System.Drawing.Size(170, 22);
			this.OdooRefreshDropdown.Text = "Refresh View";
			this.OdooRefreshDropdown.Click += new System.EventHandler(this.OdooRefreshDropdown_Click);
			// 
			// OdooSearchDropdown
			// 
			this.OdooSearchDropdown.Name = "OdooSearchDropdown";
			this.OdooSearchDropdown.Size = new System.Drawing.Size(170, 22);
			this.OdooSearchDropdown.Text = "Search";
			this.OdooSearchDropdown.Click += new System.EventHandler(this.OdooSearchDropdown_Click);
			// 
			// OdooManageTypesDropdown
			// 
			this.OdooManageTypesDropdown.Name = "OdooManageTypesDropdown";
			this.OdooManageTypesDropdown.Size = new System.Drawing.Size(170, 22);
			this.OdooManageTypesDropdown.Text = "Manage File Types";
			this.OdooManageTypesDropdown.Click += new System.EventHandler(this.OdooManageTypesDropdown_Click);
			// 
			// unDeleteToolStripMenuItem1
			// 
			this.unDeleteToolStripMenuItem1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.topDirectoryToolStripMenuItem1,
            this.allDirectoriesToolStripMenuItem1});
			this.unDeleteToolStripMenuItem1.Name = "unDeleteToolStripMenuItem1";
			this.unDeleteToolStripMenuItem1.Size = new System.Drawing.Size(180, 22);
			this.unDeleteToolStripMenuItem1.Text = "UnDelete";
			this.unDeleteToolStripMenuItem1.Click += new System.EventHandler(this.unDeleteToolStripMenuItem1_Click);
			// 
			// topDirectoryToolStripMenuItem1
			// 
			this.topDirectoryToolStripMenuItem1.Name = "topDirectoryToolStripMenuItem1";
			this.topDirectoryToolStripMenuItem1.Size = new System.Drawing.Size(180, 22);
			this.topDirectoryToolStripMenuItem1.Text = "Top Directory";
			this.topDirectoryToolStripMenuItem1.Click += new System.EventHandler(this.topDirectoryToolStripMenuItem1_Click);
			// 
			// allDirectoriesToolStripMenuItem1
			// 
			this.allDirectoriesToolStripMenuItem1.Name = "allDirectoriesToolStripMenuItem1";
			this.allDirectoriesToolStripMenuItem1.Size = new System.Drawing.Size(180, 22);
			this.allDirectoriesToolStripMenuItem1.Text = "All Directories";
			this.allDirectoriesToolStripMenuItem1.Click += new System.EventHandler(this.allDirectoriesToolStripMenuItem1_Click);
			// 
			// HackFileManager
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(1478, 807);
			this.Controls.Add(this.toolStrip1);
			this.Controls.Add(this.ShowInactive);
			this.Controls.Add(this.OdooModelViewer);
			this.Controls.Add(this.panel1);
			this.Controls.Add(this.tabControl1);
			this.Controls.Add(this.OdooEntryList);
			this.Controls.Add(this.OdooDirectoryTree);
			this.Name = "HackFileManager";
			this.Text = "Odoo File Directory";
			this.OdooCMSTree.ResumeLayout(false);
			this.OdooCMSList.ResumeLayout(false);
			this.tabControl1.ResumeLayout(false);
			this.OdooHistoryPage.ResumeLayout(false);
			this.OdooParentsPage.ResumeLayout(false);
			this.OdooChildrenPage.ResumeLayout(false);
			this.OdooPropertiesPage.ResumeLayout(false);
			this.OdooVersionPage.ResumeLayout(false);
			this.panel1.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.OdooEntryImage)).EndInit();
			this.toolStrip1.ResumeLayout(false);
			this.toolStrip1.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TreeView OdooDirectoryTree;
        private System.Windows.Forms.ListView OdooEntryList;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage OdooHistoryPage;
        private System.Windows.Forms.TabPage OdooParentsPage;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.ContextMenuStrip OdooCMSTree;
        private System.Windows.Forms.ToolStripMenuItem getLatestToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem commitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem undoToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem analyzeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem deleteToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem logicalDeleteToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem perminentDeleteToolStripMenuItem;
        private System.Windows.Forms.ContextMenuStrip OdooCMSList;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem2;
        private System.Windows.Forms.ToolStripMenuItem CheckoutStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem CommitEntryStrip;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem5;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem6;
        private System.Windows.Forms.ToolStripMenuItem OpenEntryStrip;
        private System.Windows.Forms.ToolStripMenuItem logicalDeleteToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem unDeleteToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem permanentDeleteToolStripMenuItem;
        private System.Windows.Forms.ImageList ilTreeIcons;
        private System.Windows.Forms.ImageList ilListIcons;
        private System.Windows.Forms.PictureBox OdooEntryImage;
        private System.Windows.Forms.TabPage OdooChildrenPage;
        private System.Windows.Forms.TabPage OdooPropertiesPage;
        private System.Windows.Forms.ListView OdooHistory;
        private System.Windows.Forms.ListView OdooParents;
        private System.Windows.Forms.ListView OdooChildren;
        private System.Windows.Forms.ListView OdooProperties;
        private System.Windows.Forms.Button OdooModelViewer;
		private System.Windows.Forms.TabPage OdooVersionPage;
		private System.Windows.Forms.ListView OdooVersionInfoList;
		private System.Windows.Forms.CheckBox ShowInactive;
		private System.Windows.Forms.ToolStrip toolStrip1;
		private System.Windows.Forms.ToolStripDropDownButton toolStripDropDownButton1;
		private System.Windows.Forms.ToolStripMenuItem OdooRefreshDropdown;
		private System.Windows.Forms.ToolStripMenuItem OdooSearchDropdown;
		private System.Windows.Forms.ToolStripMenuItem OdooManageTypesDropdown;
		private System.Windows.Forms.ToolStripMenuItem OpenLatestRemoteStrip;
		private System.Windows.Forms.ToolStripMenuItem OpenLatestLocalStrip;
		private System.Windows.Forms.ToolStripMenuItem topDirectoryToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem allDirectoriesToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem fileDirectoryToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem unDeleteToolStripMenuItem1;
		private System.Windows.Forms.ToolStripMenuItem topDirectoryToolStripMenuItem1;
		private System.Windows.Forms.ToolStripMenuItem allDirectoriesToolStripMenuItem1;
	}
}