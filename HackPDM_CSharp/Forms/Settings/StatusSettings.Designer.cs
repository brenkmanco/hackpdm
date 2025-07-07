namespace HackPDM
{
    partial class StatusSettings
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
            this.skippedSetting = new System.Windows.Forms.CheckBox();
            this.batchSizeTextbox = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.StatusHistoryLengthTextbox = new System.Windows.Forms.TextBox();
            this.StatusHistoryTextbox = new System.Windows.Forms.Label();
            this.StatusSaveButton = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.StatusErrorMessage = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // skippedSetting
            // 
            this.skippedSetting.AutoSize = true;
            this.skippedSetting.Location = new System.Drawing.Point(13, 13);
            this.skippedSetting.Name = "skippedSetting";
            this.skippedSetting.Size = new System.Drawing.Size(95, 17);
            this.skippedSetting.TabIndex = 0;
            this.skippedSetting.Text = "Show Skipped";
            this.skippedSetting.UseVisualStyleBackColor = true;
            // 
            // batchSizeTextbox
            // 
            this.batchSizeTextbox.Location = new System.Drawing.Point(16, 71);
            this.batchSizeTextbox.Name = "batchSizeTextbox";
            this.batchSizeTextbox.Size = new System.Drawing.Size(38, 20);
            this.batchSizeTextbox.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 55);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(109, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Batch Download Size";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(60, 74);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(123, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "recommended size (~25)";
            // 
            // StatusHistoryLengthTextbox
            // 
            this.StatusHistoryLengthTextbox.Location = new System.Drawing.Point(16, 133);
            this.StatusHistoryLengthTextbox.Name = "StatusHistoryLengthTextbox";
            this.StatusHistoryLengthTextbox.Size = new System.Drawing.Size(100, 20);
            this.StatusHistoryLengthTextbox.TabIndex = 4;
            // 
            // StatusHistoryTextbox
            // 
            this.StatusHistoryTextbox.AutoSize = true;
            this.StatusHistoryTextbox.Location = new System.Drawing.Point(16, 114);
            this.StatusHistoryTextbox.Name = "StatusHistoryTextbox";
            this.StatusHistoryTextbox.Size = new System.Drawing.Size(108, 13);
            this.StatusHistoryTextbox.TabIndex = 5;
            this.StatusHistoryTextbox.Text = "Status History Length";
            // 
            // StatusSaveButton
            // 
            this.StatusSaveButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.StatusSaveButton.Location = new System.Drawing.Point(189, 90);
            this.StatusSaveButton.Name = "StatusSaveButton";
            this.StatusSaveButton.Size = new System.Drawing.Size(75, 65);
            this.StatusSaveButton.TabIndex = 6;
            this.StatusSaveButton.Text = "Save";
            this.StatusSaveButton.UseVisualStyleBackColor = true;
            this.StatusSaveButton.Click += new System.EventHandler(this.button1_Click);
            // 
            // button2
            // 
            this.button2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button2.Location = new System.Drawing.Point(189, 12);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(75, 72);
            this.button2.TabIndex = 7;
            this.button2.Text = "Revert To Default";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // StatusErrorMessage
            // 
            this.StatusErrorMessage.AutoSize = true;
            this.StatusErrorMessage.Location = new System.Drawing.Point(16, 324);
            this.StatusErrorMessage.Name = "StatusErrorMessage";
            this.StatusErrorMessage.Size = new System.Drawing.Size(0, 13);
            this.StatusErrorMessage.TabIndex = 8;
            // 
            // StatusSettings
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(269, 173);
            this.Controls.Add(this.StatusErrorMessage);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.StatusSaveButton);
            this.Controls.Add(this.StatusHistoryTextbox);
            this.Controls.Add(this.StatusHistoryLengthTextbox);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.batchSizeTextbox);
            this.Controls.Add(this.skippedSetting);
            this.Name = "StatusSettings";
            this.Text = "StatusSettings";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox skippedSetting;
        private System.Windows.Forms.TextBox batchSizeTextbox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox StatusHistoryLengthTextbox;
        private System.Windows.Forms.Label StatusHistoryTextbox;
        private System.Windows.Forms.Button StatusSaveButton;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Label StatusErrorMessage;
    }
}