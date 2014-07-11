using System;
using System.Windows.Forms;

namespace RomVaultX
{
    public partial class frmMain : Form
    {
        public frmMain()
        {
            InitializeComponent();
            DirTree.Setup(DataAccessLayer.ReadTreeFromDB());
        }

        private void btnUpdateDats_Click(object sender, EventArgs e)
        {
            UpdateDats();
            DirTree.Setup(DataAccessLayer.ReadTreeFromDB());
        }

        private void UpdateDats()
        {
            FrmProgressWindow progress = new FrmProgressWindow(this, "Scanning Dats", DatUpdate.UpdateDat);
            progress.ShowDialog(this);
            progress.Dispose();
        }






    }
}
