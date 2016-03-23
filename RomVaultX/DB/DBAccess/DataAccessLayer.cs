/*



File Insert
-----------
RomVault will handle a file Insert, updating the FileId in ROM table.
(This will trigger the RomUpdate tigger.)

File Update
-----------
Assumtion is made that a file record will never be updated. (Just Inserts and Deletes permitted.)

File Delete
-----------
File Delete Trigger is in place that will null the FileId in ROM table when a file is deleted.
(This will trigger the RomUpdate tigger.)
 
 
 
Rom Insert
----------
RomInsert trigger will update the GAME table:
		RomTotal = RomTotal + 1,
        RomGot = RomGot + (IFNULL(New.FileId,0)>0),
        RomNoDump = RomNoDump + (IFNULL(New.status ='nodump' and New.crc is null and New.sha1 is null and New.md5 is null,0))
		

Rom Delete
----------
RomDelete trigger will update the GAME table:
		RomTotal = RomTotal - 1,
        RomGot = RomGot - (IFNULL(New.FileId,0)>0),
        RomNoDump = RomNoDump - (IFNULL(New.status ='nodump' and New.crc is null and New.sha1 is null and New.md5 is null,0))
		
Rom Update
----------
RomUpdate tigger assumes the only change to ROM table will be the FileId field.
		RomGot = RomGot - (IFNULL(Old.FileId,0)>0) + (IFNULL(New.FileId,0)>0)
		
		
Game Insert
-----------
GameInsert trigger will update the DAT table:
		RomTotal   =RomTotal  + New.RomTotal  , 
		RomGot     =RomGot    + New.RomGot    ,
		RomNoDump  =RomNoDump + New.RomNoDump
		  
Game Delete
-----------
GameDelete trigger will update the DAT table:
		RomTotal   =RomTotal  - New.RomTotal  , 
		RomGot     =RomGot    - New.RomGot    ,
		RomNoDump  =RomNoDump - New.RomNoDump

Game Update
-----------
GameUpdate trigger will update the DAT table:
		RomTotal   =RomTotal  - Old.RomTotal  + New.RomTotal ,
		RomGot     =RomGot    - Old.RomGot    + New.RomGot ,
		RomNoDump  =RomNoDump - Old.RomNoDump + New.RomNoDump


*/


using System;
using System.Data.SQLite;
using RomVaultX.IO;
using Convert = System.Convert;

namespace RomVaultX.DB
{
    public static class DataAccessLayer
    {
        private static readonly SQLiteConnection Connection;

        private static readonly SQLiteCommand CmdClearfoundDirDATs;

        private static readonly SQLiteCommand CmdCleanupNotFoundDATs;

        private static readonly SQLiteCommand CmdCountDATs;

        private const int DBVersion = 6;
        private static readonly string DirFilename = @"rom" + DBVersion + ".db3";
        //private static readonly string DirFilename = @":memory:";


        public static SQLiteConnection DBConnection
        {
            get { return Connection; }
        }

        static DataAccessLayer()
        {

            bool datFound = File.Exists(DirFilename);

            Connection = new SQLiteConnection(@"data source=" + DirFilename + ";Version=3");
            Connection.Open();

            CheckDbVersion(ref datFound);

            if (!datFound)
                MakeDB();

            MakeTriggers();
            MakeIndex();

            ExecuteNonQuery("PRAGMA temp_store = MEMORY");
            //ExecuteNonQuery("PRAGMA journal_mode= MEMORY");
            ExecuteNonQuery("Attach Database ':memory:' AS 'memdb'");




            CmdClearfoundDirDATs = new SQLiteCommand(
                @"
                    UPDATE DIR SET Found=0;
                    UPDATE DAT SET Found=0;
                ", Connection);

            CmdCleanupNotFoundDATs = new SQLiteCommand(
                @"
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


            CmdCountDATs = new SQLiteCommand(@"select count(1) from dat", Connection);
        }


        public static void ExecuteNonQuery(string query, params object[] args)
        {
            using (SQLiteCommand command = new SQLiteCommand(query, Connection))
            {
                for (int i = 0; i < args.Length; i += 2)
                    command.Parameters.Add(new SQLiteParameter(args[i].ToString(), args[i + 1]));

                command.ExecuteNonQuery();
            }
        }

        private static void CheckDbVersion(ref bool datFound)
        {
            if (!datFound)
                return;

            int testVersion = 0;
            try
            {

                SQLiteCommand dbVersionCommand = new SQLiteCommand(@"SELECT version from version limit 1", Connection);
                object res = dbVersionCommand.ExecuteScalar();

                if (res != null && res != DBNull.Value)
                    testVersion = Convert.ToInt32(res);

                if (testVersion == DBVersion)
                    return;
            }
            catch (Exception)
            {
            }

            Connection.Close();
            File.Delete(DirFilename);
            Connection.Open();
            datFound = false;
        }


        private static void MakeDB()
        {
            ExecuteNonQuery(@"CREATE TABLE IF NOT EXISTS [VERSION] ([Version] INTEGER NOT NULL); INSERT INTO VERSION (version) VALUES (@Version);", "version", DBVersion);

            ExecuteNonQuery(@"
                                
                CREATE TABLE IF NOT EXISTS [DIR] (
                    [DirId] INTEGER PRIMARY KEY NOT NULL,
                    [ParentDirId] INTEGER NULL,
                    [name] NVARCHAR(300) NOT NULL,
                    [fullname] NVARCHAR(300) NOT NULL,
                    [expanded] BOOLEAN DEFAULT 1 NOT NULL,
                    [found] BOOLEAN DEFAULT 1,
                    [RomTotal] INTEGER NULL,
                    [RomGot] iNTEGER NULL,
                    [RomNoDump] INTEGER NULL
                );
             
        ");

            RvDat.MakeDB();
            RvGame.MakeDB();
            RvRom.MakeDB();
            RvFile.MakeDB();

        }

        private static void MakeTriggers()
        {
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
                    UPDATE ROM SET FileId=null WHERE FileId=OLD.FileId;
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
                        RomNoDump = RomNoDump + (IFNULL(New.status ='nodump' and New.crc is null and New.sha1 is null and New.md5 is null,0))
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
                        RomNoDump = RomNoDump - (IFNULL(Old.status ='nodump' and Old.crc is null and Old.sha1 is null and Old.md5 is null,0))
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
                        RomGot = RomGot - (IFNULL(Old.FileId,0)>0) + (IFNULL(New.FileId,0)>0)
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

        public static void MakeIndex()
        {
            ExecuteNonQuery(@"
                CREATE INDEX IF NOT EXISTS [ROMSHA1Index]   ON [ROM]   ([sha1]        ASC);
                CREATE INDEX IF NOT EXISTS [ROMMD5Index]    ON [ROM]   ([md5]         ASC);
                CREATE INDEX IF NOT EXISTS [ROMCRCIndex]    ON [ROM]   ([crc]         ASC);
                CREATE INDEX IF NOT EXISTS [ROMSizeIndex]   ON [ROM]   ([size]        ASC);
                CREATE INDEX IF NOT EXISTS [ROMFileIdIndex] ON [ROM]   ([FileId]      ASC);
                CREATE INDEX IF NOT EXISTS [ROMGameId]      ON [ROM]   ([GameId]      ASC,[name] ASC);

                CREATE INDEX IF NOT EXISTS [GameDatId]      ON [GAME]  ([DatId]       ASC,[name] ASC);

                CREATE INDEX IF NOT EXISTS [FILESHA1]       ON [FILES] ([sha1]        ASC);
                CREATE INDEX IF NOT EXISTS [FILEMD5]        ON [FILES] ([md5]         ASC);
                CREATE INDEX IF NOT EXISTS [FILECRC]        ON [FILES] ([crc]         ASC);

                CREATE INDEX IF NOT EXISTS [DATDIRID]       ON [DAT]   ([DirId]       ASC);
                CREATE INDEX IF NOT EXISTS [DIRPARENTDIRID] ON [DIR]   ([ParentDirId] ASC);
            ");
        }
        public static void DropIndex()
        {

            ExecuteNonQuery(@"
                DROP INDEX IF EXISTS [ROMSHA1Index];
                DROP INDEX IF EXISTS [ROMMD5Index];
                DROP INDEX IF EXISTS [ROMCRCIndex];
                DROP INDEX IF EXISTS [ROMSizeIndex];
                DROP INDEX IF EXISTS [ROMFileIdIndex];
                DROP INDEX IF EXISTS [ROMGameId];");
        }

        public static void UpdateGotTotal()
        {
            ExecuteNonQuery(@"

            UPDATE DIR SET RomTotal=null, ROMGot=null,RomNoDump=null;

            UPDATE DIR SET
                romtotal = (SELECT SUM(romtotal) FROM dat WHERE dat.dirid=dir.dirid)
            WHERE
                (SELECT COUNT(1) FROM dat WHERE dat.dirid=dir.dirid)>0;

            UPDATE DIR SET
                romgot = (SELECT SUM(romgot) FROM dat WHERE dat.dirid=dir.dirid)
            WHERE
                (SELECT COUNT(1) FROM dat WHERE dat.dirid=dir.dirid)>0;

            UPDATE DIR SET
                romnodump = (SELECT SUM(romnodump) FROM dat WHERE dat.dirid=dir.dirid)
            WHERE
                (SELECT COUNT(1) FROM dat WHERE dat.dirid=dir.dirid)>0;


            UPDATE DIR SET romtotal=0, romgot=0, romnodump=0
            WHERE
            (select count(1) from dir as p1 where p1.parentdirid=dir.dirid)=0 and
            (select count(1) from dat       where dat.DirId=dir.dirid)=0;");


            SQLiteCommand sqlNullCount = new SQLiteCommand(@"SELECT COUNT(1) FROM dir WHERE RomTotal IS null", Connection);

            int nullcount;
            do
            {
                ExecuteNonQuery(@"
                    UPDATE dir SET
                        romtotal  =(SELECT SUM(p1.romtotal ) FROM dir AS p1 WHERE p1.parentdirid=dir.dirid),
                        romGot    =(SELECT SUM(p1.romgot   ) FROM dir AS p1 WHERE p1.parentdirid=dir.dirid),
                        romnodump =(SELECT SUM(p1.romnodump) FROM dir AS p1 WHERE p1.parentdirid=dir.dirid)
                    WHERE
                        romtotal IS null AND
                        (SELECT COUNT(1) FROM dir AS p WHERE p.romtotal IS null AND p.parentdirid=dir.dirid)=0");

                object res = sqlNullCount.ExecuteScalar();
                nullcount = Convert.ToInt32(res);

            } while (nullcount > 0);
        }


        public static void ClearFoundDATs()
        {
            CmdClearfoundDirDATs.ExecuteNonQuery();
        }

        public static void RemoveNotFoundDATs()
        {
            CmdCleanupNotFoundDATs.ExecuteNonQuery();
        }

        public static int DatDBCount()
        {
            CmdCountDATs.ExecuteScalar();

            object res = CmdCountDATs.ExecuteScalar();

            if (res != null && res != DBNull.Value)
                return Convert.ToInt32(res);

            return 0;
        }
    }
}
