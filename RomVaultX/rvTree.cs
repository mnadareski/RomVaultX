using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using RomVaultX;

namespace ROMVault2
{

    public partial class RvTree : UserControl
    {
        public event MouseEventHandler RvSelected;

        private List<RvTreeRow> rows;

        public RvTree()
        {
            rows = new List<RvTreeRow>();
            InitializeComponent();
        }

        #region "Setup"

        private int _yPos;

        public void Setup()
        {
            rows.Clear();

            using (SQLiteDataReader dr = DataAccessLayer.GetTree())
            {
                int iDirName = dr.GetOrdinal("dirname");
                int iFullName = dr.GetOrdinal("fullname");
                int iDatName = dr.GetOrdinal("datname");

                while (dr.Read())
                {
                    RvTreeRow tr = new RvTreeRow();
                    tr.dirName = dr.GetString(iDirName);
                    tr.dirFullName = dr.GetString(iFullName);
                    tr.datName = dr.IsDBNull(iDatName) ? "" : dr.GetString(iDatName);
                    rows.Add(tr);
                }
            }
            SetupInt();
        }

        private void SetupInt()
        {
            _yPos = 0;

            int treeCount = rows.Count;
            for (int i = 0; i < treeCount; i++)
            {
                RvTreeRow pTree = rows[i];

                int nodeDepth = pTree.dirFullName.Count(x => x == '\\');


                pTree.RTree = new Rectangle(0, _yPos - 8, nodeDepth * 18, 16);
                pTree.RExpand = new Rectangle(5 + nodeDepth * 18, _yPos + 4, 9, 9);
                pTree.RChecked = new Rectangle(20 + nodeDepth * 18, _yPos + 2, 13, 13);
                pTree.RIcon = new Rectangle(35 + nodeDepth * 18, _yPos, 16, 16);
                pTree.RText = new Rectangle(51 + nodeDepth * 18, _yPos, 500, 16);
                pTree.TreeBranches = "";

                _yPos = _yPos + 16;

            }
            AutoScrollMinSize = new Size(500, _yPos);
            Refresh();
        }


        #endregion

        #region "Paint"

        private int _hScroll;
        private int _vScroll;

        protected override void OnPaint(PaintEventArgs e)
        {
            
            Graphics g = e.Graphics;

            _hScroll = HorizontalScroll.Value;
            _vScroll = VerticalScroll.Value;

            Rectangle t = new Rectangle(e.ClipRectangle.Left + _hScroll, e.ClipRectangle.Top + _vScroll, e.ClipRectangle.Width, e.ClipRectangle.Height);

            g.FillRectangle(Brushes.White, e.ClipRectangle);


            int treeCount = rows.Count;
            for (int i = 0; i < treeCount; i++)
            {
                RvTreeRow pTree = rows[i];
                PaintTree(pTree, g, t);
            }



        }

        private void PaintTree(RvTreeRow pTree, Graphics g, Rectangle t)
        {
            int y = pTree.RTree.Top - _vScroll;

            if (pTree.RTree.IntersectsWith(t))
            {
                Pen p = new Pen(Brushes.Gray, 1) { DashStyle = DashStyle.Dot };

                string lTree = pTree.TreeBranches;
                for (int j = 0; j < lTree.Length; j++)
                {
                    int x = j * 18 - _hScroll;
                    string cTree = lTree.Substring(j, 1);
                    switch (cTree)
                    {
                        case "│":
                            g.DrawLine(p, x + 9, y, x + 9, y + 16);
                            break;

                        case "├":
                        case "└":
                            g.DrawLine(p, x + 9, y, x + 9, y + 16);
                            g.DrawLine(p, x + 9, y + 16, x + 27, y + 16);
                            break;
                    }
                }
            }

            if (!pTree.RExpand.IsEmpty)
                if (pTree.RExpand.IntersectsWith(t))
                {
                    g.DrawImage(pTree.TreeExpanded ? RvImages.ExpandBoxMinus : RvImages.ExpandBoxPlus, RSub(pTree.RExpand, _hScroll, _vScroll));
                }


            if (pTree.RChecked.IntersectsWith(t))
            {
                switch (pTree.Checked)
                {
                    case RvTreeRow.TreeSelect.Disabled:
                        g.DrawImage(RvImages.TickBoxDisabled, RSub(pTree.RChecked, _hScroll, _vScroll));
                        break;
                    case RvTreeRow.TreeSelect.UnSelected:
                        g.DrawImage(RvImages.TickBoxUnTicked, RSub(pTree.RChecked, _hScroll, _vScroll));
                        break;
                    case RvTreeRow.TreeSelect.Selected:
                        g.DrawImage(RvImages.TickBoxTicked, RSub(pTree.RChecked, _hScroll, _vScroll));
                        break;
                }
            }

            if (pTree.RIcon.IntersectsWith(t))
            {
                int icon = 2;
                /*
                if (pTree.DirStatus.HasInToSort())
                {
                    icon = 4;
                }
                else if (!pTree.DirStatus.HasCorrect())
                {
                    icon = 1;
                }
                else if (!pTree.DirStatus.HasMissing())
                {
                    icon = 3;
                }
                */


                Bitmap bm;
                //if (pTree.Dat == null && pTree.DirDatCount != 1) // Directory above DAT's in Tree
                if (string.IsNullOrEmpty(pTree.datName))
                    bm = RvImages.GetBitmap("DirectoryTree" + icon);
                else
                    bm = RvImages.GetBitmap("Tree" + icon);
               
                if (bm != null)
                {
                    g.DrawImage(bm, RSub(pTree.RIcon, _hScroll, _vScroll));
                }
            }



            Rectangle recBackGround = new Rectangle(pTree.RText.X, pTree.RText.Y, Width - pTree.RText.X + _hScroll, pTree.RText.Height);

            if (recBackGround.IntersectsWith(t))
            {
                string thistxt = string.IsNullOrEmpty(pTree.datName) ? pTree.dirName : pTree.datName;

                g.DrawString(thistxt, new Font("Microsoft Sans Serif", 8), Brushes.Black, pTree.RText.Left - _hScroll, pTree.RText.Top + 1 - _vScroll);


            }
        }


        private static Rectangle RSub(Rectangle r, int h, int v)
        {
            Rectangle ret = new Rectangle(r.Left - h, r.Top - v, r.Width, r.Height);
            return ret;
        }

        #endregion
    }
}
