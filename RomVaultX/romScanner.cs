using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using RomVaultX.DB;
using RomVaultX.SupportedFiles;
using RomVaultX.SupportedFiles.CHD;
using RomVaultX.SupportedFiles.Files;
using RomVaultX.SupportedFiles.GZ;
using RomVaultX.SupportedFiles.SevenZip;
using RomVaultX.SupportedFiles.Zip;
using RomVaultX.Util;

using Alphaleonis.Win32.Filesystem;

using Stream = System.IO.Stream;

namespace RomVaultX
{
    public static class RomScanner
    {
        private static BackgroundWorker _bgw;

        public static string RootDir = @"ToSort";
        public static bool DelFiles = true;

        private const int Buffersize = 1024 * 1024;
        private static readonly byte[] Buffer = new byte[Buffersize];

        private static ulong inMemorySize;

        public static void ScanFiles(object sender, DoWorkEventArgs e)
        {
            string sInMemorySize = AppSettings.ReadSetting("ScanInMemorySize");
            if (!ulong.TryParse(sInMemorySize, out inMemorySize))
                inMemorySize = 1000000;

            _bgw = sender as BackgroundWorker;
            Program.SyncCont = e.Argument as SynchronizationContext;
            if (Program.SyncCont == null)
            {
                _bgw = null;
                return;
            }

            ScanADirNew(RootDir);

            DatUpdate.UpdateGotTotal();
            _bgw?.ReportProgress(0, new bgwText("Scanning Files Complete"));
            _bgw = null;
            Program.SyncCont = null;
        }

        private static bool ScanAFile(string filename, Stream fStream)
        {
            bool ret = false;
            int offset;
            FileType foundFileType = FileHeaderReader.GetType(fStream, out offset);

            fStream.Position = 0;
            RvFile tFile = UnCompFiles.CheckSumRead(fStream, offset);
            tFile.AltType = foundFileType;

            if (foundFileType == FileType.CHD)
            {
                uint? version;
                CHD.CheckFile(fStream, out tFile.AltSHA1, out tFile.AltMD5, out version);
            }

            // test if needed.
            FindStatus res = RvRomFileMatchup.FileneededTest(tFile);

            if (res == FindStatus.FileNeededInArchive)
            {
                _bgw?.ReportProgress(0, new bgwShowEvent(filename, "found"));
                Debug.WriteLine("Reading file as " + VarFix.ToDBString(tFile.SHA1));
                GZip gz = new GZip(tFile);
                string outfile = GetFilename(tFile.SHA1);
                fStream.Position = 0;
                gz.WriteGZip(outfile, fStream, false);

                tFile.CompressedSize = gz.compressedSize;
                tFile.DBWrite();
                ret = true;
            }
            else if (res == FindStatus.FoundFileInArchive)
            {
                ret = true;
            }

            if (foundFileType == FileType.ZIP)
            {
                ZipFile fz = new ZipFile();
                fStream.Position = 0;
                ZipReturn zp = fz.ZipFileOpen(fStream);
                if (zp == ZipReturn.ZipGood)
                {
                    bool allZipFound = true;
                    for (int i = 0; i < fz.LocalFilesCount(); i++)
                    {
                        Stream stream;
                        ulong streamSize;
                        ushort compressionMethod;
                        fz.ZipFileOpenReadStream(i, false, out stream, out streamSize, out compressionMethod);

                        if (streamSize <= inMemorySize)
                        {
                            byte[] tmpFile = new byte[streamSize];
                            stream.Read(tmpFile, 0, (int)streamSize);
                            Stream memFs = new System.IO.MemoryStream(tmpFile, false);
                            allZipFound &= ScanAFile(fz.Filename(i), memFs);
                            memFs.Close();
                            memFs.Dispose();
                        }
                        else
                        {
                            string file = @"tmp\" + Guid.NewGuid();
                            if (!Directory.Exists("tmp"))
                            {
                                Directory.CreateDirectory("tmp");
                            }
                            Stream fs = File.OpenWrite(file);
                            ulong sizetogo = streamSize;
                            while (sizetogo > 0)
                            {
                                int sizenow = sizetogo > (ulong)Buffersize ? Buffersize : (int)sizetogo;
                                stream.Read(Buffer, 0, sizenow);
                                fs.Write(Buffer, 0, sizenow);
                                sizetogo -= (ulong)sizenow;
                            }
                            fs.Close();

                            Stream fstreamNext;
                            try
                            {
                                fstreamNext = File.OpenRead(file);
                            }
                            catch
                            {
                                return false;
                            }

                            allZipFound &= ScanAFile(fz.Filename(i), fstreamNext);
                            fstreamNext.Close();
                            fstreamNext.Dispose();
                            File.Delete(file);
                        }
                        fz.ZipFileCloseReadStream();

                    }
                    fz.ZipFileClose();
                    ret |= allZipFound;
                }
                else
                {
                    ret = false;
                }
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
                            Stream memFs = new System.IO.MemoryStream(tmpFile, false);
                            ret |= ScanAFile(filename, memFs);
                            memFs.Close();
                            memFs.Dispose();
                        }
                        else
                        {
                            string file = @"tmp\" + Guid.NewGuid();
                            if (!Directory.Exists("tmp"))
                            {
                                Directory.CreateDirectory("tmp");
                            }
                            Stream fs = File.OpenWrite(file);
                            ulong sizetogo = streamSize;
                            while (sizetogo > 0)
                            {
                                int sizenow = sizetogo > (ulong)Buffersize ? Buffersize : (int)sizetogo;
                                stream.Read(Buffer, 0, sizenow);
                                fs.Write(Buffer, 0, sizenow);
                                sizetogo -= (ulong)sizenow;
                            }
                            fs.Close();
                            stream.Close();

                            Stream fstreamNext;
                            try
                            {
                                fstreamNext = File.OpenRead(file);
                            }
                            catch
                            {
                                return false;
                            }

                            ret |= ScanAFile(filename, fstreamNext);
                            fstreamNext.Close();
                            fstreamNext.Dispose();
                            File.Delete(file);
                        }
                    }
                    // gz.Close(); do not close the Stream gZip
                }
            }

            if (foundFileType == FileType.SevenZip)
            {
                SevenZipFile fz = new SevenZipFile();
                fStream.Position = 0;
                ZipReturn zp = fz.ZipFileOpen(fStream);
                if (zp == ZipReturn.ZipGood)
                {
                    bool allZipFound = true;
                    for (int i = 0; i < fz.LocalFilesCount(); i++)
                    {
                        Stream stream;
                        ulong streamSize;
                        if (fz.ZipFileOpenReadStream(i, out stream, out streamSize) != ZipReturn.ZipGood || stream == null)
                        {
                            return false;
                        }

                        if (streamSize <= inMemorySize)
                        {
                            byte[] tmpFile = new byte[streamSize];
                            stream.Read(tmpFile, 0, (int)streamSize);
                            Stream memFs = new System.IO.MemoryStream(tmpFile, false);
                            allZipFound &= ScanAFile(fz.Filename(i), memFs);
                            memFs.Close();
                            memFs.Dispose();
                        }
                        else
                        {
                            string file = @"tmp\" + Guid.NewGuid();
                            if (!Directory.Exists("tmp"))
                            {
                                Directory.CreateDirectory("tmp");
                            }
                            Stream fs = File.OpenWrite(file);
                            ulong sizetogo = streamSize;
                            while (sizetogo > 0)
                            {
                                int sizenow = sizetogo > (ulong)Buffersize ? Buffersize : (int)sizetogo;
                                stream.Read(Buffer, 0, sizenow);
                                fs.Write(Buffer, 0, sizenow);
                                sizetogo -= (ulong)sizenow;
                            }
                            fs.Close();

                            Stream fstreamNext;
                            try
                            {
                                fstreamNext = File.OpenRead(file);
                            }
                            catch
                            {
                                return false;
                            }

                            allZipFound &= ScanAFile(fz.Filename(i), fstreamNext);
                            fstreamNext.Close();
                            fstreamNext.Dispose();
                            File.Delete(file);
                        }
                        fz.ZipFileCloseReadStream();

                    }
                    fz.ZipFileClose();
                    ret |= allZipFound;
                }
                else
                    ret = false;
            }
            return ret;
        }

        private static void ScanADirNew(string directory)
        {
            _bgw.ReportProgress(0, new bgwText("Scanning Dir : " + directory));
            DirectoryInfo di = new DirectoryInfo(directory);

            FileInfo[] fi = di.GetFiles();

            _bgw.ReportProgress(0, new bgwRange2Visible(true));
            _bgw.ReportProgress(0, new bgwSetRange2(fi.Length));

            for (int j = 0; j < fi.Length; j++)
            {
                if (_bgw.CancellationPending)
                    return;

                FileInfo f = fi[j];
                _bgw.ReportProgress(0, new bgwValue2(j));
                _bgw.ReportProgress(0, new bgwText2(f.Name));

                Stream fstreamNext;
                try
                {
                    fstreamNext = File.OpenRead(f.FullName);
                }
                catch
                {
                    return;
                }

                bool fileFound = ScanAFile(f.FullName, fstreamNext);
                fstreamNext.Close();
                fstreamNext.Dispose();
                if (fileFound)
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

        private static bool IsDirectoryEmpty(string path)
        {
            return !Directory.EnumerateFileSystemEntries(path).Any();
        }

        private static string GetFilename(byte[] sha1)
        {
            string path = "";

            bool exists = false;
            int i = 0;
            while (!exists)
            {
                string romRoot = AppSettings.ReadSetting("Depot" + i);
                if (romRoot == null)
                {
                    i++;
                    break;
                }
                else if (!Directory.Exists(romRoot))
                {
                    i++;
                    break;
                }

                path = romRoot + @"\" + VarFix.ToString(sha1[0]) + @"\" +
                         VarFix.ToString(sha1[1]) + @"\" +
                         VarFix.ToString(sha1[2]) + @"\" +
                         VarFix.ToString(sha1[3]) + @"\" +
                         VarFix.ToString(sha1) + ".gz";
                exists = true;
            }

            if (!exists)
            {
                path = @"RomRoot\" + VarFix.ToString(sha1[0]) + @"\" +
                         VarFix.ToString(sha1[1]) + @"\" +
                         VarFix.ToString(sha1[2]) + @"\" +
                         VarFix.ToString(sha1[3]) + @"\" +
                         VarFix.ToString(sha1) + ".gz";
            }

            return path;
        }
    }
}
