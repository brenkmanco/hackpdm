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
			this.txtMeasureFile = new System.Windows.Forms.Label();
			this.txtMeasureByte = new System.Windows.Forms.Label();
			this.txtByteInput = new System.Windows.Forms.TextBox();
			this.btnSubmit = new System.Windows.Forms.Button();
			this.lblOdooDb = new System.Windows.Forms.Label();
			this.lblOdooUrl = new System.Windows.Forms.Label();
			this.txtProjectInput = new System.Windows.Forms.TextBox();
			this.txtPwaInput = new System.Windows.Forms.TextBox();
			this.txtFileInput = new System.Windows.Forms.ComboBox();
			this.SuspendLayout();
			// 
			// txtMeasureFile
			// 
			this.txtMeasureFile.AutoSize = true;
			this.txtMeasureFile.Location = new System.Drawing.Point(13, 93);
			this.txtMeasureFile.Name = "txtMeasureFile";
			this.txtMeasureFile.Size = new System.Drawing.Size(90, 13);
			this.txtMeasureFile.TabIndex = 35;
			this.txtMeasureFile.Text = "Measure File Size";
			// 
			// txtMeasureByte
			// 
			this.txtMeasureByte.AutoSize = true;
			this.txtMeasureByte.Location = new System.Drawing.Point(13, 67);
			this.txtMeasureByte.Name = "txtMeasureByte";
			this.txtMeasureByte.Size = new System.Drawing.Size(95, 13);
			this.txtMeasureByte.TabIndex = 34;
			this.txtMeasureByte.Text = "Measure Byte Size";
			// 
			// txtByteInput
			// 
			this.txtByteInput.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.txtByteInput.Location = new System.Drawing.Point(137, 64);
			this.txtByteInput.Name = "txtByteInput";
			this.txtByteInput.Size = new System.Drawing.Size(368, 20);
			this.txtByteInput.TabIndex = 32;
			// 
			// btnSubmit
			// 
			this.btnSubmit.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.btnSubmit.Location = new System.Drawing.Point(16, 124);
			this.btnSubmit.Name = "btnSubmit";
			this.btnSubmit.Size = new System.Drawing.Size(488, 22);
			this.btnSubmit.TabIndex = 31;
			this.btnSubmit.Text = "Submit";
			this.btnSubmit.UseVisualStyleBackColor = true;
			this.btnSubmit.Click += new System.EventHandler(this.btnSubmit_Click);
			// 
			// lblOdooDb
			// 
			this.lblOdooDb.AutoSize = true;
			this.lblOdooDb.Location = new System.Drawing.Point(13, 41);
			this.lblOdooDb.Name = "lblOdooDb";
			this.lblOdooDb.Size = new System.Drawing.Size(85, 13);
			this.lblOdooDb.TabIndex = 30;
			this.lblOdooDb.Text = "Project Directory";
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
			// txtProjectInput
			// 
			this.txtProjectInput.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.txtProjectInput.Location = new System.Drawing.Point(137, 38);
			this.txtProjectInput.Name = "txtProjectInput";
			this.txtProjectInput.Size = new System.Drawing.Size(368, 20);
			this.txtProjectInput.TabIndex = 28;
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
			// txtFileInput
			// 
			this.txtFileInput.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.txtFileInput.FormattingEnabled = true;
			this.txtFileInput.Items.AddRange(new object[] {
            "Byte",
            "KiloByte",
            "MegaByte",
            "GigaByte",
            "TeraByte"});
			this.txtFileInput.Location = new System.Drawing.Point(137, 90);
			this.txtFileInput.Name = "txtFileInput";
			this.txtFileInput.Size = new System.Drawing.Size(368, 21);
			this.txtFileInput.TabIndex = 40;
			// 
			// HackSettings
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(516, 153);
			this.Controls.Add(this.txtFileInput);
			this.Controls.Add(this.txtMeasureFile);
			this.Controls.Add(this.txtMeasureByte);
			this.Controls.Add(this.txtByteInput);
			this.Controls.Add(this.btnSubmit);
			this.Controls.Add(this.lblOdooDb);
			this.Controls.Add(this.lblOdooUrl);
			this.Controls.Add(this.txtProjectInput);
			this.Controls.Add(this.txtPwaInput);
			this.Name = "HackSettings";
			this.Text = "HackSettings";
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion
		private System.Windows.Forms.Label txtMeasureFile;
		private System.Windows.Forms.Label txtMeasureByte;
		private System.Windows.Forms.TextBox txtByteInput;
		private System.Windows.Forms.Button btnSubmit;
		private System.Windows.Forms.Label lblOdooDb;
		private System.Windows.Forms.Label lblOdooUrl;
		private System.Windows.Forms.TextBox txtProjectInput;
		private System.Windows.Forms.TextBox txtPwaInput;
		private System.Windows.Forms.ComboBox txtFileInput;
	}
}