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


            public Detector(FileType fType, int headerLength, int fileOffset,string headerId, Data data)
            {
                FType = fType;
                HeaderLength = headerLength;
                FileOffset = fileOffset;
                HeaderId = headerId.ToLower();
                Datas = new List<Data> { data };
            }
            public Detector(FileType fType, int headerLength, int fileOffset,string headerId, List<Data> datas)
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

        static FileHeaderReader()
        {
            Detectors = new List<Detector>
            {
                new Detector(FileType.ZIP, 22, 0,"", new Data(0, new byte[] {0x50, 0x4b, 0x03, 0x04})),
                new Detector(FileType.GZ, 18, 0,"", new Data(0, new byte[] {0x1f, 0x8b, 0x08})), 
                new Detector(FileType.SevenZip, 6, 0,"", new Data(0, new byte[] {0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C})), 
                new Detector(FileType.RAR, 6, 0,"", new Data(0, new byte[] {0x52, 0x61, 0x72, 0x21, 0x1A, 0x07})), 
           
                new Detector(FileType.CHD, 76, 0,"", new Data(0, new byte[] {(byte) 'M', (byte) 'C', (byte) 'o', (byte) 'm', (byte) 'p', (byte) 'r', (byte) 'H', (byte) 'D'})),
                new Detector(FileType.Lynx, 40, 40,"No-Intro_A7800.xml", new Data(0, new byte[] {0x4C, 0x59, 0x4E, 0x58})),
                new Detector(FileType.NES, 10, 10,"No-Intro_FDS.xml", new Data(0, new byte[] {0x04E, 0x45, 0x53}))
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
            return fileType == FileType.Lynx || fileType == FileType.NES;
        }
     
        public static FileType GetType(Stream sIn,out int offset)
        {

            int headSize = 76;
            if (sIn.Length < headSize)
                headSize = (int)sIn.Length;

            byte[] buffer = new byte[headSize];

            sIn.Read(buffer, 0, headSize);

            foreach (Detector detector in Detectors)
            {
                if (headSize <= detector.HeaderLength) continue;

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
