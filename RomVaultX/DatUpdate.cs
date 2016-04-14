using System;
using System.ComponentModel;
using System.Threading;
using RomVaultX.DB;
using RomVaultX.DB.DBAccess;
using RomVaultX.IO;

namespace RomVaultX
{
    public static class DatUpdate
    {
        private static int _datCount;
        private static int _datsProcessed;
        private static BackgroundWorker _bgw;
        public static bool NoFilesInDB;

        public static void ShowDat(string message, string filename)
        {
            if (_bgw != null)
                _bgw.ReportProgress(0, new bgwShowError(filename, message));
        }
        public static void SendAndShowDat(string message, string filename)
        {
            if (_bgw != null)
                _bgw.ReportProgress(0, new bgwShowError(filename, message));
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
                DataAccessLayer.ClearFoundDATs();

                const string datRoot = @"";
                uint DirId = FindOrInsert.FindOrInsertIntoDir(0, "DatRoot", "DatRoot\\");

                _bgw.ReportProgress(0, new bgwText("Pull File DB into memory"));
                NoFilesInDB = FindAFile.copyDBtoMem();

                _bgw.ReportProgress(0, new bgwText("Finding Dats"));
                _datCount = 0;
                DatCount(datRoot, "DatRoot");

                int dbDatCount = DataAccessLayer.DatDBCount();
                bool dropIndex = false;

                dropIndex = (_datCount - dbDatCount > 10);

                if (dropIndex)
                {
                    _bgw.ReportProgress(0, new bgwText("Removing Indexes"));
                    DataAccessLayer.DropIndex();
                }

                _bgw.ReportProgress(0, new bgwText("Scanning Dats"));
                _datsProcessed = 0;

                _bgw.ReportProgress(0, new bgwSetRange(_datCount - 1));
                DataAccessLayer.Begin();
                ReadDats(DirId, datRoot, "DatRoot");
                DataAccessLayer.Commit();

                _bgw.ReportProgress(0, new bgwText("Removing old DATs"));
                DataAccessLayer.RemoveNotFoundDATs();

                if (dropIndex)
                {
                    _bgw.ReportProgress(0, new bgwText("Re-Creating Indexes"));
                    DataAccessLayer.MakeIndex();
                }

                _bgw.ReportProgress(0, new bgwText("Re-calculating DIR Got Totals"));
                DataAccessLayer.UpdateGotTotal();

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

        private static void ReadDats(uint ParentId, string datRoot, string subPath)
        {
            DirectoryInfo di = new DirectoryInfo(Path.Combine(datRoot, subPath));

            DirectoryInfo[] dis = di.GetDirectories();
            foreach (DirectoryInfo d in dis)
            {
                uint DirId = FindOrInsert.FindOrInsertIntoDir(ParentId, d.Name, Path.Combine(subPath, d.Name) + "\\");
                ReadDats(DirId, datRoot, Path.Combine(subPath, d.Name));
                if (_bgw.CancellationPending)
                    return;
            }

            FileInfo[] fis = di.GetFiles("*.DAT");
            ReadDat(fis, subPath, ParentId);

            fis = di.GetFiles("*.XML");
            ReadDat(fis, subPath, ParentId);
        }

        private static void ReadDat(FileInfo[] fis, string subPath, uint ParentId)
        {
            foreach (FileInfo f in fis)
            {
                _datsProcessed++;
                _bgw.ReportProgress(_datsProcessed);

                uint datId = FindDAT.Execute(subPath, f.Name, f.LastWriteTime);
                if (datId > 0)
                    continue;

                _bgw.ReportProgress(0, new bgwText("Dat : " + subPath + @"\" + f.Name));

                RvDat rvDat;
                if (DatReader.DatReader.ReadDat(f.FullName, f.LastWriteTime, _bgw, out rvDat))
                {
                    rvDat.DirId = ParentId;
                    DataAccessLayer.Commit();
                    DataAccessLayer.Begin();
                    rvDat.DbWrite();
                    DataAccessLayer.Commit();
                    DataAccessLayer.Begin();
                }

                if (_bgw.CancellationPending)
                    return;
            }
        }
    }
}
