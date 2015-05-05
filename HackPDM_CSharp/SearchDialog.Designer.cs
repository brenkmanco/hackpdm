﻿namespace HackPDM
{
    partial class SearchDialog
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
            this.label1 = new System.Windows.Forms.Label();
            this.txtFilename = new System.Windows.Forms.TextBox();
            this.cbxCheckedMe = new System.Windows.Forms.CheckBox();
            this.cboProperty = new System.Windows.Forms.ComboBox();
            this.txtProperty = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.btnSearch = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.cbxDeletedLocal = new System.Windows.Forms.CheckBox();
            this.lvSearchResults = new System.Windows.Forms.ListView();
            this.cmsListItemMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.goToToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cbxLocalOnly = new System.Windows.Forms.CheckBox();
            this.cmsListItemMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(93, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Filename Contains";
            // 
            // txtFilename
            // 
            this.txtFilename.Location = new System.Drawing.Point(15, 25);
            this.txtFilename.Name = "txtFilename";
            this.txtFilename.Size = new System.Drawing.Size(341, 20);
            this.txtFilename.TabIndex = 10;
            // 
            // cbxCheckedMe
            // 
            this.cbxCheckedMe.AutoSize = true;
            this.cbxCheckedMe.Location = new System.Drawing.Point(437, 12);
            this.cbxCheckedMe.Name = "cbxCheckedMe";
            this.cbxCheckedMe.Size = new System.Drawing.Size(119, 17);
            this.cbxCheckedMe.TabIndex = 40;
            this.cbxCheckedMe.Text = "Checked Out to Me";
            this.cbxCheckedMe.UseVisualStyleBackColor = true;
            // 
            // cboProperty
            // 
            this.cboProperty.FormattingEnabled = true;
            this.cboProperty.Location = new System.Drawing.Point(15, 70);
            this.cboProperty.Name = "cboProperty";
            this.cboProperty.Size = new System.Drawing.Size(121, 21);
            this.cboProperty.TabIndex = 20;
            // 
            // txtProperty
            // 
            this.txtProperty.Location = new System.Drawing.Point(150, 70);
            this.txtProperty.Name = "txtProperty";
            this.txtProperty.Size = new System.Drawing.Size(206, 20);
            this.txtProperty.TabIndex = 30;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 54);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(46, 13);
            this.label2.TabIndex = 8;
            this.label2.Text = "Property";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(147, 54);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(48, 13);
            this.label3.TabIndex = 9;
            this.label3.Text = "Contains";
            // 
            // btnSearch
            // 
            this.btnSearch.Location = new System.Drawing.Point(871, 12);
            this.btnSearch.Name = "btnSearch";
            this.btnSearch.Size = new System.Drawing.Size(75, 23);
            this.btnSearch.TabIndex = 70;
            this.btnSearch.Text = "Search";
            this.btnSearch.UseVisualStyleBackColor = true;
            this.btnSearch.Click += new System.EventHandler(this.btnSearch_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(871, 41);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 80;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // cbxDeletedLocal
            // 
            this.cbxDeletedLocal.AutoSize = true;
            this.cbxDeletedLocal.Location = new System.Drawing.Point(437, 35);
            this.cbxDeletedLocal.Name = "cbxDeletedLocal";
            this.cbxDeletedLocal.Size = new System.Drawing.Size(138, 17);
            this.cbxDeletedLocal.TabIndex = 50;
            this.cbxDeletedLocal.Text = "Deleted Existing Locally";
            this.cbxDeletedLocal.UseVisualStyleBackColor = true;
            // 
            // lvSearchResults
            // 
            this.lvSearchResults.Location = new System.Drawing.Point(12, 117);
            this.lvSearchResults.Name = "lvSearchResults";
            this.lvSearchResults.Size = new System.Drawing.Size(934, 424);
            this.lvSearchResults.TabIndex = 90;
            this.lvSearchResults.UseCompatibleStateImageBehavior = false;
            // 
            // cmsListItemMenu
            // 
            this.cmsListItemMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.goToToolStripMenuItem,
            this.openToolStripMenuItem});
            this.cmsListItemMenu.Name = "cmsListItemMenu";
            this.cmsListItemMenu.Size = new System.Drawing.Size(107, 48);
            // 
            // goToToolStripMenuItem
            // 
            this.goToToolStripMenuItem.Name = "goToToolStripMenuItem";
            this.goToToolStripMenuItem.Size = new System.Drawing.Size(106, 22);
            this.goToToolStripMenuItem.Text = "Go To";
            this.goToToolStripMenuItem.Click += new System.EventHandler(this.goToToolStripMenuItem_Click);
            // 
            // openToolStripMenuItem
            // 
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.Size = new System.Drawing.Size(106, 22);
            this.openToolStripMenuItem.Text = "Open";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);
            // 
            // cbxLocalOnly
            // 
            this.cbxLocalOnly.AutoSize = true;
            this.cbxLocalOnly.Location = new System.Drawing.Point(437, 58);
            this.cbxLocalOnly.Name = "cbxLocalOnly";
            this.cbxLocalOnly.Size = new System.Drawing.Size(122, 17);
            this.cbxLocalOnly.TabIndex = 60;
            this.cbxLocalOnly.Text = "Only Existing Locally";
            this.cbxLocalOnly.UseVisualStyleBackColor = true;
            // 
            // SearchDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(958, 553);
            this.Controls.Add(this.cbxLocalOnly);
            this.Controls.Add(this.lvSearchResults);
            this.Controls.Add(this.cbxDeletedLocal);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnSearch);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.txtProperty);
            this.Controls.Add(this.cboProperty);
            this.Controls.Add(this.cbxCheckedMe);
            this.Controls.Add(this.txtFilename);
            this.Controls.Add(this.label1);
            this.Name = "SearchDialog";
            this.Text = "Search";
            this.cmsListItemMenu.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtFilename;
        private System.Windows.Forms.CheckBox cbxCheckedMe;
        private System.Windows.Forms.ComboBox cboProperty;
        private System.Windows.Forms.TextBox txtProperty;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnSearch;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.CheckBox cbxDeletedLocal;
        private System.Windows.Forms.ListView lvSearchResults;
        private System.Windows.Forms.ContextMenuStrip cmsListItemMenu;
        private System.Windows.Forms.ToolStripMenuItem goToToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.CheckBox cbxLocalOnly;
    }
}