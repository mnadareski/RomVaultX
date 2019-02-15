using System;
using System.IO;
using RomVaultX.SupportedFiles.SevenZip.Compress.LZMA;

namespace RomVaultX.SupportedFiles.SevenZip.Structure
{
    public class Header
    {
        public StreamsInfo StreamsInfo;
        public SevenZipFileInfo FileInfo;

        public void Read(BinaryReader br)
        {
            Util.log("Begin : ReadHeader", 1);
            for (;;)
            {
                HeaderProperty hp = (HeaderProperty)br.ReadByte();
                Util.log("HeaderProperty = " + hp);
                switch (hp)
                {
                    case HeaderProperty.kMainStreamsInfo:
                        StreamsInfo = new StreamsInfo();
                        StreamsInfo.Read(br);
                        break;

                    case HeaderProperty.kFilesInfo:
                        FileInfo = new SevenZipFileInfo();
                        FileInfo.Read(br);
                        break;

                    case HeaderProperty.kEnd:
                        Util.log("End : ReadHeader", -1);
                        return;

                    default:
                        throw new Exception(hp.ToString());
                }
            }
        }


        private void Write(BinaryWriter bw)
        {
            bw.Write((byte)HeaderProperty.kHeader);
            StreamsInfo.Write(bw);
            FileInfo.Write(bw);
            bw.Write((byte)HeaderProperty.kEnd);
        }

        public void WriteHeader(BinaryWriter bw)
        {
            Write(bw);
        }

        public static ZipReturn ReadHeaderOrPackedHeader(Stream stream, long baseOffset, out Header header)
        {
            header = null;
            Util.log("Begin : ReadHeaderOrPackedHeader", 1);

            BinaryReader br = new BinaryReader(stream);

            HeaderProperty hp = (HeaderProperty)br.ReadByte();
            Util.log("HeaderProperty = " + hp);
            switch (hp)
            {
                case HeaderProperty.kEncodedHeader:
                    {
                        StreamsInfo streamsInfo = new StreamsInfo();
                        streamsInfo.Read(br);

                        if (streamsInfo.Folders.Length > 1)
                            return ZipReturn.ZipUnsupportedCompression;

                        Folder firstFolder = streamsInfo.Folders[0];
                        if (firstFolder.Coders.Length > 1)
                            return ZipReturn.ZipUnsupportedCompression;

                        byte[] method = firstFolder.Coders[0].Method;
                        if (!(method.Length == 3 && method[0] == 3 && method[1] == 1 && method[2] == 1))  // LZMA
                            return ZipReturn.ZipUnsupportedCompression;

                        stream.Seek(baseOffset + (long)streamsInfo.PackPosition, SeekOrigin.Begin);
                        using (Decoder decoder = new Decoder())
                        {
                            decoder.SetDecoderProperties(firstFolder.Coders[0].Properties);
                            decoder.SetUpStream(stream);
                            ZipReturn zr = ReadHeaderOrPackedHeader(decoder, baseOffset, out header);
                            if (zr != ZipReturn.ZipGood)
                                return zr;
                        }
                        Util.log("End : ReadHeaderOrPackedHeader (Packed)", -1);
                        return ZipReturn.ZipGood;
                    }
                case HeaderProperty.kHeader:
                    {
                        header = new Header();
                        header.Read(br);
                        Util.log("End : ReadHeaderOrPackedHeader (UnPacked)", -1);
                        return ZipReturn.ZipGood;
                    }
            }

            return ZipReturn.ZipCenteralDirError;
        }
    }
}
