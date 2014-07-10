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
            this.rvTree1 = new ROMVault2.RvTree();
            this.SuspendLayout();
            // 
            // btnUpdateDats
            // 
            this.btnUpdateDats.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("btnUpdateDats.BackgroundImage")));
            this.btnUpdateDats.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
            this.btnUpdateDats.Location = new System.Drawing.Point(12, 12);
            this.btnUpdateDats.Name = "btnUpdateDats";
            this.btnUpdateDats.Size = new System.Drawing.Size(80, 80);
            this.btnUpdateDats.TabIndex = 2;
            this.btnUpdateDats.Text = "Update DATs";
            this.btnUpdateDats.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btnUpdateDats.UseVisualStyleBackColor = true;
            this.btnUpdateDats.Click += new System.EventHandler(this.btnUpdateDats_Click);
            // 
            // rvTree1
            // 
            this.rvTree1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.rvTree1.Location = new System.Drawing.Point(155, 40);
            this.rvTree1.Name = "rvTree1";
            this.rvTree1.Size = new System.Drawing.Size(482, 622);
            this.rvTree1.TabIndex = 3;
            // 
            // RomVaultX
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(826, 722);
            this.Controls.Add(this.rvTree1);
            this.Controls.Add(this.btnUpdateDats);
            this.Name = "RomVaultX";
            this.Text = "ROM Vault X";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnUpdateDats;
        private ROMVault2.RvTree rvTree1;
    }
}

