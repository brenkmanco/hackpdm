namespace HackPDM
{
    partial class OdooSettings
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
			this.lblAreaFactor = new System.Windows.Forms.Label();
			this.txtAreaFactor = new System.Windows.Forms.TextBox();
			this.lblSwKey = new System.Windows.Forms.Label();
			this.txtSwKey = new System.Windows.Forms.TextBox();
			this.lblOdooPass = new System.Windows.Forms.Label();
			this.lblOdooUser = new System.Windows.Forms.Label();
			this.txtOdooPass = new System.Windows.Forms.TextBox();
			this.txtOdooUser = new System.Windows.Forms.TextBox();
			this.btnSubmit = new System.Windows.Forms.Button();
			this.lblOdooDb = new System.Windows.Forms.Label();
			this.lblOdooUrl = new System.Windows.Forms.Label();
			this.txtOdooDb = new System.Windows.Forms.TextBox();
			this.txtOdooUrl = new System.Windows.Forms.TextBox();
			this.SuspendLayout();
			// 
			// lblAreaFactor
			// 
			this.lblAreaFactor.AutoSize = true;
			this.lblAreaFactor.Location = new System.Drawing.Point(12, 150);
			this.lblAreaFactor.Name = "lblAreaFactor";
			this.lblAreaFactor.Size = new System.Drawing.Size(62, 13);
			this.lblAreaFactor.TabIndex = 26;
			this.lblAreaFactor.Text = "Area Factor";
			// 
			// txtAreaFactor
			// 
			this.txtAreaFactor.Location = new System.Drawing.Point(77, 147);
			this.txtAreaFactor.Name = "txtAreaFactor";
			this.txtAreaFactor.Size = new System.Drawing.Size(283, 20);
			this.txtAreaFactor.TabIndex = 25;
			// 
			// lblSwKey
			// 
			this.lblSwKey.AutoSize = true;
			this.lblSwKey.Location = new System.Drawing.Point(12, 124);
			this.lblSwKey.Name = "lblSwKey";
			this.lblSwKey.Size = new System.Drawing.Size(46, 13);
			this.lblSwKey.TabIndex = 24;
			this.lblSwKey.Text = "SW Key";
			// 
			// txtSwKey
			// 
			this.txtSwKey.Location = new System.Drawing.Point(77, 121);
			this.txtSwKey.Name = "txtSwKey";
			this.txtSwKey.Size = new System.Drawing.Size(283, 20);
			this.txtSwKey.TabIndex = 23;
			// 
			// lblOdooPass
			// 
			this.lblOdooPass.AutoSize = true;
			this.lblOdooPass.Location = new System.Drawing.Point(12, 98);
			this.lblOdooPass.Name = "lblOdooPass";
			this.lblOdooPass.Size = new System.Drawing.Size(59, 13);
			this.lblOdooPass.TabIndex = 22;
			this.lblOdooPass.Text = "Odoo Pass";
			// 
			// lblOdooUser
			// 
			this.lblOdooUser.AutoSize = true;
			this.lblOdooUser.Location = new System.Drawing.Point(12, 72);
			this.lblOdooUser.Name = "lblOdooUser";
			this.lblOdooUser.Size = new System.Drawing.Size(58, 13);
			this.lblOdooUser.TabIndex = 21;
			this.lblOdooUser.Text = "Odoo User";
			// 
			// txtOdooPass
			// 
			this.txtOdooPass.Location = new System.Drawing.Point(77, 95);
			this.txtOdooPass.Name = "txtOdooPass";
			this.txtOdooPass.PasswordChar = '*';
			this.txtOdooPass.Size = new System.Drawing.Size(283, 20);
			this.txtOdooPass.TabIndex = 20;
			// 
			// txtOdooUser
			// 
			this.txtOdooUser.Location = new System.Drawing.Point(77, 69);
			this.txtOdooUser.Name = "txtOdooUser";
			this.txtOdooUser.Size = new System.Drawing.Size(283, 20);
			this.txtOdooUser.TabIndex = 19;
			// 
			// btnSubmit
			// 
			this.btnSubmit.Location = new System.Drawing.Point(285, 173);
			this.btnSubmit.Name = "btnSubmit";
			this.btnSubmit.Size = new System.Drawing.Size(75, 23);
			this.btnSubmit.TabIndex = 18;
			this.btnSubmit.Text = "Submit";
			this.btnSubmit.UseVisualStyleBackColor = true;
			this.btnSubmit.Click += new System.EventHandler(this.btnSubmit_Click);
			// 
			// lblOdooDb
			// 
			this.lblOdooDb.AutoSize = true;
			this.lblOdooDb.Location = new System.Drawing.Point(12, 46);
			this.lblOdooDb.Name = "lblOdooDb";
			this.lblOdooDb.Size = new System.Drawing.Size(51, 13);
			this.lblOdooDb.TabIndex = 17;
			this.lblOdooDb.Text = "Odoo DB";
			// 
			// lblOdooUrl
			// 
			this.lblOdooUrl.AutoSize = true;
			this.lblOdooUrl.Location = new System.Drawing.Point(12, 20);
			this.lblOdooUrl.Name = "lblOdooUrl";
			this.lblOdooUrl.Size = new System.Drawing.Size(58, 13);
			this.lblOdooUrl.TabIndex = 16;
			this.lblOdooUrl.Text = "Odoo URL";
			// 
			// txtOdooDb
			// 
			this.txtOdooDb.Location = new System.Drawing.Point(77, 43);
			this.txtOdooDb.Name = "txtOdooDb";
			this.txtOdooDb.Size = new System.Drawing.Size(283, 20);
			this.txtOdooDb.TabIndex = 15;
			// 
			// txtOdooUrl
			// 
			this.txtOdooUrl.Location = new System.Drawing.Point(77, 17);
			this.txtOdooUrl.Name = "txtOdooUrl";
			this.txtOdooUrl.Size = new System.Drawing.Size(283, 20);
			this.txtOdooUrl.TabIndex = 14;
			// 
			// OdooSettings
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(374, 204);
			this.Controls.Add(this.lblAreaFactor);
			this.Controls.Add(this.txtAreaFactor);
			this.Controls.Add(this.lblSwKey);
			this.Controls.Add(this.txtSwKey);
			this.Controls.Add(this.lblOdooPass);
			this.Controls.Add(this.lblOdooUser);
			this.Controls.Add(this.txtOdooPass);
			this.Controls.Add(this.txtOdooUser);
			this.Controls.Add(this.btnSubmit);
			this.Controls.Add(this.lblOdooDb);
			this.Controls.Add(this.lblOdooUrl);
			this.Controls.Add(this.txtOdooDb);
			this.Controls.Add(this.txtOdooUrl);
			this.Name = "OdooSettings";
			this.Text = "OdooSettings";
			this.ResumeLayout(false);
			this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblAreaFactor;
        private System.Windows.Forms.TextBox txtAreaFactor;
        private System.Windows.Forms.Label lblSwKey;
        private System.Windows.Forms.TextBox txtSwKey;
        private System.Windows.Forms.Label lblOdooPass;
        private System.Windows.Forms.Label lblOdooUser;
        private System.Windows.Forms.TextBox txtOdooPass;
        private System.Windows.Forms.TextBox txtOdooUser;
        private System.Windows.Forms.Button btnSubmit;
        private System.Windows.Forms.Label lblOdooDb;
        private System.Windows.Forms.Label lblOdooUrl;
        private System.Windows.Forms.TextBox txtOdooDb;
        private System.Windows.Forms.TextBox txtOdooUrl;
    }
}