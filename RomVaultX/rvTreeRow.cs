/******************************************************
 *     ROMVault2 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2014                                 *
 ******************************************************/

using System.Drawing;
using System.IO;

namespace ROMVault2
{
    public class RvTreeRow
    {
        public enum TreeSelect { UnSelected, Selected, Disabled }

        public string dirName;
        public string dirFullName;
        public string datName;

        public string TreeBranches;
        private long _filePointer = -1;
        private bool _pTreeExpanded;
        private TreeSelect _pChecked;

        public Rectangle RTree;
        public Rectangle RExpand;
        public Rectangle RChecked;
        public Rectangle RIcon;
        public Rectangle RText;

        public RvTreeRow()
        {
            _pTreeExpanded = true;
            _pChecked = TreeSelect.Selected;
        }

        public void Write(BinaryWriter bw)
        {
            _filePointer = bw.BaseStream.Position;
            bw.Write(_pTreeExpanded);
            bw.Write((byte)_pChecked);
        }

        public void Read(BinaryReader br)
        {
            _filePointer = br.BaseStream.Position;
            _pTreeExpanded = br.ReadBoolean();
            _pChecked = (TreeSelect)br.ReadByte();
        }

        public bool TreeExpanded
        {
            get
            {
                return _pTreeExpanded;
            }
            set
            {
                if (_pTreeExpanded != value)
                {
                    _pTreeExpanded = value;
                    CacheUpdate();
                }
            }
        }

        public TreeSelect Checked
        {
            get
            {
                return _pChecked;
            }
            set
            {
                if (_pChecked != value)
                {
                    _pChecked = value;
                    CacheUpdate();
                }
            }
        }

        private void CacheUpdate()
        {
           
        }
    }

}
