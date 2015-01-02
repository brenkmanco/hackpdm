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
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.pictureBox1 = new System.Windows.Forms.PictureBox();
			this.label4 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.cboCat = new System.Windows.Forms.ComboBox();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.txtTypeID = new System.Windows.Forms.TextBox();
			this.btnTypesNew = new System.Windows.Forms.Button();
			this.btnTypesCommit = new System.Windows.Forms.Button();
			this.btnRefreshTypes = new System.Windows.Forms.Button();
			this.lvTypes = new System.Windows.Forms.ListView();
			((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
			this.splitContainer1.Panel1.SuspendLayout();
			this.splitContainer1.Panel2.SuspendLayout();
			this.splitContainer1.SuspendLayout();
			this.groupBox1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.dgFilters)).BeginInit();
			this.groupBox2.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
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
			this.splitContainer1.Panel2.Controls.Add(this.groupBox2);
			this.splitContainer1.Size = new System.Drawing.Size(764, 739);
			this.splitContainer1.SplitterDistance = 235;
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
			this.groupBox1.Size = new System.Drawing.Size(764, 236);
			this.groupBox1.TabIndex = 6;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Name Filtering";
			// 
			// btnRefreshFilters
			// 
			this.btnRefreshFilters.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btnRefreshFilters.Location = new System.Drawing.Point(9, 204);
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
			this.btnFiltersCommit.Location = new System.Drawing.Point(696, 204);
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
			this.dgFilters.Size = new System.Drawing.Size(752, 179);
			this.dgFilters.TabIndex = 2;
			// 
			// groupBox2
			// 
			this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
						| System.Windows.Forms.AnchorStyles.Left)
						| System.Windows.Forms.AnchorStyles.Right)));
			this.groupBox2.Controls.Add(this.pictureBox1);
			this.groupBox2.Controls.Add(this.label4);
			this.groupBox2.Controls.Add(this.label3);
			this.groupBox2.Controls.Add(this.textBox2);
			this.groupBox2.Controls.Add(this.cboCat);
			this.groupBox2.Controls.Add(this.label2);
			this.groupBox2.Controls.Add(this.textBox1);
			this.groupBox2.Controls.Add(this.label1);
			this.groupBox2.Controls.Add(this.txtTypeID);
			this.groupBox2.Controls.Add(this.btnTypesNew);
			this.groupBox2.Controls.Add(this.btnTypesCommit);
			this.groupBox2.Controls.Add(this.btnRefreshTypes);
			this.groupBox2.Controls.Add(this.lvTypes);
			this.groupBox2.Location = new System.Drawing.Point(0, 3);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(764, 497);
			this.groupBox2.TabIndex = 7;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "File Types";
			// 
			// pictureBox1
			// 
			this.pictureBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.pictureBox1.Location = new System.Drawing.Point(694, 391);
			this.pictureBox1.Name = "pictureBox1";
			this.pictureBox1.Size = new System.Drawing.Size(64, 64);
			this.pictureBox1.TabIndex = 12;
			this.pictureBox1.TabStop = false;
			// 
			// label4
			// 
			this.label4.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(491, 464);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(49, 13);
			this.label4.TabIndex = 11;
			this.label4.Text = "Category";
			// 
			// label3
			// 
			this.label3.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(491, 438);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(39, 13);
			this.label3.TabIndex = 10;
			this.label3.Text = "RegEx";
			// 
			// textBox2
			// 
			this.textBox2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.textBox2.Location = new System.Drawing.Point(562, 435);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(121, 20);
			this.textBox2.TabIndex = 9;
			// 
			// cboCat
			// 
			this.cboCat.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.cboCat.FormattingEnabled = true;
			this.cboCat.Location = new System.Drawing.Point(562, 461);
			this.cboCat.Name = "cboCat";
			this.cboCat.Size = new System.Drawing.Size(196, 21);
			this.cboCat.TabIndex = 8;
			// 
			// label2
			// 
			this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(491, 416);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(53, 13);
			this.label2.TabIndex = 7;
			this.label2.Text = "Extension";
			// 
			// textBox1
			// 
			this.textBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.textBox1.Location = new System.Drawing.Point(562, 413);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(121, 20);
			this.textBox1.TabIndex = 6;
			// 
			// label1
			// 
			this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(491, 394);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(42, 13);
			this.label1.TabIndex = 5;
			this.label1.Text = "TypeID";
			// 
			// txtTypeID
			// 
			this.txtTypeID.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.txtTypeID.Location = new System.Drawing.Point(562, 391);
			this.txtTypeID.Name = "txtTypeID";
			this.txtTypeID.Size = new System.Drawing.Size(121, 20);
			this.txtTypeID.TabIndex = 4;
			// 
			// btnTypesNew
			// 
			this.btnTypesNew.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btnTypesNew.Location = new System.Drawing.Point(126, 428);
			this.btnTypesNew.Name = "btnTypesNew";
			this.btnTypesNew.Size = new System.Drawing.Size(75, 23);
			this.btnTypesNew.TabIndex = 3;
			this.btnTypesNew.Text = "New";
			this.btnTypesNew.UseVisualStyleBackColor = true;
			this.btnTypesNew.Click += new System.EventHandler(this.btnTypesNew_Click);
			// 
			// btnTypesCommit
			// 
			this.btnTypesCommit.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btnTypesCommit.Location = new System.Drawing.Point(126, 457);
			this.btnTypesCommit.Name = "btnTypesCommit";
			this.btnTypesCommit.Size = new System.Drawing.Size(75, 23);
			this.btnTypesCommit.TabIndex = 2;
			this.btnTypesCommit.Text = "Commit";
			this.btnTypesCommit.UseVisualStyleBackColor = true;
			this.btnTypesCommit.Click += new System.EventHandler(this.btnTypesCommit_Click);
			// 
			// btnRefreshTypes
			// 
			this.btnRefreshTypes.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btnRefreshTypes.Location = new System.Drawing.Point(9, 391);
			this.btnRefreshTypes.Name = "btnRefreshTypes";
			this.btnRefreshTypes.Size = new System.Drawing.Size(89, 23);
			this.btnRefreshTypes.TabIndex = 1;
			this.btnRefreshTypes.Text = "Refresh Types";
			this.btnRefreshTypes.UseVisualStyleBackColor = true;
			this.btnRefreshTypes.Click += new System.EventHandler(this.btnRefreshTypes_Click);
			// 
			// lvTypes
			// 
			this.lvTypes.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
						| System.Windows.Forms.AnchorStyles.Left)
						| System.Windows.Forms.AnchorStyles.Right)));
			this.lvTypes.FullRowSelect = true;
			this.lvTypes.HideSelection = false;
			this.lvTypes.Location = new System.Drawing.Point(6, 19);
			this.lvTypes.Name = "lvTypes";
			this.lvTypes.Size = new System.Drawing.Size(752, 366);
			this.lvTypes.SmallImageList = this.ilTypes;
			this.lvTypes.Sorting = System.Windows.Forms.SortOrder.Ascending;
			this.lvTypes.TabIndex = 0;
			this.lvTypes.UseCompatibleStateImageBehavior = false;
			this.lvTypes.View = System.Windows.Forms.View.Details;
			// 
			// FileTypeManager
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(774, 748);
			this.Controls.Add(this.splitContainer1);
			this.Name = "FileTypeManager";
			this.Text = "FileTypeDialog";
			this.splitContainer1.Panel1.ResumeLayout(false);
			this.splitContainer1.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
			this.splitContainer1.ResumeLayout(false);
			this.groupBox1.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.dgFilters)).EndInit();
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
			this.ResumeLayout(false);

		}
		private System.Windows.Forms.ImageList ilTypes;
		private System.Windows.Forms.SplitContainer splitContainer1;
		private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.Button btnFiltersCommit;
		private System.Windows.Forms.DataGridView dgFilters;
		private System.Windows.Forms.GroupBox groupBox2;
		private System.Windows.Forms.PictureBox pictureBox1;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.TextBox textBox2;
		private System.Windows.Forms.ComboBox cboCat;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.TextBox textBox1;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.TextBox txtTypeID;
		private System.Windows.Forms.Button btnTypesNew;
		private System.Windows.Forms.Button btnTypesCommit;
		private System.Windows.Forms.Button btnRefreshTypes;
		private System.Windows.Forms.ListView lvTypes;
		private System.Windows.Forms.Button btnRefreshFilters;
	}
}
