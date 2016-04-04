using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using RomVaultX.DB;
using RomVaultX.DB.DBAccess;
using RomVaultX.SupportedFiles;
using RomVaultX.SupportedFiles.Files;
using RomVaultX.SupportedFiles.GZ;
using RomVaultX.SupportedFiles.Zip;
using RomVaultX.Util;
using Directory = System.IO.Directory;
using DirectoryInfo = RomVaultX.IO.DirectoryInfo;
using File = System.IO.File;
using FileInfo = RomVaultX.IO.FileInfo;
using Path = RomVaultX.IO.Path;

namespace RomVaultX
{

    public static class romScanner
    {
        private static BackgroundWorker _bgw;

        public static string rootDir = @"ToSort";
        public static bool delFiles = true;

        private static int Buffersize = 1024 * 1024;
        private static byte[] _buffer = new byte[Buffersize];

        public static void ScanFiles(object sender, DoWorkEventArgs e)
        {
            _bgw = sender as BackgroundWorker;
            Program.SyncCont = e.Argument as SynchronizationContext;
            if (Program.SyncCont == null)
            {
                _bgw = null;
                return;
            }

            ScanADirNew(rootDir);

            DataAccessLayer.UpdateGotTotal();
            _bgw.ReportProgress(0, new bgwText("Scanning Files Complete"));
            _bgw = null;
            Program.SyncCont = null;
        }

        private static bool ScanAFile(Stream fStream)
        {
            bool ret = false;
            int offset;
            FileType foundFileType = FileHeaderReader.GetType(fStream, out offset);

            fStream.Position = 0;
            RvFile tFile = UnCompFiles.CheckSumRead(fStream, offset);
            tFile.AltType = foundFileType;


            if (foundFileType == FileType.CHD)
            {
                // read altheader values from CHD file.
            }

            // test if needed.
            FindStatus res = fileneededTest(tFile);

            if (res == FindStatus.FileNeededInArchive)
            {
                Debug.WriteLine("Reading file as " + tFile.SHA1);
                GZip gz = new GZip(tFile);
                string outfile = Getfilename(tFile.SHA1);
                fStream.Position = 0;
                gz.WriteGZip(outfile, fStream, false);

                tFile.CompressedSize = gz.compressedSize;
                tFile.DBWrite();
                ret = true;
            }
            else if (res == FindStatus.FoundFileInArchive)
                ret = true;

            if (foundFileType == FileType.ZIP)
            {
                ZipFile fz = new ZipFile();
                fStream.Position = 0;
                fz.ZipFileOpen(fStream);

                bool allZipFound = true;
                for (int i = 0; i < fz.LocalFilesCount(); i++)
                {
                    // this needs to go back into the Zip library.

                    Stream stream;
                    ulong streamSize;
                    ushort compressionMethod;
                    fz.ZipFileOpenReadStream(i, false, out stream, out streamSize, out compressionMethod);

                    // test keep in memory if less than 1024*1024
                    ulong memkeepSize = 1024 * 1024;
                    if (streamSize <= memkeepSize)
                    {
                        byte[] tmpFile = new byte[streamSize];
                        stream.Read(tmpFile, 0, (int)streamSize);
                        Stream memFS = new MemoryStream(tmpFile, false);
                        allZipFound &= ScanAFile(memFS);
                        memFS.Close();
                        memFS.Dispose();
                    }
                    else
                    {
                        string file = @"C:\tmp\" + Guid.NewGuid();
                        Stream Fs;
                        IO.FileStream.OpenFileWrite(file, out Fs);
                        ulong sizetogo = streamSize;
                        while (sizetogo > 0)
                        {
                            int sizenow = sizetogo > (ulong)Buffersize ? Buffersize : (int)sizetogo;
                            stream.Read(_buffer, 0, sizenow);
                            Fs.Write(_buffer, 0, sizenow);
                            sizetogo -= (ulong)sizenow;
                        }
                        Fs.Close();

                        Stream fstreamNext;
                        int errorCode = IO.FileStream.OpenFileRead(file, out fstreamNext);
                        if (errorCode != 0)
                            return false;

                        allZipFound &= ScanAFile(fstreamNext);
                        fstreamNext.Close();
                        fstreamNext.Dispose();
                        File.Delete(file);
                    }
                    fz.ZipFileCloseReadStream();

                }
                fz.ZipFileClose();
                ret |= allZipFound;
            }
            if (foundFileType == FileType.GZ)
            {
                GZip gz = new GZip();
                fStream.Position = 0;
                ZipReturn zr = gz.ReadGZip(fStream, false);
                if (zr == ZipReturn.ZipGood)
                {
                    ulong streamSize = gz.uncompressedSize;
                    if (streamSize > 0)
                    {
                        Stream stream;
                        gz.GetStream(out stream);
                        ulong memkeepSize = 1024 * 1024;
                        if (streamSize <= memkeepSize)
                        {
                            byte[] tmpFile = new byte[streamSize];
                            stream.Read(tmpFile, 0, (int)streamSize);
                            Stream memFS = new MemoryStream(tmpFile, false);
                            ret |= ScanAFile(memFS);
                            memFS.Close();
                            memFS.Dispose();
                        }
                        else
                        {

                            string file = @"C:\tmp\" + Guid.NewGuid();
                            Stream Fs;
                            IO.FileStream.OpenFileWrite(file, out Fs);
                            ulong sizetogo = streamSize;
                            while (sizetogo > 0)
                            {
                                int sizenow = sizetogo > (ulong)Buffersize ? Buffersize : (int)sizetogo;
                                stream.Read(_buffer, 0, sizenow);
                                Fs.Write(_buffer, 0, sizenow);
                                sizetogo -= (ulong)sizenow;
                            }
                            Fs.Close();
                            stream.Close();

                            Stream fstreamNext;
                            int errorCode = IO.FileStream.OpenFileRead(file, out fstreamNext);
                            if (errorCode != 0)
                                return false;

                            ret |= ScanAFile(fstreamNext);
                            fstreamNext.Close();
                            fstreamNext.Dispose();
                            File.Delete(file);
                        }
                    }
                    // gz.Close(); do not close the Stream gZip
                }
            }
            return ret;
        }

        private static void ScanADirNew(string directory)
        {
            _bgw.ReportProgress(0, new bgwText("Scanning Dir : " + directory));
            DirectoryInfo di = new DirectoryInfo(directory);

            FileInfo[] fi = di.GetFiles();

            _bgw.ReportProgress(0, new bgwRange2Visible(true));
            _bgw.ReportProgress(0, new bgwSetRange2(fi.Count()));

            for (int j = 0; j < fi.Count(); j++)
            {
                if (_bgw.CancellationPending)
                    return;

                FileInfo f = fi[j];
                _bgw.ReportProgress(0, new bgwValue2(j));
                _bgw.ReportProgress(0, new bgwText2(f.Name));

                Stream fstreamNext;
                int errorCode = IO.FileStream.OpenFileRead(f.FullName, out fstreamNext);
                if (errorCode != 0)
                    return;

                bool FileFound = ScanAFile(fstreamNext);
                fstreamNext.Close();
                fstreamNext.Dispose();
                if (FileFound)
                   File.Delete(f.FullName);
            }

            DirectoryInfo[] childdi = di.GetDirectories();
            foreach (DirectoryInfo d in childdi)
            {
                if (_bgw.CancellationPending)
                    return;
                ScanADirNew(d.FullName);
            }

            if (directory == "ToSort")
                return;
            if (IsDirectoryEmpty(directory))
                Directory.Delete(directory);

        }



        /*
        private static void ScanADir(string directory)
        {
            _bgw.ReportProgress(0, new bgwText("Scanning Dir : " + directory));
            DirectoryInfo di = new DirectoryInfo(directory);

            FileInfo[] fi = di.GetFiles();

            _bgw.ReportProgress(0, new bgwRange2Visible(true));
            _bgw.ReportProgress(0, new bgwSetRange2(fi.Count()));

            for (int j = 0; j < fi.Count(); j++)
            {

                if (_bgw.CancellationPending)
                    return;

                FileInfo f = fi[j];
                _bgw.ReportProgress(0, new bgwValue2(j));
                _bgw.ReportProgress(0, new bgwText2(f.Name));
                string ext = Path.GetExtension(f.Name);

                if (ext.ToLower() == ".zip")
                {
                    ZipFile fz = new ZipFile();
                    ZipReturn zr = fz.ZipFileOpen(f.FullName, f.LastWriteTime, true);
                    if (zr != ZipReturn.ZipGood)
                        continue;

                    fz.DeepScan();

                    int FileUsedCount = 0;

                    for (int i = 0; i < fz.LocalFilesCount(); i++)
                    {
                        Debug.WriteLine(fz.Filename(i));
                        RvFile tFile = new RvFile();
                        tFile.Size = fz.UncompressedSize(i);
                        tFile.CRC = fz.CRC32(i);
                        tFile.MD5 = fz.MD5(i);
                        tFile.SHA1 = fz.SHA1(i);
                        Debug.WriteLine("CRC " + VarFix.ToString(tFile.CRC));
                        Debug.WriteLine("MD5 " + VarFix.ToString(tFile.MD5));
                        Debug.WriteLine("SHA1 " + VarFix.ToString(tFile.SHA1));


                        FindStatus res = fileneededTest(tFile);

                        if (res == FindStatus.FileUnknown)
                            continue;

                        FileUsedCount++;

                        if (res != FindStatus.FoundFileInArchive)
                        {
                            GZip gz = new GZip();
                            gz.crc = tFile.CRC;
                            gz.md5Hash = tFile.MD5;
                            gz.sha1Hash = tFile.SHA1;
                            gz.uncompressedSize = tFile.Size;

                            bool isZipTrrntzip = (fz.ZipStatus == ZipStatus.TrrntZip);
                            ulong compressedSize;
                            ushort method;
                            Stream zds;

                            fz.ZipFileOpenReadStream(i, isZipTrrntzip, out zds, out compressedSize, out method);
                            gz.compressedSize = compressedSize;

                            string outfile = Getfilename(tFile.SHA1);
                            gz.WriteGZip(outfile, zds, isZipTrrntzip);
                            tFile.CompressedSize = gz.compressedSize;
                            fz.ZipFileCloseReadStream();

                            tFile.DBWrite();
                        }
                    }
                    fz.ZipFileClose();

                    if (delFiles && FileUsedCount == fz.LocalFilesCount())
                    {
                        File.SetAttributes(f.FullName, FileAttributes.Normal);
                        File.Delete(f.FullName);
                    }
                }
                else if (ext.ToLower() == ".gz")
                {
                    GZip gZipTest = new GZip();
                    ZipReturn errorcode = gZipTest.ReadGZip(f.FullName, true);
                    if (errorcode != ZipReturn.ZipGood)
                        continue;
                    RvFile tFile = new RvFile();
                    tFile.CRC = gZipTest.crc;
                    tFile.MD5 = gZipTest.md5Hash;
                    tFile.SHA1 = gZipTest.sha1Hash;
                    tFile.Size = gZipTest.uncompressedSize;

                    FindStatus res = fileneededTest(tFile);

                    if (res == FindStatus.FileUnknown)
                        continue;

                    if (res != FindStatus.FoundFileInArchive)
                    {
                        GZip gz = new GZip();
                        gz.crc = tFile.CRC;
                        gz.md5Hash = tFile.MD5;
                        gz.sha1Hash = tFile.SHA1;
                        gz.uncompressedSize = tFile.Size;

                        Stream ds;
                        gZipTest.GetStream(out ds);
                        string outfile = Getfilename(tFile.SHA1);
                        gz.WriteGZip(outfile, ds, false);
                        ds.Close();
                        ds.Dispose();

                        gZipTest.Close();
                        tFile.CompressedSize = gz.compressedSize;
                        tFile.DBWrite();
                    }

                    if (delFiles)
                    {
                        File.SetAttributes(f.FullName, FileAttributes.Normal);
                        File.Delete(f.FullName);
                    }
                }
                else
                {
                    RvFile tFile = new RvFile();
                    int errorcode = UnCompFiles.CheckSumRead(f.FullName, true, out tFile.CRC, out tFile.MD5, out tFile.SHA1, out tFile.Size);

                    if (errorcode != 0)
                        continue;

                    // test if needed.
                    FindStatus res = fileneededTest(tFile);

                    if (res == FindStatus.FileUnknown)
                        continue;

                    if (res != FindStatus.FoundFileInArchive)
                    {
                        GZip gz = new GZip();
                        gz.crc = tFile.CRC;
                        gz.md5Hash = tFile.MD5;
                        gz.sha1Hash = tFile.SHA1;
                        gz.uncompressedSize = tFile.Size;

                        Stream ds;
                        int errorCode = IO.FileStream.OpenFileRead(f.FullName, out ds);
                        string outfile = Getfilename(tFile.SHA1);
                        gz.WriteGZip(outfile, ds, false);
                        ds.Close();
                        ds.Dispose();

                        tFile.CompressedSize = gz.compressedSize;
                        tFile.DBWrite();
                    }

                    if (delFiles)
                    {
                        File.SetAttributes(f.FullName, FileAttributes.Normal);
                        File.Delete(f.FullName);
                    }
                }

            }

            DirectoryInfo[] childdi = di.GetDirectories();
            foreach (DirectoryInfo d in childdi)
            {
                if (_bgw.CancellationPending)
                    return;
                ScanADir(d.FullName);
            }

            if (directory != rootDir && IsDirectoryEmpty(directory))
                Directory.Delete(directory);
        }
        */

        private static bool IsDirectoryEmpty(string path)
        {
            return !Directory.EnumerateFileSystemEntries(path).Any();
        }

        private static string Getfilename(byte[] SHA1)
        {
            return @"RomRoot\" + VarFix.ToString(SHA1[0]) + @"\" +
                         VarFix.ToString(SHA1[1]) + @"\" +
                         VarFix.ToString(SHA1) + ".gz";

        }

        private enum FindStatus
        {
            FileUnknown,
            FoundFileInArchive,
            FileNeededInArchive,
        };
        private static FindStatus fileneededTest(RvFile tFile)
        {
            // first check to see if we already have it in the file table
            bool inFileDB = FindInFiles.Execute(tFile); // returns true if found in File table
            if (inFileDB) return FindStatus.FoundFileInArchive;

            // now check if needed in any ROMs
            if (FindInROMs.Execute(tFile))
                return FindStatus.FileNeededInArchive;

            if (FileHeaderReader.AltHeaderFile(tFile.AltType) && FindInROMs.ExecuteAlt(tFile))
                return FindStatus.FileNeededInArchive;

            return FindStatus.FileUnknown;
        }
    }
}
