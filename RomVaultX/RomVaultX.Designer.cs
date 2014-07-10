namespace RomVaultX
{
    partial class RomVaultX
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RomVaultX));
            this.btnUpdateDats = new System.Windows.Forms.Button();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.splitContainer3 = new System.Windows.Forms.SplitContainer();
            this.DirTree = new ROMVault2.RvTree();
            this.splitContainer4 = new System.Windows.Forms.SplitContainer();
            this.splitContainer5 = new System.Windows.Forms.SplitContainer();
            this.gbDatInfo = new System.Windows.Forms.GroupBox();
            this.lblDIRomsUnknown = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.lblDITRomsUnknown = new System.Windows.Forms.Label();
            this.lblDITRomsFixable = new System.Windows.Forms.Label();
            this.lblDITRomsMissing = new System.Windows.Forms.Label();
            this.lblDITRomsGot = new System.Windows.Forms.Label();
            this.lblDITRomPath = new System.Windows.Forms.Label();
            this.lblDITPath = new System.Windows.Forms.Label();
            this.lblDIDate = new System.Windows.Forms.Label();
            this.lblDIAuthor = new System.Windows.Forms.Label();
            this.lblDITDate = new System.Windows.Forms.Label();
            this.lblDITAuthor = new System.Windows.Forms.Label();
            this.lblDIVersion = new System.Windows.Forms.Label();
            this.lblDICategory = new System.Windows.Forms.Label();
            this.lblDITVersion = new System.Windows.Forms.Label();
            this.lblDITCategory = new System.Windows.Forms.Label();
            this.lblDIDescription = new System.Windows.Forms.Label();
            this.lblDIName = new System.Windows.Forms.Label();
            this.lblDITDescription = new System.Windows.Forms.Label();
            this.lblDITName = new System.Windows.Forms.Label();
            this.lblDIRomsFixable = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            this.splitContainer3.Panel1.SuspendLayout();
            this.splitContainer3.Panel2.SuspendLayout();
            this.splitContainer3.SuspendLayout();
            this.splitContainer4.Panel2.SuspendLayout();
            this.splitContainer4.SuspendLayout();
            this.splitContainer5.SuspendLayout();
            this.gbDatInfo.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnUpdateDats
            // 
            this.btnUpdateDats.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("btnUpdateDats.BackgroundImage")));
            this.btnUpdateDats.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.btnUpdateDats.Location = new System.Drawing.Point(0, 0);
            this.btnUpdateDats.Name = "btnUpdateDats";
            this.btnUpdateDats.Size = new System.Drawing.Size(80, 80);
            this.btnUpdateDats.TabIndex = 0;
            this.btnUpdateDats.Text = "Update DATs";
            this.btnUpdateDats.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btnUpdateDats.UseVisualStyleBackColor = true;
            this.btnUpdateDats.Click += new System.EventHandler(this.btnUpdateDats_Click);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer1.IsSplitterFixed = true;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.BackColor = System.Drawing.Color.White;
            this.splitContainer1.Panel1.Controls.Add(this.btnUpdateDats);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.splitContainer2);
            this.splitContainer1.Size = new System.Drawing.Size(1139, 722);
            this.splitContainer1.SplitterDistance = 80;
            this.splitContainer1.TabIndex = 4;
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.splitContainer3);
            this.splitContainer2.Panel1MinSize = 450;
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.splitContainer4);
            this.splitContainer2.Size = new System.Drawing.Size(1055, 722);
            this.splitContainer2.SplitterDistance = 478;
            this.splitContainer2.TabIndex = 0;
            // 
            // splitContainer3
            // 
            this.splitContainer3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer3.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer3.IsSplitterFixed = true;
            this.splitContainer3.Location = new System.Drawing.Point(0, 0);
            this.splitContainer3.Name = "splitContainer3";
            this.splitContainer3.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer3.Panel1
            // 
            this.splitContainer3.Panel1.Controls.Add(this.gbDatInfo);
            // 
            // splitContainer3.Panel2
            // 
            this.splitContainer3.Panel2.Controls.Add(this.DirTree);
            this.splitContainer3.Size = new System.Drawing.Size(478, 722);
            this.splitContainer3.SplitterDistance = 148;
            this.splitContainer3.TabIndex = 0;
            // 
            // DirTree
            // 
            this.DirTree.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.DirTree.Dock = System.Windows.Forms.DockStyle.Fill;
            this.DirTree.Location = new System.Drawing.Point(0, 0);
            this.DirTree.Name = "DirTree";
            this.DirTree.Size = new System.Drawing.Size(478, 570);
            this.DirTree.TabIndex = 0;
            // 
            // splitContainer4
            // 
            this.splitContainer4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer4.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer4.Location = new System.Drawing.Point(0, 0);
            this.splitContainer4.Name = "splitContainer4";
            this.splitContainer4.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer4.Panel2
            // 
            this.splitContainer4.Panel2.Controls.Add(this.splitContainer5);
            this.splitContainer4.Size = new System.Drawing.Size(573, 722);
            this.splitContainer4.SplitterDistance = 148;
            this.splitContainer4.TabIndex = 0;
            // 
            // splitContainer5
            // 
            this.splitContainer5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer5.Location = new System.Drawing.Point(0, 0);
            this.splitContainer5.Name = "splitContainer5";
            this.splitContainer5.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.splitContainer5.Size = new System.Drawing.Size(573, 570);
            this.splitContainer5.SplitterDistance = 270;
            this.splitContainer5.TabIndex = 0;
            // 
            // gbDatInfo
            // 
            this.gbDatInfo.Controls.Add(this.lblDIRomsUnknown);
            this.gbDatInfo.Controls.Add(this.label9);
            this.gbDatInfo.Controls.Add(this.lblDITRomsUnknown);
            this.gbDatInfo.Controls.Add(this.lblDITRomsFixable);
            this.gbDatInfo.Controls.Add(this.lblDITRomsMissing);
            this.gbDatInfo.Controls.Add(this.lblDITRomsGot);
            this.gbDatInfo.Controls.Add(this.lblDITRomPath);
            this.gbDatInfo.Controls.Add(this.lblDITPath);
            this.gbDatInfo.Controls.Add(this.lblDIDate);
            this.gbDatInfo.Controls.Add(this.lblDIAuthor);
            this.gbDatInfo.Controls.Add(this.lblDITDate);
            this.gbDatInfo.Controls.Add(this.lblDITAuthor);
            this.gbDatInfo.Controls.Add(this.lblDIVersion);
            this.gbDatInfo.Controls.Add(this.lblDICategory);
            this.gbDatInfo.Controls.Add(this.lblDITVersion);
            this.gbDatInfo.Controls.Add(this.lblDITCategory);
            this.gbDatInfo.Controls.Add(this.lblDIDescription);
            this.gbDatInfo.Controls.Add(this.lblDIName);
            this.gbDatInfo.Controls.Add(this.lblDITDescription);
            this.gbDatInfo.Controls.Add(this.lblDITName);
            this.gbDatInfo.Controls.Add(this.lblDIRomsFixable);
            this.gbDatInfo.Controls.Add(this.label8);
            this.gbDatInfo.Location = new System.Drawing.Point(3, 0);
            this.gbDatInfo.Name = "gbDatInfo";
            this.gbDatInfo.Size = new System.Drawing.Size(440, 147);
            this.gbDatInfo.TabIndex = 4;
            this.gbDatInfo.TabStop = false;
            this.gbDatInfo.Text = "Dat Info :";
            // 
            // lblDIRomsUnknown
            // 
            this.lblDIRomsUnknown.Location = new System.Drawing.Point(214, 121);
            this.lblDIRomsUnknown.Name = "lblDIRomsUnknown";
            this.lblDIRomsUnknown.Size = new System.Drawing.Size(92, 13);
            this.lblDIRomsUnknown.TabIndex = 26;
            this.lblDIRomsUnknown.Text = "ROMs Unknown :";
            this.lblDIRomsUnknown.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label9
            // 
            this.label9.Location = new System.Drawing.Point(10, 105);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(75, 13);
            this.label9.TabIndex = 23;
            this.label9.Text = "ROMs Got :";
            this.label9.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblDITRomsUnknown
            // 
            this.lblDITRomsUnknown.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITRomsUnknown.Location = new System.Drawing.Point(311, 120);
            this.lblDITRomsUnknown.Name = "lblDITRomsUnknown";
            this.lblDITRomsUnknown.Size = new System.Drawing.Size(120, 17);
            this.lblDITRomsUnknown.TabIndex = 21;
            // 
            // lblDITRomsFixable
            // 
            this.lblDITRomsFixable.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITRomsFixable.Location = new System.Drawing.Point(311, 104);
            this.lblDITRomsFixable.Name = "lblDITRomsFixable";
            this.lblDITRomsFixable.Size = new System.Drawing.Size(120, 17);
            this.lblDITRomsFixable.TabIndex = 20;
            // 
            // lblDITRomsMissing
            // 
            this.lblDITRomsMissing.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITRomsMissing.Location = new System.Drawing.Point(89, 120);
            this.lblDITRomsMissing.Name = "lblDITRomsMissing";
            this.lblDITRomsMissing.Size = new System.Drawing.Size(120, 17);
            this.lblDITRomsMissing.TabIndex = 19;
            // 
            // lblDITRomsGot
            // 
            this.lblDITRomsGot.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITRomsGot.Location = new System.Drawing.Point(89, 104);
            this.lblDITRomsGot.Name = "lblDITRomsGot";
            this.lblDITRomsGot.Size = new System.Drawing.Size(120, 17);
            this.lblDITRomsGot.TabIndex = 18;
            // 
            // lblDITRomPath
            // 
            this.lblDITRomPath.Location = new System.Drawing.Point(10, 79);
            this.lblDITRomPath.Name = "lblDITRomPath";
            this.lblDITRomPath.Size = new System.Drawing.Size(75, 13);
            this.lblDITRomPath.TabIndex = 15;
            this.lblDITRomPath.Text = "ROM Path:";
            this.lblDITRomPath.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblDITPath
            // 
            this.lblDITPath.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITPath.Location = new System.Drawing.Point(89, 78);
            this.lblDITPath.Name = "lblDITPath";
            this.lblDITPath.Size = new System.Drawing.Size(342, 17);
            this.lblDITPath.TabIndex = 13;
            // 
            // lblDIDate
            // 
            this.lblDIDate.Location = new System.Drawing.Point(214, 63);
            this.lblDIDate.Name = "lblDIDate";
            this.lblDIDate.Size = new System.Drawing.Size(92, 13);
            this.lblDIDate.TabIndex = 12;
            this.lblDIDate.Text = "Date :";
            this.lblDIDate.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblDIAuthor
            // 
            this.lblDIAuthor.Location = new System.Drawing.Point(10, 63);
            this.lblDIAuthor.Name = "lblDIAuthor";
            this.lblDIAuthor.Size = new System.Drawing.Size(75, 13);
            this.lblDIAuthor.TabIndex = 11;
            this.lblDIAuthor.Text = "Author :";
            this.lblDIAuthor.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblDITDate
            // 
            this.lblDITDate.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITDate.Location = new System.Drawing.Point(311, 62);
            this.lblDITDate.Name = "lblDITDate";
            this.lblDITDate.Size = new System.Drawing.Size(120, 17);
            this.lblDITDate.TabIndex = 10;
            // 
            // lblDITAuthor
            // 
            this.lblDITAuthor.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITAuthor.Location = new System.Drawing.Point(89, 62);
            this.lblDITAuthor.Name = "lblDITAuthor";
            this.lblDITAuthor.Size = new System.Drawing.Size(120, 17);
            this.lblDITAuthor.TabIndex = 9;
            // 
            // lblDIVersion
            // 
            this.lblDIVersion.Location = new System.Drawing.Point(214, 47);
            this.lblDIVersion.Name = "lblDIVersion";
            this.lblDIVersion.Size = new System.Drawing.Size(92, 13);
            this.lblDIVersion.TabIndex = 8;
            this.lblDIVersion.Text = "Version :";
            this.lblDIVersion.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblDICategory
            // 
            this.lblDICategory.Location = new System.Drawing.Point(10, 47);
            this.lblDICategory.Name = "lblDICategory";
            this.lblDICategory.Size = new System.Drawing.Size(75, 13);
            this.lblDICategory.TabIndex = 7;
            this.lblDICategory.Text = "Category :";
            this.lblDICategory.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblDITVersion
            // 
            this.lblDITVersion.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITVersion.Location = new System.Drawing.Point(311, 46);
            this.lblDITVersion.Name = "lblDITVersion";
            this.lblDITVersion.Size = new System.Drawing.Size(120, 17);
            this.lblDITVersion.TabIndex = 6;
            // 
            // lblDITCategory
            // 
            this.lblDITCategory.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITCategory.Location = new System.Drawing.Point(89, 46);
            this.lblDITCategory.Name = "lblDITCategory";
            this.lblDITCategory.Size = new System.Drawing.Size(120, 17);
            this.lblDITCategory.TabIndex = 5;
            // 
            // lblDIDescription
            // 
            this.lblDIDescription.Location = new System.Drawing.Point(10, 31);
            this.lblDIDescription.Name = "lblDIDescription";
            this.lblDIDescription.Size = new System.Drawing.Size(75, 13);
            this.lblDIDescription.TabIndex = 4;
            this.lblDIDescription.Text = "Description :";
            this.lblDIDescription.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblDIName
            // 
            this.lblDIName.Location = new System.Drawing.Point(10, 15);
            this.lblDIName.Name = "lblDIName";
            this.lblDIName.Size = new System.Drawing.Size(75, 13);
            this.lblDIName.TabIndex = 3;
            this.lblDIName.Text = "Name :";
            this.lblDIName.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblDITDescription
            // 
            this.lblDITDescription.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITDescription.Location = new System.Drawing.Point(89, 30);
            this.lblDITDescription.Name = "lblDITDescription";
            this.lblDITDescription.Size = new System.Drawing.Size(342, 17);
            this.lblDITDescription.TabIndex = 2;
            // 
            // lblDITName
            // 
            this.lblDITName.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDITName.Location = new System.Drawing.Point(89, 14);
            this.lblDITName.Name = "lblDITName";
            this.lblDITName.Size = new System.Drawing.Size(342, 17);
            this.lblDITName.TabIndex = 1;
            // 
            // lblDIRomsFixable
            // 
            this.lblDIRomsFixable.Location = new System.Drawing.Point(214, 105);
            this.lblDIRomsFixable.Name = "lblDIRomsFixable";
            this.lblDIRomsFixable.Size = new System.Drawing.Size(92, 13);
            this.lblDIRomsFixable.TabIndex = 25;
            this.lblDIRomsFixable.Text = "ROMs Fixable :";
            this.lblDIRomsFixable.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label8
            // 
            this.label8.Location = new System.Drawing.Point(2, 121);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(83, 13);
            this.label8.TabIndex = 24;
            this.label8.Text = "ROMs Missing :";
            this.label8.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // RomVaultX
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1139, 722);
            this.Controls.Add(this.splitContainer1);
            this.Name = "RomVaultX";
            this.Text = "ROM Vault X";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            this.splitContainer2.ResumeLayout(false);
            this.splitContainer3.Panel1.ResumeLayout(false);
            this.splitContainer3.Panel2.ResumeLayout(false);
            this.splitContainer3.ResumeLayout(false);
            this.splitContainer4.Panel2.ResumeLayout(false);
            this.splitContainer4.ResumeLayout(false);
            this.splitContainer5.ResumeLayout(false);
            this.gbDatInfo.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnUpdateDats;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.SplitContainer splitContainer3;
        private System.Windows.Forms.SplitContainer splitContainer4;
        private System.Windows.Forms.SplitContainer splitContainer5;
        private ROMVault2.RvTree DirTree;
        private System.Windows.Forms.GroupBox gbDatInfo;
        private System.Windows.Forms.Label lblDIRomsUnknown;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label lblDITRomsUnknown;
        private System.Windows.Forms.Label lblDITRomsFixable;
        private System.Windows.Forms.Label lblDITRomsMissing;
        private System.Windows.Forms.Label lblDITRomsGot;
        private System.Windows.Forms.Label lblDITRomPath;
        private System.Windows.Forms.Label lblDITPath;
        private System.Windows.Forms.Label lblDIDate;
        private System.Windows.Forms.Label lblDIAuthor;
        private System.Windows.Forms.Label lblDITDate;
        private System.Windows.Forms.Label lblDITAuthor;
        private System.Windows.Forms.Label lblDIVersion;
        private System.Windows.Forms.Label lblDICategory;
        private System.Windows.Forms.Label lblDITVersion;
        private System.Windows.Forms.Label lblDITCategory;
        private System.Windows.Forms.Label lblDIDescription;
        private System.Windows.Forms.Label lblDIName;
        private System.Windows.Forms.Label lblDITDescription;
        private System.Windows.Forms.Label lblDITName;
        private System.Windows.Forms.Label lblDIRomsFixable;
        private System.Windows.Forms.Label label8;
    }
}

