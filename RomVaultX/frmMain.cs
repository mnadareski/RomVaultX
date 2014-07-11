using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace RomVaultX
{
    public partial class frmMain : Form
    {
        private Single _scaleFactorX = 1;
        private Single _scaleFactorY = 1;

        protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
        {
            base.ScaleControl(factor, specified);
            splitContainer1.SplitterDistance = (int)(splitContainer1.SplitterDistance * factor.Width);
            splitContainer2.SplitterDistance = (int)(splitContainer2.SplitterDistance * factor.Width);
            splitContainer2.Panel1MinSize = (int)(splitContainer2.Panel1MinSize * factor.Width);

            splitContainer3.SplitterDistance = (int)(splitContainer3.SplitterDistance * factor.Height);
            splitContainer4.SplitterDistance = (int)(splitContainer4.SplitterDistance * factor.Height);

            _scaleFactorX *= factor.Width;
            _scaleFactorY *= factor.Height;
        }

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

        private void DirTree_RvSelected(object sender, MouseEventArgs e)
        {
            RvTreeRow tr = (RvTreeRow)sender;
            Debug.WriteLine(tr.dirFullName);
            updateDatInfo(tr);
        }



        #region DAT dsiplay code
        private void splitContainer3_Panel1_Resize(object sender, EventArgs e)
        {
            gbDatInfo.Width = splitContainer3.Panel1.Width - (gbDatInfo.Left * 2);
        }


        private void gbDatInfo_Resize(object sender, EventArgs e)
        {
            const int leftPos = 89;
            int rightPos = (int)(gbDatInfo.Width / _scaleFactorX) - 15;
            if (rightPos > 600) rightPos = 600;
            int width = rightPos - leftPos;
            int widthB1 = (int)((double)width * 120 / 340);
            int leftB2 = rightPos - widthB1;


            int backD = 97;

            width = (int)(width * _scaleFactorX);
            widthB1 = (int)(widthB1 * _scaleFactorX);
            leftB2 = (int)(leftB2 * _scaleFactorX);
            backD = (int)(backD * _scaleFactorX);


            lblDITName.Width = width;
            lblDITDescription.Width = width;

            lblDITCategory.Width = widthB1;
            lblDITAuthor.Width = widthB1;

            lblDIVersion.Left = leftB2 - backD;
            lblDIDate.Left = leftB2 - backD;

            lblDITVersion.Left = leftB2;
            lblDITVersion.Width = widthB1;
            lblDITDate.Left = leftB2;
            lblDITDate.Width = widthB1;

            lblDITPath.Width = width;

            lblDITRomsGot.Width = widthB1;
            lblDITRomsMissing.Width = widthB1;

            lblDIRomsFixable.Left = leftB2 - backD;
            lblDIRomsUnknown.Left = leftB2 - backD;

            lblDITRomsFixable.Left = leftB2;
            lblDITRomsFixable.Width = widthB1;
            lblDITRomsUnknown.Left = leftB2;
            lblDITRomsUnknown.Width = widthB1;
        }


        private void updateDatInfo(RvTreeRow tr)
        {
            lblDITName.Text = tr.datName;
            lblDITPath.Text = tr.dirFullName;

            if (tr.DatId != null)
            {
                string Description, Category, Version, Author, Date;
                DataAccessLayer.ReadDatInfo((int)tr.DatId, out Description, out Category, out Version, out Author, out Date);
                lblDITDescription.Text = Description;
                lblDITCategory.Text = Category;
                lblDITVersion.Text = Version;
                lblDITAuthor.Text = Author;
                lblDITDate.Text = Date;
            }
            else
            {
                lblDITDescription.Text = "";
                lblDITCategory.Text = "";
                lblDITVersion.Text = "";
                lblDITAuthor.Text = "";
                lblDITDate.Text = "";
            }

        }


       
        #endregion

        #region Game display code

        #endregion

    }
}
