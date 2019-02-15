using System;
using System.IO;

namespace RomVaultX.SupportedFiles.SevenZip.Structure
{
    public class StreamsInfo
    {
        public ulong PackPosition;
        public PackedStreamInfo[] PackedStreams;
        public Folder[] Folders;

        public void Read(BinaryReader br)
        {
            Util.log("Begin : ReadStreamsInfo", 1);
            for (;;)
            {
                HeaderProperty hp = (HeaderProperty)br.ReadByte();
                Util.log("HeaderProperty = " + hp);
                switch (hp)
                {
                    case HeaderProperty.kPackInfo:
                        PackedStreamInfo.Read(br, out PackPosition, out PackedStreams);
                        continue;

                    case HeaderProperty.kUnPackInfo:
                        Folder.ReadUnPackInfo(br, out Folders);
                        continue;

                    case HeaderProperty.kSubStreamsInfo:
                        Folder.ReadSubStreamsInfo(br, ref Folders);
                        continue;

                    case HeaderProperty.kEnd:
                        Util.log("End : ReadStreamInfo", -1);
                        return;

                    default:
                        throw new Exception(hp.ToString());
                }
            }
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write((byte)HeaderProperty.kMainStreamsInfo);
            PackedStreamInfo.Write(bw, PackPosition, PackedStreams);
            Folder.WriteUnPackInfo(bw, Folders);
            Folder.WriteSubStreamsInfo(bw, Folders);
            bw.Write((byte)HeaderProperty.kEnd);
        }
    }
}
