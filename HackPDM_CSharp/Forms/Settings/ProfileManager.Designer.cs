namespace HackPDM
{
    partial class ProfileManager
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ProfileManager));
            this.OdooLoginBtn = new System.Windows.Forms.Button();
            this.odooSettingsBtn = new System.Windows.Forms.Button();
            this.HackSettingsBtn = new System.Windows.Forms.Button();
            this.ProfileManStatusList = new System.Windows.Forms.ListView();
            this.ProfileManStatusHead = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ProfileManMessageHead = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.NotifyPopup = new System.Windows.Forms.NotifyIcon(this.components);
            this.SuspendLayout();
            // 
            // OdooLoginBtn
            // 
            this.OdooLoginBtn.Location = new System.Drawing.Point(12, 83);
            this.OdooLoginBtn.Name = "OdooLoginBtn";
            this.OdooLoginBtn.Size = new System.Drawing.Size(173, 77);
            this.OdooLoginBtn.TabIndex = 149;
            this.OdooLoginBtn.Text = "Odoo Login";
            this.OdooLoginBtn.UseVisualStyleBackColor = true;
            this.OdooLoginBtn.Click += new System.EventHandler(this.OdooLoginBtn_Click);
            // 
            // odooSettingsBtn
            // 
            this.odooSettingsBtn.Location = new System.Drawing.Point(12, 48);
            this.odooSettingsBtn.Name = "odooSettingsBtn";
            this.odooSettingsBtn.Size = new System.Drawing.Size(173, 29);
            this.odooSettingsBtn.TabIndex = 150;
            this.odooSettingsBtn.Text = "Odoo Settings";
            this.odooSettingsBtn.UseVisualStyleBackColor = true;
            this.odooSettingsBtn.Click += new System.EventHandler(this.odooSettingsBtn_Click);
            // 
            // HackSettingsBtn
            // 
            this.HackSettingsBtn.Location = new System.Drawing.Point(12, 13);
            this.HackSettingsBtn.Name = "HackSettingsBtn";
            this.HackSettingsBtn.Size = new System.Drawing.Size(173, 29);
            this.HackSettingsBtn.TabIndex = 151;
            this.HackSettingsBtn.Text = "Hack Settings";
            this.HackSettingsBtn.UseVisualStyleBackColor = true;
            this.HackSettingsBtn.Click += new System.EventHandler(this.HackSettingsBtn_Click);
            // 
            // ProfileManStatusList
            // 
            this.ProfileManStatusList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.ProfileManStatusList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.ProfileManStatusHead,
            this.ProfileManMessageHead});
            this.ProfileManStatusList.HideSelection = false;
            this.ProfileManStatusList.Location = new System.Drawing.Point(192, 13);
            this.ProfileManStatusList.Name = "ProfileManStatusList";
            this.ProfileManStatusList.Size = new System.Drawing.Size(383, 147);
            this.ProfileManStatusList.TabIndex = 152;
            this.ProfileManStatusList.UseCompatibleStateImageBehavior = false;
            this.ProfileManStatusList.View = System.Windows.Forms.View.Details;
            // 
            // ProfileManStatusHead
            // 
            this.ProfileManStatusHead.Text = "Status";
            // 
            // ProfileManMessageHead
            // 
            this.ProfileManMessageHead.Text = "Message";
            this.ProfileManMessageHead.Width = 313;
            // 
            // NotifyPopup
            // 
            this.NotifyPopup.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info;
            this.NotifyPopup.Icon = ((System.Drawing.Icon)(resources.GetObject("NotifyPopup.Icon")));
            this.NotifyPopup.Text = "HackPDM";
            this.NotifyPopup.Visible = true;
            // 
            // ProfileManager
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(587, 172);
            this.Controls.Add(this.ProfileManStatusList);
            this.Controls.Add(this.HackSettingsBtn);
            this.Controls.Add(this.odooSettingsBtn);
            this.Controls.Add(this.OdooLoginBtn);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Name = "ProfileManager";
            this.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.Text = "ProfileManager";
            this.Load += new System.EventHandler(this.ProfileManager_Load);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Button OdooLoginBtn;
        private System.Windows.Forms.Button odooSettingsBtn;
		private System.Windows.Forms.Button HackSettingsBtn;
		private System.Windows.Forms.ListView ProfileManStatusList;
		private System.Windows.Forms.ColumnHeader ProfileManStatusHead;
		private System.Windows.Forms.ColumnHeader ProfileManMessageHead;
        public System.Windows.Forms.NotifyIcon NotifyPopup;
    }
}