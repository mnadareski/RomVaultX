namespace RomVaultX.DB
{
    public class RvFile
    {
        public ulong Size;
        public ulong CompressedSize;
        public byte[] CRC;
        public byte[] SHA1;
        public byte[] MD5;

        public FileType AltType;
        public ulong? AltSize;
        public byte[] AltCRC;
        public byte[] AltSHA1;
        public byte[] AltMD5;


        public void DBWrite()
        {
            Program.db.Begin();
            uint fileId=Program.db.RvFileWrite(this);

            if (Size != 0)
            {
                Program.db.RvFileUpdateRom(fileId, this);

                if (FileHeaderReader.AltHeaderFile(AltType))
                    Program.db.RvFileUpdateRomAlt(fileId, this);
            }
            else
                Program.db.RvFileUpdateZeroRom(fileId, this);

            Program.db.Commit();
        }
    }
}
