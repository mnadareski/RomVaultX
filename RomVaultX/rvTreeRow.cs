using System.Drawing;
using System.Runtime.InteropServices;

namespace RomVaultX
{
    public class RvTreeRow
    {
        public int DirId;
        public string dirName;
        public string dirFullName;
        public bool Expanded;

        public int? DatId;
        public string datName;
        public string description;

        public int RomTotal;
        public int RomGot;

        public string TreeBranches;

        public bool MultiDatDir;

        public Rectangle RTree;
        public Rectangle RExpand;
        public Rectangle RIcon;
        public Rectangle RText;
    }

}
