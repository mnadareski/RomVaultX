/******************************************************
 *     ROMVault2 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2014                                 *
 ******************************************************/

using System.IO;
using System.Security.Cryptography;
using RomVaultX.DB;
using RomVaultX.SupportedFiles.Zip.ZLib;

namespace RomVaultX.SupportedFiles.Files
{
    public static class UnCompFiles
    {
        private const int Buffersize = 1024 * 1024 * 64;
        private static readonly byte[] Buffer;

        static UnCompFiles()
        {
            Buffer = new byte[Buffersize];
        }

        public static int CheckSumRead(string filename, bool testDeep, out byte[] crc, out byte[] bMD5, out byte[] bSHA1, out ulong size)
        {
            bMD5 = null;
            bSHA1 = null;
            crc = null;
            size = 0;

            Stream ds;
            int errorCode = IO.FileStream.OpenFileRead(filename, out ds);
            if (errorCode != 0)
                return errorCode;

            CRC32Hash crc32 = new CRC32Hash();

            MD5 md5 = null;
            if (testDeep) md5 = MD5.Create();
            SHA1 sha1 = null;
            if (testDeep) sha1 = SHA1.Create();

            size = (ulong)ds.Length;
            long sizetogo = ds.Length;

            while (sizetogo > 0)
            {
                int sizenow = sizetogo > Buffersize ? Buffersize : (int)sizetogo;

                ds.Read(Buffer, 0, sizenow);
                crc32.TransformBlock(Buffer, 0, sizenow, null, 0);
                if (testDeep) md5.TransformBlock(Buffer, 0, sizenow, null, 0);
                if (testDeep) sha1.TransformBlock(Buffer, 0, sizenow, null, 0);
                sizetogo -= sizenow;
            }

            crc32.TransformFinalBlock(Buffer, 0, 0);
            if (testDeep) md5.TransformFinalBlock(Buffer, 0, 0);
            if (testDeep) sha1.TransformFinalBlock(Buffer, 0, 0);

            ds.Close();

            crc = crc32.Hash;
            if (testDeep) bMD5 = md5.Hash;
            if (testDeep) bSHA1 = sha1.Hash;

            return 0;
        }

        public static RvFile CheckSumRead(Stream ds, int offset)
        {
            ds.Position = 0;
            RvFile file=new RvFile();

            CRC32Hash crc32 = new CRC32Hash();
            MD5 md5 = MD5.Create();
            SHA1 sha1 = SHA1.Create();

            CRC32Hash altCrc32 = null;
            MD5 altMd5 = null;
            SHA1 altSha1 = null;
            if (offset > 0)
            {
                altCrc32 = new CRC32Hash();
                altMd5 = MD5.Create();
                altSha1 = SHA1.Create();
            }

            file.Size = (ulong)ds.Length;
            long sizetogo = ds.Length;

            // just read header into main Hash
            if (offset > 0)
            {
                int sizenow = sizetogo > offset ? offset : (int)sizetogo;

                ds.Read(Buffer, 0, sizenow);
                crc32.TransformBlock(Buffer, 0, sizenow, null, 0);
                md5.TransformBlock(Buffer, 0, sizenow, null, 0);
                sha1.TransformBlock(Buffer, 0, sizenow, null, 0);
                sizetogo -= sizenow;

            }

            while (sizetogo > 0)
            {
                int sizenow = sizetogo > Buffersize ? Buffersize : (int)sizetogo;

                ds.Read(Buffer, 0, sizenow);
                crc32.TransformBlock(Buffer, 0, sizenow, null, 0);
                md5.TransformBlock(Buffer, 0, sizenow, null, 0);
                sha1.TransformBlock(Buffer, 0, sizenow, null, 0);

                if (offset > 0)
                {
                    altCrc32.TransformBlock(Buffer, 0, sizenow, null, 0);
                    altMd5.TransformBlock(Buffer, 0, sizenow, null, 0);
                    altSha1.TransformBlock(Buffer, 0, sizenow, null, 0);
                }

                sizetogo -= sizenow;
            }

            crc32.TransformFinalBlock(Buffer, 0, 0);
            md5.TransformFinalBlock(Buffer, 0, 0);
            sha1.TransformFinalBlock(Buffer, 0, 0);
            file.CRC = crc32.Hash;
            file.MD5 = md5.Hash;
            file.SHA1 = sha1.Hash;

            if (offset > 0)
            {
                altCrc32.TransformFinalBlock(Buffer, 0, 0);
                altMd5.TransformFinalBlock(Buffer, 0, 0);
                altSha1.TransformFinalBlock(Buffer, 0, 0);
                file.AltSize = (ulong?)(ds.Length - offset);
                file.AltCRC = altCrc32.Hash;
                file.AltMD5 = altMd5.Hash;
                file.AltSHA1 = altSha1.Hash;
            }
            else
            {
                file.AltSize = null;
                file.AltCRC = null;
                file.AltSHA1 = null;
                file.AltMD5 = null;
            }
            ds.Close();

            return file;
        }
    }
}
