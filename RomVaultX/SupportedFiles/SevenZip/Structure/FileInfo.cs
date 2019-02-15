using System;
using System.IO;

namespace RomVaultX.SupportedFiles.SevenZip.Structure
{
    public class SevenZipFileInfo
    {
        public string[] Names;
        public bool[] EmptyStreamFlags;
        public bool[] EmptyFileFlags;
        public uint[] Attributes;

        public void Read(BinaryReader br)
        {
            Util.log("Begin : ReadFileInfo", 1);

            ulong size = br.ReadEncodedUInt64();
            Names = new string[size];

            ulong numEmptyFiles = 0;

            for (;;)
            {
                HeaderProperty hp = (HeaderProperty)br.ReadByte();
                Util.log("HeaderProperty = " + hp);
                if (hp == HeaderProperty.kEnd)
                {
                    Util.log("End : ReadFileInfo", -1);
                    return;
                }

                ulong bytessize = br.ReadEncodedUInt64();
                switch (hp)
                {
                    case HeaderProperty.kName:
                        if (br.ReadByte() != 0)
                            throw new Exception("Cannot be external");

                        Util.log("Looping Names Begin " + size, 1);
                        for (ulong i = 0; i < size; i++)
                        {
                            Names[i] = br.ReadName();
                            Util.log("enteries[" + i + "]=" + Names[i]);
                        }
                        Util.log("Looping Names End " + size, -1);
                        continue;

                    case HeaderProperty.kEmptyStream:
                        Util.log("reading EmptyStreamFlags Total=" + size);
                        EmptyStreamFlags = Util.ReadBoolFlags(br, (ulong)Names.Length);
                        for (ulong i = 0; i < size; i++)
                            if (EmptyStreamFlags[i]) numEmptyFiles++;
                        continue;

                    case HeaderProperty.kEmptyFile:
                        Util.log("reading numEmptyFilesFlags Total=" + numEmptyFiles);
                        EmptyFileFlags = Util.ReadBoolFlags(br, numEmptyFiles);
                        continue;

                    case HeaderProperty.kWinAttributes:
                        Util.log("skipping bytes " + bytessize);
                        Attributes = Util.ReadUInt32Def(br, size);
                        continue;

                    case HeaderProperty.kLastWriteTime:
                        Util.log("skipping bytes " + bytessize);
                        br.ReadBytes((int)bytessize);
                        continue;

                    case HeaderProperty.kDummy:
                        Util.log("skipping bytes " + bytessize);
                        br.ReadBytes((int)bytessize);
                        continue;

                    default:
                        throw new Exception(hp.ToString());
                }
            }
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write((byte)HeaderProperty.kFilesInfo);
            bw.WriteEncodedUInt64((UInt64)Names.Length);


            byte[] namebyte;
            using (MemoryStream nameMem = new MemoryStream())
            {
                using (BinaryWriter nameBw = new BinaryWriter(nameMem))
                {
                    nameBw.Write((byte)0); //not external
                    foreach (string name in Names)
                        nameBw.WriteName(name);

                    namebyte = new byte[nameMem.Length];
                    nameMem.Position = 0;
                    nameMem.Read(namebyte, 0, namebyte.Length);
                }
            }

            bw.Write((byte)HeaderProperty.kName);
            bw.WriteEncodedUInt64((UInt64)namebyte.Length);
            bw.Write(namebyte);

            if (EmptyStreamFlags != null)
            {

                bw.Write((byte)HeaderProperty.kEmptyStream);
                Util.WriteBoolFlags(bw, EmptyStreamFlags);
            }

            if (EmptyFileFlags != null)
            {
                bw.Write((byte)HeaderProperty.kEmptyFile);
                Util.WriteBoolFlags(bw, EmptyFileFlags);
            }

            if (Attributes != null)
            {
                bw.Write((byte)HeaderProperty.kWinAttributes);
                Util.WriteUint32Def(bw, Attributes);
            }

            bw.Write((byte)HeaderProperty.kEnd);
        }
    }
}
