using System;
using System.Collections.Generic;
using System.Data.SQLite;
using RomVaultX.IO;
using RomVaultX.Util;
using Convert = System.Convert;

namespace RomVaultX.DB
{
    public static class DataAccessLayer
    {
        private static readonly SQLiteConnection Connection;


        private static readonly SQLiteCommand CmdAddInFiles;

        private static readonly SQLiteCommand CmdClearfound;

        private static readonly SQLiteCommand CmdCleanupNotFound;

        private const int DBVersion = 5;
        private static readonly string DirFilename = @"C:\RomVaultX\rom" + DBVersion + ".db";


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

            //ExecuteNonQuery("PRAGMA journal_mode= MEMORY");
            ExecuteNonQuery("Attach Database ':memory:' AS 'memdb'");

         

        




            CmdAddInFiles = new SQLiteCommand(
                @"INSERT INTO FILES (size,crc,sha1,md5)
                        VALUES (@Size,@CRC,@SHA1,@MD5);", Connection);
            CmdAddInFiles.Parameters.Add(new SQLiteParameter("size"));
            CmdAddInFiles.Parameters.Add(new SQLiteParameter("crc"));
            CmdAddInFiles.Parameters.Add(new SQLiteParameter("sha1"));
            CmdAddInFiles.Parameters.Add(new SQLiteParameter("md5"));



            CmdClearfound = new SQLiteCommand(
                @"UPDATE DIR SET Found=0; UPDATE DAT SET Found=0;", Connection);




            CmdCleanupNotFound = new SQLiteCommand(
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


            MakeIndex();
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
                    [expanded] BOOLEAN DEFAULT '1' NOT NULL,
                    [found] BOOLEAN DEFAULT '1',
                    [RomTotal] INTEGER  NULL,
                    [RomGot] iNTEGER  NULL
                );
              
                CREATE TABLE IF NOT EXISTS [FILES] (
                    [FileId] INTEGER PRIMARY KEY NOT NULL,
                    [size] INTEGER NOT NULL,
                    [crc] VARCHAR(8) NULL,
                    [sha1] VARCHAR(40) NULL,
                    [md5] VARCHAR(32) NULL
                );

        ");

            RvDat.MakeDB();
            RvGame.MakeDB();
            RvRom.MakeDB();

            /**** FILE Triggers ****/
            /*INSERT*/
            ExecuteNonQuery(@"
                CREATE TRIGGER IF NOT EXISTS [FileInsert] 
                AFTER INSERT ON [FILES] 
                FOR EACH ROW 
                BEGIN 
                    UPDATE ROM SET 
	                    FileId = new.FileId
                    WHERE
	                    (                 sha1 = new.sha1 ) AND
	                    ( md5  is NULL OR md5  = new.md5  ) AND 
	                    ( crc  is NULL OR crc  = new.crc  ) AND
	                    ( size is NULL OR size = new.Size ) AND
	                    ( status != 'nodump' OR status is NULL) AND 
                        FileId IS NULL;
		
                    UPDATE ROM SET 
	                    FileId = new.FileId
                    WHERE
	                    (                 md5  = new.md5  ) AND 
	                    ( sha1 is NULL OR sha1 = new.sha1 ) AND
	                    ( crc  is NULL OR crc  = new.crc  ) AND
	                    ( size is NULL OR size = new.Size ) AND
	                    ( status != 'nodump' OR status is NULL) AND 
                        FileId IS NULL;
		
                    UPDATE ROM SET 
	                    FileId = new.FileId
                    WHERE
	                    (                 crc  = new.crc  ) AND
	                    ( sha1 is NULL OR sha1 = new.sha1 ) AND
	                    ( md5  is NULL OR md5  = new.md5  ) AND 
	                    ( size is NULL OR size = new.Size ) AND
	                    ( status != 'nodump' OR status is NULL) AND 
                        FileId IS NULL;

                    UPDATE ROM SET 
	                    FileId = new.FileId
                    WHERE
	                    (                 size = new.Size ) AND
	                    ( crc  is NULL OR crc  = new.crc  ) AND
	                    ( sha1 is NULL OR sha1 = new.sha1 ) AND
	                    ( md5  is NULL OR md5  = new.md5  ) AND 
	                    ( status != 'nodump' OR status is NULL) AND 
                        FileId IS NULL;
                END;
            ");
            /*DELETE*/
            ExecuteNonQuery(@"
                CREATE TRIGGER IF NOT EXISTS [FileDelete] 
                AFTER DELETE ON [FILES] 
                FOR EACH ROW 
                BEGIN 
                    UPDATE ROM SET FileId=null WHERE FileId=OLD.FileId;
                END;
            ");

            /******* ROM Triggers ******/

            /*
 CREATE TRIGGER IF NOT EXISTS [RomInsert_FileIdIsNull] 
                AFTER INSERT ON [ROM] 
                FOR EACH ROW WHEN new.FileId is null 
                BEGIN 

                update ROM SET
                       FileId=coalesce(

                       (select Files.FileId from files
                            WHERE
                                        (                     rom.sha1 = files.sha1 ) AND
                                        ( rom.md5  is NULL OR rom.md5  = files.md5  ) AND
                                        ( rom.crc  is NULL OR rom.crc  = files.crc  ) AND
                                        ( rom.size is NULL OR rom.size = files.Size )
                            limit 1)
                       ,
                       (select Files.FileId from files
                            WHERE
                                        ( rom.sha1 is NULL OR rom.sha1 = files.sha1 ) AND
                                        (                     rom.md5  = files.md5  ) AND
                                        ( rom.crc  is NULL OR rom.crc  = files.crc  ) AND
                                        ( rom.size is NULL OR rom.size = files.Size )
                            limit 1)
                       ,
                       (select Files.FileId from files
                            WHERE
                                        ( rom.sha1 is NULL OR rom.sha1 = files.sha1 ) AND
                                        ( rom.md5  is NULL OR rom.md5  = files.md5  ) AND
                                        (                     rom.crc  = files.crc  ) AND
                                        ( rom.size is NULL OR rom.size = files.Size )
                            limit 1)
                       )
                where romid=new.romid;


                UPDATE GAME SET
                      RomTotal = RomTotal + 1,
                      RomGot = RomGot + (IFNULL((select fileId from rom where romid=new.romid),0)>0)
                WHERE
                      Game.GameId = New.GameId;            
            
            */
            /*INSERT*/
            ExecuteNonQuery(@"
                CREATE TRIGGER IF NOT EXISTS [RomInsert] 
                AFTER INSERT ON [ROM] 
                FOR EACH ROW
                BEGIN 
                    UPDATE GAME SET
                        RomTotal = RomTotal + 1,
                        RomGot = RomGot + (IFNULL(New.FileId,0)>0)
                    WHERE 
                        Game.GameId = New.GameId;
                END;
            ");
            /*DELETE*/
            ExecuteNonQuery(@"
                CREATE TRIGGER IF NOT EXISTS [RomDelete] 
                AFTER DELETE ON [ROM] 
                FOR EACH ROW
                BEGIN 
                    UPDATE GAME SET
                        RomTotal = RomTotal - 1,
                        RomGot = RomGot - (IFNULL(Old.FileId,0)>0)
                    WHERE 
                        Game.GameId = Old.GameId;
                END;
            ");
            /*UPDATE*/
            ExecuteNonQuery(@"
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

            /**** GAME Triggers ****/
            /*INSERT*/
            ExecuteNonQuery(@"
                CREATE TRIGGER IF NOT EXISTS [GameInsert]
                AFTER INSERT ON [GAME]
                FOR EACH ROW
                BEGIN
                    UPDATE DAT SET
                            RomTotal=RomTotal + New.RomTotal , 
                            RomGot  =RomGot   + New.RomGot
                    WHERE
                            DatId= New.DatId;
                END;
            ");
            /*DELETE*/
            ExecuteNonQuery(@"
                CREATE TRIGGER IF NOT EXISTS [GameDelete]
                AFTER DELETE ON [GAME]
                FOR EACH ROW
                BEGIN
                    UPDATE DAT SET 
                            RomTotal=RomTotal - Old.RomTotal ,
                            RomGot  =RomGot   - Old.RomGot
                    WHERE
                            DatId=Old.DatId;
                END;
            ");
            /*UPDATE*/
            ExecuteNonQuery(@"
                CREATE TRIGGER IF NOT EXISTS [GameUpdate] 
                AFTER UPDATE ON [GAME] 
                FOR EACH ROW WHEN Old.RomTotal!=New.RomTotal OR Old.RomGot!=New.RomGot 
                BEGIN 
                  UPDATE DAT SET
                            RomTotal=RomTotal - Old.RomTotal + New.RomTotal ,
                            RomGot  =RomGot   - Old.RomGot   + New.RomGot
                    WHERE
                            DatId=New.DatId;
                END;
            ");

        }

        private static void MakeIndex()
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

        public static void UpdateGotTotal()
        {
            ExecuteNonQuery(@"

            UPDATE DIR SET RomTotal=null, ROMGot=null;

            UPDATE DIR SET
                romtotal = (SELECT SUM(romtotal) FROM dat WHERE dat.dirid=dir.dirid)
            WHERE
                (SELECT COUNT(1) FROM dat WHERE dat.dirid=dir.dirid)>0;

            UPDATE DIR SET
                romgot = (SELECT SUM(romgot) FROM dat WHERE dat.dirid=dir.dirid)
            WHERE
                (SELECT COUNT(1) FROM dat WHERE dat.dirid=dir.dirid)>0;

            UPDATE DIR SET romtotal=0, romgot=0
            WHERE
            (select count(1) from dir as p1 where p1.parentdirid=dir.dirid)=0 and
            (select count(1) from dat       where dat.DirId=dir.dirid)=0;");


            SQLiteCommand sqlNullCount = new SQLiteCommand(@"SELECT COUNT(1) FROM dir WHERE RomTotal IS null", Connection);

            int nullcount;
            do
            {
                ExecuteNonQuery(@"
                    UPDATE dir SET
                        romtotal=(SELECT SUM(p1.romtotal) FROM dir AS p1 WHERE p1.parentdirid=dir.dirid),
                        romGot  =(SELECT SUM(p1.romgot  ) FROM dir AS p1 WHERE p1.parentdirid=dir.dirid)
                    WHERE
                        romtotal IS null AND
                        (SELECT COUNT(1) FROM dir AS p WHERE p.romtotal IS null AND p.parentdirid=dir.dirid)=0");

                object res = sqlNullCount.ExecuteScalar();
                nullcount = Convert.ToInt32(res);

            } while (nullcount > 0);
        }



      
     

      

        public static void AddInFiles(rvFile tFile)
        {
            CmdAddInFiles.Parameters["size"].Value = tFile.Size;
            CmdAddInFiles.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
            CmdAddInFiles.Parameters["sha1"].Value = VarFix.ToDBString(tFile.SHA1);
            CmdAddInFiles.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5);

            CmdAddInFiles.ExecuteNonQuery();
        }



        public static void ClearFound()
        {
            CmdClearfound.ExecuteNonQuery();
        }

        public static void RemoveNotFound()
        {
            CmdCleanupNotFound.ExecuteNonQuery();
        }




    }
}
