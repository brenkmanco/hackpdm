namespace HackPDM.Forms.Hack
{
	partial class HackSettings
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
            this.btnSubmit = new System.Windows.Forms.Button();
            this.lblOdooUrl = new System.Windows.Forms.Label();
            this.txtPwaInput = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.HackTempFolderPath = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // btnSubmit
            // 
            this.btnSubmit.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSubmit.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSubmit.Location = new System.Drawing.Point(16, 66);
            this.btnSubmit.Name = "btnSubmit";
            this.btnSubmit.Size = new System.Drawing.Size(488, 41);
            this.btnSubmit.TabIndex = 31;
            this.btnSubmit.Text = "Submit";
            this.btnSubmit.UseVisualStyleBackColor = true;
            this.btnSubmit.Click += new System.EventHandler(this.btnSubmit_Click);
            // 
            // lblOdooUrl
            // 
            this.lblOdooUrl.AutoSize = true;
            this.lblOdooUrl.Location = new System.Drawing.Point(13, 15);
            this.lblOdooUrl.Name = "lblOdooUrl";
            this.lblOdooUrl.Size = new System.Drawing.Size(64, 13);
            this.lblOdooUrl.TabIndex = 29;
            this.lblOdooUrl.Text = "PWA Folder";
            // 
            // txtPwaInput
            // 
            this.txtPwaInput.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtPwaInput.Location = new System.Drawing.Point(137, 12);
            this.txtPwaInput.Name = "txtPwaInput";
            this.txtPwaInput.Size = new System.Drawing.Size(368, 20);
            this.txtPwaInput.TabIndex = 27;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 40);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(114, 13);
            this.label1.TabIndex = 42;
            this.label1.Text = "Temporary Folder Path";
            // 
            // HackTempFolderPath
            // 
            this.HackTempFolderPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.HackTempFolderPath.Location = new System.Drawing.Point(137, 40);
            this.HackTempFolderPath.Name = "HackTempFolderPath";
            this.HackTempFolderPath.Size = new System.Drawing.Size(368, 20);
            this.HackTempFolderPath.TabIndex = 41;
            // 
            // HackSettings
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(516, 114);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.HackTempFolderPath);
            this.Controls.Add(this.btnSubmit);
            this.Controls.Add(this.lblOdooUrl);
            this.Controls.Add(this.txtPwaInput);
            this.Name = "HackSettings";
            this.Text = "HackSettings";
            this.ResumeLayout(false);
            this.PerformLayout();

		}

		#endregion
		private System.Windows.Forms.Button btnSubmit;
		private System.Windows.Forms.Label lblOdooUrl;
		private System.Windows.Forms.TextBox txtPwaInput;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.TextBox HackTempFolderPath;
    }
}