/******************************************************
 *     ROMVault2 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2014                                 *
 ******************************************************/

using System.Drawing;

namespace RomVaultX
{
    public class RvTreeRow
    {
        public int DirId;
        public string dirName;
        public string dirFullName;
        public int? DatId;
        public string datName;

        public string TreeBranches;
        public bool TreeExpanded;

        public bool MultiDatDir;

        public Rectangle RTree;
        public Rectangle RExpand;
        public Rectangle RIcon;
        public Rectangle RText;

        public RvTreeRow()
        {
            TreeExpanded = true;
        }
    }

}
