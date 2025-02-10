namespace HackPDM.Forms.Settings
{
	partial class DebugForm
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
			this.DebugChooseForm = new System.Windows.Forms.ComboBox();
			this.DebugChooseFormBtn = new System.Windows.Forms.Button();
			this.SuspendLayout();
			// 
			// DebugChooseForm
			// 
			this.DebugChooseForm.FormattingEnabled = true;
			this.DebugChooseForm.Location = new System.Drawing.Point(13, 13);
			this.DebugChooseForm.Name = "DebugChooseForm";
			this.DebugChooseForm.Size = new System.Drawing.Size(209, 21);
			this.DebugChooseForm.TabIndex = 0;
			// 
			// DebugChooseFormBtn
			// 
			this.DebugChooseFormBtn.Location = new System.Drawing.Point(228, 11);
			this.DebugChooseFormBtn.Name = "DebugChooseFormBtn";
			this.DebugChooseFormBtn.Size = new System.Drawing.Size(141, 23);
			this.DebugChooseFormBtn.TabIndex = 1;
			this.DebugChooseFormBtn.Text = "Load Form";
			this.DebugChooseFormBtn.UseVisualStyleBackColor = true;
			this.DebugChooseFormBtn.Click += new System.EventHandler(this.DebugChooseFormBtn_Click);
			// 
			// DebugForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(381, 50);
			this.Controls.Add(this.DebugChooseFormBtn);
			this.Controls.Add(this.DebugChooseForm);
			this.Name = "DebugForm";
			this.Text = "DebugForm";
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.ComboBox DebugChooseForm;
		private System.Windows.Forms.Button DebugChooseFormBtn;
	}
}