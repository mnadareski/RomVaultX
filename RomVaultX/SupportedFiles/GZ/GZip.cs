using System;
using System.IO;
using System.Security.Cryptography;
using RomVaultX.DB;
using RomVaultX.SupportedFiles.Zip.ZLib;

namespace RomVaultX.SupportedFiles.GZ
{
    public class GZip
    {
        const int Buffersize = 4096 * 128;
        private static byte[] _buffer;

        private string _filename;
        public byte[] crc;
        public byte[] sha1Hash;         
        public byte[] md5Hash;
        public FileType altType;
        public byte[] altcrc;
        public byte[] altsha1Hash;
        public byte[] altmd5Hash;

        public ulong uncompressedSize;
        public ulong? uncompressedAltSize;
        public ulong compressedSize;
        public long datapos;

        private Stream _zipFs;

        public GZip()
        { }

        public GZip(RvFile tFile)
        {
            altType = tFile.AltType;
            crc = tFile.CRC;
            md5Hash = tFile.MD5;
            sha1Hash = tFile.SHA1;
            uncompressedSize = tFile.Size;
            altcrc = tFile.AltCRC;
            altsha1Hash = tFile.AltSHA1;
            altmd5Hash = tFile.AltMD5;
            uncompressedAltSize = tFile.AltSize;
        }

        public ZipReturn ReadGZip(string filename, bool deepScan)
        {

            _filename = "";
            if (!IO.File.Exists(filename))
            {
                return ZipReturn.ZipErrorFileNotFound;
            }
            _filename = filename;

            int errorCode = IO.FileStream.OpenFileRead(filename, out _zipFs);
            if (errorCode != 0)
            {
                if (errorCode == 32)
                    return ZipReturn.ZipFileLocked;
                return ZipReturn.ZipErrorOpeningFile;
            }
            BinaryReader zipBr = new BinaryReader(_zipFs);

            byte ID1 = zipBr.ReadByte();
            byte ID2 = zipBr.ReadByte();

            if (ID1 != 0x1f || ID2 != 0x8b)
            {
                _zipFs.Close();
                return ZipReturn.ZipSignatureError;
            }

            byte CM = zipBr.ReadByte();
            if (CM != 8)
            {
                _zipFs.Close();
                return ZipReturn.ZipUnsupportedCompression;
            }
            byte FLG = zipBr.ReadByte();


            UInt32 MTime = zipBr.ReadUInt32();
            byte XFL = zipBr.ReadByte();
            byte OS = zipBr.ReadByte();

            //if FLG.FEXTRA set
            if ((FLG & 0x4) == 0x4)
            {
                int XLen = zipBr.ReadInt16();
                byte[] bytes = zipBr.ReadBytes(XLen);

                if (XLen == 28)
                {
                    md5Hash = new byte[16]; Array.Copy(bytes, 0, md5Hash, 0, 16);
                    crc = new byte[4]; Array.Copy(bytes, 16, crc, 0, 4);
                    uncompressedSize = BitConverter.ToUInt64(bytes, 20);
                }
                if (XLen == 77)
                {
                    md5Hash = new byte[16]; Array.Copy(bytes, 0, md5Hash, 0, 16);
                    crc = new byte[4]; Array.Copy(bytes, 16, crc, 0, 4);
                    uncompressedSize = BitConverter.ToUInt64(bytes, 20);
                    altType = (FileType)bytes[28];
                    altmd5Hash = new byte[16]; Array.Copy(bytes, 29, altmd5Hash, 0, 16);
                    altsha1Hash = new byte[20]; Array.Copy(bytes, 45, altmd5Hash, 0, 20);
                    altcrc = new byte[4]; Array.Copy(bytes, 65, altcrc, 0, 4);
                    uncompressedAltSize = BitConverter.ToUInt64(bytes, 69);
                }
            }

            //if FLG.FNAME set
            if ((FLG & 0x8) == 0x8)
            {
                int XLen = zipBr.ReadInt16();
                byte[] bytes = zipBr.ReadBytes(XLen);
            }

            //if FLG.FComment set
            if ((FLG & 0x10) == 0x10)
            {
                int XLen = zipBr.ReadInt16();
                byte[] bytes = zipBr.ReadBytes(XLen);
            }

            //if FLG.FHCRC set
            if ((FLG & 0x2) == 0x2)
            {
                uint crc16 = zipBr.ReadUInt16();
            }

            compressedSize = (ulong)(_zipFs.Length - _zipFs.Position) - 8;

            datapos = _zipFs.Position;
            if (deepScan)
            {

                Stream sInput = new DeflateStream(_zipFs, CompressionMode.Decompress, true);


                CRC32Hash crc32 = new CRC32Hash();
                MD5 lmd5 = MD5.Create();
                SHA1 lsha1 = SHA1.Create();

                if (_buffer == null)
                    _buffer = new byte[Buffersize];

                ulong uncompressedRead = 0;

                int sizeRead = 1;
                while (sizeRead > 0)
                {
                    sizeRead = sInput.Read(_buffer, 0, Buffersize);

                    if (sizeRead > 0)
                    {
                        crc32.TransformBlock(_buffer, 0, sizeRead, null, 0);
                        lmd5.TransformBlock(_buffer, 0, sizeRead, null, 0);
                        lsha1.TransformBlock(_buffer, 0, sizeRead, null, 0);
                    }
                    uncompressedRead += (ulong)sizeRead;
                }

                crc32.TransformFinalBlock(_buffer, 0, 0);
                lmd5.TransformFinalBlock(_buffer, 0, 0);
                lsha1.TransformFinalBlock(_buffer, 0, 0);

                sInput.Close();
                sInput.Dispose();

                if (uncompressedSize != 0)
                {
                    if (uncompressedSize != uncompressedRead)
                    {
                        _zipFs.Close();
                        return ZipReturn.ZipDecodeError;
                    }
                }
                else
                    uncompressedSize = uncompressedRead;

                byte[] testcrc = crc32.Hash;
                if (crc != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (crc[i] == testcrc[i]) continue;
                        _zipFs.Close();
                        return ZipReturn.ZipDecodeError;
                    }
                }
                else
                    crc = testcrc;

                byte[] testmd5 = lmd5.Hash;
                if (md5Hash != null)
                {
                    for (int i = 0; i < 16; i++)
                    {
                        if (md5Hash[i] == testmd5[i]) continue;
                        _zipFs.Close();
                        return ZipReturn.ZipDecodeError;
                    }
                }
                else
                    md5Hash = testmd5;

                byte[] testsha1 = lsha1.Hash;
                if (sha1Hash != null)
                {
                    for (int i = 0; i < 20; i++)
                    {
                        if (sha1Hash[i] == testsha1[i]) continue;
                        _zipFs.Close();
                        return ZipReturn.ZipDecodeError;
                    }
                }
                else
                    sha1Hash = testsha1;

                sInput.Close();
                sInput.Dispose();

            }
            _zipFs.Position = _zipFs.Length - 8;
            byte[] gzcrc = zipBr.ReadBytes(4);
            uint gzLength = zipBr.ReadUInt32();

            if (crc != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (gzcrc[3 - i] == crc[i]) continue;
                    _zipFs.Close();
                    return ZipReturn.ZipDecodeError;
                }
            }
            else
                crc = new[] { gzcrc[3], gzcrc[2], gzcrc[1], gzcrc[0] };

            if (uncompressedSize != 0)
            {
                if (gzLength != (uncompressedSize & 0xffffffff))
                {
                    _zipFs.Close();
                    return ZipReturn.ZipDecodeError;
                }
            }

            _zipFs.Close();

            return ZipReturn.ZipGood;
        }

        public ZipReturn GetStream(out Stream st)
        {
            st = null;
            if (!IO.File.Exists(_filename))
            {
                return ZipReturn.ZipErrorFileNotFound;
            }

            int errorCode = IO.FileStream.OpenFileRead(_filename, out _zipFs);
            if (errorCode != 0)
            {
                if (errorCode == 32)
                    return ZipReturn.ZipFileLocked;
                return ZipReturn.ZipErrorOpeningFile;
            }

            _zipFs.Position=datapos;

            st = new DeflateStream(_zipFs, CompressionMode.Decompress, true);

            return ZipReturn.ZipGood;
        }

        public ZipReturn GetRawStream(out Stream st)
        {
            st = null;
            if (!IO.File.Exists(_filename))
            {
                return ZipReturn.ZipErrorFileNotFound;
            }

            int errorCode = IO.FileStream.OpenFileRead(_filename, out _zipFs);
            if (errorCode != 0)
            {
                if (errorCode == 32)
                    return ZipReturn.ZipFileLocked;
                return ZipReturn.ZipErrorOpeningFile;
            }

            _zipFs.Position = datapos;

            st = _zipFs;

            return ZipReturn.ZipGood;
        }

        public void Close()
        {
            if (_zipFs==null) return;

            _zipFs.Close();
            _zipFs.Dispose();
            _zipFs = null;
        }

        public ZipReturn WriteGZip(string filename, Stream sInput, bool isCompressedStream)
        {
            CreateDirForFile(filename);
            
            Stream _zipFs;
            IO.FileStream.OpenFileWrite(filename, out _zipFs);
            BinaryWriter zipBw = new BinaryWriter(_zipFs);

            zipBw.Write((byte)0x1f); // ID1 = 0x1f
            zipBw.Write((byte)0x8b); // ID2 = 0x8b
            zipBw.Write((byte)0x08); // CM  = 0x08
            zipBw.Write((byte)0x04); // FLG = 0x04
            zipBw.Write((UInt32)0);  // MTime = 0
            zipBw.Write((byte)0x00); // XFL = 0x00
            zipBw.Write((byte)0xff); // OS  = 0x00

            // writing FEXTRA
            if(FileHeaderReader.AltHeaderFile(altType))
                zipBw.Write((Int16)77); // XLEN 16+4+8+1+16+20+4+8
            else
                zipBw.Write((Int16)28); // XLEN 16+4+8

            zipBw.Write(md5Hash);           // 16 bytes
            zipBw.Write(crc);               // 4 bytes
            zipBw.Write(uncompressedSize);  // 8 bytes

            if (FileHeaderReader.AltHeaderFile(altType))
            {
                zipBw.Write((byte) altType);  // 1
                zipBw.Write(altmd5Hash);      // 16
                zipBw.Write(altsha1Hash);     // 20
                zipBw.Write(altcrc);          // 4
                zipBw.Write((ulong) uncompressedAltSize);  // 8
            }


            if (_buffer == null)
                _buffer = new byte[Buffersize];

            ulong dataStartPos =(ulong) zipBw.BaseStream.Position;
            if (isCompressedStream)
            {
                ulong sizetogo = compressedSize;
                while (sizetogo > 0)
                {
                    int sizenow = sizetogo > Buffersize ? Buffersize : (int)sizetogo;
                    sInput.Read(_buffer, 0, sizenow);
                    _zipFs.Write(_buffer, 0, sizenow);
                    sizetogo -= (ulong)sizenow;
                }
            }
            else
            {
                ulong sizetogo = uncompressedSize;
                Stream writeStream = new DeflateStream(_zipFs, CompressionMode.Compress, CompressionLevel.BestCompression, true);
                while (sizetogo > 0)
                {
                    int sizenow = sizetogo > Buffersize ? Buffersize : (int)sizetogo;
                    sInput.Read(_buffer, 0, sizenow);
                    writeStream.Write(_buffer, 0, sizenow);
                    sizetogo -= (ulong)sizenow;
                }
                writeStream.Flush();
                writeStream.Close();
                writeStream.Dispose();
            }
            compressedSize = (ulong)zipBw.BaseStream.Position - dataStartPos;

            zipBw.Write(crc[3]);
            zipBw.Write(crc[2]);
            zipBw.Write(crc[1]);
            zipBw.Write(crc[0]);
            zipBw.Write((UInt32)uncompressedSize);
            zipBw.Flush();
            zipBw.Close();
            _zipFs.Close();

            return ZipReturn.ZipGood;
        }

        private static void CreateDirForFile(string sFilename)
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
    }
}
