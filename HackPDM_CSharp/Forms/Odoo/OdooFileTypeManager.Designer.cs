namespace HackPDM.Forms.Odoo
{
	partial class OdooFileTypeManager
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose( bool disposing )
		{
			if ( disposing && ( components != null ) )
			{
				components.Dispose();
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(OdooFileTypeManager));
			this.btnAddSel = new System.Windows.Forms.Button();
			this.OdooLocTypes = new System.Windows.Forms.ListView();
			this.TypeImageList = new System.Windows.Forms.ImageList(this.components);
			this.groupBox3 = new System.Windows.Forms.GroupBox();
			this.AddAllNewTypesBtn = new System.Windows.Forms.Button();
			this.button3 = new System.Windows.Forms.Button();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.LocalDataTypeGrid = new System.Windows.Forms.DataGridView();
			this.LocalFileCount = new System.Windows.Forms.Label();
			this.btnRefreshLocal = new System.Windows.Forms.Button();
			this.label1 = new System.Windows.Forms.Label();
			this.txtDesc = new System.Windows.Forms.TextBox();
			this.pbIcon = new System.Windows.Forms.PictureBox();
			this.label4 = new System.Windows.Forms.Label();
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.label3 = new System.Windows.Forms.Label();
			this.txtRegex = new System.Windows.Forms.TextBox();
			this.cboCat = new System.Windows.Forms.ComboBox();
			this.label2 = new System.Windows.Forms.Label();
			this.txtExt = new System.Windows.Forms.TextBox();
			this.btnTypesCommit = new System.Windows.Forms.Button();
			this.btnRefreshRemote = new System.Windows.Forms.Button();
			this.OdooRemTypes = new System.Windows.Forms.ListView();
			this.btnRefreshFilters = new System.Windows.Forms.Button();
			this.btnFiltersCommit = new System.Windows.Forms.Button();
			this.OdooEntryFilters = new System.Windows.Forms.DataGridView();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.OdooOpenImage = new System.Windows.Forms.OpenFileDialog();
			this.groupBox3.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.LocalDataTypeGrid)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.pbIcon)).BeginInit();
			this.groupBox2.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.OdooEntryFilters)).BeginInit();
			this.groupBox1.SuspendLayout();
			this.SuspendLayout();
			// 
			// btnAddSel
			// 
			this.btnAddSel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btnAddSel.Location = new System.Drawing.Point(6, 424);
			this.btnAddSel.Name = "btnAddSel";
			this.btnAddSel.Size = new System.Drawing.Size(113, 23);
			this.btnAddSel.TabIndex = 3;
			this.btnAddSel.Text = "Add Selected";
			this.btnAddSel.UseVisualStyleBackColor = true;
			this.btnAddSel.Click += new System.EventHandler(this.btnAddSel_Click);
			// 
			// OdooLocTypes
			// 
			this.OdooLocTypes.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.OdooLocTypes.FullRowSelect = true;
			this.OdooLocTypes.HideSelection = false;
			this.OdooLocTypes.Location = new System.Drawing.Point(6, 19);
			this.OdooLocTypes.MultiSelect = false;
			this.OdooLocTypes.Name = "OdooLocTypes";
			this.OdooLocTypes.Size = new System.Drawing.Size(616, 402);
			this.OdooLocTypes.SmallImageList = this.TypeImageList;
			this.OdooLocTypes.Sorting = System.Windows.Forms.SortOrder.Ascending;
			this.OdooLocTypes.TabIndex = 0;
			this.OdooLocTypes.UseCompatibleStateImageBehavior = false;
			this.OdooLocTypes.View = System.Windows.Forms.View.Details;
			// 
			// TypeImageList
			// 
			this.TypeImageList.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("TypeImageList.ImageStream")));
			this.TypeImageList.TransparentColor = System.Drawing.Color.Transparent;
			this.TypeImageList.Images.SetKeyName(0, "default");
			this.TypeImageList.Images.SetKeyName(1, "UnknownImage.png");
			this.TypeImageList.Images.SetKeyName(2, "delete_image_button.png");
			this.TypeImageList.Images.SetKeyName(3, "square_empty.png");
			// 
			// groupBox3
			// 
			this.groupBox3.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.groupBox3.Controls.Add(this.AddAllNewTypesBtn);
			this.groupBox3.Controls.Add(this.button3);
			this.groupBox3.Controls.Add(this.button2);
			this.groupBox3.Controls.Add(this.button1);
			this.groupBox3.Controls.Add(this.LocalDataTypeGrid);
			this.groupBox3.Controls.Add(this.LocalFileCount);
			this.groupBox3.Controls.Add(this.btnAddSel);
			this.groupBox3.Controls.Add(this.btnRefreshLocal);
			this.groupBox3.Controls.Add(this.OdooLocTypes);
			this.groupBox3.Location = new System.Drawing.Point(631, 6);
			this.groupBox3.Name = "groupBox3";
			this.groupBox3.Size = new System.Drawing.Size(628, 784);
			this.groupBox3.TabIndex = 9;
			this.groupBox3.TabStop = false;
			this.groupBox3.Text = "Local Types";
			// 
			// AddAllNewTypesBtn
			// 
			this.AddAllNewTypesBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.AddAllNewTypesBtn.Location = new System.Drawing.Point(125, 424);
			this.AddAllNewTypesBtn.Name = "AddAllNewTypesBtn";
			this.AddAllNewTypesBtn.Size = new System.Drawing.Size(112, 23);
			this.AddAllNewTypesBtn.TabIndex = 16;
			this.AddAllNewTypesBtn.Text = "Add All New";
			this.AddAllNewTypesBtn.UseVisualStyleBackColor = true;
			this.AddAllNewTypesBtn.Click += new System.EventHandler(this.AddAllNewTypesBtn_Click);
			// 
			// button3
			// 
			this.button3.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.button3.Location = new System.Drawing.Point(6, 755);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(89, 23);
			this.button3.TabIndex = 15;
			this.button3.Text = "Commit";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(this.button3_Click);
			// 
			// button2
			// 
			this.button2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.button2.Location = new System.Drawing.Point(456, 756);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(166, 23);
			this.button2.TabIndex = 7;
			this.button2.Text = "Reset";
			this.button2.UseVisualStyleBackColor = true;
			// 
			// button1
			// 
			this.button1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.button1.Location = new System.Drawing.Point(284, 755);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(166, 23);
			this.button1.TabIndex = 6;
			this.button1.Text = "Delete";
			this.button1.UseVisualStyleBackColor = true;
			// 
			// LocalDataTypeGrid
			// 
			this.LocalDataTypeGrid.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.LocalDataTypeGrid.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;
			this.LocalDataTypeGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.LocalDataTypeGrid.Location = new System.Drawing.Point(6, 450);
			this.LocalDataTypeGrid.Name = "LocalDataTypeGrid";
			this.LocalDataTypeGrid.Size = new System.Drawing.Size(564, 300);
			this.LocalDataTypeGrid.TabIndex = 5;
			this.LocalDataTypeGrid.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.LocalDataTypeGrid_CellClick);
			// 
			// LocalFileCount
			// 
			this.LocalFileCount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.LocalFileCount.AutoSize = true;
			this.LocalFileCount.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.LocalFileCount.Location = new System.Drawing.Point(323, 427);
			this.LocalFileCount.Name = "LocalFileCount";
			this.LocalFileCount.Size = new System.Drawing.Size(65, 17);
			this.LocalFileCount.TabIndex = 4;
			this.LocalFileCount.Text = "file count";
			// 
			// btnRefreshLocal
			// 
			this.btnRefreshLocal.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnRefreshLocal.Location = new System.Drawing.Point(456, 424);
			this.btnRefreshLocal.Name = "btnRefreshLocal";
			this.btnRefreshLocal.Size = new System.Drawing.Size(166, 23);
			this.btnRefreshLocal.TabIndex = 1;
			this.btnRefreshLocal.Text = "Refresh Types";
			this.btnRefreshLocal.UseVisualStyleBackColor = true;
			this.btnRefreshLocal.Click += new System.EventHandler(this.btnRefreshLocal_Click);
			// 
			// label1
			// 
			this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(343, 476);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(60, 13);
			this.label1.TabIndex = 14;
			this.label1.Text = "Description";
			// 
			// txtDesc
			// 
			this.txtDesc.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.txtDesc.Location = new System.Drawing.Point(343, 492);
			this.txtDesc.Name = "txtDesc";
			this.txtDesc.Size = new System.Drawing.Size(194, 20);
			this.txtDesc.TabIndex = 13;
			// 
			// pbIcon
			// 
			this.pbIcon.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.pbIcon.BackColor = System.Drawing.SystemColors.ActiveCaption;
			this.pbIcon.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pbIcon.BackgroundImage")));
			this.pbIcon.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
			this.pbIcon.Location = new System.Drawing.Point(543, 477);
			this.pbIcon.Name = "pbIcon";
			this.pbIcon.Size = new System.Drawing.Size(64, 64);
			this.pbIcon.TabIndex = 12;
			this.pbIcon.TabStop = false;
			this.pbIcon.Click += new System.EventHandler(this.pbIcon_Click);
			// 
			// label4
			// 
			this.label4.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(191, 476);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(49, 13);
			this.label4.TabIndex = 11;
			this.label4.Text = "Category";
			// 
			// groupBox2
			// 
			this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
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
			this.groupBox2.Controls.Add(this.OdooRemTypes);
			this.groupBox2.Location = new System.Drawing.Point(12, 243);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(613, 547);
			this.groupBox2.TabIndex = 8;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "Remote Types";
			// 
			// label3
			// 
			this.label3.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(97, 476);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(39, 13);
			this.label3.TabIndex = 10;
			this.label3.Text = "RegEx";
			// 
			// txtRegex
			// 
			this.txtRegex.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.txtRegex.Location = new System.Drawing.Point(97, 492);
			this.txtRegex.Name = "txtRegex";
			this.txtRegex.Size = new System.Drawing.Size(90, 20);
			this.txtRegex.TabIndex = 9;
			// 
			// cboCat
			// 
			this.cboCat.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.cboCat.FormattingEnabled = true;
			this.cboCat.Location = new System.Drawing.Point(189, 492);
			this.cboCat.Name = "cboCat";
			this.cboCat.Size = new System.Drawing.Size(152, 21);
			this.cboCat.TabIndex = 8;
			// 
			// label2
			// 
			this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(31, 476);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(53, 13);
			this.label2.TabIndex = 7;
			this.label2.Text = "Extension";
			// 
			// txtExt
			// 
			this.txtExt.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.txtExt.Location = new System.Drawing.Point(31, 492);
			this.txtExt.Name = "txtExt";
			this.txtExt.Size = new System.Drawing.Size(64, 20);
			this.txtExt.TabIndex = 6;
			// 
			// btnTypesCommit
			// 
			this.btnTypesCommit.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btnTypesCommit.Location = new System.Drawing.Point(101, 518);
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
			this.btnRefreshRemote.Location = new System.Drawing.Point(6, 518);
			this.btnRefreshRemote.Name = "btnRefreshRemote";
			this.btnRefreshRemote.Size = new System.Drawing.Size(89, 23);
			this.btnRefreshRemote.TabIndex = 1;
			this.btnRefreshRemote.Text = "Refresh Types";
			this.btnRefreshRemote.UseVisualStyleBackColor = true;
			this.btnRefreshRemote.Click += new System.EventHandler(this.btnRefreshRemote_Click);
			// 
			// OdooRemTypes
			// 
			this.OdooRemTypes.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.OdooRemTypes.FullRowSelect = true;
			this.OdooRemTypes.HideSelection = false;
			this.OdooRemTypes.Location = new System.Drawing.Point(6, 19);
			this.OdooRemTypes.MultiSelect = false;
			this.OdooRemTypes.Name = "OdooRemTypes";
			this.OdooRemTypes.Size = new System.Drawing.Size(601, 452);
			this.OdooRemTypes.SmallImageList = this.TypeImageList;
			this.OdooRemTypes.Sorting = System.Windows.Forms.SortOrder.Ascending;
			this.OdooRemTypes.TabIndex = 0;
			this.OdooRemTypes.UseCompatibleStateImageBehavior = false;
			this.OdooRemTypes.View = System.Windows.Forms.View.Details;
			// 
			// btnRefreshFilters
			// 
			this.btnRefreshFilters.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnRefreshFilters.Location = new System.Drawing.Point(518, 202);
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
			this.btnFiltersCommit.Location = new System.Drawing.Point(450, 202);
			this.btnFiltersCommit.Name = "btnFiltersCommit";
			this.btnFiltersCommit.Size = new System.Drawing.Size(62, 23);
			this.btnFiltersCommit.TabIndex = 4;
			this.btnFiltersCommit.Text = "Commit";
			this.btnFiltersCommit.UseVisualStyleBackColor = true;
			this.btnFiltersCommit.Click += new System.EventHandler(this.btnFiltersCommit_Click);
			// 
			// OdooEntryFilters
			// 
			this.OdooEntryFilters.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.OdooEntryFilters.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.OdooEntryFilters.Location = new System.Drawing.Point(6, 19);
			this.OdooEntryFilters.Name = "OdooEntryFilters";
			this.OdooEntryFilters.Size = new System.Drawing.Size(601, 177);
			this.OdooEntryFilters.TabIndex = 2;
			this.OdooEntryFilters.CellValidated += new System.Windows.Forms.DataGridViewCellEventHandler(this.OdooEntryFilters_CellValidated);
			// 
			// groupBox1
			// 
			this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
			this.groupBox1.Controls.Add(this.btnRefreshFilters);
			this.groupBox1.Controls.Add(this.btnFiltersCommit);
			this.groupBox1.Controls.Add(this.OdooEntryFilters);
			this.groupBox1.Location = new System.Drawing.Point(12, 6);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(613, 231);
			this.groupBox1.TabIndex = 6;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Name Filtering";
			// 
			// OdooOpenImage
			// 
			this.OdooOpenImage.DefaultExt = "png";
			this.OdooOpenImage.FileName = "Icon";
			// 
			// OdooFileTypeManager
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(1271, 802);
			this.Controls.Add(this.groupBox1);
			this.Controls.Add(this.groupBox3);
			this.Controls.Add(this.groupBox2);
			this.Name = "OdooFileTypeManager";
			this.Text = "OdooFileTypeManager";
			this.groupBox3.ResumeLayout(false);
			this.groupBox3.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.LocalDataTypeGrid)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.pbIcon)).EndInit();
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.OdooEntryFilters)).EndInit();
			this.groupBox1.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Button btnAddSel;
		private System.Windows.Forms.ListView OdooLocTypes;
		private System.Windows.Forms.ImageList TypeImageList;
		private System.Windows.Forms.GroupBox groupBox3;
		private System.Windows.Forms.Button btnRefreshLocal;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.TextBox txtDesc;
		private System.Windows.Forms.PictureBox pbIcon;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.GroupBox groupBox2;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.TextBox txtRegex;
		private System.Windows.Forms.ComboBox cboCat;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.TextBox txtExt;
		private System.Windows.Forms.Button btnTypesCommit;
		private System.Windows.Forms.Button btnRefreshRemote;
		private System.Windows.Forms.ListView OdooRemTypes;
		private System.Windows.Forms.Button btnRefreshFilters;
		private System.Windows.Forms.Button btnFiltersCommit;
		private System.Windows.Forms.DataGridView OdooEntryFilters;
		private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.Label LocalFileCount;
		private System.Windows.Forms.OpenFileDialog OdooOpenImage;
		private System.Windows.Forms.DataGridView LocalDataTypeGrid;
		private System.Windows.Forms.Button button2;
		private System.Windows.Forms.Button button1;
		private System.Windows.Forms.Button button3;
		private System.Windows.Forms.Button AddAllNewTypesBtn;
	}
}