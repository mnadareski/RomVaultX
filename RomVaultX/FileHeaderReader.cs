using System.Collections.Generic;
using System.IO;
using RomVaultX.DB;

namespace RomVaultX
{
    public enum FileType
    {
        Nothing = 0,
        ZIP,
        GZ,
        SevenZip,
        RAR,

        CHD,

        A7800,
        Lynx,
        NES
    }


    public static class FileHeaderReader
    {
        private static readonly List<Detector> Detectors;

        private class Detector
        {
            public readonly FileType FType;
            public readonly int HeaderLength;
            public readonly int FileOffset;
            public readonly string HeaderId;
            public readonly List<Data> Datas;


            public Detector(FileType fType, int headerLength, int fileOffset, string headerId, Data data)
            {
                FType = fType;
                HeaderLength = headerLength;
                FileOffset = fileOffset;
                HeaderId = headerId.ToLower();
                Datas = new List<Data> { data };
            }
            public Detector(FileType fType, int headerLength, int fileOffset, string headerId, List<Data> datas)
            {
                FType = fType;
                HeaderLength = headerLength;
                FileOffset = fileOffset;
                HeaderId = headerId.ToLower();
                Datas = datas;
            }
        }

        private class Data
        {
            public readonly int Offset;
            public readonly byte[] Value;

            public Data(int offset, byte[] value)
            {
                Offset = offset;
                Value = value;
            }
        }


        /*
        <detector>
            <name>No-Intro Atari 7800 Dat Header Skipper</name>
            <author>Connie</author>
            <version>20130123</version>
            <rule start_offset="80" end_offset="EOF" operation="none">
                <data offset="1" value="415441524937383030" result="true"/>
                <data offset="60" value="0000000041435455414C20434152542044415441205354415254532048455245" result="true"/>
            </rule>
        </detector>

        <detector>
            <name>No-Intro Lynx Dat LNX Header Skipper</name>
            <author>Yakushi~Kabuto</author>
            <version>20070408</version>
            <rule start_offset="40">
                <data offset="0" value="4C594E58"/>
            </rule>
        </detector>

        <detector>
            <name>No-Intro NES Dat iNES Header Skipper</name>
            <author>Yakushi~Kabuto</author>
            <version>20070321</version>
            <rule start_offset="10">
                <data offset="0" value="4E4553"/>
            </rule>
        </detector>

        */

        static FileHeaderReader()
        {
            Detectors = new List<Detector>
            {
                new Detector(FileType.ZIP, 22, 0,"", new Data(0, new byte[] {0x50, 0x4b, 0x03, 0x04})),
                new Detector(FileType.GZ, 18, 0,"", new Data(0, new byte[] {0x1f, 0x8b, 0x08})),
                new Detector(FileType.SevenZip, 6, 0,"", new Data(0, new byte[] {0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C})),
                new Detector(FileType.RAR, 6, 0,"", new Data(0, new byte[] {0x52, 0x61, 0x72, 0x21, 0x1A, 0x07})),

                new Detector(FileType.CHD, 76, 0,"", new Data(0, new byte[] {(byte) 'M', (byte) 'C', (byte) 'o', (byte) 'm', (byte) 'p', (byte) 'r', (byte) 'H', (byte) 'D'})),
                new Detector(FileType.A7800, 128, 128,"No-Intro_A7800.xml", new Data(1, new byte[] {0x41,0x54,0x41,0x52,0x49,0x37,0x38,0x30,0x30})),
                new Detector(FileType.Lynx, 64, 64,"No-Intro_LNX.xml", new Data(0, new byte[] {0x4C, 0x59, 0x4E, 0x58})),
                new Detector(FileType.NES, 16, 16,"No-Intro_NES.xml", new Data(0, new byte[] {0x04E, 0x45, 0x53, 0x1A}))
            };
        }

        public static FileType GetFileTypeFromHeader(string header)
        {
            string theader = header.ToLower();
            foreach (Detector d in Detectors)
            {
                if (string.IsNullOrEmpty(d.HeaderId))
                    continue;

                if (theader == d.HeaderId)
                    return d.FType;
            }
            return FileType.Nothing;
        }

        public static bool AltHeaderFile(FileType fileType)
        {
            return fileType == FileType.A7800 || fileType == FileType.Lynx || fileType == FileType.NES;
        }

        public static FileType GetType(Stream sIn, out int offset)
        {

            int headSize = 128;
            if (sIn.Length < headSize)
                headSize = (int)sIn.Length;

            byte[] buffer = new byte[headSize];

            sIn.Read(buffer, 0, headSize);

            foreach (Detector detector in Detectors)
            {
                if (headSize < detector.HeaderLength) continue;

                bool found = true;
                foreach (Data data in detector.Datas)
                    found &= ByteComp(buffer, data);

                if (found)
                {
                    offset = detector.FileOffset;
                    return detector.FType;
                }
            }

            offset = 0;
            return FileType.Nothing;
        }

        private static bool ByteComp(byte[] buffer, Data d)
        {
            if (buffer.Length < d.Value.Length + d.Offset) return false;
            for (int i = 0; i < d.Value.Length; i++)
            {
                if (buffer[i + d.Offset] != d.Value[i])
                    return false;
            }
            return true;
        }
    }
}
