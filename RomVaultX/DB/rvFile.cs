using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RomVaultX.DB
{
    public class rvFile
    {
        public ulong Size;
        public ulong CompressedSize;
        public byte[] CRC;
        public byte[] SHA1;
        public byte[] MD5;
    }
}
