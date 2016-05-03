using System.Collections.Generic;

namespace RomVaultX.DB
{
    public class RvRom
    {
        public uint RomId;
        public uint GameId;
        public string Name;
        public ulong? Size;
        public FileType altType;
        public byte[] CRC;
        public byte[] SHA1;
        public byte[] MD5;
        public string Merge;
        public string Status;
        public bool PutInZip;
        public ulong? FileId;

        public ulong? fileSize;
        public ulong? fileCompressedSize;
        public byte[] fileCRC;
        public byte[] fileSHA1;
        public byte[] fileMD5;


    

        public static List<RvRom> ReadRoms(uint gameId)
        {
            return Program.db.RvRomsRead(gameId);
        }

        public void DBWrite()
        {
            FileId = DatUpdate.NoFilesInDb ? null : Program.db.FindAFile(this);
            Program.db.RvRomWrite(this);
        }
    }
}
