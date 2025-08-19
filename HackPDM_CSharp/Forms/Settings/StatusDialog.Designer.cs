/*
 * Created by SharpDevelop.
 * User: matt
 * Date: 12/24/2012
 * Time: 9:30 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
namespace HackPDM.Forms.Settings
{
    partial class StatusDialog
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
            this.cmdCancel = new System.Windows.Forms.Button();
            this.cmdClose = new System.Windows.Forms.Button();
            this.cbxAutoClose = new System.Windows.Forms.CheckBox();
            this.StatusList = new System.Windows.Forms.ListView();
            this.chAction = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.chDesc = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.fileCheckStatus = new System.Windows.Forms.ProgressBar();
            this.ProgressText = new System.Windows.Forms.Label();
            this.StatusSettings = new System.Windows.Forms.Button();
            this.SkippedLabel = new System.Windows.Forms.Label();
            this.TotalDownload = new System.Windows.Forms.Label();
            this.Downloaded = new System.Windows.Forms.Label();
            this.StatusContainer1 = new System.Windows.Forms.SplitContainer();
            this.StatusContainer2 = new System.Windows.Forms.SplitContainer();
            this.InfoList = new System.Windows.Forms.ListView();
            this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ErrorList = new System.Windows.Forms.ListView();
            this.columnHeader3 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader4 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            ((System.ComponentModel.ISupportInitialize)(this.StatusContainer1)).BeginInit();
            this.StatusContainer1.Panel1.SuspendLayout();
            this.StatusContainer1.Panel2.SuspendLayout();
            this.StatusContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.StatusContainer2)).BeginInit();
            this.StatusContainer2.Panel1.SuspendLayout();
            this.StatusContainer2.Panel2.SuspendLayout();
            this.StatusContainer2.SuspendLayout();
            this.SuspendLayout();
            // 
            // cmdCancel
            // 
            this.cmdCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cmdCancel.Location = new System.Drawing.Point(1120, 516);
            this.cmdCancel.Name = "cmdCancel";
            this.cmdCancel.Size = new System.Drawing.Size(75, 23);
            this.cmdCancel.TabIndex = 0;
            this.cmdCancel.Text = "Cancel";
            this.cmdCancel.UseVisualStyleBackColor = true;
            this.cmdCancel.Click += new System.EventHandler(this.CmdCancelClick);
            // 
            // cmdClose
            // 
            this.cmdClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdClose.Enabled = false;
            this.cmdClose.Location = new System.Drawing.Point(1201, 516);
            this.cmdClose.Name = "cmdClose";
            this.cmdClose.Size = new System.Drawing.Size(75, 23);
            this.cmdClose.TabIndex = 2;
            this.cmdClose.Text = "Close";
            this.cmdClose.UseVisualStyleBackColor = true;
            this.cmdClose.Click += new System.EventHandler(this.CmdCloseClick);
            // 
            // cbxAutoClose
            // 
            this.cbxAutoClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cbxAutoClose.Location = new System.Drawing.Point(947, 516);
            this.cbxAutoClose.Name = "cbxAutoClose";
            this.cbxAutoClose.Size = new System.Drawing.Size(138, 24);
            this.cbxAutoClose.TabIndex = 3;
            this.cbxAutoClose.Text = "Close When Complete";
            this.cbxAutoClose.UseVisualStyleBackColor = true;
            // 
            // StatusList
            // 
            this.StatusList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.chAction,
            this.chDesc});
            this.StatusList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.StatusList.HideSelection = false;
            this.StatusList.Location = new System.Drawing.Point(0, 0);
            this.StatusList.Name = "StatusList";
            this.StatusList.Size = new System.Drawing.Size(1288, 224);
            this.StatusList.TabIndex = 4;
            this.StatusList.UseCompatibleStateImageBehavior = false;
            this.StatusList.View = System.Windows.Forms.View.Details;
            this.StatusList.DrawItem += new System.Windows.Forms.DrawListViewItemEventHandler(this.lvMessages_DrawItem);
            this.StatusList.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.Lists_ItemCheck);
            // 
            // chAction
            // 
            this.chAction.Text = "Action";
            this.chAction.Width = 120;
            // 
            // chDesc
            // 
            this.chDesc.Text = "Description";
            this.chDesc.Width = 1158;
            // 
            // fileCheckStatus
            // 
            this.fileCheckStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.fileCheckStatus.Location = new System.Drawing.Point(12, 487);
            this.fileCheckStatus.Name = "fileCheckStatus";
            this.fileCheckStatus.Size = new System.Drawing.Size(1264, 15);
            this.fileCheckStatus.Step = 1;
            this.fileCheckStatus.TabIndex = 5;
            // 
            // ProgressText
            // 
            this.ProgressText.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.ProgressText.AutoSize = true;
            this.ProgressText.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ProgressText.Location = new System.Drawing.Point(12, 506);
            this.ProgressText.Name = "ProgressText";
            this.ProgressText.Size = new System.Drawing.Size(42, 34);
            this.ProgressText.TabIndex = 6;
            this.ProgressText.Text = "(0%) \r\n0 / 0 ";
            // 
            // StatusSettings
            // 
            this.StatusSettings.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.StatusSettings.Location = new System.Drawing.Point(800, 517);
            this.StatusSettings.Name = "StatusSettings";
            this.StatusSettings.Size = new System.Drawing.Size(141, 23);
            this.StatusSettings.TabIndex = 7;
            this.StatusSettings.Text = "configure settings";
            this.StatusSettings.UseVisualStyleBackColor = true;
            this.StatusSettings.Click += new System.EventHandler(this.StatusSettings_Click);
            // 
            // SkippedLabel
            // 
            this.SkippedLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.SkippedLabel.AutoSize = true;
            this.SkippedLabel.Location = new System.Drawing.Point(378, 526);
            this.SkippedLabel.Name = "SkippedLabel";
            this.SkippedLabel.Size = new System.Drawing.Size(61, 13);
            this.SkippedLabel.TabIndex = 8;
            this.SkippedLabel.Text = "(0) Skipped";
            // 
            // TotalDownload
            // 
            this.TotalDownload.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.TotalDownload.AutoSize = true;
            this.TotalDownload.Location = new System.Drawing.Point(1063, -1);
            this.TotalDownload.Name = "TotalDownload";
            this.TotalDownload.Size = new System.Drawing.Size(100, 13);
            this.TotalDownload.TabIndex = 9;
            this.TotalDownload.Text = "Total Downloaded: ";
            // 
            // Downloaded
            // 
            this.Downloaded.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.Downloaded.AutoSize = true;
            this.Downloaded.Location = new System.Drawing.Point(854, -1);
            this.Downloaded.Name = "Downloaded";
            this.Downloaded.Size = new System.Drawing.Size(73, 13);
            this.Downloaded.TabIndex = 10;
            this.Downloaded.Text = "Downloaded: ";
            // 
            // StatusContainer1
            // 
            this.StatusContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.StatusContainer1.Location = new System.Drawing.Point(0, 15);
            this.StatusContainer1.Name = "StatusContainer1";
            this.StatusContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // StatusContainer1.Panel1
            // 
            this.StatusContainer1.Panel1.Controls.Add(this.StatusList);
            // 
            // StatusContainer1.Panel2
            // 
            this.StatusContainer1.Panel2.Controls.Add(this.StatusContainer2);
            this.StatusContainer1.Size = new System.Drawing.Size(1288, 466);
            this.StatusContainer1.SplitterDistance = 224;
            this.StatusContainer1.TabIndex = 11;
            // 
            // StatusContainer2
            // 
            this.StatusContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.StatusContainer2.Location = new System.Drawing.Point(0, 0);
            this.StatusContainer2.Name = "StatusContainer2";
            // 
            // StatusContainer2.Panel1
            // 
            this.StatusContainer2.Panel1.Controls.Add(this.InfoList);
            // 
            // StatusContainer2.Panel2
            // 
            this.StatusContainer2.Panel2.Controls.Add(this.ErrorList);
            this.StatusContainer2.Size = new System.Drawing.Size(1288, 238);
            this.StatusContainer2.SplitterDistance = 617;
            this.StatusContainer2.TabIndex = 0;
            // 
            // InfoList
            // 
            this.InfoList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader2});
            this.InfoList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.InfoList.HideSelection = false;
            this.InfoList.Location = new System.Drawing.Point(0, 0);
            this.InfoList.Name = "InfoList";
            this.InfoList.Size = new System.Drawing.Size(617, 238);
            this.InfoList.TabIndex = 5;
            this.InfoList.UseCompatibleStateImageBehavior = false;
            this.InfoList.View = System.Windows.Forms.View.Details;
            this.InfoList.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.Lists_ItemCheck);
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "Action";
            this.columnHeader1.Width = 94;
            // 
            // columnHeader2
            // 
            this.columnHeader2.Text = "Description";
            this.columnHeader2.Width = 514;
            // 
            // ErrorList
            // 
            this.ErrorList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader3,
            this.columnHeader4});
            this.ErrorList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ErrorList.HideSelection = false;
            this.ErrorList.Location = new System.Drawing.Point(0, 0);
            this.ErrorList.Name = "ErrorList";
            this.ErrorList.Size = new System.Drawing.Size(667, 238);
            this.ErrorList.TabIndex = 6;
            this.ErrorList.UseCompatibleStateImageBehavior = false;
            this.ErrorList.View = System.Windows.Forms.View.Details;
            this.ErrorList.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.Lists_ItemCheck);
            // 
            // columnHeader3
            // 
            this.columnHeader3.Text = "Action";
            this.columnHeader3.Width = 120;
            // 
            // columnHeader4
            // 
            this.columnHeader4.Text = "Description";
            this.columnHeader4.Width = 535;
            // 
            // StatusDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cmdCancel;
            this.ClientSize = new System.Drawing.Size(1288, 551);
            this.ControlBox = false;
            this.Controls.Add(this.StatusContainer1);
            this.Controls.Add(this.Downloaded);
            this.Controls.Add(this.TotalDownload);
            this.Controls.Add(this.SkippedLabel);
            this.Controls.Add(this.StatusSettings);
            this.Controls.Add(this.ProgressText);
            this.Controls.Add(this.fileCheckStatus);
            this.Controls.Add(this.cbxAutoClose);
            this.Controls.Add(this.cmdClose);
            this.Controls.Add(this.cmdCancel);
            this.Name = "StatusDialog";
            this.Opacity = 0.98D;
            this.Text = "StatusDialog";
            this.Load += new System.EventHandler(this.StatusDialog_Load);
            this.StatusContainer1.Panel1.ResumeLayout(false);
            this.StatusContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.StatusContainer1)).EndInit();
            this.StatusContainer1.ResumeLayout(false);
            this.StatusContainer2.Panel1.ResumeLayout(false);
            this.StatusContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.StatusContainer2)).EndInit();
            this.StatusContainer2.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        private System.Windows.Forms.ColumnHeader chDesc;
        private System.Windows.Forms.ColumnHeader chAction;
        private System.Windows.Forms.ListView StatusList;
        private System.Windows.Forms.CheckBox cbxAutoClose;
        private System.Windows.Forms.Button cmdClose;
        private System.Windows.Forms.Button cmdCancel;
        private System.Windows.Forms.ProgressBar fileCheckStatus;
        private System.Windows.Forms.Label ProgressText;
        private System.Windows.Forms.Button StatusSettings;
        private System.Windows.Forms.Label SkippedLabel;
        private System.Windows.Forms.Label TotalDownload;
        private System.Windows.Forms.Label Downloaded;
        private System.Windows.Forms.SplitContainer StatusContainer1;
        private System.Windows.Forms.SplitContainer StatusContainer2;
        private System.Windows.Forms.ListView InfoList;
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.ColumnHeader columnHeader2;
        private System.Windows.Forms.ListView ErrorList;
        private System.Windows.Forms.ColumnHeader columnHeader3;
        private System.Windows.Forms.ColumnHeader columnHeader4;
    }
}
