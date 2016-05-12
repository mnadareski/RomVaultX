using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;
using System.Data.SQLite;
using RomVaultX.IO;
using RomVaultX.Util;
using Convert = System.Convert;

namespace RomVaultX.DB.NewDB
{

    public class DBSqlite
    {
        private const int DBVersion = 7;
        private string DBFilename;
        public SQLiteConnection Connection;

        public string ConnectToDB()
        {
            DBFilename = AppSettings.ReadSetting("DBFileName");
            if (DBFilename == null)
            {
                AppSettings.AddUpdateAppSettings("DBFileName", "rom");
                
                DBFilename = AppSettings.ReadSetting("DBFileName");
            }
            string dbMemCacheSize = AppSettings.ReadSetting("DBMemCacheSize");
            if (dbMemCacheSize == null)
            {
                // I use 8000000
                AppSettings.AddUpdateAppSettings("DBMemCacheSize", "2000");
                dbMemCacheSize = AppSettings.ReadSetting("DBMemCacheSize");
            }


            DBFilename += DBVersion + ".db3";

            bool datFound = File.Exists(DBFilename);

            Connection = new SQLiteConnection(@"data source=" + DBFilename + ";Version=3");
            Connection.Open();

            ExecuteNonQuery("PRAGMA temp_store = MEMORY");
            ExecuteNonQuery("PRAGMA cache_size = -"+dbMemCacheSize);
            //ExecuteNonQuery("PRAGMA journal_mode = MEMORY");
            ExecuteNonQuery("PRAGMA journal_mode = PERSIST");
            ExecuteNonQuery("PRAGMA threads = 7");
            //ExecuteNonQuery("Attach Database ':memory:' AS 'memdb'");


            string dbCheckOnStartup= AppSettings.ReadSetting("DBCheckOnStartup");
            if (dbCheckOnStartup == null)
            {
                AppSettings.AddUpdateAppSettings("DBCheckOnStartup", "false");
                dbCheckOnStartup = AppSettings.ReadSetting("DBCheckOnStartup");
            }

            if (dbCheckOnStartup.ToLower() == "true")
            {
                DbCommand dbCheck = new SQLiteCommand(@"PRAGMA quick_check;", Connection);
                object res = dbCheck.ExecuteScalar();
                string sRes = res.ToString();

                if (sRes != "ok")
                    return sRes;
            }

            CheckDbVersion(ref datFound);

            InitializeSqlCommands();

            if (!datFound)
                MakeDB();

            MakeIndex();

            return null;
        }

        private void CheckDbVersion(ref bool datFound)
        {
            if (!datFound)
                return;

            int testVersion = 0;
            try
            {

                DbCommand dbVersionCommand = new SQLiteCommand(@"SELECT version from version limit 1", Connection);
                object res = dbVersionCommand.ExecuteScalar();

                if (res != null && res != DBNull.Value)
                    testVersion = System.Convert.ToInt32(res);

                if (testVersion == DBVersion)
                    return;
            }
            catch (Exception)
            {
            }

            Connection.Close();
            File.Delete(DBFilename);
            Connection.Open();
            datFound = false;
        }


        public void ExecuteNonQuery(string query, params object[] args)
        {
            using (SQLiteCommand command = new SQLiteCommand(query, Connection))
            {
                for (int i = 0; i < args.Length; i += 2)
                    command.Parameters.Add(new SQLiteParameter(args[i].ToString(), args[i + 1]));

                command.ExecuteNonQuery();
            }
        }

        private void MakeDB()
        {
            /******** Create Tables ***********/

            ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS [VERSION] (
                    [Version] INTEGER NOT NULL);
                INSERT INTO VERSION (version) VALUES (@Version);",
                "version", DBVersion);

          
            RvDir.CreateTable();
            RvDat.CreateTable();
            RvGame.CreateTable();
            RvFile.CreateTable();
            RvRom.CreateTable();

            /******** Create Triggers ***********/

            /**** FILE Triggers ****/
            /*INSERT*/
            ExecuteNonQuery(@"
                DROP TRIGGER IF EXISTS [FileInsert];
                ");



            /*DELETE*/
            ExecuteNonQuery(@"
                DROP TRIGGER IF EXISTS [FileDelete];
                CREATE TRIGGER IF NOT EXISTS [FileDelete] 
                AFTER DELETE ON [FILES] 
                FOR EACH ROW 
                BEGIN 
                    UPDATE ROM SET 
                        FileId=null,
                        LocalFileHeader=null,
                        LocalFileHeaderOffset=null,
                        LocalFileHeaderLength=null 
                    WHERE 
                        FileId=OLD.FileId;
                END;
            ");


            //**** ROM Triggers ****
            //INSERT
            ExecuteNonQuery(@"
                DROP TRIGGER IF EXISTS [RomInsert];
                CREATE TRIGGER IF NOT EXISTS [RomInsert] 
                AFTER INSERT ON [ROM] 
                FOR EACH ROW
                BEGIN 
                    UPDATE GAME SET
                        RomTotal = RomTotal + 1,
                        RomGot = RomGot + (IFNULL(New.FileId,0)>0),
                        RomNoDump = RomNoDump + (IFNULL(New.status ='nodump' and New.crc is null and New.sha1 is null and New.md5 is null,0)),
                        ZipFileLength=null,
                        ZipFileTimeStamp=null,
                        CentralDirectory=null,
                        CentralDirectoryOffset=null,
                        CentralDirectoryLength=null
                    WHERE 
                        Game.GameId = New.GameId;
                END;
            ");
            //DELETE
            ExecuteNonQuery(@"
                DROP TRIGGER IF EXISTS [RomDelete];
                CREATE TRIGGER IF NOT EXISTS [RomDelete] 
                AFTER DELETE ON [ROM] 
                FOR EACH ROW
                BEGIN 
                    UPDATE GAME SET
                        RomTotal = RomTotal - 1,
                        RomGot = RomGot - (IFNULL(Old.FileId,0)>0),
                        RomNoDump = RomNoDump - (IFNULL(Old.status ='nodump' and Old.crc is null and Old.sha1 is null and Old.md5 is null,0)),
                        ZipFileLength=null,
                        ZipFileTimeStamp=null,
                        CentralDirectory=null,
                        CentralDirectoryOffset=null,
                        CentralDirectoryLength=null
                    WHERE 
                        Game.GameId = Old.GameId;
                END;
            ");
            //UPDATE
            ExecuteNonQuery(@"
                DROP TRIGGER IF EXISTS [RomUpdate];
                CREATE TRIGGER IF NOT EXISTS [RomUpdate]
                AFTER UPDATE ON [ROM]
                FOR EACH ROW WHEN (IFNULL(Old.FileId,0)>0) != (IFNULL(New.FileId,0)>0)
                BEGIN 
                    UPDATE GAME SET
                        RomGot = RomGot - (IFNULL(Old.FileId,0)>0) + (IFNULL(New.FileId,0)>0),
                        ZipFileLength=null,
                        ZipFileTimeStamp=null,
                        CentralDirectory=null,
                        CentralDirectoryOffset=null,
                        CentralDirectoryLength=null
                    WHERE 
                        Game.GameId = New.GameId;
                END;
            ");

            //**** GAME Triggers ****
            //INSERT
            ExecuteNonQuery(@"
                DROP TRIGGER IF EXISTS [GameInsert];
                CREATE TRIGGER IF NOT EXISTS [GameInsert]
                AFTER INSERT ON [GAME]
                FOR EACH ROW
                BEGIN
                    UPDATE DAT SET
                            RomTotal   =RomTotal  + New.RomTotal  , 
                            RomGot     =RomGot    + New.RomGot    ,
                            RomNoDump  =RomNoDump + New.RomNoDump
                    WHERE
                            DatId= New.DatId;
                END;
            ");
            //DELETE
            ExecuteNonQuery(@"
                DROP TRIGGER IF EXISTS [GameDelete];
                CREATE TRIGGER IF NOT EXISTS [GameDelete]
                AFTER DELETE ON [GAME]
                FOR EACH ROW
                BEGIN
                    UPDATE DAT SET 
                            RomTotal   =RomTotal  - Old.RomTotal  ,
                            RomGot     =RomGot    - Old.RomGot    ,
                            RomNoDump  =RomNoDump - Old.RomNoDump
                    WHERE
                            DatId=Old.DatId;
                END;
            ");
            //UPDATE
            ExecuteNonQuery(@"
                DROP TRIGGER IF EXISTS [GameUpdate];
                CREATE TRIGGER IF NOT EXISTS [GameUpdate] 
                AFTER UPDATE ON [GAME] 
                FOR EACH ROW WHEN Old.RomTotal!=New.RomTotal OR Old.RomGot!=New.RomGot 
                BEGIN 
                  UPDATE DAT SET
                            RomTotal   =RomTotal  - Old.RomTotal  + New.RomTotal ,
                            RomGot     =RomGot    - Old.RomGot    + New.RomGot ,
                            RomNoDump  =RomNoDump - Old.RomNoDump + New.RomNoDump
                    WHERE
                            DatId=New.DatId;
                END;
            ");

        }

        public void MakeIndex(BackgroundWorker bgw = null)
        {
            if (bgw != null) { bgw.ReportProgress(0, new bgwRange2Visible(true)); bgw.ReportProgress(0, new bgwSetRange2(6)); };
            if (bgw != null) { bgw.ReportProgress(0, new bgwValue2(0)); bgw.ReportProgress(0, new bgwText2("Creating Index ROM-SHA1")); }
            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [ROMSHA1Index]   ON [ROM]   ([sha1]        ASC);");

            if (bgw != null) { bgw.ReportProgress(0, new bgwValue2(1)); bgw.ReportProgress(0, new bgwText2("Creating Index ROM-MD5")); }
            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [ROMMD5Index]    ON [ROM]   ([md5]         ASC); ");

            if (bgw != null) { bgw.ReportProgress(0, new bgwValue2(2)); bgw.ReportProgress(0, new bgwText2("Creating Index ROM-CRC")); }
            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [ROMCRCIndex]    ON [ROM]   ([crc]         ASC); ");

            if (bgw != null) { bgw.ReportProgress(0, new bgwValue2(3)); bgw.ReportProgress(0, new bgwText2("Creating Index ROM-Size")); }
            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [ROMSizeIndex]   ON [ROM]   ([size]        ASC); ");

            if (bgw != null) { bgw.ReportProgress(0, new bgwValue2(4)); bgw.ReportProgress(0, new bgwText2("Creating Index ROM-FileId")); }
            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [ROMFileIdIndex] ON [ROM]   ([FileId]      ASC); ");

            if (bgw != null) { bgw.ReportProgress(0, new bgwValue2(5)); bgw.ReportProgress(0, new bgwText2("Creating Index ROM-GameId-Name")); }
            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [ROMGameId]      ON [ROM]   ([GameId]      ASC,[name] ASC);");

            if (bgw != null) { bgw.ReportProgress(0, new bgwValue2(6)); bgw.ReportProgress(0, new bgwText2("Indexing Complete")); }

            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [GameDatId]      ON [GAME]  ([DatId]       ASC,[name] ASC);");

            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [FILESHA1]       ON [FILES] ([sha1]        ASC);");
            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [FILEMD5]        ON [FILES] ([md5]         ASC);");
            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [FILECRC]        ON [FILES] ([crc]         ASC);");

            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [DATDIRID]       ON [DAT]   ([DirId]       ASC);");
            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [DIRPARENTDIRID] ON [DIR]   ([ParentDirId] ASC);");
        }
        public void DropIndex()
        {

            ExecuteNonQuery(@"
                DROP INDEX IF EXISTS [ROMSHA1Index];
                DROP INDEX IF EXISTS [ROMMD5Index];
                DROP INDEX IF EXISTS [ROMCRCIndex];
                DROP INDEX IF EXISTS [ROMSizeIndex];
                DROP INDEX IF EXISTS [ROMFileIdIndex];
                DROP INDEX IF EXISTS [ROMGameId];");

        }


        public void Begin()
        {
            ExecuteNonQuery("BEGIN TRANSACTION");
        }

        public void Commit()
        {
            ExecuteNonQuery("COMMIT TRANSACTION");
        }

        public void UpdateGotTotal()
        {
            ExecuteNonQuery(@"

            UPDATE DIR SET RomTotal=null, ROMGot=null,RomNoDump=null;

            UPDATE DIR SET 
                RomTotal = (SELECT SUM(RomTotal) FROM Dat WHERE dat.dirid=dir.dirid) ,
                RomGot = (SELECT SUM(RomGot) FROM dat WHERE dat.dirid=dir.dirid) , 
                RomNoDump = (SELECT SUM(RomNoDump) FROM dat WHERE dat.dirid=dir.dirid)
            WHERE
                (SELECT COUNT(1) FROM dir AS dir1 WHERE dir1.parentdirId=dir.dirid)=0;
            ");

            SQLiteCommand sqlUpdateCounts = new SQLiteCommand(@"
                    UPDATE dir SET
                        romTotal =(IFNULL((SELECT SUM(dir1.romTotal ) FROM dir AS dir1 WHERE dir1.parentdirid=dir.dirid),0)) + (IFNULL((SELECT SUM(RomTotal ) FROM Dat WHERE dat.dirid=dir.dirid),0)),
                        romGot   =(IFNULL((SELECT SUM(dir1.romGot   ) FROM dir AS dir1 WHERE dir1.parentdirid=dir.dirid),0)) + (IFNULL((SELECT SUM(RomGot   ) FROM Dat WHERE dat.dirid=dir.dirid),0)),
                        romNodump=(IFNULL((SELECT SUM(dir1.romNodump) FROM dir AS dir1 WHERE dir1.parentdirid=dir.dirid),0)) + (IFNULL((SELECT SUM(RomNoDump) FROM Dat WHERE dat.dirid=dir.dirid),0))
                    WHERE
                        romtotal IS null AND
                        (SELECT COUNT(1) FROM dir AS dir1 WHERE dir1.parentdirid=dir.dirid AND dir1.romtotal IS null) = 0;", Connection);

            SQLiteCommand sqlNullCount = new SQLiteCommand(@"SELECT COUNT(1) FROM dir WHERE RomTotal IS null", Connection);

            int nullcount;
            do
            {
                sqlUpdateCounts.ExecuteNonQuery();
              
                object res = sqlNullCount.ExecuteScalar();
                nullcount = Convert.ToInt32(res);

            } while (nullcount > 0);
            sqlNullCount.Dispose();
        }




        private SQLiteCommand CommandClearfoundDirDATs;
        private SQLiteCommand CommandCleanupNotFoundDATs;
        private SQLiteCommand CommandCountDATs;

        private SQLiteCommand CommandFindDat;
        private SQLiteCommand CommandSetDatFound;


        private SQLiteCommand CommandFindInDir;
        private SQLiteCommand CommandSetDirFound;
        private SQLiteCommand CommandInsertIntoDir;




        private SQLiteCommand CommandReadTree;
        private SQLiteCommand CommandSetTreeExpanded;

        private SQLiteCommand CommandGetFirstExpanded;
       // private SQLiteCommand CommandUpdateExpanded;


        private SQLiteCommand CommandWriteLocalHeaderToRom;
        private SQLiteCommand CommandWriteCentralDirToGame;
        private SQLiteCommand CommandGetAllGamesWithRoms;
        private SQLiteCommand CommandFindRomsInGame;

        private void InitializeSqlCommands()
        {
         

          


          
          


        


           







            CommandClearfoundDirDATs = new SQLiteCommand(@"
                    UPDATE DIR SET Found=0;
                    UPDATE DAT SET Found=0;
                ", Connection);

            CommandCleanupNotFoundDATs = new SQLiteCommand(@"
                delete from rom where rom.GameId in
                (
                    select gameid from game where game.datid in
                    (
                        select datId from dat where found=0
                    )
                );

                delete from game where game.datid in
                (
                    select datId from dat where found=0
                );

                delete from dat where found=0;

                delete from dir where found=0;
            ", Connection);


            CommandCountDATs = new SQLiteCommand(@"
                select count(1) from dat
            ", Connection);




            CommandFindDat = new SQLiteCommand(@"
                SELECT DatId FROM Dat WHERE path=@path AND Filename=@filename AND DatTimeStamp=@DatTimeStamp AND ExtraDir=@ExtraDir
            ", Connection);
            CommandFindDat.Parameters.Add(new SQLiteParameter("path"));
            CommandFindDat.Parameters.Add(new SQLiteParameter("filename"));
            CommandFindDat.Parameters.Add(new SQLiteParameter("DatTimeStamp"));
            CommandFindDat.Parameters.Add(new SQLiteParameter("ExtraDir"));

            CommandSetDatFound = new SQLiteCommand(@"
                Update Dat SET Found=1 WHERE DatId=@DatId;
                Update Dir SET Found=1 WHERE DirId=(select DirId from Dat WHERE DatId=@DatId);
            ", Connection);
            CommandSetDatFound.Parameters.Add(new SQLiteParameter("DatId"));



            CommandFindInDir = new SQLiteCommand(@"SELECT DirId FROM dir WHERE fullname=@fullname LIMIT 1", Connection);
            CommandFindInDir.Parameters.Add(new SQLiteParameter("fullname"));

            CommandSetDirFound = new SQLiteCommand(@"Update Dir SET Found=1 WHERE DirId=@DirId", Connection);
            CommandSetDirFound.Parameters.Add(new SQLiteParameter("DirId"));

            CommandInsertIntoDir = new SQLiteCommand(@"
                    INSERT INTO DIR (ParentDirId,Name,FullName)
                         VALUES (@ParentDirId,@Name,@FullName);

                         SELECT last_insert_rowid();
                    ", Connection);

            CommandInsertIntoDir.Parameters.Add(new SQLiteParameter("ParentDirId"));
            CommandInsertIntoDir.Parameters.Add(new SQLiteParameter("Name"));
            CommandInsertIntoDir.Parameters.Add(new SQLiteParameter("FullName"));



            CommandReadTree = new SQLiteCommand(@"
                    SELECT 
                        dir.DirId as DirId,
                        dir.name as dirname,
                        dir.fullname,
                        dir.expanded,
                        dir.RomTotal as dirRomTotal,
                        dir.RomGot as dirRomGot,
                        dir.RomNoDump as dirNoDump,
                        dat.DatId,
                        dat.name as datname,
                        dat.description,
                        dat.RomTotal,
                        dat.RomGot,
                        dat.RomNoDump
                    FROM dir LEFT JOIN dat ON dir.DirId=dat.DirId
                    ORDER BY dir.Fullname,dat.Filename", Connection);

            CommandSetTreeExpanded = new SQLiteCommand(@"
                    UPDATE dir SET expanded=@expanded WHERE DirId=@dirId", Connection);
            CommandSetTreeExpanded.Parameters.Add(new SQLiteParameter("expanded"));
            CommandSetTreeExpanded.Parameters.Add(new SQLiteParameter("dirId"));


            CommandGetFirstExpanded = new SQLiteCommand(@"
                SELECT expanded FROM dir WHERE ParentDirId=@DirId ORDER BY fullname LIMIT 1
            ", Connection);
            CommandGetFirstExpanded.Parameters.Add(new SQLiteParameter("DirId"));




            CommandWriteLocalHeaderToRom = new SQLiteCommand(
               @"UPDATE ROM SET 
                    LocalFileHeader=@localFileHeader,
                    LocalFileHeaderOffset=@localFileHeaderOffset,
                    LocalFileHeaderLength=@localFileHeaderLength
                WHERE
                    RomId=@romID", Connection);
            CommandWriteLocalHeaderToRom.Parameters.Add(new SQLiteParameter("localFileHeader"));
            CommandWriteLocalHeaderToRom.Parameters.Add(new SQLiteParameter("localFileHeaderOffset"));
            CommandWriteLocalHeaderToRom.Parameters.Add(new SQLiteParameter("localFileHeaderLength"));
            CommandWriteLocalHeaderToRom.Parameters.Add(new SQLiteParameter("RomId"));



            CommandWriteCentralDirToGame = new SQLiteCommand(
               @"UPDATE GAME SET 
                    ZipFileLength=@zipFileLength,
                    ZipFileTimeStamp=@zipFileTimeStamp,
                    CentralDirectory=@centralDirectory,
                    CentralDirectoryOffset=@centralDirectoryOffset,
                    CentralDirectoryLength=@centralDirectoryLength
                WHERE
                    GameId=@gameID", Connection);
            CommandWriteCentralDirToGame.Parameters.Add(new SQLiteParameter("zipFileLength"));
            CommandWriteCentralDirToGame.Parameters.Add(new SQLiteParameter("zipFileTimeStamp"));
            CommandWriteCentralDirToGame.Parameters.Add(new SQLiteParameter("centralDirectory"));
            CommandWriteCentralDirToGame.Parameters.Add(new SQLiteParameter("centralDirectoryOffset"));
            CommandWriteCentralDirToGame.Parameters.Add(new SQLiteParameter("centralDirectoryLength"));
            CommandWriteCentralDirToGame.Parameters.Add(new SQLiteParameter("GameId"));

            CommandGetAllGamesWithRoms = new SQLiteCommand(@"SELECT GameId,name FROM game WHERE RomGot>0 AND ZipFileLength is null", Connection);

            CommandFindRomsInGame = new SQLiteCommand(
                @"SELECT
                    ROM.RomId, 
                    ROM.name,
                    FILES.size,
                    FILES.compressedsize,
                    FILES.crc
                 FROM ROM,FILES WHERE ROM.FileId=FILES.FileId AND ROM.GameId=@GameId AND ROM.PutInZip ORDER BY ROM.RomId", Connection);
            CommandFindRomsInGame.Parameters.Add(new SQLiteParameter("GameId"));

        }

        

       

        public void ClearFoundDATs()
        {
            CommandClearfoundDirDATs.ExecuteNonQuery();
        }

        public void RemoveNotFoundDATs()
        {
            CommandCleanupNotFoundDATs.ExecuteNonQuery();
        }

        public int DatDBCount()
        {
            object res = CommandCountDATs.ExecuteScalar();

            if (res != null && res != DBNull.Value)
                return Convert.ToInt32(res);

            return 0;
        }




        public bool SetUpFindAFile()
        {
           SQLiteCommand count = new SQLiteCommand("SELECT COUNT(1) FROM FILES LIMIT 1", Connection);
            object res = count.ExecuteScalar();
            count.Dispose();
            if (res == null || res == DBNull.Value)
                return true;
            return Convert.ToInt32(res) == 0;
        }

        public uint? FindDat(string fulldir, string filename, long DatTimeStamp, bool ExtraDir)
        {
            CommandFindDat.Parameters["path"].Value = fulldir;
            CommandFindDat.Parameters["filename"].Value = filename;
            CommandFindDat.Parameters["DatTimeStamp"].Value = DatTimeStamp.ToString();
            CommandFindDat.Parameters["ExtraDir"].Value = ExtraDir;

            object res = CommandFindDat.ExecuteScalar();

            if (res == null || res == DBNull.Value)
                return null;
            return Convert.ToUInt32(res);

        }

        public void SetDatFound(uint datId)
        {
            CommandSetDatFound.Parameters["DatId"].Value = datId;
            CommandSetDatFound.ExecuteNonQuery();

        }


        private uint? FindInDir(string fullname)
        {
            CommandFindInDir.Parameters["FullName"].Value = fullname;
            object resFind = CommandFindInDir.ExecuteScalar();
            if (resFind == null || resFind == DBNull.Value)
                return null;

            return (uint?)Convert.ToInt32(resFind);
        }


        private void SetDirFound(uint foundDatId)
        {
            CommandSetDirFound.Parameters["DirId"].Value = foundDatId;
            CommandSetDirFound.ExecuteNonQuery();
        }

        private uint InsertIntoDir(uint parentDirId, string name, string fullName)
        {
            CommandInsertIntoDir.Parameters["ParentDirId"].Value = parentDirId;
            CommandInsertIntoDir.Parameters["Name"].Value = name;
            CommandInsertIntoDir.Parameters["FullName"].Value = fullName;

            object res = CommandInsertIntoDir.ExecuteScalar();

            if (res == null || res == DBNull.Value)
                return 0;
            return Convert.ToUInt32(res);

        }

        public uint FindOrInsertIntoDir(uint parentDirId, string name, string fullName)
        {

            uint? foundDatId = FindInDir(fullName);
            if (foundDatId != null)
            {
                SetDirFound((uint)foundDatId);
                return (uint)foundDatId;
            }

            return InsertIntoDir(parentDirId, name, fullName);
        }







        public DbDataReader CommandReadTreeGetReader()
        {
            return CommandReadTree.ExecuteReader();
        }

        public void SetTreeExpanded(uint DirId, bool expanded)
        {
            CommandSetTreeExpanded.Parameters["dirId"].Value = DirId;
            CommandSetTreeExpanded.Parameters["expanded"].Value = expanded;
            CommandSetTreeExpanded.ExecuteNonQuery();
        }


        public int? GetFirstExpanded(uint DirId)
        {
            CommandGetFirstExpanded.Parameters["DirId"].Value = DirId;
            object res = CommandGetFirstExpanded.ExecuteScalar();
            if (res == null || res == DBNull.Value)
                return null;
            return Convert.ToInt32(res);
        }

        public void UpdateSelectedFromList(List<uint> todo, int value)
        {
            string todoList = string.Join(",", todo);
            using (DbCommand SetStatus = new SQLiteCommand(@"UPDATE dir SET expanded=" + value + " WHERE ParentDirId in (" + todoList + ")", Connection))
            {
                SetStatus.ExecuteNonQuery();
            }
        }

        public List<uint> UpdateSelectedGetChildList(List<uint> todo)
        {
            string todoList = string.Join(",", todo);
            List<uint> retList = new List<uint>();
            using (DbCommand GetChild = new SQLiteCommand(@"select DirId from dir where ParentDirId in (" + todoList + ")", Connection))
            {
                using (DbDataReader dr = GetChild.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        uint id = Convert.ToUInt32(dr["DirId"]);
                        retList.Add(id);
                    }
                    dr.Close();
                }
            }
            return retList;
        }



        public void ZipSetLocalFileHeader(int RomId, byte[] localHeader, ulong fileOffset)
        {
            CommandWriteLocalHeaderToRom.Parameters["localFileHeader"].Value = localHeader;
            CommandWriteLocalHeaderToRom.Parameters["localFileHeaderOffset"].Value = fileOffset;
            CommandWriteLocalHeaderToRom.Parameters["localFileHeaderLength"].Value = localHeader.Length;
            CommandWriteLocalHeaderToRom.Parameters["RomId"].Value = RomId;
            CommandWriteLocalHeaderToRom.ExecuteNonQuery();
        }


        public void ZipSetCentralFileHeader(int GameId, ulong zipFileLength, long timestamp, byte[] centeralDir, ulong fileOffset)
        {
            CommandWriteCentralDirToGame.Parameters["zipFileLength"].Value = zipFileLength;
            CommandWriteCentralDirToGame.Parameters["zipFileTimeStamp"].Value = timestamp;
            CommandWriteCentralDirToGame.Parameters["centralDirectory"].Value = centeralDir;
            CommandWriteCentralDirToGame.Parameters["centralDirectoryOffset"].Value = fileOffset;
            CommandWriteCentralDirToGame.Parameters["centralDirectoryLength"].Value = centeralDir.Length;
            CommandWriteCentralDirToGame.Parameters["GameId"].Value = GameId;
            CommandWriteCentralDirToGame.ExecuteNonQuery();
        }

        public DbDataReader ZipSetGetAllGames()
        {
            return CommandGetAllGamesWithRoms.ExecuteReader();
        }

        public DbDataReader ZipSetGetRomsInGame(int GameId)
        {
            CommandFindRomsInGame.Parameters["GameId"].Value = GameId;
            return CommandFindRomsInGame.ExecuteReader();
        }

        public DbCommand Command(string command)
        {
            return new SQLiteCommand(command, Connection);
        }

        public DbParameter Parameter(string param, object value)
        {
            return new SQLiteParameter(param, value);
        }
    }
}
