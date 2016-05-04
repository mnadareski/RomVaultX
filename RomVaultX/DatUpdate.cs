using System;
using System.Collections.Generic;
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
                        string extraDirName = VarFix.CleanFileName(rvDat.GetExtraDirName()); // read this from dat.
                        nextDirId = Program.db.FindOrInsertIntoDir(dirId, extraDirName, Path.Combine(subPath, extraDirName) + "\\");
                    }

                    rvDat.DirId = nextDirId;
                    rvDat.ExtraDir = extraDir;
                    rvDat.Path = subPath;
                    rvDat.DatTimeStamp = f.LastWriteTime;



                    DatSetRemoveUnneededDirs(rvDat);
                    DatSetCheckParentSets(rvDat);
                    DatSetRenameAndRemoveDups(rvDat);


                    if ((rvDat.MergeType??"full").ToLower() == "full")
                        DatSetMergeSets(rvDat);

                    DatSetCheckCollect(rvDat);

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


        private static void DatSetRemoveUnneededDirs(RvDat tDat)
        {
            for (int g = 0; g < tDat.Games.Count; g++)
            {
                RvGame tGame = tDat.Games[g];
                for (int r = 0; r < tGame.RomCount - 1; r++)
                {
                    // first find any directories, zero length with filename ending in a '/'
                    // there are RvFiles that are really directories (probably inside a zip file)
                    RvRom f0 = tGame.Roms[r];
                    if (f0.Name.Length == 0)
                        continue;
                    if (f0.Name.Substring(f0.Name.Length - 1, 1) != "/")
                        continue;

                    // if the next file contains that found directory, then the directory file can be deleted
                    RvRom f1 = tGame.Roms[r + 1];
                    if (f1.Name.Length <= f0.Name.Length)
                        continue;

                    if (f0.Name != f1.Name.Substring(0, f0.Name.Length))
                        continue;

                    tGame.Roms.RemoveAt(r);
                    r--;
                }
            }
        }



        private static void DatSetCheckParentSets(RvDat tDat)
        {
            // First we are going to try and fix any missing CRC information by checking for roms with the same names
            // in Parent and Child sets, and if the same named rom is found and one has a CRC and the other does not
            // then we will set the missing CRC by using the CRC in the other set.

            // we keep trying to find fixes until no more fixes are found.
            // this is need as the first time round a fix could be found in a parent set from one child set.
            // then the second time around that fixed parent set could fix another of its childs sets.

            bool fix = true;
            while (fix)
            {
                fix = false;

                // loop around every ROM Set looking for fixes.
                for (int g = 0; g < tDat.Games.Count; g++)
                {

                    // get a list of that ROM Sets parents.
                    RvGame mGame = tDat.Games[g];

                    List<RvGame> lstParentGames = new List<RvGame>();
                    FindParentSet(mGame, tDat, ref lstParentGames);

                    // if this set have parents
                    if (lstParentGames.Count == 0)
                        continue;

                    // now loop every ROM in the current set.
                    for (int r = 0; r < mGame.RomCount; r++)
                    {
                        // and loop every ROM of every parent set of this current set.
                        // and see if anything can be fixed.
                        bool found = false;

                        // loop the parent sets
                        foreach (RvGame romofGame in lstParentGames)
                        {
                            // loop the ROMs in the parent sets
                            for (int r1 = 0; r1 < romofGame.RomCount; r1++)
                            {
                                // only find fixes if the Name and the Size of the ROMs are the same
                                if (mGame.Roms[r].Name != romofGame.Roms[r1].Name || mGame.Roms[r].Size != romofGame.Roms[r1].Size)
                                    continue;

                                // now check if one of the matching roms has missing or incorrect CRC information
                                bool b1 = mGame.Roms[r].CRC == null;
                                bool b2 = romofGame.Roms[r1].CRC == null;

                                // if one has correct information and the other does not, fix the missing one
                                if (b1 == b2)
                                    continue;

                                if (b1)
                                {
                                    mGame.Roms[r].CRC = romofGame.Roms[r1].CRC;
                                    mGame.Roms[r].Status = "(CRCFound)";
                                }
                                else
                                {
                                    romofGame.Roms[r1].CRC = mGame.Roms[r].CRC;
                                    romofGame.Roms[r1].Status = "(CRCFound)";
                                }

                                // flag that a fix was found so that we will go all the way around again.
                                fix = true;
                                found = true;
                                break;
                            }
                            if (found) break;
                        }
                    }
                }
            }
        }

        private static void FindParentSet(RvGame searchGame, RvDat parentDir, ref List<RvGame> lstParentGames)
        {
            if (String.IsNullOrEmpty(searchGame.RomOf))
                return;

            int intIndex;
            string searchRom = searchGame.RomOf;
            if (searchRom == searchGame.Name)
                searchRom = searchGame.CloneOf;
            if (String.IsNullOrEmpty(searchRom))
                return;
            if (searchRom == searchGame.Name)
                return;

            int intResult = parentDir.ChildNameSearch(searchRom, out intIndex);
            if (intResult == 0)
            {
                RvGame parentGame = parentDir.Games[intIndex];
                lstParentGames.Add(parentGame);
                FindParentSet(parentGame, parentDir, ref lstParentGames);
            }
        }


        private static void DatSetRenameAndRemoveDups(RvDat tDat)
        {
            for (int g = 0; g < tDat.Games.Count; g++)
            {
                RvGame tGame = tDat.Games[g];
                for (int r = 0; r < tGame.RomCount - 1; r++)
                {
                    RvRom f0 = tGame.Roms[r];
                    RvRom f1 = tGame.Roms[r + 1];

                    if (f0.Name != f1.Name)
                        continue;

                    if (f0.Size != f1.Size || ArrByte.iCompare(f0.CRC, f1.CRC) != 0)
                    {
                        tGame.Roms.RemoveAt(r + 1); // remove F1
                        f1.Name = f1.Name + "_" + ArrByte.ToString(f1.CRC); // rename F1;
                        int pos = tGame.AddRom(f1);
                        // if this rename moved the File back up the list, start checking again from that file.
                        if (pos < r)
                            r = pos;
                    }
                    else
                    {

                        tGame.Roms.RemoveAt(r + 1);
                    }
                    r--;
                }
            }
        }


        private static void DatSetMergeSets(RvDat tDat)
        {
            for (int g = tDat.Games.Count - 1; g >= 0; g--)
            {
                RvGame mGame = tDat.Games[g];

                List<RvGame> lstParentGames = new List<RvGame>();
                FindParentSet(mGame, tDat, ref lstParentGames);
                while (lstParentGames.Count > 0 && (lstParentGames[lstParentGames.Count - 1].IsBios??"").ToLower() == "yes")
                    lstParentGames.RemoveAt(lstParentGames.Count - 1);

                if (lstParentGames.Count <= 0) continue;

                RvGame romofGame = lstParentGames[lstParentGames.Count - 1];

                bool founderror = false;
                for (int r = 0; r < mGame.RomCount; r++)
                {
                    string name = mGame.Roms[r].Name;
                    string mergename = mGame.Roms[r].Merge;

                    for (int r1 = 0; r1 < romofGame.RomCount; r1++)
                    {
                        if ((name == romofGame.Roms[r1].Name || mergename == romofGame.Roms[r1].Name) &&
                             ArrByte.iCompare(mGame.Roms[r].CRC, romofGame.Roms[r1].CRC) != 0 ||
                             mGame.Roms[r].Size != romofGame.Roms[r1].Size)
                            founderror = true;

                    }
                }
                if (founderror)
                {
                    mGame.RomOf = null;
                    continue;
                }

                for (int r = 0; r < mGame.RomCount; r++)
                {
                    string name = mGame.Roms[r].Name;
                    string mergename = mGame.Roms[r].Merge;

                    bool found = false;
                    for (int r1 = 0; r1 < romofGame.RomCount; r1++)
                    {
                        if ((name == romofGame.Roms[r1].Name || mergename == romofGame.Roms[r1].Name) &&
                            ArrByte.iCompare(mGame.Roms[r].CRC, romofGame.Roms[r1].CRC) == 0 &&
                            mGame.Roms[r].Size == romofGame.Roms[r1].Size)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                        romofGame.AddRom(mGame.Roms[r]);
                }
                tDat.Games.RemoveAt(g);
            }
        }


        private static void DatSetCheckCollect(RvDat tDat)
        {
            // now look for merged roms.
            // check if a rom exists in a parent set where the Name,Size and CRC all match.

            for (int g = 0; g < tDat.Games.Count; g++)
            {
                RvGame mGame = tDat.Games[g];
                List<RvGame> lstParentGames = new List<RvGame>();
                FindParentSet(mGame, tDat, ref lstParentGames);

                if (lstParentGames.Count == 0 || mGame.IsBios.ToLower() == "yes")
                {
                    for (int r = 0; r < mGame.RomCount; r++)
                        RomCheckCollect(mGame.Roms[r], false);
                }
                else
                {
                    for (int r = 0; r < mGame.RomCount; r++)
                    {
                        bool found = false;
                        foreach (RvGame romofGame in lstParentGames)
                        {
                            for (int r1 = 0; r1 < romofGame.RomCount; r1++)
                            {
                                if (mGame.Roms[r].Name != romofGame.Roms[r1].Name ||
                                    !ArrByte.bCompare(mGame.Roms[r].CRC, romofGame.Roms[r1].CRC) ||
                                    mGame.Roms[r].Size != romofGame.Roms[r1].Size)
                                    continue;

                                found = true;
                                break;
                            }
                            if (found) break;
                        }
                        RomCheckCollect(mGame.Roms[r], found);
                    }
                }
            }
        }


        private static void RomCheckCollect(RvRom tRom, bool merge)
        {
            if (merge)
            {
                if (string.IsNullOrEmpty(tRom.Merge))
                    tRom.Merge = "(Auto Merged)";

                tRom.PutInZip = false;
                return;
            }

            if (!string.IsNullOrEmpty(tRom.Merge))
                tRom.Merge = "(No-Merge) " + tRom.Merge;

            if (tRom.Status == "nodump")
            {
                tRom.CRC = null;
                tRom.PutInZip = false;
                return;
            }

            if (ArrByte.bCompare(tRom.CRC, new byte[] { 0, 0, 0, 0 }) && tRom.Size == 0)
            {
                tRom.PutInZip = true;
                return;
            }

            /*
            if (ArrByte.bCompare(tRom.CRC, new byte[] { 0, 0, 0, 0 }) || (tRom.CRC.Length != 8))
            {
                tRom.CRC = null;
                tRom.DatStatus = DatStatus.InDatBad;
                return;
            }
            */

            tRom.PutInZip = true;
        }


    }
}