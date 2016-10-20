using System;
using System.IO;
using RomVaultX.SupportedFiles.SevenZip.Common;

namespace RomVaultX.SupportedFiles.SevenZip.Structure
{
	internal class SignatureHeader
	{
		private static readonly byte[] Signature = { (byte)'7', (byte)'z', 0xBC, 0xAF, 0x27, 0x1C };

		private byte _major;
		private byte _minor;

		private uint _startHeaderCRC;

		public ulong NextHeaderOffset;
		public ulong NextHeaderSize;
		public uint NextHeaderCRC;

		public bool Read(BinaryReader br)
		{
			Util.log("Begin: Read Header", 1);
			byte[] signatureBytes = br.ReadBytes(6);
			if (!signatureBytes.Compare(Signature))
			{
				Util.log("End: Read Header signature Failed", -1);
				return false;
			}

			_major = br.ReadByte();
			Util.log("Major = " + _major);
			_minor = br.ReadByte();
			Util.log("Minor = " + _minor);

			_startHeaderCRC = br.ReadUInt32();
			Util.log("HeaderCRC = " + _startHeaderCRC.ToString("X"));

			long pos = br.BaseStream.Position;
			byte[] mainHeader = new byte[8 + 8 + 4];
			br.BaseStream.Read(mainHeader, 0, mainHeader.Length);
			if (!CRC.VerifyDigest(_startHeaderCRC, mainHeader, 0, (uint)mainHeader.Length))
				return false;
			br.BaseStream.Seek(pos, SeekOrigin.Begin);

			NextHeaderOffset = br.ReadUInt64();
			Util.log("NextHeaderOffset = " + NextHeaderOffset);
			NextHeaderSize = br.ReadUInt64();
			Util.log("NextHeaderSize = " + NextHeaderSize);
			NextHeaderCRC = br.ReadUInt32();
			Util.log("NextHeaderCRC = " + NextHeaderCRC.ToString("X"));

			Util.log("End: Read Header", -1);
			return true;
		}


		private long _crcOffset;
		public long BaseOffset { get; private set; }

		public void Write(BinaryWriter bw)
		{
			//SignatureHeader
			//~~~~~~~~~~~~~~~

			bw.Write(Signature);

			//ArchiveVersion
			//{
			bw.Write((byte)0); //  BYTE Major
			bw.Write((byte)3); //  BYTE Minor
							   //};

			_crcOffset = bw.BaseStream.Position;
			bw.Write((uint)0); //HeaderCRC

			//StartHeader
			//{
			bw.Write((ulong)0); //NextHeaderOffset
			bw.Write((ulong)0); //NextHeaderSize
			bw.Write((uint)0); //NextHeaderCRC
							   //}

			BaseOffset = bw.BaseStream.Position;
		}

		public void WriteFinal(BinaryWriter bw, ulong headerpos, ulong headerLength, uint headerCRC)
		{
			long fileEnd = bw.BaseStream.Position;


			byte[] sigHeaderBytes;
			using (MemoryStream sigHeaderMem = new MemoryStream())
			{
				using (BinaryWriter sigHeaderBw = new BinaryWriter(sigHeaderMem))
				{
					sigHeaderBw.Write((ulong)((long)headerpos - BaseOffset)); //NextHeaderOffset
					sigHeaderBw.Write(headerLength); //NextHeaderSize
					sigHeaderBw.Write(headerCRC); //NextHeaderCRC

					sigHeaderBytes = new byte[sigHeaderMem.Length];
					sigHeaderMem.Position = 0;
					sigHeaderMem.Read(sigHeaderBytes, 0, sigHeaderBytes.Length);
				}
			}

			CRC sigHeadercrc = new CRC();
			sigHeadercrc.Update(sigHeaderBytes, 0, (uint)sigHeaderBytes.Length);
			UInt32 sigHeaderCRC = sigHeadercrc.GetDigest();

			bw.BaseStream.Position = _crcOffset;
			bw.Write(sigHeaderCRC); //Header CRC
			bw.Write(sigHeaderBytes);

			bw.BaseStream.Seek(fileEnd, SeekOrigin.Begin);
		}
	}
}
