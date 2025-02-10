namespace HackPDM
{
    partial class OdooViewer
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
			this.model_viewer = new System.Windows.Forms.TreeView();
			this.StatusBox = new System.Windows.Forms.ListBox();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.CreateRecord = new System.Windows.Forms.TabControl();
			this.CreatorTab = new System.Windows.Forms.TabPage();
			this.tabPage4 = new System.Windows.Forms.TabPage();
			this.tabControl1.SuspendLayout();
			this.CreateRecord.SuspendLayout();
			this.SuspendLayout();
			// 
			// model_viewer
			// 
			this.model_viewer.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
			this.model_viewer.Location = new System.Drawing.Point(12, 13);
			this.model_viewer.Name = "model_viewer";
			this.model_viewer.Size = new System.Drawing.Size(226, 722);
			this.model_viewer.TabIndex = 0;
			this.model_viewer.NodeMouseClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.model_viewer_NodeMouseClick);
			// 
			// StatusBox
			// 
			this.StatusBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
			this.StatusBox.FormattingEnabled = true;
			this.StatusBox.Location = new System.Drawing.Point(244, 497);
			this.StatusBox.Name = "StatusBox";
			this.StatusBox.Size = new System.Drawing.Size(836, 238);
			this.StatusBox.TabIndex = 2;
			this.StatusBox.SelectedIndexChanged += new System.EventHandler(this.StatusBox_SelectedIndexChanged);
			// 
			// tabPage1
			// 
			this.tabPage1.Location = new System.Drawing.Point(4, 22);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage1.Size = new System.Drawing.Size(417, 456);
			this.tabPage1.TabIndex = 0;
			this.tabPage1.Text = "model fields";
			this.tabPage1.UseVisualStyleBackColor = true;
			this.tabPage1.Click += new System.EventHandler(this.tabPage1_Click);
			// 
			// tabPage2
			// 
			this.tabPage2.Location = new System.Drawing.Point(4, 22);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage2.Size = new System.Drawing.Size(417, 456);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "model instances";
			this.tabPage2.UseVisualStyleBackColor = true;
			// 
			// tabControl1
			// 
			this.tabControl1.Controls.Add(this.tabPage1);
			this.tabControl1.Controls.Add(this.tabPage2);
			this.tabControl1.Location = new System.Drawing.Point(244, 13);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(425, 482);
			this.tabControl1.TabIndex = 5;
			// 
			// CreateRecord
			// 
			this.CreateRecord.Controls.Add(this.CreatorTab);
			this.CreateRecord.Controls.Add(this.tabPage4);
			this.CreateRecord.Location = new System.Drawing.Point(675, 13);
			this.CreateRecord.Name = "CreateRecord";
			this.CreateRecord.SelectedIndex = 0;
			this.CreateRecord.Size = new System.Drawing.Size(409, 482);
			this.CreateRecord.TabIndex = 6;
			// 
			// CreatorTab
			// 
			this.CreatorTab.Location = new System.Drawing.Point(4, 22);
			this.CreatorTab.Name = "CreatorTab";
			this.CreatorTab.Padding = new System.Windows.Forms.Padding(3);
			this.CreatorTab.Size = new System.Drawing.Size(401, 456);
			this.CreatorTab.TabIndex = 0;
			this.CreatorTab.Text = "Create Record";
			this.CreatorTab.UseVisualStyleBackColor = true;
			// 
			// tabPage4
			// 
			this.tabPage4.Location = new System.Drawing.Point(4, 22);
			this.tabPage4.Name = "tabPage4";
			this.tabPage4.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage4.Size = new System.Drawing.Size(401, 456);
			this.tabPage4.TabIndex = 1;
			this.tabPage4.Text = "tabPage4";
			this.tabPage4.UseVisualStyleBackColor = true;
			// 
			// OdooViewer
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(1096, 753);
			this.Controls.Add(this.CreateRecord);
			this.Controls.Add(this.tabControl1);
			this.Controls.Add(this.StatusBox);
			this.Controls.Add(this.model_viewer);
			this.Name = "OdooViewer";
			this.Text = "OdooViewer";
			this.tabControl1.ResumeLayout(false);
			this.CreateRecord.ResumeLayout(false);
			this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TreeView model_viewer;
        private System.Windows.Forms.ListBox StatusBox;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.TabControl CreateRecord;
        private System.Windows.Forms.TabPage CreatorTab;
        private System.Windows.Forms.TabPage tabPage4;
    }
}