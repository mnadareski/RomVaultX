﻿using System;
using System.Windows.Forms;

namespace RomVaultX
{
    public partial class RomVaultX : Form
    {
        public RomVaultX()
        {
            InitializeComponent();
            DirTree.Setup();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void btnUpdateDats_Click(object sender, EventArgs e)
        {
            UpdateDats();
            DirTree.Setup();
        }

        private void UpdateDats()
        {
            FrmProgressWindow progress = new FrmProgressWindow(this, "Scanning Dats", DatUpdate.UpdateDat);
            progress.ShowDialog(this);
            progress.Dispose();
        }
    }
}
