using System.IO;

namespace RomVaultX.SupportedFiles.SevenZip.Structure
{
    public class BindPair
    {
        public ulong InIndex;
        public ulong OutIndex;

        public void Read(BinaryReader br)
        {
            InIndex = br.ReadEncodedUInt64();
            Util.log("InIndex = " + InIndex);
            OutIndex = br.ReadEncodedUInt64();
            Util.log("OutIndex = " + OutIndex);
        }

        public void Write(BinaryWriter bw)
        {
            bw.WriteEncodedUInt64(InIndex);
            bw.WriteEncodedUInt64(OutIndex);
        }
    }
}
