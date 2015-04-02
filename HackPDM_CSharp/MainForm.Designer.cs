/*
 * Created by SharpDevelop.
 * User: matt
 * Date: 9/29/2012
 * Time: 5:34 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
namespace HackPDM
{
	partial class MainForm
	{
		/// <summary>
		/// Designer variable used to keep track of non-visual components.
		/// </summary>
		private System.ComponentModel.IContainer components = null;
		
		/// <summary>
		/// Disposes resources used by the form.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing) {
				if (components != null) {
					components.Dispose();
				}
			}
			base.Dispose(disposing);
		}
		
		/// <summary>
		/// This method is required for Windows Forms designer support.
		/// Do not change the method contents inside the source code editor. The Forms designer might
		/// not be able to load this method if it was changed manually.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
			this.cmsTree = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.cmsTreeGetLatest = new System.Windows.Forms.ToolStripMenuItem();
			this.cmsTreeCheckout = new System.Windows.Forms.ToolStripMenuItem();
			this.cmsTreeCommit = new System.Windows.Forms.ToolStripMenuItem();
			this.cmsTreeUndoCheckout = new System.Windows.Forms.ToolStripMenuItem();
			this.cmsTreeAnalyze = new System.Windows.Forms.ToolStripMenuItem();
			this.ilTreeIcons = new System.Windows.Forms.ImageList(this.components);
			this.cmsList = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.cmsListGetLatest = new System.Windows.Forms.ToolStripMenuItem();
			this.cmsListCheckOut = new System.Windows.Forms.ToolStripMenuItem();
			this.cmsListCommit = new System.Windows.Forms.ToolStripMenuItem();
			this.cmsListUndoCheckout = new System.Windows.Forms.ToolStripMenuItem();
			this.ilListIcons = new System.Windows.Forms.ImageList(this.components);
			this.statusStrip1 = new System.Windows.Forms.StatusStrip();
			this.toolStripDropDownButton1 = new System.Windows.Forms.ToolStripDropDownButton();
			this.cmdRefreshView = new System.Windows.Forms.ToolStripMenuItem();
			this.cmdManageFileTypes = new System.Windows.Forms.ToolStripMenuItem();
			this.splitContainer1 = new System.Windows.Forms.SplitContainer();
			this.splitContainer2 = new System.Windows.Forms.SplitContainer();
			this.treeView1 = new System.Windows.Forms.TreeView();
			this.listView1 = new System.Windows.Forms.ListView();
			this.pbPreview = new System.Windows.Forms.PictureBox();
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.lvHistory = new System.Windows.Forms.ListView();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.lvParents = new System.Windows.Forms.ListView();
			this.tabPage3 = new System.Windows.Forms.TabPage();
			this.lvChildren = new System.Windows.Forms.ListView();
			this.tabPage4 = new System.Windows.Forms.TabPage();
			this.lvProperties = new System.Windows.Forms.ListView();
			this.logicalDeleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.logicalDeleteToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
			this.permanentDeleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.cmsTree.SuspendLayout();
			this.cmsList.SuspendLayout();
			this.statusStrip1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
			this.splitContainer1.Panel1.SuspendLayout();
			this.splitContainer1.Panel2.SuspendLayout();
			this.splitContainer1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
			this.splitContainer2.Panel1.SuspendLayout();
			this.splitContainer2.Panel2.SuspendLayout();
			this.splitContainer2.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.pbPreview)).BeginInit();
			this.tabControl1.SuspendLayout();
			this.tabPage1.SuspendLayout();
			this.tabPage2.SuspendLayout();
			this.tabPage3.SuspendLayout();
			this.tabPage4.SuspendLayout();
			this.SuspendLayout();
			// 
			// cmsTree
			// 
			this.cmsTree.ImageScalingSize = new System.Drawing.Size(20, 20);
			this.cmsTree.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.cmsTreeGetLatest,
            this.cmsTreeCheckout,
            this.cmsTreeCommit,
            this.cmsTreeUndoCheckout,
            this.cmsTreeAnalyze});
			this.cmsTree.Name = "cmsTreeMenu";
			this.cmsTree.ShowImageMargin = false;
			this.cmsTree.Size = new System.Drawing.Size(133, 114);
			// 
			// cmsTreeGetLatest
			// 
			this.cmsTreeGetLatest.Enabled = false;
			this.cmsTreeGetLatest.Name = "cmsTreeGetLatest";
			this.cmsTreeGetLatest.Size = new System.Drawing.Size(132, 22);
			this.cmsTreeGetLatest.Text = "Get Latest";
			this.cmsTreeGetLatest.Click += new System.EventHandler(this.CmsTreeGetLatestClick);
			// 
			// cmsTreeCheckout
			// 
			this.cmsTreeCheckout.Enabled = false;
			this.cmsTreeCheckout.Name = "cmsTreeCheckout";
			this.cmsTreeCheckout.Size = new System.Drawing.Size(132, 22);
			this.cmsTreeCheckout.Text = "Checkout";
			this.cmsTreeCheckout.Click += new System.EventHandler(this.CmsTreeCheckoutClick);
			// 
			// cmsTreeCommit
			// 
			this.cmsTreeCommit.Enabled = false;
			this.cmsTreeCommit.Name = "cmsTreeCommit";
			this.cmsTreeCommit.Size = new System.Drawing.Size(132, 22);
			this.cmsTreeCommit.Text = "Commit";
			this.cmsTreeCommit.Click += new System.EventHandler(this.CmsTreeCommitClick);
			// 
			// cmsTreeUndoCheckout
			// 
			this.cmsTreeUndoCheckout.Enabled = false;
			this.cmsTreeUndoCheckout.Name = "cmsTreeUndoCheckout";
			this.cmsTreeUndoCheckout.Size = new System.Drawing.Size(132, 22);
			this.cmsTreeUndoCheckout.Text = "Undo Checkout";
			this.cmsTreeUndoCheckout.Click += new System.EventHandler(this.CmsTreeUndoCheckoutClick);
			// 
			// cmsTreeAnalyze
			// 
			this.cmsTreeAnalyze.Enabled = false;
			this.cmsTreeAnalyze.Name = "cmsTreeAnalyze";
			this.cmsTreeAnalyze.Size = new System.Drawing.Size(132, 22);
			this.cmsTreeAnalyze.Text = "Analyze";
			this.cmsTreeAnalyze.Click += new System.EventHandler(this.CmsTreeAnalyzeClick);
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
			// 
			// cmsList
			// 
			this.cmsList.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.cmsListGetLatest,
            this.cmsListCheckOut,
            this.cmsListCommit,
            this.cmsListUndoCheckout,
            this.logicalDeleteToolStripMenuItem});
			this.cmsList.Name = "cmsTreeMenu";
			this.cmsList.ShowImageMargin = false;
			this.cmsList.Size = new System.Drawing.Size(133, 136);
			// 
			// cmsListGetLatest
			// 
			this.cmsListGetLatest.Enabled = false;
			this.cmsListGetLatest.Name = "cmsListGetLatest";
			this.cmsListGetLatest.Size = new System.Drawing.Size(132, 22);
			this.cmsListGetLatest.Text = "Get Latest";
			this.cmsListGetLatest.Click += new System.EventHandler(this.CmsListGetLatestClick);
			// 
			// cmsListCheckOut
			// 
			this.cmsListCheckOut.Enabled = false;
			this.cmsListCheckOut.Name = "cmsListCheckOut";
			this.cmsListCheckOut.Size = new System.Drawing.Size(132, 22);
			this.cmsListCheckOut.Text = "Checkout";
			this.cmsListCheckOut.Click += new System.EventHandler(this.CmsListCheckOutClick);
			// 
			// cmsListCommit
			// 
			this.cmsListCommit.Enabled = false;
			this.cmsListCommit.Name = "cmsListCommit";
			this.cmsListCommit.Size = new System.Drawing.Size(132, 22);
			this.cmsListCommit.Text = "Commit";
			this.cmsListCommit.Click += new System.EventHandler(this.CmsListCommitClick);
			// 
			// cmsListUndoCheckout
			// 
			this.cmsListUndoCheckout.Enabled = false;
			this.cmsListUndoCheckout.Name = "cmsListUndoCheckout";
			this.cmsListUndoCheckout.Size = new System.Drawing.Size(132, 22);
			this.cmsListUndoCheckout.Text = "Undo Checkout";
			this.cmsListUndoCheckout.Click += new System.EventHandler(this.CmsListUndoCheckoutClick);
			// 
			// ilListIcons
			// 
			this.ilListIcons.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("ilListIcons.ImageStream")));
			this.ilListIcons.TransparentColor = System.Drawing.Color.Transparent;
			this.ilListIcons.Images.SetKeyName(0, "cm");
			this.ilListIcons.Images.SetKeyName(1, "co");
			this.ilListIcons.Images.SetKeyName(2, "lo");
			this.ilListIcons.Images.SetKeyName(3, "ro");
			this.ilListIcons.Images.SetKeyName(4, "if");
			this.ilListIcons.Images.SetKeyName(5, "ft");
			this.ilListIcons.Images.SetKeyName(6, "nv");
			this.ilListIcons.Images.SetKeyName(7, "lm");
			// 
			// statusStrip1
			// 
			this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripDropDownButton1});
			this.statusStrip1.Location = new System.Drawing.Point(0, 596);
			this.statusStrip1.Name = "statusStrip1";
			this.statusStrip1.Size = new System.Drawing.Size(884, 22);
			this.statusStrip1.TabIndex = 2;
			this.statusStrip1.Text = "statusStrip1";
			// 
			// toolStripDropDownButton1
			// 
			this.toolStripDropDownButton1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.toolStripDropDownButton1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.cmdRefreshView,
            this.cmdManageFileTypes});
			this.toolStripDropDownButton1.Image = ((System.Drawing.Image)(resources.GetObject("toolStripDropDownButton1.Image")));
			this.toolStripDropDownButton1.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.toolStripDropDownButton1.Name = "toolStripDropDownButton1";
			this.toolStripDropDownButton1.Size = new System.Drawing.Size(29, 20);
			this.toolStripDropDownButton1.Text = "toolStripDropDownButton1";
			// 
			// cmdRefreshView
			// 
			this.cmdRefreshView.Name = "cmdRefreshView";
			this.cmdRefreshView.Size = new System.Drawing.Size(172, 22);
			this.cmdRefreshView.Text = "Refresh View";
			this.cmdRefreshView.Click += new System.EventHandler(this.CmdRefreshViewClick);
			// 
			// cmdManageFileTypes
			// 
			this.cmdManageFileTypes.Name = "cmdManageFileTypes";
			this.cmdManageFileTypes.Size = new System.Drawing.Size(172, 22);
			this.cmdManageFileTypes.Text = "Manage File Types";
			this.cmdManageFileTypes.Click += new System.EventHandler(this.CmdManageFileTypesClick);
			// 
			// splitContainer1
			// 
			this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.splitContainer1.Location = new System.Drawing.Point(0, 0);
			this.splitContainer1.Name = "splitContainer1";
			this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
			// 
			// splitContainer1.Panel1
			// 
			this.splitContainer1.Panel1.Controls.Add(this.splitContainer2);
			// 
			// splitContainer1.Panel2
			// 
			this.splitContainer1.Panel2.Controls.Add(this.pbPreview);
			this.splitContainer1.Panel2.Controls.Add(this.tabControl1);
			this.splitContainer1.Panel2MinSize = 271;
			this.splitContainer1.Size = new System.Drawing.Size(884, 596);
			this.splitContainer1.SplitterDistance = 325;
			this.splitContainer1.TabIndex = 3;
			// 
			// splitContainer2
			// 
			this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
			this.splitContainer2.Location = new System.Drawing.Point(0, 0);
			this.splitContainer2.Name = "splitContainer2";
			// 
			// splitContainer2.Panel1
			// 
			this.splitContainer2.Panel1.Controls.Add(this.treeView1);
			// 
			// splitContainer2.Panel2
			// 
			this.splitContainer2.Panel2.Controls.Add(this.listView1);
			this.splitContainer2.Size = new System.Drawing.Size(884, 325);
			this.splitContainer2.SplitterDistance = 267;
			this.splitContainer2.TabIndex = 0;
			// 
			// treeView1
			// 
			this.treeView1.ContextMenuStrip = this.cmsTree;
			this.treeView1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.treeView1.FullRowSelect = true;
			this.treeView1.HideSelection = false;
			this.treeView1.ImageIndex = 0;
			this.treeView1.ImageList = this.ilTreeIcons;
			this.treeView1.Location = new System.Drawing.Point(0, 0);
			this.treeView1.Name = "treeView1";
			this.treeView1.SelectedImageIndex = 0;
			this.treeView1.Size = new System.Drawing.Size(267, 325);
			this.treeView1.TabIndex = 0;
			this.treeView1.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.TreeView1AfterSelect);
			this.treeView1.MouseClick += new System.Windows.Forms.MouseEventHandler(this.TreeRightMouseClick);
			// 
			// listView1
			// 
			this.listView1.ContextMenuStrip = this.cmsList;
			this.listView1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.listView1.HideSelection = false;
			this.listView1.Location = new System.Drawing.Point(0, 0);
			this.listView1.Name = "listView1";
			this.listView1.Size = new System.Drawing.Size(613, 325);
			this.listView1.SmallImageList = this.ilListIcons;
			this.listView1.TabIndex = 0;
			this.listView1.UseCompatibleStateImageBehavior = false;
			this.listView1.View = System.Windows.Forms.View.Details;
			this.listView1.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.lv1ColumnClick);
			this.listView1.SelectedIndexChanged += new System.EventHandler(this.ListView1SelectedIndexChanged);
			this.listView1.MouseClick += new System.Windows.Forms.MouseEventHandler(this.ListRightMouseClick);
			// 
			// pbPreview
			// 
			this.pbPreview.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.pbPreview.InitialImage = ((System.Drawing.Image)(resources.GetObject("pbPreview.InitialImage")));
			this.pbPreview.Location = new System.Drawing.Point(622, 5);
			this.pbPreview.MinimumSize = new System.Drawing.Size(256, 256);
			this.pbPreview.Name = "pbPreview";
			this.pbPreview.Size = new System.Drawing.Size(256, 268);
			this.pbPreview.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
			this.pbPreview.TabIndex = 1;
			this.pbPreview.TabStop = false;
			// 
			// tabControl1
			// 
			this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.tabControl1.Controls.Add(this.tabPage1);
			this.tabControl1.Controls.Add(this.tabPage2);
			this.tabControl1.Controls.Add(this.tabPage3);
			this.tabControl1.Controls.Add(this.tabPage4);
			this.tabControl1.Location = new System.Drawing.Point(0, 0);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(616, 276);
			this.tabControl1.TabIndex = 0;
			this.tabControl1.SelectedIndexChanged += new System.EventHandler(this.TabControl1SelectedIndexChanged);
			// 
			// tabPage1
			// 
			this.tabPage1.Controls.Add(this.lvHistory);
			this.tabPage1.Location = new System.Drawing.Point(4, 22);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage1.Size = new System.Drawing.Size(608, 250);
			this.tabPage1.TabIndex = 0;
			this.tabPage1.Text = "History";
			this.tabPage1.UseVisualStyleBackColor = true;
			// 
			// lvHistory
			// 
			this.lvHistory.Dock = System.Windows.Forms.DockStyle.Fill;
			this.lvHistory.Location = new System.Drawing.Point(3, 3);
			this.lvHistory.Name = "lvHistory";
			this.lvHistory.Size = new System.Drawing.Size(602, 244);
			this.lvHistory.TabIndex = 0;
			this.lvHistory.UseCompatibleStateImageBehavior = false;
			this.lvHistory.View = System.Windows.Forms.View.Details;
			// 
			// tabPage2
			// 
			this.tabPage2.Controls.Add(this.lvParents);
			this.tabPage2.Location = new System.Drawing.Point(4, 22);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage2.Size = new System.Drawing.Size(608, 246);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "WhereUsed";
			this.tabPage2.UseVisualStyleBackColor = true;
			// 
			// lvParents
			// 
			this.lvParents.Dock = System.Windows.Forms.DockStyle.Fill;
			this.lvParents.Location = new System.Drawing.Point(3, 3);
			this.lvParents.Name = "lvParents";
			this.lvParents.Size = new System.Drawing.Size(602, 240);
			this.lvParents.TabIndex = 0;
			this.lvParents.UseCompatibleStateImageBehavior = false;
			this.lvParents.View = System.Windows.Forms.View.Details;
			// 
			// tabPage3
			// 
			this.tabPage3.Controls.Add(this.lvChildren);
			this.tabPage3.Location = new System.Drawing.Point(4, 22);
			this.tabPage3.Name = "tabPage3";
			this.tabPage3.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage3.Size = new System.Drawing.Size(608, 246);
			this.tabPage3.TabIndex = 2;
			this.tabPage3.Text = "Dependents";
			this.tabPage3.UseVisualStyleBackColor = true;
			// 
			// lvChildren
			// 
			this.lvChildren.Dock = System.Windows.Forms.DockStyle.Fill;
			this.lvChildren.Location = new System.Drawing.Point(3, 3);
			this.lvChildren.Name = "lvChildren";
			this.lvChildren.Size = new System.Drawing.Size(602, 240);
			this.lvChildren.TabIndex = 0;
			this.lvChildren.UseCompatibleStateImageBehavior = false;
			this.lvChildren.View = System.Windows.Forms.View.Details;
			// 
			// tabPage4
			// 
			this.tabPage4.Controls.Add(this.lvProperties);
			this.tabPage4.Location = new System.Drawing.Point(4, 22);
			this.tabPage4.Name = "tabPage4";
			this.tabPage4.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage4.Size = new System.Drawing.Size(608, 246);
			this.tabPage4.TabIndex = 3;
			this.tabPage4.Text = "Properties";
			this.tabPage4.UseVisualStyleBackColor = true;
			// 
			// lvProperties
			// 
			this.lvProperties.Dock = System.Windows.Forms.DockStyle.Fill;
			this.lvProperties.Location = new System.Drawing.Point(3, 3);
			this.lvProperties.Name = "lvProperties";
			this.lvProperties.Size = new System.Drawing.Size(602, 240);
			this.lvProperties.TabIndex = 0;
			this.lvProperties.UseCompatibleStateImageBehavior = false;
			this.lvProperties.View = System.Windows.Forms.View.Details;
			// 
			// logicalDeleteToolStripMenuItem
			// 
			this.logicalDeleteToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.logicalDeleteToolStripMenuItem1,
            this.permanentDeleteToolStripMenuItem});
			this.logicalDeleteToolStripMenuItem.Name = "logicalDeleteToolStripMenuItem";
			this.logicalDeleteToolStripMenuItem.Size = new System.Drawing.Size(132, 22);
			this.logicalDeleteToolStripMenuItem.Text = "Delete";
			// 
			// logicalDeleteToolStripMenuItem1
			// 
			this.logicalDeleteToolStripMenuItem1.Name = "logicalDeleteToolStripMenuItem1";
			this.logicalDeleteToolStripMenuItem1.Size = new System.Drawing.Size(168, 22);
			this.logicalDeleteToolStripMenuItem1.Text = "Logical Delete";
			this.logicalDeleteToolStripMenuItem1.Click += new System.EventHandler(this.CmsListDeleteLogicalClick);
			// 
			// permanentDeleteToolStripMenuItem
			// 
			this.permanentDeleteToolStripMenuItem.Name = "permanentDeleteToolStripMenuItem";
			this.permanentDeleteToolStripMenuItem.Size = new System.Drawing.Size(168, 22);
			this.permanentDeleteToolStripMenuItem.Text = "Permanent Delete";
			this.permanentDeleteToolStripMenuItem.Click += new System.EventHandler(this.CmsListDeletePermanentClick);
			// 
			// MainForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(884, 618);
			this.Controls.Add(this.splitContainer1);
			this.Controls.Add(this.statusStrip1);
			this.Name = "MainForm";
			this.Text = "HackPDM";
			this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.MainFormFormClosed);
			this.cmsTree.ResumeLayout(false);
			this.cmsList.ResumeLayout(false);
			this.statusStrip1.ResumeLayout(false);
			this.statusStrip1.PerformLayout();
			this.splitContainer1.Panel1.ResumeLayout(false);
			this.splitContainer1.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
			this.splitContainer1.ResumeLayout(false);
			this.splitContainer2.Panel1.ResumeLayout(false);
			this.splitContainer2.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
			this.splitContainer2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.pbPreview)).EndInit();
			this.tabControl1.ResumeLayout(false);
			this.tabPage1.ResumeLayout(false);
			this.tabPage2.ResumeLayout(false);
			this.tabPage3.ResumeLayout(false);
			this.tabPage4.ResumeLayout(false);
			this.ResumeLayout(false);
			this.PerformLayout();

		}
		private System.Windows.Forms.ToolStripMenuItem cmdManageFileTypes;
		private System.Windows.Forms.ToolStripMenuItem cmdRefreshView;
		private System.Windows.Forms.ToolStripDropDownButton toolStripDropDownButton1;
		private System.Windows.Forms.ListView lvProperties;
		private System.Windows.Forms.PictureBox pbPreview;
		private System.Windows.Forms.TabPage tabPage4;
		private System.Windows.Forms.ListView lvChildren;
		private System.Windows.Forms.TabPage tabPage3;
		private System.Windows.Forms.ListView lvParents;
		private System.Windows.Forms.TabPage tabPage2;
		private System.Windows.Forms.TabPage tabPage1;
		private System.Windows.Forms.TabControl tabControl1;
		private System.Windows.Forms.ListView lvHistory;
		private System.Windows.Forms.ListView listView1;
		private System.Windows.Forms.TreeView treeView1;
		private System.Windows.Forms.SplitContainer splitContainer2;
		private System.Windows.Forms.SplitContainer splitContainer1;
		private System.Windows.Forms.StatusStrip statusStrip1;
		private System.Windows.Forms.ToolStripMenuItem cmsTreeAnalyze;
		private System.Windows.Forms.ToolStripMenuItem cmsListUndoCheckout;
		private System.Windows.Forms.ToolStripMenuItem cmsListCommit;
		private System.Windows.Forms.ToolStripMenuItem cmsTreeUndoCheckout;
		private System.Windows.Forms.ToolStripMenuItem cmsTreeCommit;
		private System.Windows.Forms.ToolStripMenuItem cmsListCheckOut;
		private System.Windows.Forms.ToolStripMenuItem cmsListGetLatest;
		private System.Windows.Forms.ContextMenuStrip cmsList;
		private System.Windows.Forms.ToolStripMenuItem cmsTreeCheckout;
		private System.Windows.Forms.ToolStripMenuItem cmsTreeGetLatest;
		private System.Windows.Forms.ContextMenuStrip cmsTree;
		private System.Windows.Forms.ImageList ilListIcons;
		private System.Windows.Forms.ImageList ilTreeIcons;
		private System.Windows.Forms.ToolStripMenuItem logicalDeleteToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem logicalDeleteToolStripMenuItem1;
		private System.Windows.Forms.ToolStripMenuItem permanentDeleteToolStripMenuItem;
	}
}
