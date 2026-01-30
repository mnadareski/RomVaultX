using System;
using System.Collections.Generic;
using System.IO;

namespace FileHeaderReader
{
    public enum HeaderFileType
    {
        // Archive
        Nothing = 0,
        ZIP,
        GZ,
        SevenZip,
        RAR,

        // Specialty
        CHD,

        // Copier header
        A7800,
        Lynx,
        FDS,
        NES,
        PCE,
        PSID,
        SNES,
        SPC,
    }

    public static class FileHeaderReader
    {
        private static readonly List<Detector> Detectors;

        static FileHeaderReader()
        {
            Detectors =
            [
                // Standard archive types
                new(HeaderFileType.ZIP, 22, 0, "", new Data(0, [0x50, 0x4b, 0x03, 0x04])),
                new(HeaderFileType.GZ, 18, 0, "", new Data(0, [0x1f, 0x8b, 0x08])),
                new(HeaderFileType.SevenZip, 6, 0, "", new Data(0, [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C])),
                new(HeaderFileType.RAR, 6, 0, "", new Data(0, [0x52, 0x61, 0x72, 0x21, 0x1A, 0x07])),

                // CHD
                new(HeaderFileType.CHD, 76, 0, "", new Data(0, [(byte) 'M', (byte) 'C', (byte) 'o', (byte) 'm', (byte) 'p', (byte) 'r', (byte) 'H', (byte) 'D'])),

                // Headered files

                // Atari 7800
                new(HeaderFileType.A7800, 128, 128, "a7800.xml", new Data(1, [0x41, 0x54, 0x41, 0x52, 0x49, 0x37, 0x38, 0x30, 0x30])),
                new(HeaderFileType.A7800, 128, 128, "a7800.xml", new Data(100, [0x41, 0x43, 0x54, 0x55, 0x41, 0x4C, 0x20, 0x43, 0x41, 0x52, 0x54, 0x20, 0x44, 0x41, 0x54, 0x41, 0x20, 0x53, 0x54, 0x41, 0x52, 0x54, 0x53, 0x20, 0x48, 0x45, 0x52, 0x45])),

                // Nintendo Famicom Disk System
                new(HeaderFileType.FDS, 16, 16, "fds.xml", new Data(0, [0x46, 0x44, 0x53, 0x1A, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00])),
                new(HeaderFileType.FDS, 16, 16, "fds.xml", new Data(0, [0x46, 0x44, 0x53, 0x1A, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00])),
                new(HeaderFileType.FDS, 16, 16, "fds.xml", new Data(0, [0x46, 0x44, 0x53, 0x1A, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00])),
                new(HeaderFileType.FDS, 16, 16, "fds.xml", new Data(0, [0x46, 0x44, 0x53, 0x1A, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00])),

                // Atari Lynx
                new(HeaderFileType.Lynx, 64, 64, "lynx.xml", new Data(0, [0x4C, 0x59, 0x4E, 0x58])),
                new(HeaderFileType.Lynx, 64, 64, "lynx.xml", new Data(6, [0x42, 0x53, 0x39])),

                // Nintendo Entertainment System and Nintendo Famicom
                new(HeaderFileType.NES, 16, 16, "nes.xml", new Data(0, [0x4E, 0x45, 0x53, 0x1A])),

                // NEC PC-Engine and NEC TurboGrafx-16
                new(HeaderFileType.PCE, 512, 512, "pce.xml", new Data(0, [0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xAA, 0xBB, 0x02])),

                // Commodore PSID Music Files
                new(HeaderFileType.PSID, 118, 118, "psid.xml", new Data(0, [0x50, 0x53, 0x49, 0x44, 0x00, 0x01, 0x00, 0x76])),
                new(HeaderFileType.PSID, 118, 118, "psid.xml", new Data(0, [0x50, 0x53, 0x49, 0x44, 0x00, 0x03, 0x00, 0x7c])),
                new(HeaderFileType.PSID, 124, 124, "psid.xml", new Data(0, [0x50, 0x53, 0x49, 0x44, 0x00, 0x02, 0x00, 0x7c])),
                new(HeaderFileType.PSID, 124, 124, "psid.xml", new Data(0, [0x50, 0x53, 0x49, 0x44, 0x00, 0x01, 0x00, 0x7c])),
                new(HeaderFileType.PSID, 124, 124, "psid.xml", new Data(0, [0x52, 0x53, 0x49, 0x44, 0x00, 0x02, 0x00, 0x7c])),

                // Super Nintendo Entertainment System and Nintendo Super Famicom
                new(HeaderFileType.SNES, 512, 512, "snes.xml", new Data(22, [0xAA, 0xBB, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00])),
                new(HeaderFileType.SNES, 512, 512, "snes.xml", new Data(22, [0x53, 0x55, 0x50, 0x45, 0x52, 0x55, 0x46, 0x4F])),

                // Super Nintendo Entertainment System and Nintendo Super Famicom Music Files
                new(HeaderFileType.SPC, 256, 256, "spc.xml", new Data(0, [0x53, 0x4E, 0x45, 0x53, 0x2D, 0x53, 0x50, 0x43])),
            ];
        }

        public static int GetFileHeaderLength(HeaderFileType FType)
        {
            foreach (Detector d in Detectors)
            {
                if (d.FType == FType)
                    return d.FileOffset;
            }

            return 0;
        }

        public static HeaderFileType GetFileTypeFromHeader(string header)
        {
            if (string.IsNullOrWhiteSpace(header))
                return HeaderFileType.Nothing;

            string theader = header.ToLowerInvariant();
            foreach (Detector d in Detectors)
            {
                if (string.IsNullOrEmpty(d.HeaderId))
                {
                    continue;
                }

                if (theader.Equals(d.HeaderId, StringComparison.OrdinalIgnoreCase))
                {
                    return d.FType;
                }
            }

            return HeaderFileType.Nothing;
        }

        public static bool AltHeaderFile(HeaderFileType fileType)
        {
            return (fileType == HeaderFileType.A7800) ||
                   (fileType == HeaderFileType.FDS) ||
                   (fileType == HeaderFileType.Lynx) ||
                   (fileType == HeaderFileType.NES) ||
                   (fileType == HeaderFileType.PCE) ||
                   (fileType == HeaderFileType.PSID) ||
                   (fileType == HeaderFileType.SNES) ||
                   (fileType == HeaderFileType.SPC);
            // || (fileType == HeaderFileType.CHD);
        }

        public static HeaderFileType GetType(Stream sIn, out int offset)
        {
            int headerSize = 128;
            if (sIn.Length < headerSize)
            {
                headerSize = (int)sIn.Length;
            }

            byte[] buffer = new byte[headerSize];

            headerSize = sIn.Read(buffer, 0, headerSize);

            return GetType(buffer, headerSize, out offset);
        }

        public static HeaderFileType GetType(byte[] buffer, int headerSize, out int offset)
        {
            foreach (Detector detector in Detectors)
            {
                if (headerSize < detector.Data.Value.Length + detector.Data.Offset)
                {
                    continue;
                }

                if (ByteComp(buffer, detector.Data))
                {
                    offset = detector.FileOffset;
                    return detector.FType;
                }
            }

            offset = 0;
            return HeaderFileType.Nothing;
        }

        private static bool ByteComp(byte[] buffer, Data d)
        {
            if (buffer.Length < d.Value.Length + d.Offset)
            {
                return false;
            }
            for (int i = 0; i < d.Value.Length; i++)
            {
                if (buffer[i + d.Offset] != d.Value[i])
                {
                    return false;
                }
            }
            return true;
        }

        private class Detector
        {
            public readonly HeaderFileType FType;
            public readonly int HeaderLength;
            public readonly int FileOffset;
            public readonly string HeaderId;
            public readonly Data Data;


            public Detector(HeaderFileType fType, int headerLength, int fileOffset, string headerId, Data data)
            {
                FType = fType;
                HeaderLength = headerLength;
                FileOffset = fileOffset;
                HeaderId = headerId.ToLowerInvariant();
                Data = data;
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
    }
}
