/******************************************************
 *     ROMVaultX is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2016                                 *
 ******************************************************/

using System.IO;
using RomVaultX.DB;

namespace RomVaultX.SupportedFiles.Files
{
    public static class UnCompFiles
    {
        private const int Buffersize = 1024 * 1024;
        private static readonly byte[] Buffer0;
        private static readonly byte[] Buffer1;

        static UnCompFiles()
        {
            Buffer0 = new byte[Buffersize];
            Buffer1 = new byte[Buffersize];
        }

        public static RvFile CheckSumRead(Stream ds, int offset)
        {
            ds.Position = 0;
            RvFile file = new RvFile();

            ThreadLoadBuffer lbuffer = new ThreadLoadBuffer(ds);

            ThreadCRC crc32 = new ThreadCRC();
            ThreadMD5 md5 = new ThreadMD5();
            ThreadSHA1 sha1 = new ThreadSHA1();

            ThreadCRC altCrc32 = null;
            ThreadMD5 altMd5 = null;
            ThreadSHA1 altSha1 = null;
            if (offset > 0)
            {
                altCrc32 = new ThreadCRC();
                altMd5 = new ThreadMD5();
                altSha1 = new ThreadSHA1();
            }

            file.Size = (ulong)ds.Length;
            long sizetogo = ds.Length;

            // just read header into main Hash
            if (offset > 0)
            {
                int sizenow = sizetogo > offset ? offset : (int)sizetogo;
                ds.Read(Buffer0, 0, sizenow);

                crc32.Trigger(Buffer0, sizenow);
                md5.Trigger(Buffer0, sizenow);
                sha1.Trigger(Buffer0, sizenow);
                crc32.Wait();
                md5.Wait();
                sha1.Wait();

                sizetogo -= sizenow;
            }

            // Pre load the first buffer0
            int sizeNext = sizetogo > Buffersize ? Buffersize : (int)sizetogo;
            ds.Read(Buffer0, 0, sizeNext);
            int sizebuffer = sizeNext;
            sizetogo -= sizeNext;
            bool whichBuffer = true;


            while (sizebuffer > 0)
            {
                // trigger the buffer loading worker
                sizeNext = sizetogo > Buffersize ? Buffersize : (int)sizetogo;
                if (sizeNext > 0)
                    lbuffer.Trigger(whichBuffer ? Buffer1 : Buffer0, sizeNext);

                byte[] buffer = whichBuffer ? Buffer0 : Buffer1;

                // trigger the hashing workers
                crc32.Trigger(buffer, sizebuffer);
                md5.Trigger(buffer, sizebuffer);
                sha1.Trigger(buffer, sizebuffer);
                altCrc32?.Trigger(buffer, sizebuffer);
                altMd5?.Trigger(buffer, sizebuffer);
                altSha1?.Trigger(buffer, sizebuffer);

                // wait until all the workers are complete
                if (sizeNext > 0)
                    lbuffer.Wait();
                crc32.Wait();
                md5.Wait();
                sha1.Wait();
                altCrc32?.Wait();
                altMd5?.Wait();
                altSha1?.Wait();
                
                // setup next loop around
                sizebuffer = sizeNext;
                sizetogo -= sizeNext;
                whichBuffer = !whichBuffer;
            }

            // tell all the workers we are finished
            lbuffer.Finish();
            crc32.Finish();
            md5.Finish();
            sha1.Finish();

            // get the results
            file.CRC = crc32.Hash;
            file.MD5 = md5.Hash;
            file.SHA1 = sha1.Hash;

            if (offset > 0)
            {
                altCrc32?.Finish();
                altMd5?.Finish();
                altSha1?.Finish();
                file.AltSize = (ulong?)(ds.Length - offset);
                file.AltCRC = altCrc32?.Hash;
                file.AltMD5 = altMd5?.Hash;
                file.AltSHA1 = altSha1?.Hash;
            }
            else
            {
                file.AltSize = null;
                file.AltCRC = null;
                file.AltSHA1 = null;
                file.AltMD5 = null;
            }

            // cleanup
            lbuffer.Dispose();
            crc32.Dispose();
            md5.Dispose();
            sha1.Dispose();
            altCrc32?.Dispose();
            altMd5?.Dispose();
            altSha1?.Dispose();

            return file;
        }
    }
}
