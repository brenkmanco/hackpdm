/*
 * Created by SharpDevelop.
 * User: matt
 * Date: 12/24/2012
 * Time: 9:30 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
namespace HackPDM
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
            this.lvMessages = new System.Windows.Forms.ListView();
            this.chAction = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.chDesc = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.SuspendLayout();
            // 
            // cmdCancel
            // 
            this.cmdCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdCancel.Location = new System.Drawing.Point(456, 287);
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
            this.cmdClose.Location = new System.Drawing.Point(537, 287);
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
            this.cbxAutoClose.Location = new System.Drawing.Point(283, 287);
            this.cbxAutoClose.Name = "cbxAutoClose";
            this.cbxAutoClose.Size = new System.Drawing.Size(138, 24);
            this.cbxAutoClose.TabIndex = 3;
            this.cbxAutoClose.Text = "Close When Complete";
            this.cbxAutoClose.UseVisualStyleBackColor = true;
            // 
            // lvMessages
            // 
            this.lvMessages.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lvMessages.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.chAction,
            this.chDesc});
            this.lvMessages.Location = new System.Drawing.Point(12, 12);
            this.lvMessages.Name = "lvMessages";
            this.lvMessages.Size = new System.Drawing.Size(600, 269);
            this.lvMessages.TabIndex = 4;
            this.lvMessages.UseCompatibleStateImageBehavior = false;
            this.lvMessages.View = System.Windows.Forms.View.Details;
            // 
            // chAction
            // 
            this.chAction.Text = "Action";
            this.chAction.Width = 120;
            // 
            // chDesc
            // 
            this.chDesc.Text = "Description";
            this.chDesc.Width = 460;
            // 
            // StatusDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(624, 322);
            this.ControlBox = false;
            this.Controls.Add(this.lvMessages);
            this.Controls.Add(this.cbxAutoClose);
            this.Controls.Add(this.cmdClose);
            this.Controls.Add(this.cmdCancel);
            this.Name = "StatusDialog";
            this.Text = "StatusDialog";
            this.ResumeLayout(false);

        }
        private System.Windows.Forms.ColumnHeader chDesc;
        private System.Windows.Forms.ColumnHeader chAction;
        private System.Windows.Forms.ListView lvMessages;
        private System.Windows.Forms.CheckBox cbxAutoClose;
        private System.Windows.Forms.Button cmdClose;
        private System.Windows.Forms.Button cmdCancel;
    }
}
