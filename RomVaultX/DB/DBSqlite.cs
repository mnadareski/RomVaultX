using System;
using System.ComponentModel;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using RomVaultX.Util;
using RVIO;

namespace RomVaultX.DB
{
    public class DBSqlite
    {
        private const int DBVersion = 7;
        private string? _dbFilename;
        public SqliteConnection? Connection;

        public string? ConnectToDB()
        {
            _dbFilename = AppSettings.ReadSetting("DBFileName");
            if (_dbFilename == null)
            {
                AppSettings.AddUpdateAppSettings("DBFileName", "rom");
                _dbFilename = AppSettings.ReadSetting("DBFileName");
            }

            string? dbMemCacheSize = AppSettings.ReadSetting("DBMemCacheSize");
            if (dbMemCacheSize == null)
            {
                // I use 8000000
                AppSettings.AddUpdateAppSettings("DBMemCacheSize", "8000000");
                dbMemCacheSize = AppSettings.ReadSetting("DBMemCacheSize");
            }

            _dbFilename += DBVersion + ".db3";

            bool datFound = File.Exists(_dbFilename);

            var builder = new SqliteConnectionStringBuilder();
            builder.DataSource = _dbFilename;
            builder.Mode = SqliteOpenMode.ReadWriteCreate;
            Connection = new SqliteConnection(builder.ConnectionString);

            Connection.Open();

            // ExecuteNonQuery("PRAGMA temp_store = MEMORY");
            ExecuteNonQuery("PRAGMA temp_store = FILE");
            ExecuteNonQuery("PRAGMA cache_size = -" + dbMemCacheSize);
            // ExecuteNonQuery("PRAGMA journal_mode = MEMORY");
            ExecuteNonQuery("PRAGMA journal_mode = PERSIST");
            ExecuteNonQuery("PRAGMA threads = 7");
            ExecuteNonQuery("PRAGMA auto_vacuum = FULL"); // Experimental pragma to reduce size of the DB file

            string? dbCheckOnStartup = AppSettings.ReadSetting("DBCheckOnStartup");
            if (dbCheckOnStartup == null)
            {
                AppSettings.AddUpdateAppSettings("DBCheckOnStartup", "false");
                dbCheckOnStartup = AppSettings.ReadSetting("DBCheckOnStartup");
            }

            if (dbCheckOnStartup.ToLowerInvariant() == "true")
            {
                DbCommand dbCheck = new SqliteCommand(@"PRAGMA quick_check;", Connection);
                var res = dbCheck.ExecuteScalar();
                string? sRes = res?.ToString();

                if (sRes != "ok")
                    return sRes;
            }

            CheckDbVersion(ref datFound);
            if (!datFound)
                MakeDB();

            MakeTriggers();

            string? skipIndexing = AppSettings.ReadSetting("SkipIndexingOnStartup");
            if (skipIndexing != "true")
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
                DbCommand dbVersionCommand = new SqliteCommand(@"SELECT version from version limit 1", Connection);
                var res = dbVersionCommand.ExecuteScalar();

                if (res != null && res != DBNull.Value)
                    testVersion = System.Convert.ToInt32(res);

                if (testVersion == DBVersion)
                    return;
            }
            catch { }

            Connection?.Close();
            if (_dbFilename != null && File.Exists(_dbFilename))
                File.Delete(_dbFilename);

            Connection?.Open();
            datFound = false;
        }

        public void ExecuteNonQuery(string query, params object[] args)
        {
            using var command = new SqliteCommand(query, Connection);

            for (int i = 0; i < args.Length; i += 2)
            {
                command.Parameters.Add(new SqliteParameter(args[i].ToString(), args[i + 1]));
            }

            command.ExecuteNonQuery();
        }

        private void MakeDB()
        {
            /******** Create Tables ***********/

            ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS [VERSION] (
                    [Version] INTEGER NOT NULL);
                INSERT INTO VERSION (version) VALUES (@Version);",
                "Version", DBVersion);

            RvDir.CreateTable();
            RvDat.CreateTable();
            RvGame.CreateTable();
            RvFile.CreateTable();
            RvRom.CreateTable();
        }

        private void MakeTriggers()
        {
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
                        RomNoDump = RomNoDump + (IFNULL(New.status ='nodump' and New.FileId IS null,0)),
                        ZipFileLength=null,
                        LastWriteTime=null,
                        CreationTime=null,
                        LastAccessTime=null,
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
                        RomNoDump = RomNoDump - (IFNULL(Old.status ='nodump' and Old.FileId IS null,0)),
                        ZipFileLength=null,
                        LastWriteTime=null,
                        CreationTime=null,
                        LastAccessTime=null,
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
                        RomNoDump = RomNoDump - (IFNULL(New.status ='nodump',0) and Old.FileId IS null) + (IFNULL(New.status ='nodump',0) and New.FileId IS null),
                        ZipFileLength=null,
                        LastWriteTime=null,
                        CreationTime=null,
                        LastAccessTime=null,
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
                FOR EACH ROW WHEN Old.RomTotal!=New.RomTotal OR Old.RomGot!=New.RomGot OR old.RomNoDump!=New.RomNoDump
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

        public void MakeIndex(BackgroundWorker? bgw = null)
        {
            if (bgw == null)
                ConsoleManager.Show();

            bgw?.ReportProgress(0, new bgwRange2Visible(true));
            bgw?.ReportProgress(0, new bgwSetRange2(15));

            bgw?.ReportProgress(0, new bgwValue2(0));
            bgw?.ReportProgress(0, new bgwText2("Creating Index ROM-SHA1"));
            Console.WriteLine("Creating Index 1/15: ROM-SHA1");
            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [ROMSHA1Index]   ON [ROM]   ([sha1]        ASC);");

            bgw?.ReportProgress(0, new bgwValue2(1));
            bgw?.ReportProgress(0, new bgwText2("Creating Index ROM-MD5"));
            Console.WriteLine("Creating Index 2/15: ROM-MD5");
            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [ROMMD5Index]    ON [ROM]   ([md5]         ASC); ");

            bgw?.ReportProgress(0, new bgwValue2(2));
            bgw?.ReportProgress(0, new bgwText2("Creating Index ROM-CRC"));
            Console.WriteLine("Creating Index 3/15: ROM-CRC");
            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [ROMCRCIndex]    ON [ROM]   ([crc]         ASC); ");

            bgw?.ReportProgress(0, new bgwValue2(3));
            bgw?.ReportProgress(0, new bgwText2("Creating Index ROM-Size"));
            Console.WriteLine("Creating Index 4/15: ROM-Size");
            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [ROMSizeIndex]   ON [ROM]   ([size]        ASC); ");

            bgw?.ReportProgress(0, new bgwValue2(4));
            bgw?.ReportProgress(0, new bgwText2("Creating Index ROM-FileId"));
            Console.WriteLine("Creating Index 5/15: ROM-FileId");
            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [ROMFileIdIndex] ON [ROM]   ([FileId]      ASC); ");

            bgw?.ReportProgress(0, new bgwValue2(5));
            bgw?.ReportProgress(0, new bgwText2("Creating Index ROM-GameId-Name"));
            Console.WriteLine("Creating Index 6/15: ROM-GameId-Name");
            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [ROMGameId]      ON [ROM]   ([GameId]      ASC,[name] ASC);");

            bgw?.ReportProgress(0, new bgwValue2(6));
            bgw?.ReportProgress(0, new bgwText2("Creating Index Game-DatId"));
            Console.WriteLine("Creating Index 7/15: Game-DatId");
            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [GameDatId]      ON [GAME]  ([DatId]       ASC,[name] ASC);");

            bgw?.ReportProgress(0, new bgwValue2(7));
            bgw?.ReportProgress(0, new bgwText2("Creating Index Game-DirId"));
            Console.WriteLine("Creating Index 8/15: Game-DirId");
            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [GameDirId]      ON [GAME]  ([DirId]       ASC,[ZipFileLength] ASC,[name] ASC);");

            bgw?.ReportProgress(0, new bgwValue2(8));
            bgw?.ReportProgress(0, new bgwText2("Creating Index FILE-SHA1"));
            Console.WriteLine("Creating Index 9/15: FILE-SHA1");
            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [FILESHA1]       ON [FILES] ([sha1]        ASC);");

            bgw?.ReportProgress(0, new bgwValue2(9));
            bgw?.ReportProgress(0, new bgwText2("Creating Index FILE-MD5"));
            Console.WriteLine("Creating Index 10/15: FILE-MD5");
            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [FILEMD5]        ON [FILES] ([md5]         ASC);");

            bgw?.ReportProgress(0, new bgwValue2(10));
            bgw?.ReportProgress(0, new bgwText2("Creating Index FILE-CRC"));
            Console.WriteLine("Creating Index 11/15: FILE-CRC");
            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [FILECRC]        ON [FILES] ([crc]         ASC);");

            bgw?.ReportProgress(0, new bgwValue2(11));
            bgw?.ReportProgress(0, new bgwText2("Creating Index FILE-AltSHA1"));
            Console.WriteLine("Creating Index 12/15: FILE-AltSHA1");
            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [FILEAltSHA1]    ON [FILES] ([altsha1]     ASC);");

            bgw?.ReportProgress(0, new bgwValue2(12));
            bgw?.ReportProgress(0, new bgwText2("Creating Index DAT-DirId"));
            Console.WriteLine("Creating Index 13/15: DAT-DirId");
            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [DATDIRID]       ON [DAT]   ([DirId]       ASC);");

            bgw?.ReportProgress(0, new bgwValue2(13));
            bgw?.ReportProgress(0, new bgwText2("Creating Index Dir-ParentDirId"));
            Console.WriteLine("Creating Index 14/15: Dir-ParentDirId");
            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [DIRPARENTDIRID] ON [DIR]   ([ParentDirId] ASC);");

            bgw?.ReportProgress(0, new bgwValue2(14));
            bgw?.ReportProgress(0, new bgwText2("Creating Index Dir-FullName"));
            Console.WriteLine("Creating Index 15/15: Dir-FullName");
            ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [DIRFULLNAME]    ON [DIR]   ([fullname]    ASC);");

            bgw?.ReportProgress(0, new bgwValue2(15));
            bgw?.ReportProgress(0, new bgwText2("Indexing Complete"));
            Console.WriteLine("Indexing Complete");

            if (bgw == null)
                ConsoleManager.Hide();
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

        public DbCommand Command(string command)
        {
            return new SqliteCommand(command, Connection);
        }

        public DbParameter Parameter(string param, object value)
        {
            return new SqliteParameter(param, value);
        }
    }
}