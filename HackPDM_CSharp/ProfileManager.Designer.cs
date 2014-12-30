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
            this.label10 = new System.Windows.Forms.Label();
            this.txtPassword = new System.Windows.Forms.TextBox();
            this.label9 = new System.Windows.Forms.Label();
            this.txtUsername = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.txtPfName = new System.Windows.Forms.TextBox();
            this.cmdNew = new System.Windows.Forms.Button();
            this.label7 = new System.Windows.Forms.Label();
            this.cboProfile = new System.Windows.Forms.ComboBox();
            this.label6 = new System.Windows.Forms.Label();
            this.txtDbPass = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.txtDbUser = new System.Windows.Forms.TextBox();
            this.cmdCommit = new System.Windows.Forms.Button();
            this.cmdTest = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.txtFsRoot = new System.Windows.Forms.TextBox();
            this.txtDbName = new System.Windows.Forms.TextBox();
            this.txtDbPort = new System.Windows.Forms.TextBox();
            this.txtDbServ = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // label10
            // 
            this.label10.Location = new System.Drawing.Point(6, 324);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(63, 23);
            this.label10.TabIndex = 137;
            this.label10.Text = "Password";
            // 
            // txtPassword
            // 
            this.txtPassword.Location = new System.Drawing.Point(75, 324);
            this.txtPassword.Name = "txtPassword";
            this.txtPassword.Size = new System.Drawing.Size(427, 20);
            this.txtPassword.TabIndex = 136;
            // 
            // label9
            // 
            this.label9.Location = new System.Drawing.Point(6, 301);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(63, 23);
            this.label9.TabIndex = 135;
            this.label9.Text = "Username";
            // 
            // txtUsername
            // 
            this.txtUsername.Location = new System.Drawing.Point(75, 301);
            this.txtUsername.Name = "txtUsername";
            this.txtUsername.Size = new System.Drawing.Size(427, 20);
            this.txtUsername.TabIndex = 134;
            // 
            // label8
            // 
            this.label8.Location = new System.Drawing.Point(6, 56);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(63, 23);
            this.label8.TabIndex = 133;
            this.label8.Text = "ProfName";
            // 
            // txtPfName
            // 
            this.txtPfName.Location = new System.Drawing.Point(75, 56);
            this.txtPfName.Name = "txtPfName";
            this.txtPfName.Size = new System.Drawing.Size(427, 20);
            this.txtPfName.TabIndex = 132;
            // 
            // cmdNew
            // 
            this.cmdNew.Location = new System.Drawing.Point(427, 14);
            this.cmdNew.Name = "cmdNew";
            this.cmdNew.Size = new System.Drawing.Size(75, 23);
            this.cmdNew.TabIndex = 131;
            this.cmdNew.Text = "New";
            this.cmdNew.UseVisualStyleBackColor = true;
            this.cmdNew.Click += new System.EventHandler(this.CmdNewClick);
            // 
            // label7
            // 
            this.label7.Location = new System.Drawing.Point(7, 14);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(63, 23);
            this.label7.TabIndex = 130;
            this.label7.Text = "Profile";
            // 
            // cboProfile
            // 
            this.cboProfile.FormattingEnabled = true;
            this.cboProfile.Location = new System.Drawing.Point(75, 14);
            this.cboProfile.Name = "cboProfile";
            this.cboProfile.Size = new System.Drawing.Size(346, 21);
            this.cboProfile.TabIndex = 129;
            this.cboProfile.SelectedIndexChanged += new System.EventHandler(this.CboProfileSelectedIndexChanged);
            // 
            // label6
            // 
            this.label6.Location = new System.Drawing.Point(6, 182);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(63, 23);
            this.label6.TabIndex = 126;
            this.label6.Text = "DbPass";
            // 
            // txtDbPass
            // 
            this.txtDbPass.Location = new System.Drawing.Point(75, 182);
            this.txtDbPass.Name = "txtDbPass";
            this.txtDbPass.Size = new System.Drawing.Size(427, 20);
            this.txtDbPass.TabIndex = 118;
            // 
            // label5
            // 
            this.label5.Location = new System.Drawing.Point(7, 156);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(63, 23);
            this.label5.TabIndex = 125;
            this.label5.Text = "DbUser";
            // 
            // txtDbUser
            // 
            this.txtDbUser.Location = new System.Drawing.Point(75, 156);
            this.txtDbUser.Name = "txtDbUser";
            this.txtDbUser.Size = new System.Drawing.Size(427, 20);
            this.txtDbUser.TabIndex = 117;
            // 
            // cmdCommit
            // 
            this.cmdCommit.Location = new System.Drawing.Point(427, 375);
            this.cmdCommit.Name = "cmdCommit";
            this.cmdCommit.Size = new System.Drawing.Size(75, 23);
            this.cmdCommit.TabIndex = 122;
            this.cmdCommit.Text = "Commit";
            this.cmdCommit.UseVisualStyleBackColor = true;
            this.cmdCommit.Click += new System.EventHandler(this.CmdCommitClick);
            // 
            // cmdTest
            // 
            this.cmdTest.Location = new System.Drawing.Point(346, 375);
            this.cmdTest.Name = "cmdTest";
            this.cmdTest.Size = new System.Drawing.Size(75, 23);
            this.cmdTest.TabIndex = 121;
            this.cmdTest.Text = "Test";
            this.cmdTest.UseVisualStyleBackColor = true;
            this.cmdTest.Click += new System.EventHandler(this.CmdTestClick);
            // 
            // label4
            // 
            this.label4.Location = new System.Drawing.Point(6, 255);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(63, 23);
            this.label4.TabIndex = 128;
            this.label4.Text = "WorkPath";
            // 
            // label3
            // 
            this.label3.Location = new System.Drawing.Point(6, 208);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(63, 23);
            this.label3.TabIndex = 127;
            this.label3.Text = "DbName";
            // 
            // label2
            // 
            this.label2.Location = new System.Drawing.Point(6, 130);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(63, 23);
            this.label2.TabIndex = 124;
            this.label2.Text = "ServerPort";
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(6, 104);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(63, 23);
            this.label1.TabIndex = 123;
            this.label1.Text = "ServerURL";
            // 
            // txtFsRoot
            // 
            this.txtFsRoot.Location = new System.Drawing.Point(75, 255);
            this.txtFsRoot.Name = "txtFsRoot";
            this.txtFsRoot.Size = new System.Drawing.Size(427, 20);
            this.txtFsRoot.TabIndex = 120;
            // 
            // txtDbName
            // 
            this.txtDbName.Location = new System.Drawing.Point(75, 208);
            this.txtDbName.Name = "txtDbName";
            this.txtDbName.Size = new System.Drawing.Size(427, 20);
            this.txtDbName.TabIndex = 119;
            // 
            // txtDbPort
            // 
            this.txtDbPort.Location = new System.Drawing.Point(75, 130);
            this.txtDbPort.Name = "txtDbPort";
            this.txtDbPort.Size = new System.Drawing.Size(427, 20);
            this.txtDbPort.TabIndex = 116;
            // 
            // txtDbServ
            // 
            this.txtDbServ.Location = new System.Drawing.Point(75, 104);
            this.txtDbServ.Name = "txtDbServ";
            this.txtDbServ.Size = new System.Drawing.Size(427, 20);
            this.txtDbServ.TabIndex = 115;
            // 
            // ProfileManager
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(509, 412);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.txtPassword);
            this.Controls.Add(this.label9);
            this.Controls.Add(this.txtUsername);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.txtPfName);
            this.Controls.Add(this.cmdNew);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.cboProfile);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.txtDbPass);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.txtDbUser);
            this.Controls.Add(this.cmdCommit);
            this.Controls.Add(this.cmdTest);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtFsRoot);
            this.Controls.Add(this.txtDbName);
            this.Controls.Add(this.txtDbPort);
            this.Controls.Add(this.txtDbServ);
            this.Name = "ProfileManager";
            this.Text = "ProfileManager";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TextBox txtPassword;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox txtUsername;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox txtPfName;
        private System.Windows.Forms.Button cmdNew;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.ComboBox cboProfile;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox txtDbPass;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox txtDbUser;
        private System.Windows.Forms.Button cmdCommit;
        private System.Windows.Forms.Button cmdTest;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtFsRoot;
        private System.Windows.Forms.TextBox txtDbName;
        private System.Windows.Forms.TextBox txtDbPort;
        private System.Windows.Forms.TextBox txtDbServ;
    }
}