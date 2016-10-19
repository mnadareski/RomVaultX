/******************************************************
 *     ROMVault2 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2014                                 *
 ******************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using RomVaultX.SupportedFiles.Zip.ZLib;

namespace RomVaultX.SupportedFiles.Zip
{
	public class ZipFile
	{
		const int Buffersize = 4096 * 128;
		private static byte[] _buffer;

		private const uint LocalFileHeaderSignature = 0x04034b50;
		private const uint CentralDirectoryHeaderSigniature = 0x02014b50;
		private const uint EndOfCentralDirSignature = 0x06054b50;
		private const uint Zip64EndOfCentralDirSignatue = 0x06064b50;
		private const uint Zip64EndOfCentralDirectoryLocator = 0x07064b50;

		class LocalFile
		{
			private readonly Stream _zipFs;
			public string FileName { get; private set; }
			public ushort _generalPurposeBitFlag { get; private set; }
			private ushort _compressionMethod;
			private ushort _lastModFileTime;
			private ushort _lastModFileDate;
			public byte[] CRC { get; private set; }
			private ulong _compressedSize;
			public ulong UncompressedSize { get; private set; }
			public ulong RelativeOffsetOfLocalHeader; // only in centeral directory

			private ulong _crc32Location;
			private ulong _extraLocation;
			private ulong _dataLocation;

			public bool Zip64 { get; private set; }
			public bool TrrntZip { get; private set; }

			public byte[] sha1 { get; private set; }
			public byte[] md5 { get; private set; }

			public ZipReturn FileStatus = ZipReturn.ZipUntested;
			public LocalFile(Stream zipFs)
			{
				_zipFs = zipFs;
			}
			public LocalFile(Stream zipFs, string filename)
			{
				Zip64 = false;
				_zipFs = zipFs;
				_generalPurposeBitFlag = 2;  // Maximum Compression Deflating
				_compressionMethod = 8;      // Compression Method Deflate
				_lastModFileTime = 48128;
				_lastModFileDate = 8600;

				FileName = filename;
			}

			public ZipReturn CenteralDirectoryRead()
			{
				try
				{
					BinaryReader br = new BinaryReader(_zipFs);

					uint thisSignature = br.ReadUInt32();
					if (thisSignature != CentralDirectoryHeaderSigniature)
						return ZipReturn.ZipCenteralDirError;

					br.ReadUInt16(); // Version Made By

					br.ReadUInt16(); // Version Needed To Extract

					_generalPurposeBitFlag = br.ReadUInt16();
					_compressionMethod = br.ReadUInt16();
					if (_compressionMethod != 8 && _compressionMethod != 0)
						return ZipReturn.ZipUnsupportedCompression;

					_lastModFileTime = br.ReadUInt16();
					_lastModFileDate = br.ReadUInt16();
					CRC = ReadCRC(br);

					_compressedSize = br.ReadUInt32();
					UncompressedSize = br.ReadUInt32();

					ushort fileNameLength = br.ReadUInt16();
					ushort extraFieldLength = br.ReadUInt16();
					ushort fileCommentLength = br.ReadUInt16();

					br.ReadUInt16(); // diskNumberStart
					br.ReadUInt16(); // internalFileAttributes
					br.ReadUInt32(); // externalFileAttributes

					RelativeOffsetOfLocalHeader = br.ReadUInt32();

					byte[] bFileName = br.ReadBytes(fileNameLength);
					FileName = (_generalPurposeBitFlag & (1 << 11)) == 0 ?
						GetString(bFileName) :
						Encoding.UTF8.GetString(bFileName, 0, fileNameLength);

					Byte[] extraField = br.ReadBytes(extraFieldLength);
					br.ReadBytes(fileCommentLength); // File Comments

					int pos = 0;
					while (extraFieldLength > pos)
					{
						ushort type = BitConverter.ToUInt16(extraField, pos);
						pos += 2;
						ushort blockLength = BitConverter.ToUInt16(extraField, pos);
						pos += 2;
						switch (type)
						{
							case 0x0001:
								Zip64 = true;
								if (UncompressedSize == 0xffffffff)
								{
									UncompressedSize = BitConverter.ToUInt64(extraField, pos);
									pos += 8;
								}
								if (_compressedSize == 0xffffffff)
								{
									_compressedSize = BitConverter.ToUInt64(extraField, pos);
									pos += 8;
								}
								if (RelativeOffsetOfLocalHeader == 0xffffffff)
								{
									RelativeOffsetOfLocalHeader = BitConverter.ToUInt64(extraField, pos);
									pos += 8;
								}
								break;
							case 0x7075:
								//byte version = extraField[pos];
								pos += 1;
								uint nameCRC32 = BitConverter.ToUInt32(extraField, pos);
								pos += 4;

								CRC32 crcTest = new CRC32();
								crcTest.SlurpBlock(bFileName, 0, fileNameLength);
								uint fCRC = crcTest.Crc32ResultU;

								if (nameCRC32 != fCRC) return ZipReturn.ZipCenteralDirError;

								int charLen = blockLength - 5;

								FileName = Encoding.UTF8.GetString(extraField, pos, charLen);
								pos += charLen;

								break;
							default:
								pos += blockLength;
								break;
						}
					}

					return ZipReturn.ZipGood;
				}
				catch
				{
					return ZipReturn.ZipCenteralDirError;
				}

			}
			public void CenteralDirectoryWrite(Stream crcStream)
			{
				BinaryWriter bw = new BinaryWriter(crcStream);

				const uint header = 0x2014B50;

				List<Byte> extraField = new List<byte>();

				uint cdUncompressedSize;
				if (UncompressedSize >= 0xffffffff)
				{
					Zip64 = true;
					cdUncompressedSize = 0xffffffff;
					extraField.AddRange(BitConverter.GetBytes(UncompressedSize));
				}
				else
					cdUncompressedSize = (uint)UncompressedSize;

				uint cdCompressedSize;
				if (_compressedSize >= 0xffffffff)
				{
					Zip64 = true;
					cdCompressedSize = 0xffffffff;
					extraField.AddRange(BitConverter.GetBytes(_compressedSize));
				}
				else
					cdCompressedSize = (uint)_compressedSize;

				uint cdRelativeOffsetOfLocalHeader;
				if (RelativeOffsetOfLocalHeader >= 0xffffffff)
				{
					Zip64 = true;
					cdRelativeOffsetOfLocalHeader = 0xffffffff;
					extraField.AddRange(BitConverter.GetBytes(RelativeOffsetOfLocalHeader));
				}
				else
					cdRelativeOffsetOfLocalHeader = (uint)RelativeOffsetOfLocalHeader;

				if (extraField.Count > 0)
				{
					ushort exfl = (ushort)extraField.Count;
					extraField.InsertRange(0, BitConverter.GetBytes((ushort)0x0001));
					extraField.InsertRange(2, BitConverter.GetBytes(exfl));
				}
				ushort extraFieldLength = (ushort)extraField.Count;

				byte[] bFileName;
				if (IsUnicode(FileName))
				{
					_generalPurposeBitFlag |= 1 << 11;
					bFileName = Encoding.UTF8.GetBytes(FileName);
				}
				else
					bFileName = GetBytes(FileName);
				ushort fileNameLength = (ushort)bFileName.Length;

				ushort versionNeededToExtract = (ushort)(Zip64 ? 45 : 20);

				bw.Write(header);
				bw.Write((ushort)0);
				bw.Write(versionNeededToExtract);
				bw.Write(_generalPurposeBitFlag);
				bw.Write(_compressionMethod);
				bw.Write(_lastModFileTime);
				bw.Write(_lastModFileDate);
				bw.Write(CRC[3]);
				bw.Write(CRC[2]);
				bw.Write(CRC[1]);
				bw.Write(CRC[0]);
				bw.Write(cdCompressedSize);
				bw.Write(cdUncompressedSize);
				bw.Write(fileNameLength);
				bw.Write(extraFieldLength);
				bw.Write((ushort)0); // file comment length
				bw.Write((ushort)0); // disk number start
				bw.Write((ushort)0); // internal file attributes
				bw.Write((uint)0);   // external file attributes
				bw.Write(cdRelativeOffsetOfLocalHeader);

				bw.Write(bFileName, 0, fileNameLength);
				bw.Write(extraField.ToArray(), 0, extraFieldLength);
				// No File Comment
			}

			public ZipReturn LocalFileHeaderRead()
			{
				try
				{
					TrrntZip = true;

					BinaryReader br = new BinaryReader(_zipFs);

					_zipFs.Position = (long)RelativeOffsetOfLocalHeader;
					uint thisSignature = br.ReadUInt32();
					if (thisSignature != LocalFileHeaderSignature)
						return ZipReturn.ZipLocalFileHeaderError;

					br.ReadUInt16();  // version needed to extract
					ushort generalPurposeBitFlagLocal = br.ReadUInt16();
					if (generalPurposeBitFlagLocal != _generalPurposeBitFlag) TrrntZip = false;

					ushort tshort = br.ReadUInt16();
					if (tshort != _compressionMethod) return ZipReturn.ZipLocalFileHeaderError;

					tshort = br.ReadUInt16();
					if (tshort != _lastModFileTime) return ZipReturn.ZipLocalFileHeaderError;

					tshort = br.ReadUInt16();
					if (tshort != _lastModFileDate) return ZipReturn.ZipLocalFileHeaderError;

					byte[] tCRC = ReadCRC(br);
					if (((_generalPurposeBitFlag & 8) == 0) && !ByteArrCompare(tCRC, CRC)) return ZipReturn.ZipLocalFileHeaderError;

					uint tCompressedSize = br.ReadUInt32();
					if (Zip64 && tCompressedSize != 0xffffffff && tCompressedSize != _compressedSize)   // if Zip64 File then the compressedSize should be 0xffffffff
						return ZipReturn.ZipLocalFileHeaderError;
					if ((_generalPurposeBitFlag & 8) == 8 && tCompressedSize != 0)   // if bit 4 set then no compressedSize is set yet
						return ZipReturn.ZipLocalFileHeaderError;
					if (!Zip64 && (_generalPurposeBitFlag & 8) != 8 && tCompressedSize != _compressedSize) // check the compressedSize
						return ZipReturn.ZipLocalFileHeaderError;

					uint tUnCompressedSize = br.ReadUInt32();
					if (Zip64 && tUnCompressedSize != 0xffffffff && tUnCompressedSize != UncompressedSize)   // if Zip64 File then the unCompressedSize should be 0xffffffff
						return ZipReturn.ZipLocalFileHeaderError;
					if ((_generalPurposeBitFlag & 8) == 8 && tUnCompressedSize != 0)   // if bit 4 set then no unCompressedSize is set yet
						return ZipReturn.ZipLocalFileHeaderError;
					if (!Zip64 && (_generalPurposeBitFlag & 8) != 8 && tUnCompressedSize != UncompressedSize) // check the unCompressedSize
						return ZipReturn.ZipLocalFileHeaderError;

					ushort fileNameLength = br.ReadUInt16();
					ushort extraFieldLength = br.ReadUInt16();

					byte[] bFileName = br.ReadBytes(fileNameLength);
					string tFileName = (generalPurposeBitFlagLocal & (1 << 11)) == 0 ?
						GetString(bFileName) :
						Encoding.UTF8.GetString(bFileName, 0, fileNameLength);

					byte[] extraField = br.ReadBytes(extraFieldLength);

					Zip64 = false;
					int pos = 0;
					while (extraFieldLength > pos)
					{
						ushort type = BitConverter.ToUInt16(extraField, pos);
						pos += 2;
						ushort blockLength = BitConverter.ToUInt16(extraField, pos);
						pos += 2;
						switch (type)
						{
							case 0x0001:
								Zip64 = true;
								if (tUnCompressedSize == 0xffffffff)
								{
									ulong tLong = BitConverter.ToUInt64(extraField, pos);
									if (tLong != UncompressedSize) return ZipReturn.ZipLocalFileHeaderError;
									pos += 8;
								}
								if (tCompressedSize == 0xffffffff)
								{
									ulong tLong = BitConverter.ToUInt64(extraField, pos);
									if (tLong != _compressedSize) return ZipReturn.ZipLocalFileHeaderError;
									pos += 8;
								}
								break;
							case 0x7075:
								//byte version = extraField[pos];
								pos += 1;
								uint nameCRC32 = BitConverter.ToUInt32(extraField, pos);
								pos += 4;

								CRC32 crcTest = new CRC32();
								crcTest.SlurpBlock(bFileName, 0, fileNameLength);
								uint fCRC = crcTest.Crc32ResultU;

								if (nameCRC32 != fCRC) return ZipReturn.ZipLocalFileHeaderError;

								int charLen = blockLength - 5;

								tFileName = Encoding.UTF8.GetString(extraField, pos, charLen);
								pos += charLen;

								break;
							default:
								pos += blockLength;
								break;
						}
					}

					if (!CompareString(FileName, tFileName)) return ZipReturn.ZipLocalFileHeaderError;

					_dataLocation = (ulong)_zipFs.Position;

					if ((_generalPurposeBitFlag & 8) == 0) return ZipReturn.ZipGood;

					_zipFs.Position += (long)_compressedSize;

					tCRC = ReadCRC(br);
					if (!ByteArrCompare(tCRC, new byte[] { 0x50, 0x4b, 0x07, 0x08 }))
						tCRC = ReadCRC(br);

					if (!ByteArrCompare(tCRC, CRC)) return ZipReturn.ZipLocalFileHeaderError;

					uint tint = br.ReadUInt32();
					if (tint != _compressedSize) return ZipReturn.ZipLocalFileHeaderError;

					tint = br.ReadUInt32();
					if (tint != UncompressedSize) return ZipReturn.ZipLocalFileHeaderError;

					return ZipReturn.ZipGood;
				}
				catch
				{
					return ZipReturn.ZipLocalFileHeaderError;
				}

			}
			public ZipReturn LocalFileHeaderReadQuick()
			{
				try
				{
					TrrntZip = true;

					BinaryReader br = new BinaryReader(_zipFs);

					_zipFs.Position = (long)RelativeOffsetOfLocalHeader;
					uint thisSignature = br.ReadUInt32();
					if (thisSignature != LocalFileHeaderSignature)
						return ZipReturn.ZipLocalFileHeaderError;

					br.ReadUInt16();  // version needed to extract
					_generalPurposeBitFlag = br.ReadUInt16();
					if ((_generalPurposeBitFlag & 8) == 8)
						return ZipReturn.ZipCannotFastOpen;

					_compressionMethod = br.ReadUInt16();
					_lastModFileTime = br.ReadUInt16();
					_lastModFileDate = br.ReadUInt16();
					CRC = ReadCRC(br);
					_compressedSize = br.ReadUInt32();
					UncompressedSize = br.ReadUInt32();

					ushort fileNameLength = br.ReadUInt16();
					ushort extraFieldLength = br.ReadUInt16();

					byte[] bFileName = br.ReadBytes(fileNameLength);

					FileName = (_generalPurposeBitFlag & (1 << 11)) == 0 ?
						GetString(bFileName) :
						Encoding.UTF8.GetString(bFileName, 0, fileNameLength);

					byte[] extraField = br.ReadBytes(extraFieldLength);

					Zip64 = false;
					int pos = 0;
					while (extraFieldLength > pos)
					{
						ushort type = BitConverter.ToUInt16(extraField, pos);
						pos += 2;
						ushort blockLength = BitConverter.ToUInt16(extraField, pos);
						pos += 2;
						switch (type)
						{
							case 0x0001:
								Zip64 = true;
								if (UncompressedSize == 0xffffffff)
								{
									UncompressedSize = BitConverter.ToUInt64(extraField, pos);
									pos += 8;
								}
								if (_compressedSize == 0xffffffff)
								{
									_compressedSize = BitConverter.ToUInt64(extraField, pos);
									pos += 8;
								}
								break;
							case 0x7075:
								pos += 1;
								uint nameCRC32 = BitConverter.ToUInt32(extraField, pos);
								pos += 4;

								CRC32 crcTest = new CRC32();
								crcTest.SlurpBlock(bFileName, 0, fileNameLength);
								uint fCRC = crcTest.Crc32ResultU;

								if (nameCRC32 != fCRC) return ZipReturn.ZipLocalFileHeaderError;

								int charLen = blockLength - 5;

								FileName = Encoding.UTF8.GetString(extraField, pos, charLen);

								pos += charLen;

								break;
							default:
								pos += blockLength;
								break;
						}
					}

					_dataLocation = (ulong)_zipFs.Position;
					return ZipReturn.ZipGood;

				}
				catch
				{
					return ZipReturn.ZipLocalFileHeaderError;
				}

			}

			private void LocalFileHeaderWrite()
			{
				BinaryWriter bw = new BinaryWriter(_zipFs);

				List<byte> extraField = new List<byte>();
				Zip64 = UncompressedSize >= 0xffffffff;

				byte[] bFileName;
				if (IsUnicode(FileName))
				{
					_generalPurposeBitFlag |= 1 << 11;
					bFileName = Encoding.UTF8.GetBytes(FileName);
				}
				else
					bFileName = GetBytes(FileName);

				ushort versionNeededToExtract = (ushort)(Zip64 ? 45 : 20);

				RelativeOffsetOfLocalHeader = (ulong)_zipFs.Position;
				const uint header = 0x4034B50;
				bw.Write(header);
				bw.Write(versionNeededToExtract);
				bw.Write(_generalPurposeBitFlag);
				bw.Write(_compressionMethod);
				bw.Write(_lastModFileTime);
				bw.Write(_lastModFileDate);

				_crc32Location = (ulong)_zipFs.Position;

				// these 3 values will be set correctly after the file data has been written
				bw.Write(0xffffffff);
				bw.Write(0xffffffff);
				bw.Write(0xffffffff);

				if (Zip64)
				{
					for (int i = 0; i < 20; i++)
						extraField.Add(0);
				}

				ushort fileNameLength = (ushort)bFileName.Length;
				bw.Write(fileNameLength);

				ushort extraFieldLength = (ushort)extraField.Count;
				bw.Write(extraFieldLength);

				bw.Write(bFileName, 0, fileNameLength);

				_extraLocation = (ulong)_zipFs.Position;
				bw.Write(extraField.ToArray(), 0, extraFieldLength);
			}

			public void LocalFileHeaderFake(ulong filePosition, ulong uncompressedSize, ulong compressedSize, byte[] crc32, MemoryStream ms)
			{
				RelativeOffsetOfLocalHeader = filePosition;
				TrrntZip = true;
				UncompressedSize = uncompressedSize;
				_compressedSize = compressedSize;
				CRC = crc32;

				BinaryWriter bw = new BinaryWriter(ms);

				Zip64 = UncompressedSize >= 0xffffffff;

				byte[] bFileName;
				if (IsUnicode(FileName))
				{
					_generalPurposeBitFlag |= 1 << 11;
					bFileName = Encoding.UTF8.GetBytes(FileName);
				}
				else
					bFileName = GetBytes(FileName);

				ushort versionNeededToExtract = (ushort)(Zip64 ? 45 : 20);

				const uint header = 0x4034B50;
				bw.Write(header);
				bw.Write(versionNeededToExtract);
				bw.Write(_generalPurposeBitFlag);
				bw.Write(_compressionMethod);
				bw.Write(_lastModFileTime);
				bw.Write(_lastModFileDate);

				uint tCompressedSize;
				uint tUncompressedSize;
				if (Zip64)
				{
					tCompressedSize = 0xffffffff;
					tUncompressedSize = 0xffffffff;
				}
				else
				{
					tCompressedSize = (uint)_compressedSize;
					tUncompressedSize = (uint)UncompressedSize;
				}
				bw.Write(CRC[3]);
				bw.Write(CRC[2]);
				bw.Write(CRC[1]);
				bw.Write(CRC[0]);
				bw.Write(tCompressedSize);
				bw.Write(tUncompressedSize);

				ushort fileNameLength = (ushort)bFileName.Length;
				bw.Write(fileNameLength);

				ushort extraFieldLength = (ushort)(Zip64 ? 20 : 0);
				bw.Write(extraFieldLength);

				bw.Write(bFileName, 0, fileNameLength);

				if (Zip64)
				{
					bw.Write((ushort)0x0001); // id
					bw.Write((ushort)16); // data length
					bw.Write(UncompressedSize);
					bw.Write(_compressedSize);
				}
			}

			private Stream _readStream;
			public ZipReturn LocalFileOpenReadStream(bool raw, out Stream stream, out ulong streamSize, out ushort compressionMethod)
			{
				streamSize = 0;
				compressionMethod = _compressionMethod;

				_readStream = null;
				_zipFs.Seek((long)_dataLocation, SeekOrigin.Begin);

				switch (_compressionMethod)
				{
					case 8:
						if (raw)
						{
							_readStream = _zipFs;
							streamSize = _compressedSize;
						}
						else
						{
							_readStream = new DeflateStream(_zipFs, CompressionMode.Decompress, true);
							streamSize = UncompressedSize;

						}
						break;
					case 0:
						_readStream = _zipFs;
						streamSize = _compressedSize;  // same as UncompressedSize
						break;
				}
				stream = _readStream;
				return stream == null ? ZipReturn.ZipErrorGettingDataStream : ZipReturn.ZipGood;
			}
			public ZipReturn LocalFileCloseReadStream()
			{
				DeflateStream dfStream = _readStream as DeflateStream;
				if (dfStream != null)
				{
					dfStream.Close();
					dfStream.Dispose();
				}
				return ZipReturn.ZipGood;
			}

			private Stream _writeStream;
			public ZipReturn LocalFileOpenWriteStream(bool raw, bool trrntZip, ulong uncompressedSize, ushort compressionMethod, out Stream stream)
			{
				UncompressedSize = uncompressedSize;
				_compressionMethod = compressionMethod;

				LocalFileHeaderWrite();
				_dataLocation = (ulong)_zipFs.Position;

				if (raw)
				{
					_writeStream = _zipFs;
					TrrntZip = trrntZip;
				}
				else
				{
					if (compressionMethod == 0)
					{
						_writeStream = _zipFs;
						TrrntZip = false;
					}
					else
					{
						_writeStream = new DeflateStream(_zipFs, CompressionMode.Compress, CompressionLevel.BestCompression, true);
						TrrntZip = true;
					}
				}

				stream = _writeStream;
				return stream == null ? ZipReturn.ZipErrorGettingDataStream : ZipReturn.ZipGood;
			}

			public ZipReturn LocalFileCloseWriteStream(byte[] crc32)
			{
				DeflateStream dfStream = _writeStream as DeflateStream;
				if (dfStream != null)
				{
					dfStream.Flush();
					dfStream.Close();
					dfStream.Dispose();
				}

				_compressedSize = (ulong)_zipFs.Position - _dataLocation;

				if (_compressedSize == 0 && UncompressedSize == 0)
				{
					LocalFileAddDirectory();
					_compressedSize = (ulong)_zipFs.Position - _dataLocation;
				}

				CRC = crc32;
				WriteCompressedSize();

				return ZipReturn.ZipGood;
			}
			private void WriteCompressedSize()
			{
				long posNow = _zipFs.Position;
				_zipFs.Seek((long)_crc32Location, SeekOrigin.Begin);
				BinaryWriter bw = new BinaryWriter(_zipFs);

				uint tCompressedSize;
				uint tUncompressedSize;
				if (Zip64)
				{
					tCompressedSize = 0xffffffff;
					tUncompressedSize = 0xffffffff;
				}
				else
				{
					tCompressedSize = (uint)_compressedSize;
					tUncompressedSize = (uint)UncompressedSize;
				}

				bw.Write(CRC[3]);
				bw.Write(CRC[2]);
				bw.Write(CRC[1]);
				bw.Write(CRC[0]);
				bw.Write(tCompressedSize);
				bw.Write(tUncompressedSize);

				// also need to write extradata
				if (Zip64)
				{
					_zipFs.Seek((long)_extraLocation, SeekOrigin.Begin);
					bw.Write((ushort)0x0001); // id
					bw.Write((ushort)16); // data length
					bw.Write(UncompressedSize);
					bw.Write(_compressedSize);
				}

				_zipFs.Seek(posNow, SeekOrigin.Begin);

			}

			public void LocalFileCheck()
			{
				if (FileStatus != ZipReturn.ZipUntested) return;

				try
				{
					Stream sInput = null;
					_zipFs.Seek((long)_dataLocation, SeekOrigin.Begin);

					switch (_compressionMethod)
					{
						case 8:
							sInput = new DeflateStream(_zipFs, CompressionMode.Decompress, true);
							break;
						case 0:
							sInput = _zipFs;
							break;
					}

					if (sInput == null)
					{
						FileStatus = ZipReturn.ZipErrorGettingDataStream;
						return;
					}

					CRC32Hash crc32 = new CRC32Hash();
					MD5 lmd5 = System.Security.Cryptography.MD5.Create();
					SHA1 lsha1 = System.Security.Cryptography.SHA1.Create();

					ulong sizetogo = UncompressedSize;
					if (_buffer == null)
						_buffer = new byte[Buffersize];

					while (sizetogo > 0)
					{
						int sizenow = sizetogo > Buffersize ? Buffersize : (int)sizetogo;
						sInput.Read(_buffer, 0, sizenow);

						crc32.TransformBlock(_buffer, 0, sizenow, null, 0);
						lmd5.TransformBlock(_buffer, 0, sizenow, null, 0);
						lsha1.TransformBlock(_buffer, 0, sizenow, null, 0);

						sizetogo = sizetogo - (ulong)sizenow;
					}

					crc32.TransformFinalBlock(_buffer, 0, 0);
					lmd5.TransformFinalBlock(_buffer, 0, 0);
					lsha1.TransformFinalBlock(_buffer, 0, 0);

					byte[] testcrc = crc32.Hash;
					md5 = lmd5.Hash;
					sha1 = lsha1.Hash;

					if (_compressionMethod == 8)
					{
						sInput.Close();
						sInput.Dispose();
					}

					FileStatus = ByteArrCompare(CRC, testcrc) ? ZipReturn.ZipGood : ZipReturn.ZipCRCDecodeError;
				}
				catch
				{
					FileStatus = ZipReturn.ZipDecodeError;
				}
			}

			public void LocalFileAddDirectory()
			{
				Stream ds = _zipFs;
				ds.WriteByte(03);
				ds.WriteByte(00);
			}

			public ulong LocalFilePos
			{
				get { return RelativeOffsetOfLocalHeader; }
				set { RelativeOffsetOfLocalHeader = value; }
			}
			private static byte[] ReadCRC(BinaryReader br)
			{
				byte[] tCRC = new byte[4];
				tCRC[3] = br.ReadByte();
				tCRC[2] = br.ReadByte();
				tCRC[1] = br.ReadByte();
				tCRC[0] = br.ReadByte();
				return tCRC;
			}

		}

		private IO.FileInfo _zipFileInfo;

		public string ZipFilename
		{
			get { return _zipFileInfo != null ? _zipFileInfo.FullName : ""; }
		}
		public long TimeStamp
		{
			get { return _zipFileInfo != null ? _zipFileInfo.LastWriteTime : 0; }
		}

		private ulong _centerDirStart;
		private ulong _centerDirSize;
		private ulong _endOfCenterDir64;

		byte[] _fileComment;
		private Stream _zipFs;

		private uint _localFilesCount;
		private readonly List<LocalFile> _localFiles = new List<LocalFile>();

		private ZipStatus _pZipStatus;
		private bool _zip64;
		public ZipOpenType ZipOpen;

		public ZipStatus ZipStatus { get { return _pZipStatus; } }

		public int LocalFilesCount() { return _localFiles.Count; }

		public string Filename(int i) { return _localFiles[i].FileName; }
		public ulong UncompressedSize(int i) { return _localFiles[i].UncompressedSize; }
		public ulong? LocalHeader(int i)
		{
			return ((_localFiles[i]._generalPurposeBitFlag & 8) == 0) ? (ulong?)_localFiles[i].RelativeOffsetOfLocalHeader : null;
		}
		public ZipReturn FileStatus(int i) { return _localFiles[i].FileStatus; }
		public byte[] CRC32(int i) { return _localFiles[i].CRC; }
		public byte[] MD5(int i) { return _localFiles[i].md5; }
		public byte[] SHA1(int i) { return _localFiles[i].sha1; }

		~ZipFile()
		{
			if (_zipFs != null)
			{
				_zipFs.Close();
				_zipFs.Dispose();
			}
		}

		private ZipReturn FindEndOfCentralDirSignature()
		{
			long fileSize = _zipFs.Length;
			long maxBackSearch = 0xffff;

			if (_zipFs.Length < maxBackSearch)
				maxBackSearch = fileSize;

			const long buffSize = 0x400;

			byte[] buffer = new byte[buffSize + 4];

			long backPosition = 4;
			while (backPosition < maxBackSearch)
			{
				backPosition += buffSize;
				if (backPosition > maxBackSearch) backPosition = maxBackSearch;

				long readSize = backPosition > (buffSize + 4) ? (buffSize + 4) : backPosition;

				_zipFs.Position = fileSize - backPosition;

				_zipFs.Read(buffer, 0, (int)readSize);

				for (long i = readSize - 4; i >= 0; i--)
				{
					if ((buffer[i] != 0x50) || (buffer[i + 1] != 0x4b) || (buffer[i + 2] != 0x05) || (buffer[i + 3] != 0x06)) continue;

					_zipFs.Position = (fileSize - backPosition) + i;
					return ZipReturn.ZipGood;
				}
			}
			return ZipReturn.ZipCenteralDirError;
		}

		private ZipReturn EndOfCentralDirRead()
		{
			BinaryReader zipBr = new BinaryReader(_zipFs);

			uint thisSignature = zipBr.ReadUInt32();
			if (thisSignature != EndOfCentralDirSignature)
				return ZipReturn.ZipEndOfCentralDirectoryError;

			ushort tushort = zipBr.ReadUInt16();     // NumberOfThisDisk
			if (tushort != 0) return ZipReturn.ZipEndOfCentralDirectoryError;

			tushort = zipBr.ReadUInt16();     // NumberOfThisDiskCenterDir
			if (tushort != 0) return ZipReturn.ZipEndOfCentralDirectoryError;

			_localFilesCount = zipBr.ReadUInt16();     // TotalNumberOfEnteriesDisk

			tushort = zipBr.ReadUInt16();     // TotalNumber of enteries in the central directory 
			if (tushort != _localFilesCount) return ZipReturn.ZipEndOfCentralDirectoryError;

			_centerDirSize = zipBr.ReadUInt32();     // SizeOfCenteralDir
			_centerDirStart = zipBr.ReadUInt32();     // Offset

			ushort zipFileCommentLength = zipBr.ReadUInt16();

			_fileComment = zipBr.ReadBytes(zipFileCommentLength);

			if (_zipFs.Position != _zipFs.Length) _pZipStatus |= ZipStatus.ExtraData;

			return ZipReturn.ZipGood;
		}
		private void EndOfCentralDirWrite()
		{
			BinaryWriter bw = new BinaryWriter(_zipFs);
			bw.Write(EndOfCentralDirSignature);
			bw.Write((ushort)0); // NumberOfThisDisk
			bw.Write((ushort)0); // NumberOfThisDiskCenterDir
			bw.Write((ushort)(_localFiles.Count >= 0xffff ? 0xffff : _localFiles.Count));  // TotalNumberOfEnteriesDisk
			bw.Write((ushort)(_localFiles.Count >= 0xffff ? 0xffff : _localFiles.Count));  // TotalNumber of enteries in the central directory 
			bw.Write((uint)(_centerDirSize >= 0xffffffff ? 0xffffffff : _centerDirSize));
			bw.Write((uint)(_centerDirStart >= 0xffffffff ? 0xffffffff : _centerDirStart));
			bw.Write((ushort)_fileComment.Length);
			bw.Write(_fileComment, 0, _fileComment.Length);
		}

		private ZipReturn Zip64EndOfCentralDirRead()
		{
			_zip64 = true;
			BinaryReader zipBr = new BinaryReader(_zipFs);

			uint thisSignature = zipBr.ReadUInt32();
			if (thisSignature != Zip64EndOfCentralDirSignatue)
				return ZipReturn.ZipEndOfCentralDirectoryError;

			ulong tulong = zipBr.ReadUInt64(); // Size of zip64 end of central directory record
			if (tulong != 44) return ZipReturn.Zip64EndOfCentralDirError;

			zipBr.ReadUInt16(); // version made by

			ushort tushort = zipBr.ReadUInt16(); // version needed to extract
			if (tushort != 45) return ZipReturn.Zip64EndOfCentralDirError;

			uint tuint = zipBr.ReadUInt32(); // number of this disk
			if (tuint != 0) return ZipReturn.Zip64EndOfCentralDirError;

			tuint = zipBr.ReadUInt32(); // number of the disk with the start of the central directory
			if (tuint != 0) return ZipReturn.Zip64EndOfCentralDirError;

			_localFilesCount = (uint)zipBr.ReadUInt64(); // total number of entries in the central directory on this disk

			tulong = zipBr.ReadUInt64(); // total number of entries in the central directory
			if (tulong != _localFilesCount) return ZipReturn.Zip64EndOfCentralDirError;

			_centerDirSize = zipBr.ReadUInt64(); // size of central directory

			_centerDirStart = zipBr.ReadUInt64(); // offset of start of central directory with respect to the starting disk number

			return ZipReturn.ZipGood;
		}

		private void Zip64EndOfCentralDirWrite()
		{
			BinaryWriter bw = new BinaryWriter(_zipFs);
			bw.Write(Zip64EndOfCentralDirSignatue);
			bw.Write((ulong)44); // Size of zip64 end of central directory record
			bw.Write((ushort)45); // version made by
			bw.Write((ushort)45); // version needed to extract
			bw.Write((uint)0);    // number of this disk
			bw.Write((uint)0);    // number of the disk with the start of the central directroy
			bw.Write((ulong)_localFiles.Count); // total number of entries in the central directory on this disk
			bw.Write((ulong)_localFiles.Count); // total number of entries in the central directory
			bw.Write(_centerDirSize);  // size of central directory
			bw.Write(_centerDirStart); // offset of start of central directory with respect to the starting disk number
		}

		private ZipReturn Zip64EndOfCentralDirectoryLocatorRead()
		{
			_zip64 = true;
			BinaryReader zipBr = new BinaryReader(_zipFs);

			uint thisSignature = zipBr.ReadUInt32();
			if (thisSignature != Zip64EndOfCentralDirectoryLocator)
				return ZipReturn.ZipEndOfCentralDirectoryError;

			uint tuint = zipBr.ReadUInt32();  // number of the disk with the start of the zip64 end of centeral directory
			if (tuint != 0) return ZipReturn.Zip64EndOfCentralDirectoryLocatorError;

			_endOfCenterDir64 = zipBr.ReadUInt64(); // relative offset of the zip64 end of central directroy record

			tuint = zipBr.ReadUInt32();  // total number of disks
			if (tuint != 1) return ZipReturn.Zip64EndOfCentralDirectoryLocatorError;

			return ZipReturn.ZipGood;
		}
		private void Zip64EndOfCentralDirectoryLocatorWrite()
		{
			BinaryWriter bw = new BinaryWriter(_zipFs);
			bw.Write(Zip64EndOfCentralDirectoryLocator);
			bw.Write((uint)0);    // number of the disk with the start of the zip64 end of centeral directory
			bw.Write(_endOfCenterDir64); // relative offset of the zip64 end of central directroy record
			bw.Write((uint)1);  // total number of disks
		}

		public ZipReturn ZipFileOpen(string newFilename, long timestamp, bool readHeaders)
		{
			ZipFileClose();
			_pZipStatus = ZipStatus.None;
			_zip64 = false;
			_centerDirStart = 0;
			_centerDirSize = 0;
			_zipFileInfo = null;

			try
			{
				if (!IO.File.Exists(newFilename))
				{
					ZipFileClose();
					return ZipReturn.ZipErrorFileNotFound;
				}
				_zipFileInfo = new IO.FileInfo(newFilename);
				if (_zipFileInfo.LastWriteTime != timestamp)
				{
					ZipFileClose();
					return ZipReturn.ZipErrorTimeStamp;
				}
				int errorCode = IO.FileStream.OpenFileRead(newFilename, out _zipFs);
				if (errorCode != 0)
				{
					ZipFileClose();
					if (errorCode == 32)
						return ZipReturn.ZipFileLocked;
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
			ZipOpen = ZipOpenType.OpenRead;

			if (!readHeaders)
				return ZipReturn.ZipGood;

			return ZipFileReadHeaders();
		}

		public ZipReturn ZipFileOpen(Stream zipFileStream)
		{
			ZipFileClose();
			_pZipStatus = ZipStatus.None;
			_zip64 = false;
			_centerDirStart = 0;
			_centerDirSize = 0;
			_zipFileInfo = null;
			_zipFs = zipFileStream;

			ZipOpen = ZipOpenType.OpenRead;
			return ZipFileReadHeaders();
		}

		private ZipReturn ZipFileReadHeaders()
		{
			try
			{
				ZipReturn zRet = FindEndOfCentralDirSignature();
				if (zRet != ZipReturn.ZipGood)
				{
					ZipFileClose();
					return zRet;
				}

				long endOfCentralDir = _zipFs.Position;
				zRet = EndOfCentralDirRead();
				if (zRet != ZipReturn.ZipGood)
				{
					ZipFileClose();
					return zRet;
				}

				// check if this is a ZIP64 zip and if it is read the Zip64 End Of Central Dir Info
				if (_centerDirStart == 0xffffffff || _centerDirSize == 0xffffffff || _localFilesCount == 0xffff)
				{
					_zip64 = true;
					_zipFs.Position = endOfCentralDir - 20;
					zRet = Zip64EndOfCentralDirectoryLocatorRead();
					if (zRet != ZipReturn.ZipGood)
					{
						ZipFileClose();
						return zRet;
					}
					_zipFs.Position = (long)_endOfCenterDir64;
					zRet = Zip64EndOfCentralDirRead();
					if (zRet != ZipReturn.ZipGood)
					{
						ZipFileClose();
						return zRet;
					}
				}

				bool trrntzip = false;

				// check if the ZIP has a valid TorrentZip file comment
				if (_fileComment.Length == 22)
				{
					if (GetString(_fileComment).Substring(0, 14) == "TORRENTZIPPED-")
					{
						CrcCalculatorStream crcCs = new CrcCalculatorStream(_zipFs, true);
						byte[] buffer = new byte[_centerDirSize];
						_zipFs.Position = (long)_centerDirStart;
						crcCs.Read(buffer, 0, (int)_centerDirSize);
						crcCs.Flush();
						crcCs.Close();

						uint r = (uint)crcCs.Crc;
						crcCs.Dispose();

						string tcrc = GetString(_fileComment).Substring(14, 8);
						string zcrc = r.ToString("X8");
						if (String.Compare(tcrc, zcrc, StringComparison.Ordinal) == 0)
							trrntzip = true;

					}
				}

				// now read the central directory
				_zipFs.Position = (long)_centerDirStart;

				_localFiles.Clear();
				_localFiles.Capacity = (int)_localFilesCount;
				for (int i = 0; i < _localFilesCount; i++)
				{
					LocalFile lc = new LocalFile(_zipFs);
					zRet = lc.CenteralDirectoryRead();
					if (zRet != ZipReturn.ZipGood)
					{
						ZipFileClose();
						return zRet;
					}
					_zip64 |= lc.Zip64;
					_localFiles.Add(lc);
				}

				for (int i = 0; i < _localFilesCount; i++)
				{
					zRet = _localFiles[i].LocalFileHeaderRead();
					if (zRet != ZipReturn.ZipGood)
					{
						ZipFileClose();
						return zRet;
					}
					trrntzip &= _localFiles[i].TrrntZip;
				}

				// check trrntzip file order
				if (trrntzip)
					for (int i = 0; i < _localFilesCount - 1; i++)
					{
						if (TrrntZipStringCompare(_localFiles[i].FileName, _localFiles[i + 1].FileName) < 0) continue;
						trrntzip = false;
						break;
					}

				// check trrntzip directories
				if (trrntzip)
					for (int i = 0; i < _localFilesCount - 1; i++)
					{
						// see if we found a directory
						string filename0 = _localFiles[i].FileName;
						if (filename0.Substring(filename0.Length - 1, 1) != "/") continue;

						// see if the next file is in that directory
						string filename1 = _localFiles[i + 1].FileName;
						if (filename1.Length <= filename0.Length) continue;
						if (TrrntZipStringCompare(filename0, filename1.Substring(0, filename0.Length)) != 0) continue;

						// if we found a file in the directory then we do not need the directory entry
						trrntzip = false;
						break;
					}

				if (trrntzip)
					_pZipStatus |= ZipStatus.TrrntZip;

				return ZipReturn.ZipGood;
			}
			catch
			{
				ZipFileClose();
				return ZipReturn.ZipErrorReadingFile;
			}

		}

		public ZipReturn ZipFileCreate(string newFilename)
		{
			if (ZipOpen != ZipOpenType.Closed)
				return ZipReturn.ZipFileAlreadyOpen;

			CreateDirForFile(newFilename);
			_zipFileInfo = new IO.FileInfo(newFilename);

			int errorCode = IO.FileStream.OpenFileWrite(newFilename, out _zipFs);
			if (errorCode != 0)
			{
				ZipFileClose();
				return ZipReturn.ZipErrorOpeningFile;
			}
			ZipOpen = ZipOpenType.OpenWrite;
			return ZipReturn.ZipGood;
		}

		public ZipReturn ZipCreateFake()
		{
			if (ZipOpen != ZipOpenType.Closed)
				return ZipReturn.ZipFileAlreadyOpen;

			ZipOpen = ZipOpenType.OpenFakeWrite;
			return ZipReturn.ZipGood;
		}

		public void ZipFileClose()
		{
			if (ZipOpen == ZipOpenType.Closed)
			{
				return;
			}

			if (ZipOpen == ZipOpenType.OpenRead)
			{
				if (_zipFileInfo != null && _zipFs != null)
				{
					_zipFs.Close();
					_zipFs.Dispose();
				}
				ZipOpen = ZipOpenType.Closed;
				return;
			}

			_zip64 = false;
			bool lTrrntzip = true;

			_centerDirStart = (ulong)_zipFs.Position;
			if (_centerDirStart >= 0xffffffff)
				_zip64 = true;

			CrcCalculatorStream crcCs = new CrcCalculatorStream(_zipFs, true);

			foreach (LocalFile t in _localFiles)
			{
				t.CenteralDirectoryWrite(crcCs);
				_zip64 |= t.Zip64;
				lTrrntzip &= t.TrrntZip;
			}

			crcCs.Flush();
			crcCs.Close();

			_centerDirSize = (ulong)_zipFs.Position - _centerDirStart;

			_fileComment = lTrrntzip ? GetBytes("TORRENTZIPPED-" + crcCs.Crc.ToString("X8")) : new byte[0];
			_pZipStatus = lTrrntzip ? ZipStatus.TrrntZip : ZipStatus.None;

			crcCs.Dispose();

			if (_zip64)
			{
				_endOfCenterDir64 = (ulong)_zipFs.Position;
				Zip64EndOfCentralDirWrite();
				Zip64EndOfCentralDirectoryLocatorWrite();
			}
			EndOfCentralDirWrite();

			_zipFs.SetLength(_zipFs.Position);
			_zipFs.Flush();
			_zipFs.Close();
			_zipFs.Dispose();
			_zipFileInfo = new IO.FileInfo(_zipFileInfo.FullName);
			ZipOpen = ZipOpenType.Closed;

		}

		public void ZipFileCloseFake(ulong fileOffset, out byte[] centeralDir)
		{
			centeralDir = null;
			if (ZipOpen != ZipOpenType.OpenFakeWrite)
			{
				return;
			}

			_zip64 = false;
			bool lTrrntzip = true;

			_zipFs = new MemoryStream();

			_centerDirStart = fileOffset;
			if (_centerDirStart >= 0xffffffff)
				_zip64 = true;

			CrcCalculatorStream crcCs = new CrcCalculatorStream(_zipFs, true);

			foreach (LocalFile t in _localFiles)
			{
				t.CenteralDirectoryWrite(crcCs);
				_zip64 |= t.Zip64;
				lTrrntzip &= t.TrrntZip;
			}

			crcCs.Flush();
			crcCs.Close();

			_centerDirSize = (ulong)_zipFs.Position;

			_fileComment = lTrrntzip ? GetBytes("TORRENTZIPPED-" + crcCs.Crc.ToString("X8")) : new byte[0];
			_pZipStatus = lTrrntzip ? ZipStatus.TrrntZip : ZipStatus.None;

			crcCs.Dispose();

			if (_zip64)
			{
				_endOfCenterDir64 = fileOffset + (ulong)_zipFs.Position;
				Zip64EndOfCentralDirWrite();
				Zip64EndOfCentralDirectoryLocatorWrite();
			}
			EndOfCentralDirWrite();

			centeralDir = ((MemoryStream)_zipFs).ToArray();
			_zipFs.Close();
			_zipFs.Dispose();
			ZipOpen = ZipOpenType.Closed;
		}

		public void ZipFileCloseFailed()
		{
			if (ZipOpen == ZipOpenType.Closed)
			{
				return;
			}

			if (ZipOpen == ZipOpenType.OpenRead)
			{
				if (_zipFs != null)
				{
					_zipFs.Close();
					_zipFs.Dispose();
				}
				ZipOpen = ZipOpenType.Closed;
				return;
			}

			_zipFs.Flush();
			_zipFs.Close();
			_zipFs.Dispose();
			IO.File.Delete(_zipFileInfo.FullName);
			_zipFileInfo = null;
			ZipOpen = ZipOpenType.Closed;
		}

		private int _readIndex;
		public ZipReturn ZipFileOpenReadStream(int index, bool raw, out Stream stream, out ulong streamSize, out ushort compressionMethod)
		{
			streamSize = 0;
			compressionMethod = 0;
			_readIndex = index;
			stream = null;
			if (ZipOpen != ZipOpenType.OpenRead)
				return ZipReturn.ZipReadingFromOutputFile;

			ZipReturn zRet = _localFiles[index].LocalFileHeaderRead();
			if (zRet != ZipReturn.ZipGood)
			{
				ZipFileClose();
				return zRet;
			}

			return _localFiles[index].LocalFileOpenReadStream(raw, out stream, out streamSize, out compressionMethod);
		}

		public ZipReturn ZipFileOpenReadStreamQuick(ulong pos, bool raw, out Stream stream, out ulong streamSize, out ushort compressionMethod)
		{
			LocalFile tmpFile = new LocalFile(_zipFs) { LocalFilePos = pos };
			_localFiles.Clear();
			_localFiles.Add(tmpFile);
			ZipReturn zr = tmpFile.LocalFileHeaderReadQuick();
			if (zr != ZipReturn.ZipGood)
			{
				stream = null;
				streamSize = 0;
				compressionMethod = 0;
				return zr;
			}
			_readIndex = 0;

			return tmpFile.LocalFileOpenReadStream(raw, out stream, out streamSize, out compressionMethod);
		}

		public ZipReturn ZipFileCloseReadStream()
		{
			return _localFiles[_readIndex].LocalFileCloseReadStream();
		}

		public ZipReturn ZipFileOpenWriteStream(bool raw, bool trrntzip, string filename, ulong uncompressedSize, ushort compressionMethod, out Stream stream)
		{
			stream = null;
			if (ZipOpen != ZipOpenType.OpenWrite)
				return ZipReturn.ZipWritingToInputFile;

			LocalFile lf = new LocalFile(_zipFs, filename);

			ZipReturn retVal = lf.LocalFileOpenWriteStream(raw, trrntzip, uncompressedSize, compressionMethod, out stream);

			_localFiles.Add(lf);

			return retVal;
		}

		public ZipReturn ZipFileAddFake(string filename, ulong fileOffset, ulong uncompressedSize, ulong compressedSize, byte[] crc32, out byte[] localHeader)
		{
			localHeader = null;

			if (ZipOpen != ZipOpenType.OpenFakeWrite)
				return ZipReturn.ZipWritingToInputFile;

			LocalFile lf = new LocalFile(_zipFs, filename);
			_localFiles.Add(lf);

			MemoryStream ms = new MemoryStream();
			lf.LocalFileHeaderFake(fileOffset, uncompressedSize, compressedSize, crc32, ms);

			localHeader = ms.ToArray();
			ms.Close();

			return ZipReturn.ZipGood;
		}

		public ZipReturn ZipFileCloseWriteStream(byte[] crc32)
		{
			return _localFiles[_localFiles.Count - 1].LocalFileCloseWriteStream(crc32);
		}

		public ZipReturn ZipFileRollBack()
		{
			if (ZipOpen != ZipOpenType.OpenWrite)
				return ZipReturn.ZipWritingToInputFile;

			int fileCount = _localFiles.Count;
			if (fileCount == 0)
				return ZipReturn.ZipErrorRollBackFile;

			LocalFile lf = _localFiles[fileCount - 1];

			_localFiles.RemoveAt(fileCount - 1);
			_zipFs.Position = (long)lf.LocalFilePos;
			return ZipReturn.ZipGood;
		}

		public void ZipFileAddDirectory()
		{
			_localFiles[_localFiles.Count - 1].LocalFileAddDirectory();
		}

		/*
        public void BreakTrrntZip(string filename)
        {
            _zipFs = new FileStream(filename, FileMode.Open, FileAccess.ReadWrite);
            BinaryReader zipBr = new BinaryReader(_zipFs);
            _zipFs.Position = _zipFs.Length - 22;
            byte[] fileComment = zipBr.ReadBytes(22);
            if (GetString(fileComment).Substring(0, 14) == "TORRENTZIPPED-")
            {
                _zipFs.Position = _zipFs.Length - 8;
                _zipFs.WriteByte(48); _zipFs.WriteByte(48); _zipFs.WriteByte(48); _zipFs.WriteByte(48);
                _zipFs.WriteByte(48); _zipFs.WriteByte(48); _zipFs.WriteByte(48); _zipFs.WriteByte(48);
            }

            zipBr.Close();
            _zipFs.Flush();
            _zipFs.Close();
        }
        */

		public void DeepScan()
		{
			foreach (LocalFile lfile in _localFiles)
				lfile.LocalFileCheck();
		}

		public static void CreateDirForFile(string sFilename)
		{
			string strTemp = IO.Path.GetDirectoryName(sFilename);

			if (String.IsNullOrEmpty(strTemp)) return;

			if (IO.Directory.Exists(strTemp)) return;

			while (strTemp.Length > 0 && !IO.Directory.Exists(strTemp))
			{
				int pos = strTemp.LastIndexOf(IO.Path.DirectorySeparatorChar);
				if (pos < 0) pos = 0;
				strTemp = strTemp.Substring(0, pos);
			}

			while (sFilename.IndexOf(IO.Path.DirectorySeparatorChar, strTemp.Length + 1) > 0)
			{
				strTemp = sFilename.Substring(0, sFilename.IndexOf(IO.Path.DirectorySeparatorChar, strTemp.Length + 1));
				IO.Directory.CreateDirectory(strTemp);
			}
		}

		public static string ZipErrorMessageText(ZipReturn zS)
		{
			string ret = "Unknown";
			switch (zS)
			{
				case ZipReturn.ZipGood:
					ret = "";
					break;
				case ZipReturn.ZipFileCountError:
					ret = "The number of file in the Zip does not mach the number of files in the Zips Centeral Directory";
					break;
				case ZipReturn.ZipSignatureError:
					ret = "An unknown Signature Block was found in the Zip";
					break;
				case ZipReturn.ZipExtraDataOnEndOfZip:
					ret = "Extra Data was found on the end of the Zip";
					break;
				case ZipReturn.ZipUnsupportedCompression:
					ret = "An unsupported Compression method was found in the Zip, if you recompress this zip it will be usable";
					break;
				case ZipReturn.ZipLocalFileHeaderError:
					ret = "Error reading a zipped file header information";
					break;
				case ZipReturn.ZipCenteralDirError:
					ret = "There is an error in the Zip Centeral Directory";
					break;
				case ZipReturn.ZipReadingFromOutputFile:
					ret = "Trying to write to a Zip file open for output only";
					break;
				case ZipReturn.ZipWritingToInputFile:
					ret = "Tring to read from a Zip file open for input only";
					break;
				case ZipReturn.ZipErrorGettingDataStream:
					ret = "Error creating Data Stream";
					break;
				case ZipReturn.ZipCRCDecodeError:
					ret = "CRC error";
					break;
				case ZipReturn.ZipDecodeError:
					ret = "Error unzipping a file";
					break;
			}

			return ret;
		}

		private static byte[] GetBytes(String s)
		{
			char[] c = s.ToCharArray();
			byte[] b = new byte[c.Length];
			for (int i = 0; i < c.Length; i++)
			{
				char t = c[i];
				b[i] = t > 255 ? (byte)'?' : (byte)c[i];
			}
			return b;
		}
		private static bool IsUnicode(string s)
		{
			char[] c = s.ToCharArray();
			for (int i = 0; i < c.Length; i++)
				if (c[i] > 255) return true;
			return false;
		}

		private static string GetString(byte[] b)
		{
			string s = "";
			for (int i = 0; i < b.Length; i++)
				s += (char)b[i];
			return s;
		}
		private static bool CompareString(string s1, string s2)
		{
			char[] c1 = s1.ToCharArray();
			char[] c2 = s2.ToCharArray();

			if (c1.Length != c2.Length)
				return false;

			for (int i = 0; i < c1.Length; i++)
				if (c1[i] != c2[i]) return false;
			return true;
		}

		private static bool ByteArrCompare(byte[] b0, byte[] b1)
		{
			if (b0 == null || b1 == null)
				return false;
			if (b0.Length != b1.Length)
				return false;

			for (int i = 0; i < b0.Length; i++)
			{
				if (b0[i] != b1[i])
					return false;
			}
			return true;
		}

		private static int TrrntZipStringCompare(string string1, string string2)
		{
			char[] bytes1 = string1.ToCharArray();
			char[] bytes2 = string2.ToCharArray();

			int pos1 = 0;
			int pos2 = 0;

			for (;;)
			{
				if (pos1 == bytes1.Length)
					return ((pos2 == bytes2.Length) ? 0 : -1);
				if (pos2 == bytes2.Length)
					return 1;

				int byte1 = bytes1[pos1++];
				int byte2 = bytes2[pos2++];

				if (byte1 >= 65 && byte1 <= 90) byte1 += 0x20;
				if (byte2 >= 65 && byte2 <= 90) byte2 += 0x20;

				if (byte1 < byte2)
					return -1;
				if (byte1 > byte2)
					return 1;
			}
		}
	}

}
