using System;
using System.ComponentModel;
using System.Threading;
using RomVaultX.DB;
using RomVaultX.IO;
using RomVaultX.Util;

namespace RomVaultX
{
    public static class DatUpdate
    {
        private static int _datCount;
        private static int _datsProcessed;
        private static BackgroundWorker _bgw;
        public static bool NoFilesInDb;

        public static void ShowDat(string message, string filename)
        {
            _bgw?.ReportProgress(0, new bgwShowError(filename, message));
        }

        public static void SendAndShowDat(string message, string filename)
        {
            _bgw?.ReportProgress(0, new bgwShowError(filename, message));
        }


        public static void UpdateDat(object sender, DoWorkEventArgs e)
        {
            try
            {
                _bgw = sender as BackgroundWorker;
                if (_bgw == null) return;

                Program.SyncCont = e.Argument as SynchronizationContext;
                if (Program.SyncCont == null)
                {
                    _bgw = null;
                    return;
                }

                _bgw.ReportProgress(0, new bgwText("Clearing Found DAT List"));
                Program.db.ClearFoundDATs();

                const string datRoot = @"";
                uint dirId = Program.db.FindOrInsertIntoDir(0, "DatRoot", "DatRoot\\");

                _bgw.ReportProgress(0, new bgwText("Pull File DB into memory"));
                NoFilesInDb = Program.db.SetUpFindAFile();

                _bgw.ReportProgress(0, new bgwText("Finding Dats"));
                _datCount = 0;
                DatCount(datRoot, "DatRoot");

                int dbDatCount = Program.db.DatDBCount();

                bool dropIndex = (_datCount - dbDatCount > 10);

                if (dropIndex)
                {
                    _bgw.ReportProgress(0, new bgwText("Removing Indexes"));
                    Program.db.DropIndex();
                }

                _bgw.ReportProgress(0, new bgwText("Scanning Dats"));
                _datsProcessed = 0;

                _bgw.ReportProgress(0, new bgwSetRange(_datCount - 1));
                Program.db.Begin();
                ScanDirs(dirId, datRoot, "DatRoot");
                Program.db.Commit();

                _bgw.ReportProgress(0, new bgwText("Removing old DATs"));
                Program.db.RemoveNotFoundDATs();

                _bgw.ReportProgress(0, new bgwText("Re-Creating Indexes"));
                Program.db.MakeIndex(_bgw);

                _bgw.ReportProgress(0, new bgwText("Re-calculating DIR Got Totals"));
                Program.db.UpdateGotTotal();

                _bgw.ReportProgress(0, new bgwText("Dat Update Complete"));
                _bgw = null;
                Program.SyncCont = null;
            }
            catch (Exception exc)
            {
                ReportError.UnhandledExceptionHandler(exc);


                _bgw = null;
                Program.SyncCont = null;
            }
        }

        private static void DatCount(string datRoot, string subPath)
        {
            DirectoryInfo di = new DirectoryInfo(Path.Combine(datRoot, subPath));

            DirectoryInfo[] dis = di.GetDirectories();
            foreach (DirectoryInfo d in dis)
                DatCount(datRoot, Path.Combine(subPath, d.Name));

            FileInfo[] fis = di.GetFiles("*.DAT");
            _datCount += fis.Length;

            fis = di.GetFiles("*.XML");
            _datCount += fis.Length;
        }

        private static void ScanDirs(uint dirId, string datRoot, string subPath)
        {
            DirectoryInfo di = new DirectoryInfo(Path.Combine(datRoot, subPath));

            DirectoryInfo[] dis = di.GetDirectories();
            foreach (DirectoryInfo d in dis)
            {
                uint nextDirId = Program.db.FindOrInsertIntoDir(dirId, d.Name, Path.Combine(subPath, d.Name) + "\\");
                ScanDirs(nextDirId, datRoot, Path.Combine(subPath, d.Name));
                if (_bgw.CancellationPending)
                    return;
            }

            FileInfo[] fisDat = di.GetFiles("*.DAT");
            FileInfo[] fisXml = di.GetFiles("*.XML");
            int datcount = fisDat.Length + fisXml.Length;

            ReadDat(fisDat, subPath, dirId, datcount > 1);

            ReadDat(fisXml, subPath, dirId, datcount > 1);
        }

        private static void ReadDat(FileInfo[] fis, string subPath, uint dirId, bool extraDir)
        {
            foreach (FileInfo f in fis)
            {
                _datsProcessed++;
                _bgw.ReportProgress(_datsProcessed);

                uint? datId = Program.db.FindDat(subPath, f.Name, f.LastWriteTime, extraDir);
                if (datId != null)
                {
                    Program.db.SetDatFound((uint)datId);
                    continue;
                }

                _bgw.ReportProgress(0, new bgwText("Dat : " + subPath + @"\" + f.Name));

                RvDat rvDat;
                if (DatReader.DatReader.ReadDat(f.FullName, _bgw, out rvDat))
                {
                    uint nextDirId = dirId;
                    if (extraDir)
                    {
                        string extraDirName=VarFix.CleanFileName(rvDat.GetExtraDirName()); // read this from dat.
                        nextDirId = Program.db.FindOrInsertIntoDir(dirId, extraDirName, Path.Combine(subPath, extraDirName) + "\\");
                    }

                    rvDat.DirId = nextDirId;
                    rvDat.ExtraDir = extraDir;
                    rvDat.Path = subPath;
                    rvDat.DatTimeStamp = f.LastWriteTime;
                    Program.db.Commit();
                    Program.db.Begin();
                    rvDat.DbWrite();
                    Program.db.Commit();
                    Program.db.Begin();
                }

                if (_bgw.CancellationPending)
                    return;
            }
        }
    }
}
