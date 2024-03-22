﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using Microsoft.Data.Sqlite;
using RomVaultX.DB;
using RomVaultX.Util;
using RVIO;

namespace RomVaultX
{
    public static class DatUpdate
    {
        private static int _datCount;
        private static int _datsProcessed;
        private static BackgroundWorker? _bgw;
        public static bool NoFilesInDb;

        /*********************** dat DB Processing ************************/

        public static SqliteCommand CommandCleanupNotFoundDats
        {
            get
            {
                if (_commandCleanupNotFoundDats == null)
                {
                    _commandCleanupNotFoundDats = new SqliteCommand(@"
                        DELETE FROM ROM
                        WHERE
                            ROM.GameId IN
                            (
                                SELECT
                                    GameId
                                FROM GAME
                                WHERE
                                    GAME.DatId IN ( SELECT DatId FROM DAT WHERE found = 0 )
                            );

                        DELETE FROM GAME
                        WHERE
                            GAME.DatId IN ( SELECT DatId FROM DAT WHERE found = 0 );

                        DELETE FROM DAT WHERE found = 0;

                        DELETE FROM DIR WHERE found = 0;",
                    Program.db.Connection);
                }

                return _commandCleanupNotFoundDats;
            }
        }
        private static SqliteCommand? _commandCleanupNotFoundDats;

        public static SqliteCommand CommandClearFoundDirDats
        {
            get
            {
                if (_commandClearFoundDirDats == null)
                {
                    _commandClearFoundDirDats = new SqliteCommand(@"
                        UPDATE DIR SET found = 0;
                        UPDATE DAT SET found = 0;",
                    Program.db.Connection);
                }

                return _commandClearFoundDirDats;
            }
        }
        private static SqliteCommand? _commandClearFoundDirDats;

        public static SqliteCommand CommandCountDats
        {
            get
            {
                _commandCountDats ??= new SqliteCommand(@"SELECT COUNT(1) FROM DAT", Program.db.Connection);
                return _commandCountDats;
            }
        }
        private static SqliteCommand? _commandCountDats;

        public static SqliteCommand CommandFindDat
        {
            get
            {
                if (_commandFindDat == null)
                {
                    _commandFindDat = new SqliteCommand(@"
                        SELECT
                            DatId
                        FROM DAT
                        WHERE
                            Path = @Path
                            AND Filename = @Filename
                            AND DatTimeStamp = @DatTimeStamp
                            AND ExtraDir = @ExtraDir",
                        Program.db.Connection);

                    _commandFindDat.Parameters.Add(new SqliteParameter("Path", SqliteType.Text));
                    _commandFindDat.Parameters.Add(new SqliteParameter("Filename", SqliteType.Text));
                    _commandFindDat.Parameters.Add(new SqliteParameter("DatTimeStamp", SqliteType.Text));
                    _commandFindDat.Parameters.Add(new SqliteParameter("ExtraDir", SqliteType.Integer));
                }

                return _commandFindDat;
            }
        }
        private static SqliteCommand? _commandFindDat;

        public static SqliteCommand CommandSetDatFound
        {
            get
            {
                if (_commandSetDatFound == null)
                {
                    _commandSetDatFound = new SqliteCommand(@"
                        UPDATE DAT SET found = 1 WHERE DatId = @DatId;
                        UPDATE DIR SET found = 1 WHERE DirId = (SELECT DirId FROM DAT WHERE DatId = @DatId);",
                    Program.db.Connection);

                    _commandSetDatFound.Parameters.Add(new SqliteParameter("DatId", SqliteType.Integer));
                }

                return _commandSetDatFound;
            }
        }
        private static SqliteCommand? _commandSetDatFound;

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
                if (_bgw == null)
                    return;

                Program.SyncCont = e.Argument as SynchronizationContext;
                if (Program.SyncCont == null)
                {
                    _bgw = null;
                    return;
                }

                _bgw.ReportProgress(0, new bgwText("Clearing Found DAT List"));
                ClearFoundDats();

                const string datRoot = @"";
                uint dirId = RvDir.FindOrInsertIntoDir(0, "DatRoot", "DatRoot\\");

                _bgw.ReportProgress(0, new bgwText("Pull File DB into memory"));
                NoFilesInDb = RvFile.FilesinDBCheck();

                _bgw.ReportProgress(0, new bgwText("Finding Dats"));
                _datCount = 0;
                DatCount(datRoot, "DatRoot");

                int dbDatCount = DatDBCount();

                bool dropIndex = false;
                //bool dropIndex = _datCount - dbDatCount > 10;

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
                RemoveNotFoundDATs();

                _bgw.ReportProgress(0, new bgwText("Re-Creating Indexes"));
                Program.db.MakeIndex(_bgw);

                _bgw.ReportProgress(0, new bgwText("Re-calculating DIR Got Totals"));
                UpdateGotTotal();

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
            var di = new DirectoryInfo(Path.Combine(datRoot, subPath));

            DirectoryInfo[] dis = di.GetDirectories();
            foreach (DirectoryInfo d in dis)
            {
                DatCount(datRoot, Path.Combine(subPath, d.Name));
            }

            FileInfo[] fis = di.GetFiles("*.DAT");
            _datCount += fis.Length;

            fis = di.GetFiles("*.XML");
            _datCount += fis.Length;
        }

        private static void ScanDirs(uint dirId, string datRoot, string subPath)
        {
            var di = new DirectoryInfo(Path.Combine(datRoot, subPath));

            DirectoryInfo[] dis = di.GetDirectories();
            foreach (DirectoryInfo d in dis)
            {
                uint nextDirId = RvDir.FindOrInsertIntoDir(dirId, d.Name, Path.Combine(subPath, d.Name) + "\\");
                ScanDirs(nextDirId, datRoot, Path.Combine(subPath, d.Name));
                if (_bgw?.CancellationPending == true)
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
                _bgw?.ReportProgress(_datsProcessed);

                uint? datId = FindDat(subPath, f.Name, f.LastWriteTime, extraDir);
                if (datId != null)
                {
                    SetDatFound((uint)datId);
                    continue;
                }

                _bgw?.ReportProgress(0, new bgwText("Dat : " + subPath + @"\" + f.Name));

                if (DatReader.DatReader.ReadDat(f.FullName, _bgw, out RvDat rvDat))
                {
                    uint nextDirId = dirId;
                    if (extraDir)
                    {
                        string extraDirName = VarFix.CleanFileName(rvDat.GetExtraDirName()); // read this from dat.
                        nextDirId = RvDir.FindOrInsertIntoDir(dirId, extraDirName, Path.Combine(subPath, extraDirName) + "\\");
                    }

                    rvDat.DirId = nextDirId;
                    rvDat.ExtraDir = extraDir;
                    rvDat.Path = subPath;
                    rvDat.DatTimeStamp = f.LastWriteTime;

                    DatSetRemoveUnneededDirs(rvDat);
                    DatSetCheckParentSets(rvDat);
                    DatSetRenameAndRemoveDups(rvDat);

                    if ((rvDat.MergeType ?? "").ToLowerInvariant() == "full")
                        DatSetMergeSets(rvDat);

                    DatSetCheckCollect(rvDat);

                    Program.db.Commit();
                    Program.db.Begin();
                    rvDat.DbWrite();
                    Program.db.Commit();
                    Program.db.Begin();
                }

                if (_bgw?.CancellationPending == true)
                    return;
            }
        }

        private static void DatSetRemoveUnneededDirs(RvDat tDat)
        {
            if (tDat.Games == null)
                return;

            for (int g = 0; g < tDat.Games.Count; g++)
            {
                RvGame tGame = tDat.Games[g];
                for (int r = 0; r < tGame.RomCount - 1; r++)
                {
                    // first find any directories, zero length with filename ending in a '/'
                    // there are RvFiles that are really directories (probably inside a zip file)
                    RvRom? f0 = tGame.Roms?[r];
                    if (f0?.Name == null || f0.Name.Length == 0)
                        continue;

                    if (f0.Name.Substring(f0.Name.Length - 1, 1) != "/")
                        continue;

                    // if the next file contains that found directory, then the directory file can be deleted
                    RvRom? f1 = tGame.Roms?[r + 1];
                    if (f1?.Name == null || f1.Name.Length <= f0.Name.Length)
                        continue;

                    if (f0.Name != f1.Name.Substring(0, f0.Name.Length))
                        continue;

                    tGame.Roms?.RemoveAt(r);
                    r--;
                }
            }
        }

        private static void DatSetCheckParentSets(RvDat tDat)
        {
            if (tDat.Games == null)
                return;

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

                    List<RvGame> lstParentGames = [];
                    FindParentSet(mGame, tDat, ref lstParentGames);

                    // if this set have parents
                    if (lstParentGames.Count == 0)
                        continue;

                    if (mGame.Roms == null)
                        continue;

                    // now loop every ROM in the current set.
                    for (int r = 0; r < mGame.Roms.Count; r++)
                    {
                        // and loop every ROM of every parent set of this current set.
                        // and see if anything can be fixed.
                        bool found = false;

                        // loop the parent sets
                        foreach (RvGame romofGame in lstParentGames)
                        {
                            if (romofGame.Roms == null)
                                continue;

                            // loop the ROMs in the parent sets
                            for (int r1 = 0; r1 < romofGame.Roms.Count; r1++)
                            {
                                // don't search fixes for files marked as nodump
                                if ((mGame.Roms[r].Status == "nodump") || (romofGame.Roms[r1].Status == "nodump"))
                                    continue;

                                // only find fixes if the Name and the Size of the ROMs are the same
                                if ((mGame.Roms[r].Name != romofGame.Roms[r1].Name) || (mGame.Roms[r].Size != romofGame.Roms[r1].Size))
                                    continue;

                                // now check if one of the matching roms has missing or incorrect CRC information
                                bool b1 = mGame.Roms[r].CRC == null;
                                bool b2 = romofGame.Roms[r1].CRC == null;

                                // if one has correct information and the other does not, fix the missing one
                                if (b1 == b2)
                                    continue;

                                if (b1)
                                {
                                    mGame.Roms[r].CRC = ArrByte.Copy(romofGame.Roms[r1].CRC);
                                    mGame.Roms[r].Status = "(CRCFound)";
                                }
                                else
                                {
                                    romofGame.Roms[r1].CRC = ArrByte.Copy(mGame.Roms[r].CRC);
                                    romofGame.Roms[r1].Status = "(CRCFound)";
                                }

                                // flag that a fix was found so that we will go all the way around again.
                                fix = true;
                                found = true;
                                break;
                            }

                            if (found)
                                break;
                        }
                    }
                }
            }
        }

        private static void FindParentSet(RvGame searchGame, RvDat parentDir, ref List<RvGame> lstParentGames)
        {
            string? parentName = searchGame.RomOf;
            if (string.IsNullOrEmpty(parentName) || parentName == searchGame.Name)
                parentName = searchGame.CloneOf;

            if (string.IsNullOrEmpty(parentName) || parentName == searchGame.Name)
                return;

            int intResult = parentDir.ChildNameSearch(parentName!, out int intIndex);
            if (intResult == 0)
            {
                RvGame parentGame = parentDir.Games![intIndex];
                lstParentGames.Add(parentGame);
                FindParentSet(parentGame, parentDir, ref lstParentGames);
            }
        }

        private static void DatSetRenameAndRemoveDups(RvDat tDat)
        {
            if (tDat.Games == null)
                return;

            for (int g = 0; g < tDat.Games.Count; g++)
            {
                RvGame tGame = tDat.Games[g];
                for (int r = 0; r < tGame.RomCount - 1; r++)
                {
                    RvRom f0 = tGame.Roms![r];
                    RvRom f1 = tGame.Roms![r + 1];

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
            for (int g = tDat.Games!.Count - 1; g >= 0; g--)
            {
                RvGame mGame = tDat.Games[g];

                List<RvGame> lstParentGames = [];
                FindParentSet(mGame, tDat, ref lstParentGames);
                while ((lstParentGames.Count > 0) && ((lstParentGames[lstParentGames.Count - 1].IsBios ?? "").ToLowerInvariant() == "yes"))
                {
                    lstParentGames.RemoveAt(lstParentGames.Count - 1);
                }

                if (lstParentGames.Count <= 0)
                    continue;

                RvGame romofGame = lstParentGames[lstParentGames.Count - 1];

                bool founderror = false;
                for (int r = 0; r < mGame.RomCount; r++)
                {
                    string? name = mGame.Roms![r].Name;
                    string? mergename = mGame.Roms[r].Merge;

                    for (int r1 = 0; r1 < romofGame.RomCount; r1++)
                    {
                        if ((name == romofGame.Roms![r1].Name?.ToLowerInvariant() || mergename == romofGame.Roms![r1].Name?.ToLowerInvariant())
                            && (ArrByte.iCompare(mGame.Roms[r].CRC, romofGame.Roms[r1].CRC) != 0 || mGame.Roms[r].Size != romofGame.Roms[r1].Size))
                        {
                            founderror = true;
                        }
                    }
                }

                if (founderror)
                {
                    mGame.RomOf = null;
                    continue;
                }

                for (int r = 0; r < mGame.RomCount; r++)
                {
                    string? name = mGame.Roms![r].Name;
                    string? mergename = mGame.Roms[r].Merge;

                    bool found = false;
                    for (int r1 = 0; r1 < romofGame.RomCount; r1++)
                    {
                        if ((name == romofGame.Roms![r1].Name?.ToLowerInvariant() || mergename == romofGame.Roms![r1].Name?.ToLowerInvariant())
                            && ArrByte.iCompare(mGame.Roms[r].CRC, romofGame.Roms[r1].CRC) == 0
                            && mGame.Roms[r].Size == romofGame.Roms[r1].Size)
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
            if (tDat.Games == null)
                return;

            // now look for merged roms.
            // check if a rom exists in a parent set where the Name,Size and CRC all match.

            for (int g = 0; g < tDat.Games.Count; g++)
            {
                RvGame mGame = tDat.Games[g];
                List<RvGame> lstParentGames = [];
                FindParentSet(mGame, tDat, ref lstParentGames);

                if ((lstParentGames.Count == 0) || (mGame.IsBios?.ToLowerInvariant() == "yes"))
                {
                    for (int r = 0; r < mGame.RomCount; r++)
                    {
                        RomCheckCollect(mGame.Roms![r], false);
                    }
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
                                if (mGame.Roms![r].Name?.ToLowerInvariant() != romofGame.Roms![r1].Name?.ToLowerInvariant())
                                    continue;

                                ulong? Size0 = mGame.Roms[r].Size;
                                ulong? Size1 = romofGame.Roms[r1].Size;
                                if ((Size0 != null) && (Size1 != null) && (Size0 != Size1))
                                    continue;

                                byte[]? CRC0 = mGame.Roms[r].CRC;
                                byte[]? CRC1 = romofGame.Roms[r1].CRC;
                                if ((CRC0 != null) && (CRC1 != null) && !ArrByte.bCompare(CRC0, CRC1))
                                    continue;

                                byte[]? SHA0 = mGame.Roms[r].SHA1;
                                byte[]? SHA1 = romofGame.Roms[r1].SHA1;
                                if ((SHA0 != null) && (SHA1 != null) && !ArrByte.bCompare(SHA0, SHA1))
                                    continue;

                                byte[]? MD50 = mGame.Roms[r].MD5;
                                byte[]? MD51 = romofGame.Roms[r1].MD5;
                                if ((MD50 != null) && (MD51 != null) && !ArrByte.bCompare(MD50, MD51))
                                    continue;

                                // don't merge if only one of the ROM is nodump
                                if (romofGame.Roms[r1].Status == "nodump" != (mGame.Roms[r].Status == "nodump"))
                                    continue;

                                found = true;
                                break;
                            }

                            if (found)
                                break;
                        }

                        RomCheckCollect(mGame.Roms![r], found);
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

            if (ArrByte.bCompare(tRom.CRC, [0, 0, 0, 0]) && tRom.Size == 0)
            {
                tRom.PutInZip = true;
                return;
            }

            tRom.PutInZip = true;
        }

        private static int DatDBCount()
        {
            var res = CommandCountDats.ExecuteScalar();
            if (res != null && res != DBNull.Value)
                return System.Convert.ToInt32(res);

            return 0;
        }

        private static void ClearFoundDats()
        {
            CommandClearFoundDirDats.ExecuteNonQuery();
        }

        private static uint? FindDat(string fulldir, string filename, long DatTimeStamp, bool ExtraDir)
        {
            CommandFindDat.Parameters["Path"].Value = fulldir;
            CommandFindDat.Parameters["Filename"].Value = filename;
            CommandFindDat.Parameters["DatTimeStamp"].Value = DatTimeStamp.ToString();
            CommandFindDat.Parameters["ExtraDir"].Value = ExtraDir;

            var res = CommandFindDat.ExecuteScalar();

            if (res == null || res == DBNull.Value)
                return null;

            return System.Convert.ToUInt32(res);
        }

        private static void SetDatFound(uint datId)
        {
            CommandSetDatFound.Parameters["DatId"].Value = datId;
            CommandSetDatFound.ExecuteNonQuery();
        }

        private static void RemoveNotFoundDATs()
        {
            CommandCleanupNotFoundDats.ExecuteNonQuery();
        }

        public static void UpdateGotTotal()
        {
            Program.db.ExecuteNonQuery(@"
                UPDATE DIR
                SET
                    RomTotal    = null,
                    RomGot      = null,
                    RomNoDump   = null;

                UPDATE DIR
                SET 
                    RomTotal    = (SELECT SUM(RomTotal ) FROM DAT WHERE DAT.DirId = DIR.DirId),
                    RomGot      = (SELECT SUM(RomGot   ) FROM DAT WHERE DAT.DirId = DIR.DirId), 
                    RomNoDump   = (SELECT SUM(RomNoDump) FROM DAT WHERE DAT.DirId = DIR.DirId)
                WHERE
                    (SELECT COUNT(1) FROM DIR AS dir1 WHERE dir1.ParentDirId = DIR.DirId) = 0;");

            var sqlUpdateCounts = new SqliteCommand(@"
                UPDATE DIR SET
                    RomTotal    = (IFNULL((SELECT SUM(dir1.RomTotal ) FROM DIR AS dir1 WHERE dir1.ParentDirId = DIR.DirId), 0)) + (IFNULL((SELECT SUM(RomTotal ) FROM DAT WHERE DAT.DirId = DIR.DirId), 0)),
                    RomGot      = (IFNULL((SELECT SUM(dir1.RomGot   ) FROM DIR AS dir1 WHERE dir1.ParentDirId = DIR.DirId), 0)) + (IFNULL((SELECT SUM(RomGot   ) FROM DAT WHERE DAT.DirId = DIR.DirId), 0)),
                    RomNoDump   = (IFNULL((SELECT SUM(dir1.RomNoDump) FROM DIR AS dir1 WHERE dir1.ParentDirId = DIR.DirId), 0)) + (IFNULL((SELECT SUM(RomNoDump) FROM DAT WHERE DAT.DirId = DIR.DirId), 0))
                WHERE
                    RomTotal IS null
                    AND (SELECT COUNT(1) FROM DIR AS dir1 WHERE dir1.ParentDirId = DIR.DirId AND dir1.RomTotal IS null) = 0;",
            Program.db.Connection);

            var sqlNullCount = new SqliteCommand(@"SELECT COUNT(1) FROM DIR WHERE RomTotal IS null", Program.db.Connection);

            int nullcount;
            do
            {
                sqlUpdateCounts.ExecuteNonQuery();
                var res = sqlNullCount.ExecuteScalar();
                nullcount = System.Convert.ToInt32(res);
            } while (nullcount > 0);

            sqlNullCount.Dispose();
        }
    }
}