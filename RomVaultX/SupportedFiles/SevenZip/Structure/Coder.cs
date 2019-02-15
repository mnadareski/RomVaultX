using System;
using System.IO;

namespace RomVaultX.SupportedFiles.SevenZip.Structure
{
    public class Coder
    {
        public byte[] Method;
        public ulong NumInStreams;
        public ulong NumOutStreams;
        public byte[] Properties;

        public void Read(BinaryReader br)
        {
            Util.log("Begin : ReadCoder", 1);

            byte flags = br.ReadByte();
            Util.log("Flags = " + flags.ToString("X"));
            int decompressionMethodIdSize = flags & 0xf;
            Method = br.ReadBytes(decompressionMethodIdSize);
            if ((flags & 0x10) != 0)
            {
                NumInStreams = br.ReadEncodedUInt64();
                Util.log("NumInStreams = " + NumInStreams);
                NumOutStreams = br.ReadEncodedUInt64();
                Util.log("NumOutStreams = " + NumOutStreams);
            }
            else
            {
                NumInStreams = 1;
                NumOutStreams = 1;
            }
            if ((flags & 0x20) != 0)
            {
                ulong propSize = br.ReadEncodedUInt64();
                Util.log("PropertiesSize = " + propSize);
                Properties = br.ReadBytes((int)propSize);
                Util.log("Properties = " + Properties);
            }
            if ((flags & 0x80) != 0)
                throw new NotSupportedException("External flag");

            Util.log("End : ReadCoder", -1);
        }

        public void Write(BinaryWriter bw)
        {
            byte flags = (byte)Method.Length;
            if (NumInStreams != 1 || NumOutStreams != 1)
                flags = (byte)(flags | 0x10);
            if (Properties != null && Properties.Length > 0)
                flags = (byte)(flags | 0x20);
            bw.Write(flags);

            bw.Write(Method);

            if (NumInStreams != 1 || NumOutStreams != 1)
            {
                bw.WriteEncodedUInt64(NumInStreams);
                bw.WriteEncodedUInt64(NumOutStreams);
            }

            if (Properties != null && Properties.Length > 0)
            {
                bw.WriteEncodedUInt64((ulong)Properties.Length);
                bw.Write(Properties);
            }
        }
    }
}
