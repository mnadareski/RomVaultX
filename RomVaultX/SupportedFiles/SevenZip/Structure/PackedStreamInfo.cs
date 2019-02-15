using System;
using System.IO;

namespace RomVaultX.SupportedFiles.SevenZip.Structure
{
    public class PackedStreamInfo
    {
        public ulong PackedSize;
        public ulong? Crc;
        public ulong StreamPosition;

        public static void Read(BinaryReader br, out ulong packPosition, out PackedStreamInfo[] packedStreams)
        {
            Util.log("Begin : ReadPackInfo", 1);

            packPosition = br.ReadEncodedUInt64();
            Util.log("PackPosition = " + packPosition);

            ulong numPackStreams = br.ReadEncodedUInt64();
            Util.log("NumPackStreams = " + numPackStreams);

            packedStreams = new PackedStreamInfo[numPackStreams];
            for (ulong i = 0; i < numPackStreams; i++)
                packedStreams[i] = new PackedStreamInfo();

            ulong streamPosition = 0;

            for (;;)
            {
                HeaderProperty hp = (HeaderProperty)br.ReadByte();
                Util.log("HeaderProperty = " + hp);
                switch (hp)
                {
                    case HeaderProperty.kSize:
                        for (ulong i = 0; i < numPackStreams; i++)
                        {
                            packedStreams[i].StreamPosition = streamPosition;
                            Util.log("PackdStream[" + i + "].StreamPosition = " + packedStreams[i].StreamPosition);
                            packedStreams[i].PackedSize = br.ReadEncodedUInt64();
                            Util.log("PackdStream[" + i + "].PackedSize = " + packedStreams[i].PackedSize);
                            streamPosition += packedStreams[i].PackedSize;
                        }
                        continue;

                    case HeaderProperty.kCRC:
                        for (ulong i = 0; i < numPackStreams; i++)
                        {
                            packedStreams[i].Crc = br.ReadEncodedUInt64();
                            Util.log("PackdStream[" + i + "].CRC = " + ((ulong)packedStreams[i].Crc).ToString("X"));
                        }
                        continue;

                    case HeaderProperty.kEnd:
                        Util.log("End : ReadPackInfo", -1);
                        return;

                    default:
                        throw new Exception(hp.ToString());
                }
            }
        }

        public static void Write(BinaryWriter bw, ulong packPosition, PackedStreamInfo[] packedStreams)
        {
            ulong numPackStreams = (ulong)packedStreams.Length;
            bw.Write((byte)HeaderProperty.kPackInfo);
            bw.WriteEncodedUInt64(packPosition);
            bw.WriteEncodedUInt64(numPackStreams);

            bw.Write((byte)HeaderProperty.kSize);
            ulong streamPosition = 0;
            for (ulong i = 0; i < numPackStreams; i++)
            {
                packedStreams[i].StreamPosition = streamPosition;
                bw.WriteEncodedUInt64(packedStreams[i].PackedSize);
                streamPosition += packedStreams[i].PackedSize;
            }

            // Only checking the first CRC assuming all the reset will be the same
            if (packedStreams[0].Crc != null)
            {
                bw.Write((byte)HeaderProperty.kCRC);
                for (ulong i = 0; i < numPackStreams; i++)
                    bw.WriteEncodedUInt64(packedStreams[i].Crc ?? 0);
            }

            bw.Write((byte)HeaderProperty.kEnd);

        }
    }
}
