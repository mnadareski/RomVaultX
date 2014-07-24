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

        private static readonly SQLiteCommand _findInDir;
        private static readonly SQLiteCommand _insertIntoDir;
        private static readonly SQLiteCommand _findExistingDAT;


        private static readonly SQLiteCommand _readTree;
        private static readonly SQLiteCommand _setTreeExpanded;

        private static readonly SQLiteCommand _findInFiles;
        private static readonly SQLiteCommand _findInROMs;
        private static readonly SQLiteCommand _addInFiles;



        private const int DBVersion = 3;
        private static readonly string dirFilename = @"C:\stage\rom" + DBVersion + ".db";


        static DataAccessLayer()
        {

            bool datFound = File.Exists(dirFilename);

            _connection = new SQLiteConnection(@"data source=" + dirFilename + ";Version=3");
            _connection.Open();

            CheckDbVersion(ref datFound);

            if (!datFound)
                MakeDB();

            _findInDir = new SQLiteCommand(@"SELECT DirId FROM dir WHERE fullname=@fullname LIMIT 1", _connection);
            _findInDir.Parameters.Add(new SQLiteParameter("fullname"));

            _insertIntoDir = new SQLiteCommand(
                @"INSERT INTO DIR (ParentDirId,Name,FullName)
                VALUES (@ParentDirId,@Name,@FullName);

                SELECT last_insert_rowid();", _connection);

            _insertIntoDir.Parameters.Add(new SQLiteParameter("ParentDirId"));
            _insertIntoDir.Parameters.Add(new SQLiteParameter("Name"));
            _insertIntoDir.Parameters.Add(new SQLiteParameter("FullName"));



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

            _findInFiles = new SQLiteCommand(
                @"
                    SELECT COUNT(1) FROM FILES WHERE
                        size=@size AND crc=@CRC and sha1=@SHA1 and md5=@MD5", _connection);
            _findInFiles.Parameters.Add(new SQLiteParameter("size"));
            _findInFiles.Parameters.Add(new SQLiteParameter("crc"));
            _findInFiles.Parameters.Add(new SQLiteParameter("sha1"));
            _findInFiles.Parameters.Add(new SQLiteParameter("md5"));

            _findInROMs = new SQLiteCommand(
                @"
                        SELECT
                        (
                            SELECT count(1) FROM ROM WHERE
                                ( sha1=@SHA1 ) AND 
                                ( md5=@MD5 OR md5 is NULL) AND
                                ( crc=@CRC OR crc is NULL ) AND
                                ( size=@size OR size is NULL ) AND
                            status!='nodump'
                        ) +
                        (
                            SELECT count(1) FROM ROM WHERE
                                ( sha1=@SHA1 OR sha1 is NULL ) AND 
                                ( md5=@MD5 ) AND
                                ( crc=@CRC OR crc is NULL ) AND
                                ( size=@size OR size is NULL ) AND
                            status!='nodump'
                        ) +
                        (
                            SELECT count(1) FROM ROM WHERE
                                ( sha1=@SHA1 OR sha1 is NULL ) AND 
                                ( md5=@MD5 OR md5 is NULL) AND
                                ( crc=@CRC ) AND
                                ( size=@size OR size is NULL ) AND
                            status!='nodump'
                        )
                        AS TotalFound"
                , _connection);
            _findInROMs.Parameters.Add(new SQLiteParameter("size"));
            _findInROMs.Parameters.Add(new SQLiteParameter("crc"));
            _findInROMs.Parameters.Add(new SQLiteParameter("sha1"));
            _findInROMs.Parameters.Add(new SQLiteParameter("md5"));

            _addInFiles = new SQLiteCommand(
                @"INSERT INTO FILES (size,crc,sha1,md5)
                        VALUES (@Size,@CRC,@SHA1,@MD5);", _connection);
            _addInFiles.Parameters.Add(new SQLiteParameter("size"));
            _addInFiles.Parameters.Add(new SQLiteParameter("crc"));
            _addInFiles.Parameters.Add(new SQLiteParameter("sha1"));
            _addInFiles.Parameters.Add(new SQLiteParameter("md5"));


            rvDat.SetConnection(_connection);
            RvGameRow.setConnection(_connection);
            RvGame.SetConnection(_connection);
            RvRom.SetConnection(_connection);

            _findExistingDAT = new SQLiteCommand(
                @"SELECT DatId FROM Dat,Dir WHERE Dat.DirId=Dir.DirId AND fullname=@fullname AND Filename=@filename AND DatTimeStamp=@DatTimeStamp", _connection);
            _findExistingDAT.Parameters.Add(new SQLiteParameter("fullname"));
            _findExistingDAT.Parameters.Add(new SQLiteParameter("filename"));
            _findExistingDAT.Parameters.Add(new SQLiteParameter("DatTimeStamp"));

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
                    [expanded] BOOLEAN DEFAULT '1' NOT NULL
                );
              
                CREATE TABLE IF NOT EXISTS [FILES] (
                    [FileId] INTEGER PRIMARY KEY NOT NULL,
                    [size] INTEGER NOT NULL,
                    [crc] VARCHAR(8) NULL,
                    [sha1] VARCHAR(40) NULL,
                    [md5] VARCHAR(32) NULL
                );

        ");

            rvDat.MakeDB();
            RvGame.MakeDB();
            RvRom.MakeDB();

            ExecuteNonQuery(@"
                CREATE TRIGGER IF NOT EXISTS [FileInsert] 
                AFTER INSERT ON [FILES] 
                FOR EACH ROW 
                BEGIN 
                    UPDATE ROM SET 
	                    FileId=new.FileId
                    WHERE
	                    ( sha1=new.sha1) AND
	                    ( md5=new.md5 OR md5 is NULL) AND 
	                    ( crc=new.crc OR crc is NULL ) AND
	                    ( size=new.Size OR size is NULL ) AND
	                    status!='nodump';
		
                    UPDATE ROM SET 
	                    FileId=new.FileId
                    WHERE
	                    ( sha1=new.sha1 OR sha1 is NULL ) AND
	                    ( md5=new.md5) AND 
	                    ( crc=new.crc OR crc is NULL ) AND
	                    ( size=new.Size OR size is NULL ) AND
	                    status!='nodump';
		
                    UPDATE ROM SET 
	                    FileId=new.FileId
                    WHERE
	                    ( sha1=new.sha1 OR sha1 is NULL ) AND
	                    ( md5=new.md5 OR md5 is NULL) AND 
	                    ( crc=new.crc) AND
	                    ( size=new.Size OR size is NULL ) AND
	                    status!='nodump';
                END;
            ");
            ExecuteNonQuery(@"
                CREATE TRIGGER IF NOT EXISTS [FileDelete] 
                AFTER DELETE ON [FILES] 
                FOR EACH ROW 
                BEGIN 
                    UPDATE ROM SET FileId=null WHERE FileId=OLD.FileId;
                END;
            ");
            ExecuteNonQuery(@"
                CREATE TRIGGER IF NOT EXISTS [RomInsert_FileIdIsNotNull] 
                AFTER INSERT ON [ROM] 
                FOR EACH ROW WHEN new.FileId IS NOT NULL
                BEGIN 
                    UPDATE GAME SET
                        RomTotal=RomTotal+1,
                        RomGot=RomGot+1
                    WHERE 
                        Game.GameId=New.GameId;
                END;
            ");
            ExecuteNonQuery(@"
                CREATE TRIGGER IF NOT EXISTS [RomInsert_FileIdIsNull] 
                AFTER INSERT ON [ROM] 
                FOR EACH ROW WHEN new.FileId IS NULL 
                BEGIN 
                    UPDATE GAME SET
                        RomTotal=RomTotal+1
                    WHERE 
                        Game.GameId=New.GameId;
                END;
            ");
            ExecuteNonQuery(@"
                CREATE TRIGGER IF NOT EXISTS [RomDelete_FileIdIsNotNull] 
                AFTER DELETE ON [ROM] 
                FOR EACH ROW WHEN old.FileId IS NOT NULL
                BEGIN 
                    UPDATE GAME SET
                        RomTotal=RomTotal-1,
                        RomGot=RomGot-1
                    WHERE 
                        Game.GameId=Old.GameId;
                END;
            ");
            ExecuteNonQuery(@"
                CREATE TRIGGER IF NOT EXISTS [RomDelete_FileIdIsNull] 
                AFTER DELETE ON [ROM] 
                FOR EACH ROW WHEN old.FileId IS NULL 
                BEGIN 
                    UPDATE GAME SET
                        RomTotal=RomTotal-1
                    WHERE 
                        Game.GameId=Old.GameId;
                END;
            ");
            ExecuteNonQuery(@"
                CREATE TRIGGER IF NOT EXISTS [RomUpdate_GettingFileId]
                AFTER UPDATE ON [ROM]
                FOR EACH ROW WHEN new.FileId IS NOT NULL AND old.FileId IS NULL
                BEGIN 
                    UPDATE GAME SET
                        RomGot=RomGot+1
                    WHERE 
                        Game.GameId=New.GameId;
                END;
            ");

            ExecuteNonQuery(@"
                CREATE TRIGGER IF NOT EXISTS [RomUpdate_RemovingFileId]
                AFTER UPDATE ON [ROM]
                FOR EACH ROW WHEN old.FileId IS NOT NULL AND new.FileId IS NULL
                BEGIN 
                    UPDATE GAME SET
                        RomGot=RomGot-1
                    WHERE 
                        Game.GameId=New.GameId;
                END;
            ");

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
            ");

        }


        public static int FindOrInsertIntoDir(int ParentDirId, string Name, string FullName)
        {
            _findInDir.Parameters["FullName"].Value = FullName;
            object resFind = _findInDir.ExecuteScalar();
            if (resFind != null && resFind != DBNull.Value)
            {
                int foundDatId = Convert.ToInt32(resFind);
                if (foundDatId > 0)
                    return foundDatId;
            }

            _insertIntoDir.Parameters["ParentDirId"].Value = ParentDirId;
            _insertIntoDir.Parameters["Name"].Value = Name;
            _insertIntoDir.Parameters["FullName"].Value = FullName;

            object res = _insertIntoDir.ExecuteScalar();

            if (res == null || res == DBNull.Value)
                return 0;
            return Convert.ToInt32(res);
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
                    pTree.DirId = Convert.ToInt32(dr["DirId"]);
                    pTree.dirName = dr["dirname"].ToString();
                    pTree.dirFullName = dr["fullname"].ToString();
                    pTree.Expanded = Convert.ToBoolean(dr["expanded"]);

                    pTree.DatId = dr["DatId"] == DBNull.Value ? null : (int?)Convert.ToInt32(dr["DatId"]);
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

        public static void SetTreeExpanded(int DirId, bool expanded)
        {
            _setTreeExpanded.Parameters["dirId"].Value = DirId;
            _setTreeExpanded.Parameters["expanded"].Value = expanded;
            _setTreeExpanded.ExecuteNonQuery();
        }

        public static bool FindInFiles(rvFile tFile)
        {
            _findInFiles.Parameters["size"].Value = tFile.Size;
            _findInFiles.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
            _findInFiles.Parameters["sha1"].Value = VarFix.ToDBString(tFile.SHA1);
            _findInFiles.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5);

            object res = _findInFiles.ExecuteScalar();

            if (res == null || res == DBNull.Value)
                return false;
            int count = Convert.ToInt32(res);

            return count > 0;
        }

        public static bool FindInROMs(rvFile tFile)
        {
            _findInROMs.Parameters["size"].Value = tFile.Size;
            _findInROMs.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
            _findInROMs.Parameters["sha1"].Value = VarFix.ToDBString(tFile.SHA1);
            _findInROMs.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5);

            Stopwatch sw=new Stopwatch();

            sw.Reset();
            sw.Start();
            object res = _findInROMs.ExecuteScalar();
            sw.Stop();

            if (res == null || res == DBNull.Value)
                return false;
            int count = Convert.ToInt32(res);
            
            Debug.WriteLine("Time ="+sw.ElapsedMilliseconds+" : Found "+count);

            return count > 0;
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

            Debug.WriteLine("Time to add file =" + sw.ElapsedMilliseconds );

        }

        public static int FindExistingDat(string fulldir, string filename, long DatTimeStamp)
        {
            _findExistingDAT.Parameters["fullname"].Value = fulldir + "\\";
            _findExistingDAT.Parameters["filename"].Value = filename;
            _findExistingDAT.Parameters["DatTimeStamp"].Value = DatTimeStamp.ToString();

            object res = _findExistingDAT.ExecuteScalar();

            if (res == null || res == DBNull.Value)
                return 0;
            return Convert.ToInt32(res);

        }

    }
}
