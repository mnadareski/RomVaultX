using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using RomVaultX.IO;
using RomVaultX.Util;
using Convert = System.Convert;

namespace RomVaultX.DB
{
    public static class DataAccessLayer
    {
        private static SQLiteConnection _connection;

        //private static readonly SQLiteCommand _findExistingDAT;


        private static readonly SQLiteCommand _readTree;
        private static readonly SQLiteCommand _setTreeExpanded;

        private static readonly SQLiteCommand _addInFiles;

        private static readonly SQLiteCommand _clearfound;
        private static readonly SQLiteCommand _setDirFound;
        //private static readonly SQLiteCommand _setDatFound;

        private static readonly SQLiteCommand _cleanupNotFound;

        private static readonly SQLiteCommand _findFile;

        private const int DBVersion = 4;
        private static readonly string dirFilename = @"C:\stage\rom" + DBVersion + ".db";


        public static SQLiteConnection dbConnection
        {
            get { return _connection;  }
        }


        static DataAccessLayer()
        {

            bool datFound = File.Exists(dirFilename);

            _connection = new SQLiteConnection(@"data source=" + dirFilename + ";Version=3");
            _connection.Open();

            CheckDbVersion(ref datFound);

            if (!datFound)
                MakeDB();




            _readTree = new SQLiteCommand(
                @"
                    SELECT 
                        dir.DirId as DirId,
                        dir.name as dirname,
                        dir.fullname,
                        dir.expanded,
                        dat.DatId,
                        dat.name as datname,
                        dat.description,
                        dat.RomTotal,
                        dat.RomGot
                    FROM dir LEFT JOIN dat ON dir.DirId=dat.DirId
                    ORDER BY dir.Fullname,dat.Filename", _connection);

            _setTreeExpanded = new SQLiteCommand(
                @"
                    UPDATE dir SET expanded=@expanded WHERE DirId=@dirId", _connection);
            _setTreeExpanded.Parameters.Add(new SQLiteParameter("expanded"));
            _setTreeExpanded.Parameters.Add(new SQLiteParameter("dirId"));

          
           

            _addInFiles = new SQLiteCommand(
                @"INSERT INTO FILES (size,crc,sha1,md5)
                        VALUES (@Size,@CRC,@SHA1,@MD5);", _connection);
            _addInFiles.Parameters.Add(new SQLiteParameter("size"));
            _addInFiles.Parameters.Add(new SQLiteParameter("crc"));
            _addInFiles.Parameters.Add(new SQLiteParameter("sha1"));
            _addInFiles.Parameters.Add(new SQLiteParameter("md5"));


            RvDat.SetConnection(_connection);
            RvGameGridRow.setConnection(_connection);
            RvGame.SetConnection(_connection);
            RvRom.SetConnection(_connection);

       


            _clearfound = new SQLiteCommand(
                @"UPDATE DIR SET Found=0; UPDATE DAT SET Found=0;", _connection);

           

           
            _cleanupNotFound = new SQLiteCommand(
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
            ", _connection);


            _findFile = new SQLiteCommand(
                @"

                    select (coalesce(

                       (select Files.FileId from files
                            WHERE
	                                    (                  @sha1 = files.sha1 ) AND
	                                    ( @md5  is NULL OR @md5  = files.md5  ) AND
	                                    ( @crc  is NULL OR @crc  = files.crc  ) AND
	                                    ( @size is NULL OR @size = files.Size )
                            limit 1)
                       ,
                       (select Files.FileId from files
                            WHERE
	                                    (                  @md5  = files.md5  ) AND
	                                    ( @sha1 is NULL OR @sha1 = files.sha1 ) AND
	                                    ( @crc  is NULL OR @crc  = files.crc  ) AND
	                                    ( @size is NULL OR @size = files.Size )
                            limit 1)
                       ,
                       (select Files.FileId from files
                            WHERE
	                                    (                  @crc  = files.crc  ) AND
	                                    ( @sha1 is NULL OR @sha1 = files.sha1 ) AND
	                                    ( @md5  is NULL OR @md5  = files.md5  ) AND
	                                    ( @size is NULL OR @size = files.Size )
                            limit 1)
                       )) as FileId;
                ", _connection);

            _findFile.Parameters.Add(new SQLiteParameter("sha1"));
            _findFile.Parameters.Add(new SQLiteParameter("md5"));
            _findFile.Parameters.Add(new SQLiteParameter("crc"));
            _findFile.Parameters.Add(new SQLiteParameter("size"));
            MakeIndex();
        }


        public static void ExecuteNonQuery(string query, params object[] args)
        {
            using (SQLiteCommand command = new SQLiteCommand(query, _connection))
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

                SQLiteCommand _dbVersionCommand = new SQLiteCommand(@"SELECT version from version limit 1", _connection);
                object res = _dbVersionCommand.ExecuteScalar();

                if (res != null && res != DBNull.Value)
                    testVersion = Convert.ToInt32(res);

                if (testVersion == DBVersion)
                    return;
            }
            catch (Exception)
            {
            }

            _connection.Close();
            File.Delete(dirFilename);
            _connection.Open();
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
                    [found] BOOLEAN DEFAULT '1'
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


        public static void DropIndex()
        {
            ExecuteNonQuery(@"
                DROP INDEX IF EXISTS [ROMSHA1Index];
                DROP INDEX IF EXISTS [ROMMD5Index];
                DROP INDEX IF EXISTS [ROMCRCIndex];
                DROP INDEX IF EXISTS [ROMSizeIndex];
                DROP INDEX IF EXISTS [ROMGameId];
                DROP INDEX IF EXISTS [ROMFileId];
                DROP INDEX IF EXISTS [GameDatId];
            ");
        }

        public static void MakeIndex()
        {
            ExecuteNonQuery(@"
                CREATE INDEX IF NOT EXISTS [ROMSHA1Index] ON [ROM]( [sha1] ASC);
                CREATE INDEX IF NOT EXISTS [ROMMD5Index] ON [ROM]( [md5] ASC);
                CREATE INDEX IF NOT EXISTS [ROMCRCIndex] ON [ROM]( [crc] ASC);
                CREATE INDEX IF NOT EXISTS [ROMSizeIndex] ON [ROM]( [size] ASC);
                CREATE INDEX IF NOT EXISTS [ROMFileIdIndex] ON [ROM]( [FileId] ASC);

                CREATE INDEX IF NOT EXISTS [GameDatId] ON [GAME](
                [DatId]  ASC,
                [name]  ASC
                );

                CREATE INDEX IF NOT EXISTS [ROMGameId] on [ROM](
                [GameId] ASC,
                [name] ASC
                );

                CREATE INDEX IF NOT EXISTS [FILESHA1] ON [FILES]([sha1] ASC);
                CREATE INDEX IF NOT EXISTS [FILEMD5] ON [FILES]([md5] ASC);
                CREATE INDEX IF NOT EXISTS [FILECRC] ON [FILES]([crc] ASC);
            ");

        }


       

        public static List<RvTreeRow> ReadTreeFromDB()
        {
            List<RvTreeRow> rows = new List<RvTreeRow>();

            using (SQLiteDataReader dr = _readTree.ExecuteReader())
            {
                bool multiDatDirFound = false;

                string SkipUntil = "";

                RvTreeRow lastTree = null;
                while (dr.Read())
                {
                    // a single DAT in a directory is just displayed in the tree at the same level as the directory
                    RvTreeRow pTree = new RvTreeRow();
                    pTree.DirId = Convert.ToUInt32(dr["DirId"]);
                    pTree.dirName = dr["dirname"].ToString();
                    pTree.dirFullName = dr["fullname"].ToString();
                    pTree.Expanded = Convert.ToBoolean(dr["expanded"]);

                    pTree.DatId = dr["DatId"] == DBNull.Value ? null : (uint?)Convert.ToUInt32(dr["DatId"]);
                    pTree.datName = dr["datname"] == DBNull.Value ? null : dr["datname"].ToString();
                    pTree.description = dr["description"] == DBNull.Value ? null : dr["description"].ToString();

                    pTree.RomTotal = dr["RomTotal"] == DBNull.Value ? 0 : Convert.ToInt32(dr["RomTotal"]);
                    pTree.RomGot = dr["RomGot"] == DBNull.Value ? 0 : Convert.ToInt32(dr["RomGot"]);

                    if (!string.IsNullOrEmpty(SkipUntil))
                    {
                        if (pTree.dirFullName.Length >= SkipUntil.Length)
                        {
                            if (pTree.dirFullName.Substring(0, SkipUntil.Length) == SkipUntil)
                                continue;
                        }
                    }
                    if (!pTree.Expanded)
                    {
                        SkipUntil = pTree.dirFullName;
                        pTree.DatId = null;
                        pTree.datName = null;
                        pTree.description = null;
                        pTree.RomTotal = 0;
                        pTree.RomGot = 0;
                    }
                    rows.Add(pTree);

                    if (lastTree != null)
                    {
                        // if multiple DAT's are in the same directory then we should add another level in the tree to display the directory
                        bool thisMultiDatDirFound = (lastTree.DirId == pTree.DirId);
                        if (thisMultiDatDirFound && !multiDatDirFound)
                        {
                            // found a new multidat
                            RvTreeRow dirTree = new RvTreeRow
                            {
                                DirId = lastTree.DirId,
                                dirName = lastTree.dirName,
                                dirFullName = lastTree.dirFullName,
                                Expanded = lastTree.Expanded,
                                DatId = null,
                                datName = null
                            };
                            rows.Insert(rows.Count - 2, dirTree);
                            lastTree.MultiDatDir = true;
                        }
                        if (thisMultiDatDirFound)
                            pTree.MultiDatDir = true;

                        multiDatDirFound = thisMultiDatDirFound;
                    }


                    lastTree = pTree;
                }
            }

            return rows;
        }

        public static void SetTreeExpanded(uint DirId, bool expanded)
        {
            _setTreeExpanded.Parameters["dirId"].Value = DirId;
            _setTreeExpanded.Parameters["expanded"].Value = expanded;
            _setTreeExpanded.ExecuteNonQuery();
        }


      

        public static void AddInFiles(rvFile tFile)
        {
            _addInFiles.Parameters["size"].Value = tFile.Size;
            _addInFiles.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
            _addInFiles.Parameters["sha1"].Value = VarFix.ToDBString(tFile.SHA1);
            _addInFiles.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5);
            Stopwatch sw = new Stopwatch();

            sw.Reset();
            sw.Start();
            _addInFiles.ExecuteNonQuery();
            sw.Stop();

            Debug.WriteLine("Time to add file =" + sw.ElapsedMilliseconds);

        }

     

        public static void ClearFound()
        {
            _clearfound.ExecuteNonQuery();
        }

        public static void RemoveNotFound()
        {
            _cleanupNotFound.ExecuteNonQuery();
        }

        public static uint? FindAFile(RvRom tFile)
        {
            _findFile.Parameters["size"].Value = tFile.Size;
            _findFile.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
            _findFile.Parameters["sha1"].Value = VarFix.ToDBString(tFile.SHA1);
            _findFile.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5);

            object res = _findFile.ExecuteScalar();

            if (res == null || res == DBNull.Value)
                return null;
            return (uint?)Convert.ToInt32(res);

        }
    }
}
