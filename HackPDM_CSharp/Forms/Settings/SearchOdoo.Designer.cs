namespace HackPDM.Forms.Settings
{
	partial class SearchOdoo
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
			this.lblMaxRes = new System.Windows.Forms.Label();
			this.OdooMaxRes = new System.Windows.Forms.TextBox();
			this.btnReset = new System.Windows.Forms.Button();
			this.OdooLocalOnly = new System.Windows.Forms.CheckBox();
			this.OdooSearchResults = new System.Windows.Forms.ListView();
			this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.SearchContext = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.CheckOutMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.checkoutOpenToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.unCheckoutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.OdooDeletedIsLocal = new System.Windows.Forms.CheckBox();
			this.btnCancel = new System.Windows.Forms.Button();
			this.btnSearch = new System.Windows.Forms.Button();
			this.label3 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.OdooSearchPropValue = new System.Windows.Forms.TextBox();
			this.OdooSearchProperty = new System.Windows.Forms.ComboBox();
			this.OdooCheckedMe = new System.Windows.Forms.CheckBox();
			this.FileNameTextbox = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.OdooSearchPropList = new System.Windows.Forms.ListView();
			this.OdooPropAdd = new System.Windows.Forms.Button();
			this.OdooPropDelete = new System.Windows.Forms.Button();
			this.OdooSearchPropEqual = new System.Windows.Forms.ComboBox();
			this.OdooPropComparer = new System.Windows.Forms.Label();
			this.OdooPropertyReset = new System.Windows.Forms.Button();
			this.label4 = new System.Windows.Forms.Label();
			this.OdooSearchComparer = new System.Windows.Forms.ComboBox();
			this.SearchContext.SuspendLayout();
			this.SuspendLayout();
			// 
			// lblMaxRes
			// 
			this.lblMaxRes.AutoSize = true;
			this.lblMaxRes.Location = new System.Drawing.Point(227, 73);
			this.lblMaxRes.Name = "lblMaxRes";
			this.lblMaxRes.Size = new System.Drawing.Size(65, 13);
			this.lblMaxRes.TabIndex = 108;
			this.lblMaxRes.Text = "Max Results";
			// 
			// OdooMaxRes
			// 
			this.OdooMaxRes.Location = new System.Drawing.Point(298, 71);
			this.OdooMaxRes.Name = "OdooMaxRes";
			this.OdooMaxRes.Size = new System.Drawing.Size(54, 20);
			this.OdooMaxRes.TabIndex = 107;
			this.OdooMaxRes.Text = "100";
			// 
			// btnReset
			// 
			this.btnReset.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btnReset.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold);
			this.btnReset.Location = new System.Drawing.Point(167, 557);
			this.btnReset.Name = "btnReset";
			this.btnReset.RightToLeft = System.Windows.Forms.RightToLeft.No;
			this.btnReset.Size = new System.Drawing.Size(102, 35);
			this.btnReset.TabIndex = 106;
			this.btnReset.Text = "Reset";
			this.btnReset.UseVisualStyleBackColor = true;
			this.btnReset.Click += new System.EventHandler(this.OdooReset_Click);
			// 
			// OdooLocalOnly
			// 
			this.OdooLocalOnly.AutoSize = true;
			this.OdooLocalOnly.Location = new System.Drawing.Point(11, 74);
			this.OdooLocalOnly.Name = "OdooLocalOnly";
			this.OdooLocalOnly.Size = new System.Drawing.Size(122, 17);
			this.OdooLocalOnly.TabIndex = 102;
			this.OdooLocalOnly.Text = "Only Existing Locally";
			this.OdooLocalOnly.UseVisualStyleBackColor = true;
			this.OdooLocalOnly.CheckedChanged += new System.EventHandler(this.OdooLocalOnly_CheckedChanged);
			// 
			// OdooSearchResults
			// 
			this.OdooSearchResults.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.OdooSearchResults.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader2});
			this.OdooSearchResults.ContextMenuStrip = this.SearchContext;
			this.OdooSearchResults.FullRowSelect = true;
			this.OdooSearchResults.HideSelection = false;
			this.OdooSearchResults.Location = new System.Drawing.Point(11, 139);
			this.OdooSearchResults.Name = "OdooSearchResults";
			this.OdooSearchResults.Size = new System.Drawing.Size(988, 412);
			this.OdooSearchResults.TabIndex = 105;
			this.OdooSearchResults.UseCompatibleStateImageBehavior = false;
			this.OdooSearchResults.View = System.Windows.Forms.View.Details;
			this.OdooSearchResults.DoubleClick += new System.EventHandler(this.OdooSearchResults_DoubleClick);
			// 
			// columnHeader1
			// 
			this.columnHeader1.Text = "File Name";
			this.columnHeader1.Width = 345;
			// 
			// columnHeader2
			// 
			this.columnHeader2.Text = "File Path";
			this.columnHeader2.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
			this.columnHeader2.Width = 638;
			// 
			// SearchContext
			// 
			this.SearchContext.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.CheckOutMenuItem,
            this.unCheckoutToolStripMenuItem,
            this.openToolStripMenuItem});
			this.SearchContext.Name = "SearchContext";
			this.SearchContext.Size = new System.Drawing.Size(141, 70);
			// 
			// CheckOutMenuItem
			// 
			this.CheckOutMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.checkoutOpenToolStripMenuItem});
			this.CheckOutMenuItem.Name = "CheckOutMenuItem";
			this.CheckOutMenuItem.Size = new System.Drawing.Size(140, 22);
			this.CheckOutMenuItem.Text = "Checkout";
			this.CheckOutMenuItem.Click += new System.EventHandler(this.CheckOutMenuItem_Click);
			// 
			// checkoutOpenToolStripMenuItem
			// 
			this.checkoutOpenToolStripMenuItem.Name = "checkoutOpenToolStripMenuItem";
			this.checkoutOpenToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
			this.checkoutOpenToolStripMenuItem.Text = "Checkout and Open";
			this.checkoutOpenToolStripMenuItem.Click += new System.EventHandler(this.checkoutOpenToolStripMenuItem_Click);
			// 
			// unCheckoutToolStripMenuItem
			// 
			this.unCheckoutToolStripMenuItem.Name = "unCheckoutToolStripMenuItem";
			this.unCheckoutToolStripMenuItem.Size = new System.Drawing.Size(140, 22);
			this.unCheckoutToolStripMenuItem.Text = "UnCheckout";
			this.unCheckoutToolStripMenuItem.Click += new System.EventHandler(this.unCheckoutToolStripMenuItem_Click);
			// 
			// openToolStripMenuItem
			// 
			this.openToolStripMenuItem.Name = "openToolStripMenuItem";
			this.openToolStripMenuItem.Size = new System.Drawing.Size(140, 22);
			this.openToolStripMenuItem.Text = "Open";
			this.openToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);
			// 
			// OdooDeletedIsLocal
			// 
			this.OdooDeletedIsLocal.AutoSize = true;
			this.OdooDeletedIsLocal.Location = new System.Drawing.Point(214, 51);
			this.OdooDeletedIsLocal.Name = "OdooDeletedIsLocal";
			this.OdooDeletedIsLocal.Size = new System.Drawing.Size(138, 17);
			this.OdooDeletedIsLocal.TabIndex = 101;
			this.OdooDeletedIsLocal.Text = "Deleted Existing Locally";
			this.OdooDeletedIsLocal.UseVisualStyleBackColor = true;
			this.OdooDeletedIsLocal.CheckedChanged += new System.EventHandler(this.OdooDeletedIsLocal_CheckedChanged);
			// 
			// btnCancel
			// 
			this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btnCancel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
			this.btnCancel.BackColor = System.Drawing.Color.PaleVioletRed;
			this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.btnCancel.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.btnCancel.Location = new System.Drawing.Point(11, 557);
			this.btnCancel.Name = "btnCancel";
			this.btnCancel.Size = new System.Drawing.Size(150, 35);
			this.btnCancel.TabIndex = 104;
			this.btnCancel.Text = "Cancel";
			this.btnCancel.UseVisualStyleBackColor = false;
			this.btnCancel.Click += new System.EventHandler(this.OdooCancel_Click);
			// 
			// btnSearch
			// 
			this.btnSearch.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnSearch.BackColor = System.Drawing.Color.PaleGreen;
			this.btnSearch.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold);
			this.btnSearch.Location = new System.Drawing.Point(525, 557);
			this.btnSearch.Name = "btnSearch";
			this.btnSearch.Size = new System.Drawing.Size(474, 35);
			this.btnSearch.TabIndex = 103;
			this.btnSearch.Text = "Search";
			this.btnSearch.UseVisualStyleBackColor = false;
			this.btnSearch.Click += new System.EventHandler(this.OdooSearch_Click);
			// 
			// label3
			// 
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(582, 9);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(34, 13);
			this.label3.TabIndex = 96;
			this.label3.Text = "Value";
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(370, 8);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(46, 13);
			this.label2.TabIndex = 95;
			this.label2.Text = "Property";
			// 
			// OdooSearchPropValue
			// 
			this.OdooSearchPropValue.Location = new System.Drawing.Point(585, 25);
			this.OdooSearchPropValue.Name = "OdooSearchPropValue";
			this.OdooSearchPropValue.Size = new System.Drawing.Size(248, 20);
			this.OdooSearchPropValue.TabIndex = 99;
			// 
			// OdooSearchProperty
			// 
			//this.OdooSearchProperty.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
			//this.OdooSearchProperty.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
			this.OdooSearchProperty.FormattingEnabled = true;
			this.OdooSearchProperty.Location = new System.Drawing.Point(370, 24);
			this.OdooSearchProperty.Name = "OdooSearchProperty";
			this.OdooSearchProperty.Size = new System.Drawing.Size(155, 21);
			this.OdooSearchProperty.TabIndex = 98;
			// 
			// OdooCheckedMe
			// 
			this.OdooCheckedMe.AutoSize = true;
			this.OdooCheckedMe.Location = new System.Drawing.Point(11, 51);
			this.OdooCheckedMe.Name = "OdooCheckedMe";
			this.OdooCheckedMe.Size = new System.Drawing.Size(119, 17);
			this.OdooCheckedMe.TabIndex = 100;
			this.OdooCheckedMe.Text = "Checked Out to Me";
			this.OdooCheckedMe.UseVisualStyleBackColor = true;
			this.OdooCheckedMe.CheckedChanged += new System.EventHandler(this.OdooCheckedMe_CheckedChanged);
			// 
			// FileNameTextbox
			// 
			this.FileNameTextbox.Location = new System.Drawing.Point(65, 25);
			this.FileNameTextbox.Name = "FileNameTextbox";
			this.FileNameTextbox.Size = new System.Drawing.Size(287, 20);
			this.FileNameTextbox.TabIndex = 97;
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(65, 9);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(79, 13);
			this.label1.TabIndex = 94;
			this.label1.Text = "Filename Value";
			// 
			// OdooSearchPropList
			// 
			this.OdooSearchPropList.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.OdooSearchPropList.HideSelection = false;
			this.OdooSearchPropList.Location = new System.Drawing.Point(370, 52);
			this.OdooSearchPropList.Name = "OdooSearchPropList";
			this.OdooSearchPropList.Size = new System.Drawing.Size(463, 81);
			this.OdooSearchPropList.TabIndex = 109;
			this.OdooSearchPropList.UseCompatibleStateImageBehavior = false;
			this.OdooSearchPropList.View = System.Windows.Forms.View.Details;
			// 
			// OdooPropAdd
			// 
			this.OdooPropAdd.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.OdooPropAdd.Location = new System.Drawing.Point(839, 22);
			this.OdooPropAdd.Name = "OdooPropAdd";
			this.OdooPropAdd.RightToLeft = System.Windows.Forms.RightToLeft.No;
			this.OdooPropAdd.Size = new System.Drawing.Size(160, 69);
			this.OdooPropAdd.TabIndex = 110;
			this.OdooPropAdd.Text = "Add";
			this.OdooPropAdd.UseVisualStyleBackColor = true;
			this.OdooPropAdd.Click += new System.EventHandler(this.OdooPropAdd_Click);
			// 
			// OdooPropDelete
			// 
			this.OdooPropDelete.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.OdooPropDelete.Location = new System.Drawing.Point(839, 97);
			this.OdooPropDelete.Name = "OdooPropDelete";
			this.OdooPropDelete.RightToLeft = System.Windows.Forms.RightToLeft.No;
			this.OdooPropDelete.Size = new System.Drawing.Size(75, 36);
			this.OdooPropDelete.TabIndex = 111;
			this.OdooPropDelete.Text = "Delete";
			this.OdooPropDelete.UseVisualStyleBackColor = true;
			this.OdooPropDelete.Click += new System.EventHandler(this.OdooPropDelete_Click);
			// 
			// OdooSearchPropEqual
			// 
			//this.OdooSearchPropEqual.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
			//this.OdooSearchPropEqual.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
			this.OdooSearchPropEqual.FormattingEnabled = true;
			this.OdooSearchPropEqual.Location = new System.Drawing.Point(531, 24);
			this.OdooSearchPropEqual.Name = "OdooSearchPropEqual";
			this.OdooSearchPropEqual.Size = new System.Drawing.Size(48, 21);
			this.OdooSearchPropEqual.TabIndex = 112;
			// 
			// OdooPropComparer
			// 
			this.OdooPropComparer.AutoSize = true;
			this.OdooPropComparer.Location = new System.Drawing.Point(548, 9);
			this.OdooPropComparer.Name = "OdooPropComparer";
			this.OdooPropComparer.Size = new System.Drawing.Size(14, 13);
			this.OdooPropComparer.TabIndex = 113;
			this.OdooPropComparer.Text = "is";
			// 
			// OdooPropertyReset
			// 
			this.OdooPropertyReset.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.OdooPropertyReset.Location = new System.Drawing.Point(920, 97);
			this.OdooPropertyReset.Name = "OdooPropertyReset";
			this.OdooPropertyReset.RightToLeft = System.Windows.Forms.RightToLeft.No;
			this.OdooPropertyReset.Size = new System.Drawing.Size(79, 36);
			this.OdooPropertyReset.TabIndex = 114;
			this.OdooPropertyReset.Text = "Reset List";
			this.OdooPropertyReset.UseVisualStyleBackColor = true;
			this.OdooPropertyReset.Click += new System.EventHandler(this.OdooPropertyReset_Click);
			// 
			// label4
			// 
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(28, 9);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(14, 13);
			this.label4.TabIndex = 116;
			this.label4.Text = "is";
			// 
			// OdooSearchComparer
			// 
			//this.OdooSearchComparer.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
			//this.OdooSearchComparer.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
			this.OdooSearchComparer.FormattingEnabled = true;
			this.OdooSearchComparer.Location = new System.Drawing.Point(11, 24);
			this.OdooSearchComparer.Name = "OdooSearchComparer";
			this.OdooSearchComparer.Size = new System.Drawing.Size(48, 21);
			this.OdooSearchComparer.TabIndex = 115;
			// 
			// SearchOdoo
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(1011, 598);
			this.Controls.Add(this.label4);
			this.Controls.Add(this.OdooSearchComparer);
			this.Controls.Add(this.OdooPropertyReset);
			this.Controls.Add(this.OdooPropComparer);
			this.Controls.Add(this.OdooSearchPropEqual);
			this.Controls.Add(this.OdooPropDelete);
			this.Controls.Add(this.OdooPropAdd);
			this.Controls.Add(this.OdooSearchPropList);
			this.Controls.Add(this.lblMaxRes);
			this.Controls.Add(this.OdooMaxRes);
			this.Controls.Add(this.btnReset);
			this.Controls.Add(this.OdooLocalOnly);
			this.Controls.Add(this.OdooSearchResults);
			this.Controls.Add(this.OdooDeletedIsLocal);
			this.Controls.Add(this.btnCancel);
			this.Controls.Add(this.btnSearch);
			this.Controls.Add(this.label3);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.OdooSearchPropValue);
			this.Controls.Add(this.OdooSearchProperty);
			this.Controls.Add(this.OdooCheckedMe);
			this.Controls.Add(this.FileNameTextbox);
			this.Controls.Add(this.label1);
			this.Name = "SearchOdoo";
			this.Text = "SearchOdoo";
			this.SearchContext.ResumeLayout(false);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label lblMaxRes;
		private System.Windows.Forms.TextBox OdooMaxRes;
		private System.Windows.Forms.Button btnReset;
		private System.Windows.Forms.CheckBox OdooLocalOnly;
		private System.Windows.Forms.ListView OdooSearchResults;
		private System.Windows.Forms.ColumnHeader columnHeader1;
		private System.Windows.Forms.ColumnHeader columnHeader2;
		private System.Windows.Forms.CheckBox OdooDeletedIsLocal;
		private System.Windows.Forms.Button btnCancel;
		private System.Windows.Forms.Button btnSearch;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.TextBox OdooSearchPropValue;
		private System.Windows.Forms.ComboBox OdooSearchProperty;
		private System.Windows.Forms.CheckBox OdooCheckedMe;
		private System.Windows.Forms.TextBox FileNameTextbox;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.ListView OdooSearchPropList;
		private System.Windows.Forms.Button OdooPropAdd;
		private System.Windows.Forms.Button OdooPropDelete;
		private System.Windows.Forms.ComboBox OdooSearchPropEqual;
		private System.Windows.Forms.Label OdooPropComparer;
		private System.Windows.Forms.Button OdooPropertyReset;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.ComboBox OdooSearchComparer;
		private System.Windows.Forms.ContextMenuStrip SearchContext;
		private System.Windows.Forms.ToolStripMenuItem CheckOutMenuItem;
		private System.Windows.Forms.ToolStripMenuItem unCheckoutToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem checkoutOpenToolStripMenuItem;
	}
}