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
            this.label11 = new System.Windows.Forms.Label();
            this.txtDavPass = new System.Windows.Forms.TextBox();
            this.label12 = new System.Windows.Forms.Label();
            this.txtDavUser = new System.Windows.Forms.TextBox();
            this.label14 = new System.Windows.Forms.Label();
            this.label15 = new System.Windows.Forms.Label();
            this.txtDavPort = new System.Windows.Forms.TextBox();
            this.txtDavServ = new System.Windows.Forms.TextBox();
            this.label13 = new System.Windows.Forms.Label();
            this.txtDavPath = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // label10
            // 
            this.label10.Location = new System.Drawing.Point(6, 470);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(63, 23);
            this.label10.TabIndex = 137;
            this.label10.Text = "Password";
            // 
            // txtPassword
            // 
            this.txtPassword.Location = new System.Drawing.Point(75, 470);
            this.txtPassword.Name = "txtPassword";
            this.txtPassword.Size = new System.Drawing.Size(427, 20);
            this.txtPassword.TabIndex = 136;
            // 
            // label9
            // 
            this.label9.Location = new System.Drawing.Point(6, 447);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(63, 23);
            this.label9.TabIndex = 135;
            this.label9.Text = "Username";
            // 
            // txtUsername
            // 
            this.txtUsername.Location = new System.Drawing.Point(75, 447);
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
            this.cmdCommit.Location = new System.Drawing.Point(427, 513);
            this.cmdCommit.Name = "cmdCommit";
            this.cmdCommit.Size = new System.Drawing.Size(75, 23);
            this.cmdCommit.TabIndex = 122;
            this.cmdCommit.Text = "Commit";
            this.cmdCommit.UseVisualStyleBackColor = true;
            this.cmdCommit.Click += new System.EventHandler(this.CmdCommitClick);
            // 
            // cmdTest
            // 
            this.cmdTest.Location = new System.Drawing.Point(346, 513);
            this.cmdTest.Name = "cmdTest";
            this.cmdTest.Size = new System.Drawing.Size(75, 23);
            this.cmdTest.TabIndex = 121;
            this.cmdTest.Text = "Test";
            this.cmdTest.UseVisualStyleBackColor = true;
            this.cmdTest.Click += new System.EventHandler(this.CmdTestClick);
            // 
            // label4
            // 
            this.label4.Location = new System.Drawing.Point(6, 401);
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
            this.label2.Text = "DbPort";
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(6, 104);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(63, 23);
            this.label1.TabIndex = 123;
            this.label1.Text = "DbURL";
            // 
            // txtFsRoot
            // 
            this.txtFsRoot.Location = new System.Drawing.Point(75, 401);
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
            // label11
            // 
            this.label11.Location = new System.Drawing.Point(6, 332);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(63, 23);
            this.label11.TabIndex = 146;
            this.label11.Text = "DavPass";
            // 
            // txtDavPass
            // 
            this.txtDavPass.Location = new System.Drawing.Point(75, 332);
            this.txtDavPass.Name = "txtDavPass";
            this.txtDavPass.Size = new System.Drawing.Size(427, 20);
            this.txtDavPass.TabIndex = 141;
            // 
            // label12
            // 
            this.label12.Location = new System.Drawing.Point(7, 306);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(63, 23);
            this.label12.TabIndex = 145;
            this.label12.Text = "DavUser";
            // 
            // txtDavUser
            // 
            this.txtDavUser.Location = new System.Drawing.Point(75, 306);
            this.txtDavUser.Name = "txtDavUser";
            this.txtDavUser.Size = new System.Drawing.Size(427, 20);
            this.txtDavUser.TabIndex = 140;
            // 
            // label14
            // 
            this.label14.Location = new System.Drawing.Point(6, 280);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(63, 23);
            this.label14.TabIndex = 144;
            this.label14.Text = "DavPort";
            // 
            // label15
            // 
            this.label15.Location = new System.Drawing.Point(6, 254);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(63, 23);
            this.label15.TabIndex = 143;
            this.label15.Text = "DavURL";
            // 
            // txtDavPort
            // 
            this.txtDavPort.Location = new System.Drawing.Point(75, 280);
            this.txtDavPort.Name = "txtDavPort";
            this.txtDavPort.Size = new System.Drawing.Size(427, 20);
            this.txtDavPort.TabIndex = 139;
            // 
            // txtDavServ
            // 
            this.txtDavServ.Location = new System.Drawing.Point(75, 254);
            this.txtDavServ.Name = "txtDavServ";
            this.txtDavServ.Size = new System.Drawing.Size(427, 20);
            this.txtDavServ.TabIndex = 138;
            // 
            // label13
            // 
            this.label13.Location = new System.Drawing.Point(6, 358);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(63, 23);
            this.label13.TabIndex = 148;
            this.label13.Text = "DavPath";
            // 
            // txtDavPath
            // 
            this.txtDavPath.Location = new System.Drawing.Point(75, 358);
            this.txtDavPath.Name = "txtDavPath";
            this.txtDavPath.Size = new System.Drawing.Size(427, 20);
            this.txtDavPath.TabIndex = 147;
            // 
            // ProfileManager
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(509, 543);
            this.Controls.Add(this.label13);
            this.Controls.Add(this.txtDavPath);
            this.Controls.Add(this.label11);
            this.Controls.Add(this.txtDavPass);
            this.Controls.Add(this.label12);
            this.Controls.Add(this.txtDavUser);
            this.Controls.Add(this.label14);
            this.Controls.Add(this.label15);
            this.Controls.Add(this.txtDavPort);
            this.Controls.Add(this.txtDavServ);
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
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.TextBox txtDavPass;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.TextBox txtDavUser;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.TextBox txtDavPort;
        private System.Windows.Forms.TextBox txtDavServ;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.TextBox txtDavPath;
    }
}