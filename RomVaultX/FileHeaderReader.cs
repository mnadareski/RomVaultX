using System.Collections.Generic;
//using System.IO;
using RomVaultX.DB;
using Alphaleonis.Win32.Filesystem;

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

		static FileHeaderReader()
		{
			Detectors = new List<Detector>
			{
				// Standard archive types
				new Detector(FileType.ZIP, 22, 0,"", new Data(0, new byte[] {0x50, 0x4b, 0x03, 0x04})),
				new Detector(FileType.GZ, 18, 0,"", new Data(0, new byte[] {0x1f, 0x8b, 0x08})),
				new Detector(FileType.SevenZip, 6, 0,"", new Data(0, new byte[] {0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C})),
				new Detector(FileType.RAR, 6, 0,"", new Data(0, new byte[] {0x52, 0x61, 0x72, 0x21, 0x1A, 0x07})),

				// CHDs
				new Detector(FileType.CHD, 76, 0,"", new Data(0, new byte[] {(byte) 'M', (byte) 'C', (byte) 'o', (byte) 'm', (byte) 'p', (byte) 'r', (byte) 'H', (byte) 'D'})),

				// Headered files

				// Atari 7800
				new Detector(FileType.A7800, 128, 128, "a7800.xml", new Data(1, new byte[] {0x41, 0x54, 0x41, 0x52, 0x49, 0x37, 0x38, 0x30, 0x30})),
				new Detector(FileType.A7800, 128, 128, "a7800.xml", new Data(100, new byte[] {0x41, 0x43, 0x54, 0x55, 0x41, 0x4C, 0x20, 0x43, 0x41, 0x52, 0x54, 0x20, 0x44, 0x41, 0x54, 0x41, 0x20, 0x53, 0x54, 0x41, 0x52, 0x54, 0x53, 0x20, 0x48, 0x45, 0x52, 0x45})),

				// Nintendo Famicom Disk System
				new Detector(FileType.FDS, 16, 16, "fds.xml", new Data(0, new byte[] {0x46, 0x44, 0x53, 0x1A, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00})),
				new Detector(FileType.FDS, 16, 16, "fds.xml", new Data(0, new byte[] {0x46, 0x44, 0x53, 0x1A, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00})),
				new Detector(FileType.FDS, 16, 16, "fds.xml", new Data(0, new byte[] {0x46, 0x44, 0x53, 0x1A, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00})),
				new Detector(FileType.FDS, 16, 16, "fds.xml", new Data(0, new byte[] {0x46, 0x44, 0x53, 0x1A, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00})),

				// Atari Lynx
				new Detector(FileType.Lynx, 64, 64, "lynx.xml", new Data(0, new byte[] {0x4C, 0x59, 0x4E, 0x58})),
				new Detector(FileType.Lynx, 64, 64, "lynx.xml", new Data(6, new byte[] {0x42, 0x53, 0x39})),

				// Nintendo Entertainment System and Nintendo Famicom
				new Detector(FileType.NES, 16, 16, "nes.xml", new Data(0, new byte[] {0x4E, 0x45, 0x53, 0x1A})),

				// NEC PC-Engine and NEC TurboGrafx-16
				new Detector(FileType.PCE, 512, 512, "pce.xml", new Data(0, new byte[] {0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xAA, 0xBB, 0x02})),

				// Commodore PSID Music Files
				new Detector(FileType.PSID, 118, 118, "psid.xml", new Data(0, new byte[] {0x50, 0x53, 0x49, 0x44, 0x00, 0x01, 0x00, 0x76})),
				new Detector(FileType.PSID, 118, 118, "psid.xml", new Data(0, new byte[] {0x50, 0x53, 0x49, 0x44, 0x00, 0x03, 0x00, 0x7c})),
				new Detector(FileType.PSID, 124, 124, "psid.xml", new Data(0, new byte[] {0x50, 0x53, 0x49, 0x44, 0x00, 0x02, 0x00, 0x7c})),
				new Detector(FileType.PSID, 124, 124, "psid.xml", new Data(0, new byte[] {0x50, 0x53, 0x49, 0x44, 0x00, 0x01, 0x00, 0x7c})),
				new Detector(FileType.PSID, 124, 124, "psid.xml", new Data(0, new byte[] {0x52, 0x53, 0x49, 0x44, 0x00, 0x02, 0x00, 0x7c})),

				// Super Nintendo Entertainment System and Nintendo Super Famicom
				new Detector(FileType.SNES, 512, 512, "snes.xml", new Data(22, new byte[] {0xAA, 0xBB, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00})),
				new Detector(FileType.SNES, 512, 512, "snes.xml", new Data(22, new byte[] {0x53, 0x55, 0x50, 0x45, 0x52, 0x55, 0x46, 0x4F})),

				// Super Nintendo Entertainment System and Nintendo Super Famicom Music Files
				new Detector(FileType.SPC, 256, 256, "spc.xml", new Data(0, new byte[] {0x53, 0x4E, 0x45, 0x53, 0x2D, 0x53, 0x50, 0x43})),
			};
		}

		public static FileType GetFileTypeFromHeader(string header)
		{
			string theader = header.ToLower();
			foreach (Detector d in Detectors)
			{
				if (string.IsNullOrEmpty(d.HeaderId))
				{
					continue;
				}

				if (theader == d.HeaderId)
				{
					return d.FType;
				}
			}
			return FileType.Nothing;
		}

		public static bool AltHeaderFile(FileType fileType)
		{
			return fileType == FileType.A7800
				|| fileType == FileType.FDS
				|| fileType == FileType.Lynx
				|| fileType == FileType.NES
				|| fileType == FileType.PCE
				|| fileType == FileType.PSID
				|| fileType == FileType.SNES
				|| fileType == FileType.SPC;
		}

		public static FileType GetType(System.IO.Stream sIn, out int offset)
		{
			int headSize = 512;
			if (sIn.Length < headSize)
			{
				headSize = (int)sIn.Length;
			}

			byte[] buffer = new byte[headSize];

			sIn.Read(buffer, 0, headSize);

			foreach (Detector detector in Detectors)
			{
				if (headSize < detector.HeaderLength)
				{
					continue;
				}

				bool found = true;
				foreach (Data data in detector.Datas)
				{
					found &= ByteComp(buffer, data);
				}

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
	}
}
