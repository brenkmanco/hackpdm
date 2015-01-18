/*
 * Created by SharpDevelop.
 * User: matt
 * Date: 2/18/2013
 * Time: 2:42 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
namespace HackPDM
{
	partial class FileTypeManager
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
			this.ilTypes = new System.Windows.Forms.ImageList(this.components);
			this.splitContainer1 = new System.Windows.Forms.SplitContainer();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.btnRefreshFilters = new System.Windows.Forms.Button();
			this.btnFiltersCommit = new System.Windows.Forms.Button();
			this.dgFilters = new System.Windows.Forms.DataGridView();
			this.splitContainer2 = new System.Windows.Forms.SplitContainer();
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.label1 = new System.Windows.Forms.Label();
			this.txtDesc = new System.Windows.Forms.TextBox();
			this.pbIcon = new System.Windows.Forms.PictureBox();
			this.label4 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.txtRegex = new System.Windows.Forms.TextBox();
			this.cboCat = new System.Windows.Forms.ComboBox();
			this.label2 = new System.Windows.Forms.Label();
			this.txtExt = new System.Windows.Forms.TextBox();
			this.btnTypesCommit = new System.Windows.Forms.Button();
			this.btnRefreshRemote = new System.Windows.Forms.Button();
			this.lvRemTypes = new System.Windows.Forms.ListView();
			this.groupBox3 = new System.Windows.Forms.GroupBox();
			this.btnAddSel = new System.Windows.Forms.Button();
			this.btnRefreshLocal = new System.Windows.Forms.Button();
			this.lvLocTypes = new System.Windows.Forms.ListView();
			((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
			this.splitContainer1.Panel1.SuspendLayout();
			this.splitContainer1.Panel2.SuspendLayout();
			this.splitContainer1.SuspendLayout();
			this.groupBox1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.dgFilters)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
			this.splitContainer2.Panel1.SuspendLayout();
			this.splitContainer2.Panel2.SuspendLayout();
			this.splitContainer2.SuspendLayout();
			this.groupBox2.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.pbIcon)).BeginInit();
			this.groupBox3.SuspendLayout();
			this.SuspendLayout();
			// 
			// ilTypes
			// 
			this.ilTypes.ColorDepth = System.Windows.Forms.ColorDepth.Depth8Bit;
			this.ilTypes.ImageSize = new System.Drawing.Size(32, 32);
			this.ilTypes.TransparentColor = System.Drawing.Color.Transparent;
			// 
			// splitContainer1
			// 
			this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.splitContainer1.Location = new System.Drawing.Point(5, 5);
			this.splitContainer1.Name = "splitContainer1";
			this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
			// 
			// splitContainer1.Panel1
			// 
			this.splitContainer1.Panel1.Controls.Add(this.groupBox1);
			// 
			// splitContainer1.Panel2
			// 
			this.splitContainer1.Panel2.Controls.Add(this.splitContainer2);
			this.splitContainer1.Size = new System.Drawing.Size(1232, 741);
			this.splitContainer1.SplitterDistance = 234;
			this.splitContainer1.TabIndex = 7;
			// 
			// groupBox1
			// 
			this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.groupBox1.Controls.Add(this.btnRefreshFilters);
			this.groupBox1.Controls.Add(this.btnFiltersCommit);
			this.groupBox1.Controls.Add(this.dgFilters);
			this.groupBox1.Location = new System.Drawing.Point(0, 0);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(1232, 231);
			this.groupBox1.TabIndex = 6;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Name Filtering";
			// 
			// btnRefreshFilters
			// 
			this.btnRefreshFilters.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btnRefreshFilters.Location = new System.Drawing.Point(1069, 202);
			this.btnRefreshFilters.Name = "btnRefreshFilters";
			this.btnRefreshFilters.Size = new System.Drawing.Size(89, 23);
			this.btnRefreshFilters.TabIndex = 5;
			this.btnRefreshFilters.Text = "Refresh Filters";
			this.btnRefreshFilters.UseVisualStyleBackColor = true;
			this.btnRefreshFilters.Click += new System.EventHandler(this.btnRefreshFilters_Click);
			// 
			// btnFiltersCommit
			// 
			this.btnFiltersCommit.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnFiltersCommit.Location = new System.Drawing.Point(1164, 202);
			this.btnFiltersCommit.Name = "btnFiltersCommit";
			this.btnFiltersCommit.Size = new System.Drawing.Size(62, 23);
			this.btnFiltersCommit.TabIndex = 4;
			this.btnFiltersCommit.Text = "Commit";
			this.btnFiltersCommit.UseVisualStyleBackColor = true;
			// 
			// dgFilters
			// 
			this.dgFilters.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.dgFilters.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dgFilters.Location = new System.Drawing.Point(6, 19);
			this.dgFilters.Name = "dgFilters";
			this.dgFilters.Size = new System.Drawing.Size(1220, 177);
			this.dgFilters.TabIndex = 2;
			// 
			// splitContainer2
			// 
			this.splitContainer2.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.splitContainer2.Location = new System.Drawing.Point(3, 3);
			this.splitContainer2.Name = "splitContainer2";
			// 
			// splitContainer2.Panel1
			// 
			this.splitContainer2.Panel1.Controls.Add(this.groupBox2);
			// 
			// splitContainer2.Panel2
			// 
			this.splitContainer2.Panel2.Controls.Add(this.groupBox3);
			this.splitContainer2.Size = new System.Drawing.Size(1226, 497);
			this.splitContainer2.SplitterDistance = 595;
			this.splitContainer2.TabIndex = 8;
			// 
			// groupBox2
			// 
			this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.groupBox2.Controls.Add(this.label1);
			this.groupBox2.Controls.Add(this.txtDesc);
			this.groupBox2.Controls.Add(this.pbIcon);
			this.groupBox2.Controls.Add(this.label4);
			this.groupBox2.Controls.Add(this.label3);
			this.groupBox2.Controls.Add(this.txtRegex);
			this.groupBox2.Controls.Add(this.cboCat);
			this.groupBox2.Controls.Add(this.label2);
			this.groupBox2.Controls.Add(this.txtExt);
			this.groupBox2.Controls.Add(this.btnTypesCommit);
			this.groupBox2.Controls.Add(this.btnRefreshRemote);
			this.groupBox2.Controls.Add(this.lvRemTypes);
			this.groupBox2.Location = new System.Drawing.Point(4, 3);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(588, 491);
			this.groupBox2.TabIndex = 8;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "Remote Types";
			// 
			// label1
			// 
			this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(318, 420);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(60, 13);
			this.label1.TabIndex = 14;
			this.label1.Text = "Description";
			// 
			// txtDesc
			// 
			this.txtDesc.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.txtDesc.Location = new System.Drawing.Point(318, 436);
			this.txtDesc.Name = "txtDesc";
			this.txtDesc.Size = new System.Drawing.Size(194, 20);
			this.txtDesc.TabIndex = 13;
			// 
			// pbIcon
			// 
			this.pbIcon.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.pbIcon.Location = new System.Drawing.Point(518, 421);
			this.pbIcon.Name = "pbIcon";
			this.pbIcon.Size = new System.Drawing.Size(64, 64);
			this.pbIcon.TabIndex = 12;
			this.pbIcon.TabStop = false;
			// 
			// label4
			// 
			this.label4.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(166, 420);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(49, 13);
			this.label4.TabIndex = 11;
			this.label4.Text = "Category";
			// 
			// label3
			// 
			this.label3.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(72, 420);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(39, 13);
			this.label3.TabIndex = 10;
			this.label3.Text = "RegEx";
			// 
			// txtRegex
			// 
			this.txtRegex.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.txtRegex.Location = new System.Drawing.Point(72, 436);
			this.txtRegex.Name = "txtRegex";
			this.txtRegex.Size = new System.Drawing.Size(90, 20);
			this.txtRegex.TabIndex = 9;
			// 
			// cboCat
			// 
			this.cboCat.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.cboCat.FormattingEnabled = true;
			this.cboCat.Location = new System.Drawing.Point(164, 436);
			this.cboCat.Name = "cboCat";
			this.cboCat.Size = new System.Drawing.Size(152, 21);
			this.cboCat.TabIndex = 8;
			// 
			// label2
			// 
			this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(6, 420);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(53, 13);
			this.label2.TabIndex = 7;
			this.label2.Text = "Extension";
			// 
			// txtExt
			// 
			this.txtExt.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.txtExt.Location = new System.Drawing.Point(6, 436);
			this.txtExt.Name = "txtExt";
			this.txtExt.Size = new System.Drawing.Size(64, 20);
			this.txtExt.TabIndex = 6;
			// 
			// btnTypesCommit
			// 
			this.btnTypesCommit.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btnTypesCommit.Location = new System.Drawing.Point(101, 462);
			this.btnTypesCommit.Name = "btnTypesCommit";
			this.btnTypesCommit.Size = new System.Drawing.Size(89, 23);
			this.btnTypesCommit.TabIndex = 2;
			this.btnTypesCommit.Text = "Commit";
			this.btnTypesCommit.UseVisualStyleBackColor = true;
			this.btnTypesCommit.Click += new System.EventHandler(this.btnTypesCommit_Click);
			// 
			// btnRefreshRemote
			// 
			this.btnRefreshRemote.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btnRefreshRemote.Location = new System.Drawing.Point(6, 462);
			this.btnRefreshRemote.Name = "btnRefreshRemote";
			this.btnRefreshRemote.Size = new System.Drawing.Size(89, 23);
			this.btnRefreshRemote.TabIndex = 1;
			this.btnRefreshRemote.Text = "Refresh Types";
			this.btnRefreshRemote.UseVisualStyleBackColor = true;
			this.btnRefreshRemote.Click += new System.EventHandler(this.btnRefreshRemote_Click);
			// 
			// lvRemTypes
			// 
			this.lvRemTypes.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.lvRemTypes.FullRowSelect = true;
			this.lvRemTypes.HideSelection = false;
			this.lvRemTypes.Location = new System.Drawing.Point(6, 19);
			this.lvRemTypes.MultiSelect = false;
			this.lvRemTypes.Name = "lvRemTypes";
			this.lvRemTypes.Size = new System.Drawing.Size(576, 396);
			this.lvRemTypes.SmallImageList = this.ilTypes;
			this.lvRemTypes.Sorting = System.Windows.Forms.SortOrder.Ascending;
			this.lvRemTypes.TabIndex = 0;
			this.lvRemTypes.UseCompatibleStateImageBehavior = false;
			this.lvRemTypes.View = System.Windows.Forms.View.Details;
			this.lvRemTypes.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.lvRemTypesColumnClick);
			// 
			// groupBox3
			// 
			this.groupBox3.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.groupBox3.Controls.Add(this.btnAddSel);
			this.groupBox3.Controls.Add(this.btnRefreshLocal);
			this.groupBox3.Controls.Add(this.lvLocTypes);
			this.groupBox3.Location = new System.Drawing.Point(3, 3);
			this.groupBox3.Name = "groupBox3";
			this.groupBox3.Size = new System.Drawing.Size(620, 491);
			this.groupBox3.TabIndex = 9;
			this.groupBox3.TabStop = false;
			this.groupBox3.Text = "Local Types";
			// 
			// btnAddSel
			// 
			this.btnAddSel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btnAddSel.Location = new System.Drawing.Point(6, 462);
			this.btnAddSel.Name = "btnAddSel";
			this.btnAddSel.Size = new System.Drawing.Size(95, 23);
			this.btnAddSel.TabIndex = 3;
			this.btnAddSel.Text = "Add Selected";
			this.btnAddSel.UseVisualStyleBackColor = true;
			this.btnAddSel.Click += new System.EventHandler(this.btnAddSel_Click);
			// 
			// btnRefreshLocal
			// 
			this.btnRefreshLocal.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btnRefreshLocal.Location = new System.Drawing.Point(515, 462);
			this.btnRefreshLocal.Name = "btnRefreshLocal";
			this.btnRefreshLocal.Size = new System.Drawing.Size(99, 23);
			this.btnRefreshLocal.TabIndex = 1;
			this.btnRefreshLocal.Text = "Refresh Types";
			this.btnRefreshLocal.UseVisualStyleBackColor = true;
			this.btnRefreshLocal.Click += new System.EventHandler(this.btnRefreshLocal_Click);
			// 
			// lvLocTypes
			// 
			this.lvLocTypes.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.lvLocTypes.FullRowSelect = true;
			this.lvLocTypes.HideSelection = false;
			this.lvLocTypes.Location = new System.Drawing.Point(6, 19);
			this.lvLocTypes.MultiSelect = false;
			this.lvLocTypes.Name = "lvLocTypes";
			this.lvLocTypes.Size = new System.Drawing.Size(608, 437);
			this.lvLocTypes.SmallImageList = this.ilTypes;
			this.lvLocTypes.Sorting = System.Windows.Forms.SortOrder.Ascending;
			this.lvLocTypes.TabIndex = 0;
			this.lvLocTypes.UseCompatibleStateImageBehavior = false;
			this.lvLocTypes.View = System.Windows.Forms.View.Details;
			this.lvLocTypes.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.lvLocTypesColumnClick);
			// 
			// FileTypeManager
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(1242, 750);
			this.Controls.Add(this.splitContainer1);
			this.Name = "FileTypeManager";
			this.Text = "FileTypeDialog";
			this.splitContainer1.Panel1.ResumeLayout(false);
			this.splitContainer1.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
			this.splitContainer1.ResumeLayout(false);
			this.groupBox1.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.dgFilters)).EndInit();
			this.splitContainer2.Panel1.ResumeLayout(false);
			this.splitContainer2.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
			this.splitContainer2.ResumeLayout(false);
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.pbIcon)).EndInit();
			this.groupBox3.ResumeLayout(false);
			this.ResumeLayout(false);

		}
		private System.Windows.Forms.ImageList ilTypes;
		private System.Windows.Forms.SplitContainer splitContainer1;
		private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.Button btnFiltersCommit;
		private System.Windows.Forms.DataGridView dgFilters;
		private System.Windows.Forms.Button btnRefreshFilters;
		private System.Windows.Forms.SplitContainer splitContainer2;
		private System.Windows.Forms.GroupBox groupBox2;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.TextBox txtDesc;
		private System.Windows.Forms.PictureBox pbIcon;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.TextBox txtRegex;
		private System.Windows.Forms.ComboBox cboCat;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.TextBox txtExt;
		private System.Windows.Forms.Button btnTypesCommit;
		private System.Windows.Forms.Button btnRefreshRemote;
		private System.Windows.Forms.ListView lvRemTypes;
		private System.Windows.Forms.GroupBox groupBox3;
		private System.Windows.Forms.Button btnAddSel;
		private System.Windows.Forms.Button btnRefreshLocal;
		private System.Windows.Forms.ListView lvLocTypes;
	}
}
