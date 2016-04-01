/******************************************************
 *     ROMVault2 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2014                                 *
 ******************************************************/

using System.IO;
using System.Security.Cryptography;
using RomVaultX.DB;
using RomVaultX.SupportedFiles.Zip.ZLib;
using System.Threading;

namespace RomVaultX.SupportedFiles.Files
{
    public static class UnCompFiles
    {
        private const int Buffersize = 1024 * 1024 * 64;
        private static readonly byte[] Buffer0;
        private static readonly byte[] Buffer1;

        static UnCompFiles()
        {
            Buffer0 = new byte[Buffersize];
            Buffer1 = new byte[Buffersize];
        }

        /*
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

                ds.Read(Buffer0, 0, sizenow);
                crc32.TransformBlock(Buffer0, 0, sizenow, null, 0);
                if (testDeep) md5.TransformBlock(Buffer0, 0, sizenow, null, 0);
                if (testDeep) sha1.TransformBlock(Buffer0, 0, sizenow, null, 0);
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
        */

        public static RvFile CheckSumRead(Stream ds, int offset)
        {
            ds.Position = 0;
            RvFile file = new RvFile();

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

                ds.Read(Buffer0, 0, sizenow);
                crc32.TransformBlock(Buffer0, 0, sizenow, null, 0);
                md5.TransformBlock(Buffer0, 0, sizenow, null, 0);
                sha1.TransformBlock(Buffer0, 0, sizenow, null, 0);
                sizetogo -= sizenow;
            }

            // Pre load the first buffer0
            int sizeNext = sizetogo > Buffersize ? Buffersize : (int)sizetogo;
            ds.Read(Buffer0, 0, sizeNext);
            int sizebuffer = sizeNext;
            sizetogo -= (long)sizeNext;
            bool whichBuffer = true;


            while (sizebuffer > 0)
            {
                sizeNext = sizetogo > Buffersize ? Buffersize : (int)sizetogo;

                Thread t0 = null;
                if (sizeNext > 0)
                {
                    t0 = new Thread(() => { ds.Read(whichBuffer ? Buffer1 : Buffer0, 0, sizeNext); });
                    t0.Start();
                }

                byte[] buffer = whichBuffer ? Buffer0 : Buffer1;
                Thread t1 = new Thread(() => { crc32.TransformBlock(buffer, 0, sizebuffer, null, 0); });
                Thread t2 = new Thread(() => { md5.TransformBlock(buffer, 0, sizebuffer, null, 0); });
                Thread t3 = new Thread(() => { sha1.TransformBlock(buffer, 0, sizebuffer, null, 0); });
                t1.Start();
                t2.Start();
                t3.Start();

                Thread t4 = null, t5 = null, t6 = null;
                if (offset > 0)
                {
                    t4 = new Thread(() => { altCrc32.TransformBlock(buffer, 0, sizebuffer, null, 0); });
                    t5 = new Thread(() => { altMd5.TransformBlock(buffer, 0, sizebuffer, null, 0); });
                    t6 = new Thread(() => { altSha1.TransformBlock(buffer, 0, sizebuffer, null, 0); });
                    t4.Start();
                    t5.Start();
                    t6.Start();
                }

                if (t0 != null) t0.Join();
                t1.Join();
                t2.Join();
                t3.Join();
                if (t4 != null) t4.Join();
                if (t5 != null) t5.Join();
                if (t6 != null) t6.Join();

                sizebuffer = sizeNext;
                sizetogo -= (long)sizeNext;
                whichBuffer = !whichBuffer;
            }

            crc32.TransformFinalBlock(Buffer0, 0, 0);
            md5.TransformFinalBlock(Buffer0, 0, 0);
            sha1.TransformFinalBlock(Buffer0, 0, 0);
            file.CRC = crc32.Hash;
            file.MD5 = md5.Hash;
            file.SHA1 = sha1.Hash;

            if (offset > 0)
            {
                altCrc32.TransformFinalBlock(Buffer0, 0, 0);
                altMd5.TransformFinalBlock(Buffer0, 0, 0);
                altSha1.TransformFinalBlock(Buffer0, 0, 0);
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

            return file;
        }
    }
}
