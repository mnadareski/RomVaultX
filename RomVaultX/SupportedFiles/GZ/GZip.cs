using System;
using System.IO;
using System.Security.Cryptography;
using RomVaultX.SupportedFiles.Zip.ZLib;

namespace RomVaultX.SupportedFiles.GZ
{
    public class GZip
    {

        const int Buffersize = 4096 * 128;
        private static byte[] _buffer;

        public byte[] sha1Hash;
        public byte[] md5Hash;
        public byte[] crc;
        public ulong uncompressedSize;
        public ulong compressedSize;

        public ZipReturn ReadGZip(string filename, bool deepScan)
        {
            Stream _zipFs;

            if (!IO.File.Exists(filename))
            {
                return ZipReturn.ZipErrorFileNotFound;
            }


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
                    md5Hash = new byte[16];
                    Array.Copy(bytes, 0, md5Hash, 0, 16);
                    crc = new byte[4];
                    Array.Copy(bytes, 16, crc, 0, 4);
                    uncompressedSize = BitConverter.ToUInt64(bytes, 20);
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

            if (deepScan)
            {

                Stream sInput = null;
                sInput = new DeflateStream(_zipFs, CompressionMode.Decompress, true);


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
            zipBw.Write((byte)0x00); // OS  = 0x00

            // writing FEXTRA
            zipBw.Write((Int16)28); // XLEN
            zipBw.Write(md5Hash);
            zipBw.Write(crc);
            zipBw.Write(uncompressedSize);

            if (_buffer == null)
                _buffer = new byte[Buffersize];

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
