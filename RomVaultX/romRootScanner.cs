using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using Compress;
using Compress.gZip;
using FileHeaderReader;
using RomVaultX.DB;
using RomVaultX.SupportedFiles.GZ;
using RomVaultX.Util;
using RVIO;

namespace RomVaultX
{
    public static class romRootScanner
    {
        private static bool deep;
        private static BackgroundWorker? _bgw;

        public static void ScanFiles(object? sender, DoWorkEventArgs e)
        {
            deep = false;
            _bgw = sender as BackgroundWorker;
            Program.SyncCont = e.Argument as SynchronizationContext;
            if (Program.SyncCont == null)
            {
                _bgw = null;
                return;
            }

            bool any = false;
            int i = 0;
            while (AppSettings.ReadSetting($"Depot{i}") != null)
            {
                any = true;
                ScanRomRoot(AppSettings.ReadSetting($"Depot{i}"));
                i++;
            }

            if (!any)
            {
                ScanRomRoot(@"RomRoot");
            }

            //for (int i = 0; i < 256; i++)
            //    ScanRomRoot(RomRootDir.GetRootDir((byte)i));

            DatUpdate.UpdateGotTotal();
            _bgw?.ReportProgress(0, new BgwText("Scanning Files Complete"));
            _bgw = null;
            Program.SyncCont = null;
        }

        public static void ScanFilesDeep(object? sender, DoWorkEventArgs e)
        {
            deep = true;
            _bgw = sender as BackgroundWorker;
            Program.SyncCont = e.Argument as SynchronizationContext;
            if (Program.SyncCont == null)
            {
                _bgw = null;
                return;
            }

            bool any = false;
            int i = 0;
            while (AppSettings.ReadSetting($"Depot{i}") != null)
            {
                any = true;
                ScanRomRoot(AppSettings.ReadSetting($"Depot{i}"));
                i++;
            }

            if (!any)
            {
                ScanRomRoot(@"RomRoot");
            }

            //for (int i = 0; i < 256; i++)
            //{
            //    ScanRomRoot(RomRootDir.GetRootDir((byte)i));
            //    if (_bgw.CancellationPending)
            //        break;
            //}

            DatUpdate.UpdateGotTotal();
            _bgw?.ReportProgress(0, new BgwText("Scanning Files Complete"));
            _bgw = null;
            Program.SyncCont = null;
        }

        private static void ScanRomRoot(string? directory)
        {
            _bgw?.ReportProgress(0, new BgwText($"Scanning Dir : {directory}"));
            var di = new DirectoryInfo(directory!);

            FileInfo[] fi = di.GetFiles();

            _bgw?.ReportProgress(0, new BgwRange2Visible(true));
            _bgw?.ReportProgress(0, new BgwSetRange2(fi.Length));

            for (int j = 0; j < fi.Length; j++)
            {
                if (_bgw?.CancellationPending != false)
                {
                    return;
                }

                FileInfo f = fi[j];
                _bgw.ReportProgress(0, new BgwValue2(j));
                _bgw.ReportProgress(0, new BgwText2(f.Name));
                string ext = Path.GetExtension(f.Name);

                if (ext.Equals(".gz", StringComparison.OrdinalIgnoreCase))
                {
                    var gZipTest = new GZip();
                    ZipReturn errorcode = gZipTest.ReadGZip(f.FullName, false);
                    gZipTest.sha1Hash = VarFix.CleanMD5SHA1(Path.GetFileNameWithoutExtension(f.Name), 40);
                    gZipTest.Close();

                    if (errorcode != ZipReturn.ZipGood)
                    {
                        _bgw.ReportProgress(0, new BgwShowError(f.FullName, "gz File corrupt"));
                        if (!Directory.Exists("corrupt"))
                            Directory.CreateDirectory("corrupt");
                        File.Move(f.FullName, Path.Combine("corrupt", f.Name));
                        continue;
                    }
                    var tFile = new RvFile
                    {
                        CRC = gZipTest.crc,
                        MD5 = gZipTest.md5Hash,
                        SHA1 = gZipTest.sha1Hash,
                        Size = gZipTest.uncompressedSize,
                        AltType = gZipTest.altType,
                        AltCRC = gZipTest.altcrc,
                        AltMD5 = gZipTest.altmd5Hash,
                        AltSHA1 = gZipTest.altsha1Hash,
                        AltSize = gZipTest.uncompressedAltSize,
                        CompressedSize = gZipTest.compressedSize
                    };

                    FindStatus res = FileNeededTest(tFile);

                    if (res != FindStatus.FoundFileInArchive)
                    {
                        if (deep)
                        {

                            gZipTest = new GZip();
                            try
                            {
                                errorcode = gZipTest.ReadGZip(f.FullName, true);
                                gZipTest.sha1Hash = VarFix.CleanMD5SHA1(Path.GetFileNameWithoutExtension(f.Name), 40);
                                gZipTest.Close();
                            }
                            catch
                            {
                                gZipTest.Close();
                                _bgw.ReportProgress(0, new BgwShowError(f.FullName, "gz Crashed Compression"));
                                if (!Directory.Exists("corrupt"))
                                    Directory.CreateDirectory("corrupt");
                                File.Move(f.FullName, Path.Combine("corrupt", f.Name));
                                continue;
                            }

                            if (errorcode != ZipReturn.ZipGood)
                            {
                                _bgw.ReportProgress(0, new BgwShowError(f.FullName, "gz File corrupt"));
                                if (!Directory.Exists("corrupt"))
                                    Directory.CreateDirectory("corrupt");
                                File.Move(f.FullName, Path.Combine("corrupt", f.Name));
                                continue;
                            }
                        }
                        tFile.DBWrite();
                    }
                }
                if (_bgw.CancellationPending)
                {
                    return;
                }
            }

            DirectoryInfo[] childdi = di.GetDirectories();
            foreach (DirectoryInfo d in childdi)
            {
                if (_bgw?.CancellationPending != false)
                {
                    return;
                }
                ScanRomRoot(d.FullName);
            }
        }


        private static FindStatus FileNeededTest(RvFile tFile)
        {
            // first check to see if we already have it in the file table
            bool inFileDB = RvRomFileMatchup.FindInFiles(tFile); // returns true if found in File table
            return inFileDB ? FindStatus.FoundFileInArchive : FindStatus.FileNeededInArchive;
        }

        private enum FindStatus
        {
            FileUnknown,
            FoundFileInArchive,
            FileNeededInArchive
        }
    }
}
