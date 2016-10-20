using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using RomVaultX.SupportedFiles.SevenZip.Common;
using RomVaultX.SupportedFiles.SevenZip.Compress.LZMA;
using RomVaultX.SupportedFiles.SevenZip.Filters;
using RomVaultX.SupportedFiles.SevenZip.Structure;

namespace RomVaultX.SupportedFiles.SevenZip
{
	public class SevenZipFile
	{
		public class LocalFile
		{
			public string FileName;
			public ulong UncompressedSize;
			public bool isDirectory;
			public byte[] crc;
			public byte[] sha1;
			public byte[] md5;
			public int StreamIndex;
			public ulong StreamOffset;
			public ZipReturn FileStatus = ZipReturn.ZipUntested;
		}

		private List<LocalFile> _localFiles = new List<LocalFile>();

		private FileInfo _zipFileInfo;

		private Stream _zipFs;

		private ZipOpenType _zipOpen;
		private ZipStatus _pZipStatus;

		SignatureHeader _signatureHeader;

		private bool _compressed = true;
		private Stream _tmpOutStream;



		private Stream _streamNow;
		private int _streamIndex = -1;


		public string ZipFilename
		{
			get { return _zipFileInfo != null ? _zipFileInfo.FullName : ""; }
		}
		public long TimeStamp
		{
			get { return _zipFileInfo != null ? _zipFileInfo.LastWriteTime.Ticks : 0; }
		}

		public ZipOpenType ZipOpen { get { return _zipOpen; } }
		public ZipStatus ZipStatus { get { return _pZipStatus; } }
		public int LocalFilesCount() { return _localFiles.Count; }
		public string Filename(int i) { return _localFiles[i].FileName; }
		public ulong? LocalHeader(int i) { return 0; }
		public ulong UncompressedSize(int i) { return _localFiles[i].UncompressedSize; }
		public ZipReturn FileStatus(int i) { return _localFiles[i].FileStatus; }
		public byte[] CRC32(int i) { return _localFiles[i].crc; }
		public byte[] MD5(int i) { return _localFiles[i].md5; }
		public byte[] SHA1(int i) { return _localFiles[i].sha1; }
		public bool IsDirectory(int i) { return _localFiles[i].isDirectory; }
		private long _baseOffset;

		#region open 7z files

		public ZipReturn ZipFileOpen(string filename, long timestamp)
		{
			Debug.WriteLine(filename);
			#region open file stream
			try
			{
				if (!File.Exists(filename))
				{
					ZipFileClose();
					return ZipReturn.ZipErrorFileNotFound;
				}
				_zipFileInfo = new FileInfo(filename);
				if (timestamp != -1 && _zipFileInfo.LastWriteTime.Ticks != timestamp)
				{
					ZipFileClose();
					return ZipReturn.ZipErrorTimeStamp;
				}
				try
				{
					_zipFs = File.OpenRead(filename);
				}
				catch
				{
					ZipFileClose();
					return ZipReturn.ZipErrorOpeningFile;
				}
			}
			catch (PathTooLongException)
			{
				ZipFileClose();
				return ZipReturn.ZipFileNameToLong;
			}
			catch (IOException)
			{
				ZipFileClose();
				return ZipReturn.ZipErrorOpeningFile;
			}
			#endregion

			_zipOpen = ZipOpenType.OpenRead;
			_pZipStatus = ZipStatus.None;

			SignatureHeader signatureHeader = new SignatureHeader();
			if (!signatureHeader.Read(new BinaryReader(_zipFs)))
				return ZipReturn.ZipSignatureError;

			_baseOffset = _zipFs.Position; Util.log("BaseOffset : " + _baseOffset);

			Util.log("Loading Stream : " + (_baseOffset + (long)signatureHeader.NextHeaderOffset) + " , Size : " + signatureHeader.NextHeaderSize);

			//_zipFs.Seek(_baseOffset + (long)signatureHeader.NextHeaderOffset, SeekOrigin.Begin);
			//byte[] mainHeader = new byte[signatureHeader.NextHeaderSize];
			//_zipFs.Read(mainHeader, 0, (int)signatureHeader.NextHeaderSize);
			//if (!CRC.VerifyDigest(signatureHeader.NextHeaderCRC, mainHeader, 0, (uint)signatureHeader.NextHeaderSize))
			//	return ZipReturn.Zip64EndOfCentralDirError;

			_zipFs.Seek(_baseOffset + (long)signatureHeader.NextHeaderOffset, SeekOrigin.Begin);
			ZipReturn zr = Header.ReadHeaderOrPackedHeader(_zipFs, _baseOffset, out _header);
			if (zr != ZipReturn.ZipGood)
				return zr;

			_zipFs.Seek(_baseOffset + (long)(signatureHeader.NextHeaderOffset + signatureHeader.NextHeaderSize), SeekOrigin.Begin);
			_pZipStatus = IsTorrent7z() ? ZipStatus.TrrntZip : ZipStatus.None;
			PopulateLocalFiles(out _localFiles);

			return ZipReturn.ZipGood;
		}

		public ZipReturn ZipFileOpen(Stream zipFileStream)
		{
			ZipFileClose();
			_zipFileInfo = null;
			_zipFs = zipFileStream;

			_zipOpen = ZipOpenType.OpenRead;
			_pZipStatus = ZipStatus.None;

			SignatureHeader signatureHeader = new SignatureHeader();
			if (!signatureHeader.Read(new BinaryReader(_zipFs)))
			{
				return ZipReturn.ZipSignatureError;
			}

			_baseOffset = _zipFs.Position;
			Util.log("BaseOffset : " + _baseOffset);

			Util.log("Loading Stream : " + (_baseOffset + (long)signatureHeader.NextHeaderOffset) + " , Size : " + signatureHeader.NextHeaderSize);

			//_zipFs.Seek(_baseOffset + (long)signatureHeader.NextHeaderOffset, SeekOrigin.Begin);
			//byte[] mainHeader = new byte[signatureHeader.NextHeaderSize];
			//_zipFs.Read(mainHeader, 0, (int)signatureHeader.NextHeaderSize);
			//if (!CRC.VerifyDigest(signatureHeader.NextHeaderCRC, mainHeader, 0, (uint)signatureHeader.NextHeaderSize))
			//	return ZipReturn.Zip64EndOfCentralDirError;

			_zipFs.Seek(_baseOffset + (long)signatureHeader.NextHeaderOffset, SeekOrigin.Begin);
			ZipReturn zr = Header.ReadHeaderOrPackedHeader(_zipFs, _baseOffset, out _header);
			if (zr != ZipReturn.ZipGood)
				return zr;

			_zipFs.Seek(_baseOffset + (long)(signatureHeader.NextHeaderOffset + signatureHeader.NextHeaderSize), SeekOrigin.Begin);
			_pZipStatus = IsTorrent7z() ? ZipStatus.TrrntZip : ZipStatus.None;
			PopulateLocalFiles(out _localFiles);

			return ZipReturn.ZipGood;
		}

		private void PopulateLocalFiles(out List<LocalFile> localFiles)
		{
			int emptyFileIndex = 0;
			int folderIndex = 0;
			int unpackedStreamsIndex = 0;
			ulong streamOffset = 0;
			localFiles = new List<LocalFile>();

			for (int i = 0; i < _header.FileInfo.Names.Length; i++)
			{
				LocalFile lf = new LocalFile { FileName = _header.FileInfo.Names[i] };

				if (_header.FileInfo.EmptyStreamFlags == null || !_header.FileInfo.EmptyStreamFlags[i])
				{
					lf.StreamIndex = folderIndex;
					lf.StreamOffset = streamOffset;
					lf.UncompressedSize = _header.StreamsInfo.Folders[folderIndex].UnpackedStreamInfo[unpackedStreamsIndex].UnpackedSize;
					lf.crc = Util.uinttobytes(_header.StreamsInfo.Folders[folderIndex].UnpackedStreamInfo[unpackedStreamsIndex].Crc);

					streamOffset += lf.UncompressedSize;
					unpackedStreamsIndex++;

					if (unpackedStreamsIndex >= _header.StreamsInfo.Folders[folderIndex].UnpackedStreamInfo.Length)
					{
						folderIndex++;
						unpackedStreamsIndex = 0;
						streamOffset = 0;
					}
				}
				else
				{
					lf.UncompressedSize = 0;
					lf.crc = new byte[] { 0, 0, 0, 0 };
					lf.isDirectory = _header.FileInfo.EmptyFileFlags == null || !_header.FileInfo.EmptyFileFlags[emptyFileIndex++];

					if (lf.isDirectory)
						if (lf.FileName.Substring(lf.FileName.Length - 1, 1) != "/")
							lf.FileName += "/";
				}

				localFiles.Add(lf);
			}
		}

		public void ZipFileClose()
		{
			ZipFileClose(null);
		}

		public void ZipFileClose(ICodeProgress p)
		{
			if (_zipOpen == ZipOpenType.Closed)
				return;

			if (_zipOpen == ZipOpenType.OpenRead)
			{
				ZipFileCloseReadStream();
				if (_zipFs != null)
				{
					_zipFs.Close();
					_zipFs.Dispose();
				}
				_zipOpen = ZipOpenType.Closed;
				return;
			}

			CloseWriting7Zip(p);

			_zipFileInfo = new FileInfo(_zipFileInfo.FullName);
			_zipOpen = ZipOpenType.Closed;
		}

		private Header _header;

		private bool IsTorrent7z()
		{
			const int crcsz = 128;
			const int t7ZsigSize = 16 + 1 + 9 + 4 + 4;
			byte[] kSignature = { (byte)'7', (byte)'z', 0xBC, 0xAF, 0x27, 0x1C };
			int kSignatureSize = kSignature.Length;
			const string sig = "\xa9\x9f\xd1\x57\x08\xa9\xd7\xea\x29\x64\xb2\x36\x1b\x83\x52\x33\x01torrent7z_0.9beta";
			byte[] t7Zid = Util.Enc.GetBytes(sig);
			int t7ZidSize = t7Zid.Length;

			const int tmpbufsize = 256 + t7ZsigSize + 8 + 4;
			byte[] buffer = new byte[tmpbufsize];

			// read fist 128 bytes, pad with zeros if less bytes
			int offs = 0;
			_zipFs.Seek(0, SeekOrigin.Begin);
			int ar = _zipFs.Read(buffer, offs, crcsz);
			if (ar < crcsz)
			{
				Util.memset(buffer, offs + ar, 0, crcsz - ar);
			}

			offs = crcsz;
			long foffs = _zipFs.Length;
			foffs = foffs < (crcsz + t7ZsigSize + 4) ? 0 : foffs - (crcsz + t7ZsigSize + 4);
			_zipFs.Seek(foffs, SeekOrigin.Begin);

			ar = _zipFs.Read(buffer, offs, (crcsz + t7ZsigSize + 4));
			if (ar < (crcsz + t7ZsigSize + 4))
			{
				if (ar >= t7ZsigSize + 4)
				{
					ar -= t7ZsigSize + 4;
				}
				if (ar < kSignatureSize)
				{
					ar = kSignatureSize;
				}
				Util.memset(buffer, offs + ar, 0, crcsz - ar);
				Util.memcpyr(buffer, crcsz * 2 + 8, buffer, offs + ar, t7ZsigSize + 4);
			}
			else
			{
				Util.memcpyr(buffer, crcsz * 2 + 8, buffer, crcsz * 2, t7ZsigSize + 4);
			}

			foffs = _zipFs.Length;
			foffs -= t7ZsigSize + 4;

			//memcpy(buffer, crcsz * 2, &foffs, 8);
			buffer[crcsz * 2 + 0] = (byte)((foffs >> 0) & 0xff);
			buffer[crcsz * 2 + 1] = (byte)((foffs >> 8) & 0xff);
			buffer[crcsz * 2 + 2] = (byte)((foffs >> 16) & 0xff);
			buffer[crcsz * 2 + 3] = (byte)((foffs >> 24) & 0xff);
			buffer[crcsz * 2 + 4] = 0;
			buffer[crcsz * 2 + 5] = 0;
			buffer[crcsz * 2 + 6] = 0;
			buffer[crcsz * 2 + 7] = 0;

			if (Util.memcmp(buffer, 0, kSignature, kSignatureSize))
			{
				t7Zid[16] = buffer[crcsz * 2 + 4 + 8 + 16];
				if (Util.memcmp(buffer, crcsz * 2 + 4 + 8, t7Zid, t7ZidSize))
				{
					UInt32 inCrc32 = (UInt32)((buffer[crcsz * 2 + 8 + 0]) +
											 (buffer[crcsz * 2 + 8 + 1] << 8) +
											 (buffer[crcsz * 2 + 8 + 2] << 16) +
											 (buffer[crcsz * 2 + 8 + 3] << 24));

					buffer[crcsz * 2 + 8 + 0] = 0xff;
					buffer[crcsz * 2 + 8 + 1] = 0xff;
					buffer[crcsz * 2 + 8 + 2] = 0xff;
					buffer[crcsz * 2 + 8 + 3] = 0xff;

					uint calcCrc32 = CRC.CalculateDigest(buffer, 0, crcsz * 2 + 8 + t7ZsigSize + 4);

					if (inCrc32 == calcCrc32) return true;
				}
			}

			return false;
		}

		#endregion

		public void DeepScan()
		{
			const int bufferSize = 4096 * 128;
			byte[] buffer = new byte[bufferSize];

			for (int index = 0; index < _localFiles.Count; index++)
			{
				if (_localFiles[index].isDirectory || _localFiles[index].UncompressedSize == 0)
				{
					_localFiles[index].md5 = new byte[] { 0xd4, 0x1d, 0x8c, 0xd9, 0x8f, 0x00, 0xb2, 0x04, 0xe9, 0x80, 0x09, 0x98, 0xec, 0xf8, 0x42, 0x7e };
					_localFiles[index].sha1 = new byte[] { 0xda, 0x39, 0xa3, 0xee, 0x5e, 0x6b, 0x4b, 0x0d, 0x32, 0x55, 0xbf, 0xef, 0x95, 0x60, 0x18, 0x90, 0xaf, 0xd8, 0x07, 0x09 };
					_localFiles[index].FileStatus = ZipReturn.ZipGood;

					continue;
				}

				ulong sizetogo;
				Stream inStream;
				ZipReturn zr = ZipFileOpenReadStream(index, out inStream, out sizetogo);
				if (zr != ZipReturn.ZipGood)
				{
					continue;
				}

				if (inStream == null)
				{
					continue;
				}

				CRC crc32 = new CRC();
				MD5 lmd5 = System.Security.Cryptography.MD5.Create();
				SHA1 lsha1 = System.Security.Cryptography.SHA1.Create();

				while (sizetogo > 0)
				{
					int sizenow = sizetogo > (ulong)bufferSize ? bufferSize : (int)sizetogo;
					inStream.Read(buffer, 0, sizenow);

					crc32.Update(buffer, 0, (uint)sizenow);
					lmd5.TransformBlock(buffer, 0, sizenow, null, 0);
					lsha1.TransformBlock(buffer, 0, sizenow, null, 0);

					sizetogo = sizetogo - (ulong)sizenow;
				}

				lmd5.TransformFinalBlock(buffer, 0, 0);
				lsha1.TransformFinalBlock(buffer, 0, 0);

				byte[] testcrc = Util.uinttobytes(crc32.GetDigest());
				_localFiles[index].md5 = lmd5.Hash;
				_localFiles[index].sha1 = lsha1.Hash;

				_localFiles[index].FileStatus = Util.ByteArrCompare(_localFiles[index].crc, testcrc) ? ZipReturn.ZipGood : ZipReturn.ZipCRCDecodeError;
			}
		}

		#region read 7z file

		public ZipReturn ZipFileOpenReadStream(int index, out Stream stream, out ulong unCompressedSize)
		{
			Debug.WriteLine("Opening File " + _localFiles[index].FileName);
			stream = null;
			unCompressedSize = 0;

			if (_zipOpen != ZipOpenType.OpenRead)
			{
				return ZipReturn.ZipErrorGettingDataStream;
			}

			if (IsDirectory(index))
			{
				return ZipReturn.ZipTryingToAccessADirectory;
			}

			unCompressedSize = _localFiles[index].UncompressedSize;

			int thisStreamIndex = _localFiles[index].StreamIndex;

			// first see if we can re-use the current open stream
			if (_streamIndex == thisStreamIndex)
			{
				stream = _streamNow;
				if (_streamNow is BCJFilter) // it is a BCJ + Decoder stream but need to check the position in the stream can be used.
				{
					if ((long)_localFiles[index].StreamOffset >= _streamNow.Position)
					{
						stream.Seek((long)_localFiles[index].StreamOffset - _streamNow.Position, SeekOrigin.Current);
						return ZipReturn.ZipGood;
					}
				}
				else if (_streamNow is Decoder) // it is a Decoder stream but need to check the position in the stream can be used.
				{
					if ((long)_localFiles[index].StreamOffset >= _streamNow.Position)
					{
						stream.Seek((long)_localFiles[index].StreamOffset - _streamNow.Position, SeekOrigin.Current);
						return ZipReturn.ZipGood;
					}
				}
				else // it is an uncompressed stream
				{
					if (stream != null)
						stream.Seek((long)_localFiles[index].StreamOffset - _streamNow.Position, SeekOrigin.Current);
					return ZipReturn.ZipGood;
				}
			}

			// need to open a new stream

			// first close the old streams
			ZipFileCloseReadStream();

			// open new stream
			_streamIndex = thisStreamIndex;
			_zipFs.Seek(_baseOffset + (long)_header.StreamsInfo.PackedStreams[thisStreamIndex].StreamPosition, SeekOrigin.Begin);

			byte[] method = _header.StreamsInfo.Folders[_localFiles[index].StreamIndex].Coders[0].Method;
			if (method.Length == 3 && method[0] == 3 && method[1] == 1 && method[2] == 1)  // LZMA
			{
				Decoder decoder = new Decoder();
				decoder.SetDecoderProperties(_header.StreamsInfo.Folders[_localFiles[index].StreamIndex].Coders[0].Properties);
				decoder.SetUpStream(_zipFs);
				stream = decoder;
				_streamNow = stream;

				if (_header.StreamsInfo.Folders[_localFiles[index].StreamIndex].Coders.Length > 1)  // BCJ
				{
					method = _header.StreamsInfo.Folders[_localFiles[index].StreamIndex].Coders[1].Method;
					if (method.Length == 4 && method[0] == 3 && method[1] == 3 && method[2] == 1 && method[3] == 3)
					{
						BCJFilter filter = new BCJFilter(false, stream);
						stream = filter;
						_streamNow = stream;
						stream.Seek((long)_localFiles[index].StreamOffset, SeekOrigin.Current);
						return ZipReturn.ZipGood;
					}
					return ZipReturn.ZipUnsupportedCompression;
				}

				stream.Seek((long)_localFiles[index].StreamOffset, SeekOrigin.Current);
				return ZipReturn.ZipGood;
			}
			if (method.Length == 1 && method[0] == 33) // lzma2
			{
				return ZipReturn.ZipUnsupportedCompression;
			}

			if (method.Length == 1 && method[0] == 0) // uncompressed
			{
				stream = _zipFs;
				_streamNow = stream;
				stream.Seek((long)_localFiles[index].StreamOffset, SeekOrigin.Current);
				return ZipReturn.ZipGood;
			}

			if (method.Length == 3 && method[0] == 4 && method[1] == 2 && method[2] == 2) // BZip2
			{
				return ZipReturn.ZipUnsupportedCompression;
			}
			if (method.Length == 1 && method[0] == 33)  // LZMA2
			{
				return ZipReturn.ZipUnsupportedCompression;
			}

			return ZipReturn.ZipUnsupportedCompression;
		}

		public ZipReturn ZipFileCloseReadStream()
		{
			if (_streamNow is BCJFilter)
			{
				Stream baseStream = ((BCJFilter)_streamNow).BaseStream;
				_streamNow.Dispose();
				_streamNow = baseStream;
			}
			if (_streamNow is Decoder)
			{
				_streamNow.Close();
				_streamNow.Dispose();
			}
			_streamNow = null;
			return ZipReturn.ZipGood;
		}

		#endregion


		#region write 7z File

		public void ZipFileAddDirectory()
		{
			// do nothing here for 7zip
		}

		public ZipReturn ZipFileCreate(string newFilename)
		{
			return ZipFileCreate(newFilename, true);
		}


		public ZipReturn ZipFileCreate(string newFilename, bool compressOutput)
		{
			if (_zipOpen != ZipOpenType.Closed)
				return ZipReturn.ZipFileAlreadyOpen;

			DirUtil.CreateDirForFile(newFilename);
			_zipFileInfo = new FileInfo(newFilename);

			try
			{
				_zipFs = File.OpenWrite(newFilename);
			}
			catch
			{
				ZipFileClose();
				return ZipReturn.ZipErrorOpeningFile;
			}
			_zipOpen = ZipOpenType.OpenWrite;

			_signatureHeader = new SignatureHeader();
			_header = new Header();

			BinaryWriter bw = new BinaryWriter(_zipFs);
			_signatureHeader.Write(bw);

			_compressed = compressOutput;
			_tmpOutStream = compressOutput ? new FileStream(_zipFileInfo.FullName + ".tmp", FileMode.Create, FileAccess.Write) : null;

			return ZipReturn.ZipGood;
		}

		public ZipReturn ZipFileOpenWriteStream(bool raw, bool trrntzip, string filename, ulong uncompressedSize, ushort compressionMethod, out Stream stream)
		{
			return ZipFileOpenWriteStream(filename, uncompressedSize, out stream);
		}
		public ZipReturn ZipFileOpenWriteStream(string filename, ulong uncompressedSize, out Stream stream)
		{
			LocalFile lf = new LocalFile
			{
				FileName = filename,
				UncompressedSize = uncompressedSize,
				StreamOffset = (ulong)(_zipFs.Position - _signatureHeader.BaseOffset)
			};

			_localFiles.Add(lf);
			stream = _tmpOutStream ?? _zipFs;
			return ZipReturn.ZipGood;
		}


		public ZipReturn ZipFileCloseWriteStream(byte[] crc32)
		{
			_localFiles[_localFiles.Count - 1].crc = new[] { crc32[3], crc32[2], crc32[1], crc32[0] };
			return ZipReturn.ZipGood;
		}

		private void Create7ZStructure()
		{
			int fileCount = _localFiles.Count;

			//FileInfo
			_header.FileInfo = new Structure.SevenZipFileInfo
			{
				Names = new string[fileCount]
			};

			ulong emptyStreamCount = 0;
			ulong emptyFileCount = 0;
			for (int i = 0; i < fileCount; i++)
			{
				_header.FileInfo.Names[i] = _localFiles[i].FileName;

				if (_localFiles[i].UncompressedSize != 0)
					continue;

				if (!_localFiles[i].isDirectory)
					emptyFileCount += 1;

				emptyStreamCount += 1;
			}
			ulong outFileCount = (ulong)_localFiles.Count - emptyStreamCount;

			_header.FileInfo.EmptyStreamFlags = null;
			_header.FileInfo.EmptyFileFlags = null;
			_header.FileInfo.Attributes = null;

			if (emptyStreamCount > 0)
			{
				if (emptyStreamCount != emptyFileCount) //then we found directories and need to set the attributes
					_header.FileInfo.Attributes = new uint[fileCount];

				if (emptyFileCount > 0)
					_header.FileInfo.EmptyFileFlags = new bool[emptyStreamCount];

				emptyStreamCount = 0;
				_header.FileInfo.EmptyStreamFlags = new bool[fileCount];
				for (int i = 0; i < fileCount; i++)
				{
					if (_localFiles[i].UncompressedSize != 0)
						continue;

					if (_localFiles[i].isDirectory)
						_header.FileInfo.Attributes[i] = 0x10;					  // set attributes to directory
					else
						_header.FileInfo.EmptyFileFlags[emptyStreamCount] = true;   // set empty file flag

					_header.FileInfo.EmptyStreamFlags[i] = true;
					emptyStreamCount += 1;
				}
			}

			//StreamsInfo
			_header.StreamsInfo = new StreamsInfo { PackPosition = 0 };

			//StreamsInfo.PackedStreamsInfo
			if (_compressed)
			{
				_header.StreamsInfo.PackedStreams = new PackedStreamInfo[1];
				_header.StreamsInfo.PackedStreams[0] = new PackedStreamInfo { PackedSize = _packStreamSize };
			}
			else
			{
				_header.StreamsInfo.PackedStreams = new PackedStreamInfo[outFileCount];
				int fileIndex = 0;
				for (int i = 0; i < fileCount; i++)
				{
					if (_localFiles[i].UncompressedSize == 0)
					{
						continue;
					}
					_header.StreamsInfo.PackedStreams[fileIndex++] = new PackedStreamInfo { PackedSize = _localFiles[i].UncompressedSize };
				}
			}
			//StreamsInfo.PackedStreamsInfo, no CRC or StreamPosition required

			if (_compressed)
			{
				//StreamsInfo.Folders
				_header.StreamsInfo.Folders = new Folder[1];

				Folder folder = new Folder { Coders = new Coder[1] };

				//StreamsInfo.Folders.Coder
				// flags 0x23
				folder.Coders[0] = new Coder
				{
					Method = new byte[] { 3, 1, 1 },
					NumInStreams = 1,
					NumOutStreams = 1,
					Properties = _codeMSbytes
				};
				folder.BindPairs = null;
				folder.PackedStreamIndices = new[] { (ulong)0 };
				folder.UnpackedStreamSizes = new[] { _unpackedStreamSize };
				folder.UnpackCRC = null;

				folder.UnpackedStreamInfo = new UnpackedStreamInfo[outFileCount];
				int fileIndex = 0;
				for (int i = 0; i < fileCount; i++)
				{
					if (_localFiles[i].UncompressedSize == 0)
					{
						continue;
					}
					UnpackedStreamInfo unpackedStreamInfo = new UnpackedStreamInfo
					{
						UnpackedSize = _localFiles[i].UncompressedSize,
						Crc = Util.bytestouint(_localFiles[i].crc)
					};
					folder.UnpackedStreamInfo[fileIndex++] = unpackedStreamInfo;
				}
				_header.StreamsInfo.Folders[0] = folder;
			}
			else
			{
				_header.StreamsInfo.Folders = new Folder[outFileCount];
				int fileIndex = 0;
				for (int i = 0; i < fileCount; i++)
				{
					if (_localFiles[i].UncompressedSize == 0)
					{
						continue;
					}
					Folder folder = new Folder { Coders = new Coder[1] };

					//StreamsInfo.Folders.Coder
					// flags 0x01
					folder.Coders[0] = new Coder
					{
						Method = new byte[] { 0 },
						NumInStreams = 1,
						NumOutStreams = 1,
						Properties = null
					};

					folder.BindPairs = null;
					folder.PackedStreamIndices = new[] { (ulong)i };
					folder.UnpackedStreamSizes = new[] { _localFiles[i].UncompressedSize };
					folder.UnpackCRC = null;

					folder.UnpackedStreamInfo = new UnpackedStreamInfo[1];
					UnpackedStreamInfo unpackedStreamInfo = new UnpackedStreamInfo
					{
						UnpackedSize = _localFiles[i].UncompressedSize,
						Crc = Util.bytestouint(_localFiles[i].crc)
					};
					folder.UnpackedStreamInfo[0] = unpackedStreamInfo;

					_header.StreamsInfo.Folders[fileIndex++] = folder;
				}
			}
		}

		ulong _packStreamSize;
		ulong _unpackedStreamSize;
		byte[] _codeMSbytes;

		private void CloseWriting7Zip(ICodeProgress p = null)
		{
			if (_compressed)
			{
				_unpackedStreamSize = (ulong)_tmpOutStream.Length;
				_tmpOutStream.Close();
				_tmpOutStream.Dispose();

				UInt64 packStreamStart = (UInt64)_zipFs.Position;
				using (Stream inStream = new FileStream(_zipFileInfo.FullName + ".tmp", FileMode.Open, FileAccess.Read))
				{
					LZMACompressFile.CompressFile(inStream, _zipFs, out _codeMSbytes, p);
				}
				_packStreamSize = (UInt64)_zipFs.Position - packStreamStart;

				File.Delete(_zipFileInfo.FullName + ".tmp");
			}

			Create7ZStructure();

			byte[] newHeaderByte;
			using (Stream headerMem = new MemoryStream())
			{
				using (BinaryWriter headerBw = new BinaryWriter(headerMem))
				{
					_header.WriteHeader(headerBw);
					newHeaderByte = new byte[headerMem.Length];
					headerMem.Position = 0;
					headerMem.Read(newHeaderByte, 0, newHeaderByte.Length);
				}
			}

			CRC mainHeadercrc = new CRC();
			mainHeadercrc.Update(newHeaderByte, 0, (uint)newHeaderByte.Length);
			UInt32 mainHeaderCRC = mainHeadercrc.GetDigest();

			UInt64 headerpos = (UInt64)_zipFs.Position;
			BinaryWriter bw = new BinaryWriter(_zipFs);
			bw.Write(newHeaderByte);

			_signatureHeader.WriteFinal(bw, headerpos, (ulong)newHeaderByte.Length, mainHeaderCRC);

			_zipFs.Flush();
			_zipFs.Close();
			_zipFs.Dispose();
		}

		#endregion

		public void ZipFileAddDirectory(string filename)
		{
			LocalFile lf = new LocalFile
			{
				FileName = filename,
				UncompressedSize = 0,
				isDirectory = true,
				StreamOffset = 0
			};
			_localFiles.Add(lf);
		}

		public ZipReturn ZipFileRollBack()
		{
			throw new NotImplementedException();
		}

		public void ZipFileCloseFailed()
		{
			throw new NotImplementedException();
		}
	}
}
