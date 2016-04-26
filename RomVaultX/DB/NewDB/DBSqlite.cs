using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using RomVaultX.IO;
using RomVaultX.Util;
using Convert = System.Convert;

namespace RomVaultX.DB.NewDB
{

    public class DBSqlite : iDB
    {
        private const int DBVersion = 6;
        private string DirFilename;
        private SQLiteConnection Connection;

        public void ConnectToDB()
        {
            DirFilename = AppSettings.ReadSetting("DBFileName");
            if (DirFilename == null)
            {
                AppSettings.AddUpdateAppSettings("DBFileName", "C:\\RomVaultX\\rom");
                DirFilename = AppSettings.ReadSetting("DBFileName");
            }


            DirFilename += DBVersion + ".db3";

            bool datFound = File.Exists(DirFilename);

            Connection = new SQLiteConnection(@"data source=" + DirFilename + ";Version=3");
            Connection.Open();

            CheckDbVersion(ref datFound);

            InitializeSqlCommands();

            if (!datFound)
                MakeDB();

            ExecuteNonQuery("PRAGMA temp_store = MEMORY");
            //ExecuteNonQuery("PRAGMA journal_mode= MEMORY");
            ExecuteNonQuery("Attach Database ':memory:' AS 'memdb'");
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
            File.Delete(DirFilename);
            Connection.Open();
            datFound = false;
        }


        private void ExecuteNonQuery(string query, params object[] args)
        {
            using (SQLiteCommand command = new SQLiteCommand(query,Connection))
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

            ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS [DIR] (
                    [DirId] INTEGER PRIMARY KEY NOT NULL,
                    [ParentDirId] INTEGER NULL,
                    [name] NVARCHAR(300) NOT NULL,
                    [fullname] NVARCHAR(300) NOT NULL,
                    [expanded] BOOLEAN DEFAULT 1 NOT NULL,
                    [found] BOOLEAN DEFAULT 1,
                    [RomTotal] INTEGER NULL,
                    [RomGot] INTEGER NULL,
                    [RomNoDump] INTEGER NULL
                );
             
           ");

            ExecuteNonQuery(@"
                 CREATE TABLE IF NOT EXISTS [DAT] (
                    [DatId] INTEGER  PRIMARY KEY NOT NULL,
                    [DirId] INTEGER  NOT NULL,
                    [Filename] NVARCHAR(300)  NULL,

                    [name] NVARCHAR(100)  NULL,
                    [rootdir] NVARCHAR(10)  NULL,
                    [description] NVARCHAR(10)  NULL,
                    [category] NVARCHAR(10)  NULL,
                    [version] NVARCHAR(10)  NULL,
                    [date] NVARCHAR(10)  NULL,
                    [author] NVARCHAR(10)  NULL,
                    [email] NVARCHAR(10)  NULL,
                    [homepage] NVARCHAR(10)  NULL,
                    [url] NVARCHAR(10)  NULL,
                    [comment] NVARCHAR(10) NULL,
                    [RomTotal] INTEGER DEFAULT 0 NOT NULL,
                    [RomGot] INTEGER DEFAULT 0 NOT NULL,
                    [RomNoDump] INTEGER DEFAULT 0 NOT NULL,
                    [DatTimeStamp] NVARCHAR(20)  NOT NULL,
                    [found] BOOLEAN DEFAULT 1,            
                    FOREIGN KEY(DirId) REFERENCES DIR(DirId)
                );");

            ExecuteNonQuery(@"
                 CREATE TABLE IF NOT EXISTS [GAME] (
                    [GameId] INTEGER  PRIMARY KEY NOT NULL,
                    [DatId] INTEGER NOT NULL,
                    [name] NVARCHAR(200) NOT NULL,
                    [description] NVARCHAR(220) NULL,
                    [manufacturer] NVARCHAR(20) NULL,
                    [cloneof] NVARCHAR(20) NULL,
                    [romof] NVARCHAR(20) NULL,
                    [sampleof] NVARCHAR(20) NULL,
                    [sourcefile] NVARCHAR(20) NULL,
                    [isbios] NVARCHAR(20) NULL,
                    [board] NVARCHAR(20) NULL,
                    [year] NVARCHAR(20) NULL,
                    [istrurip] BOOLEAN DEFAULT 0 NOT NULL,
                    [publisher] NVARCHAR(20) NULL,
                    [developer] NVARCHAR(20) NULL,
                    [edition] NVARCHAR(20) NULL,
                    [version] NVARCHAR(20) NULL,
                    [type] NVARCHAR(20) NULL,
                    [media] NVARCHAR(20) NULL,
                    [language] NVARCHAR(20) NULL,
                    [players] NVARCHAR(20) NULL,
                    [ratings] NVARCHAR(20) NULL,
                    [genre] NVARCHAR(20) NULL,
                    [peripheral] NVARCHAR(20) NULL,
                    [barcode] NVARCHAR(20) NULL,
                    [mediacatalognumber] NVARCHAR(20),
                    [RomTotal] INTEGER DEFAULT 0 NOT NULL,
                    [RomGot] INTEGER DEFAULT 0 NOT NULL,
                    [RomNoDump] INTEGER DEFAULT 0 NOT NULL,
                    [ZipFileLength] INTEGER NULL, 
                    [ZipFileTimeStamp] INTEGER NULL,
                    [CentralDirectory] BLOB NULL,
                    [CentralDirectoryOffset] INTEGER NULL,
                    [CentralDirectoryLength] INTEGER NULL,
                    FOREIGN KEY(DatId) REFERENCES DAT(DatId)
                );");

            ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS [FILES] (
                    [FileId] INTEGER PRIMARY KEY NOT NULL,
                    [size] INTEGER NOT NULL,
                    [compressedsize] INTEGER NULL,
                    [crc] VARCHAR(8) NULL,
                    [sha1] VARCHAR(40) NULL,
                    [md5] VARCHAR(32) NULL,
                    [alttype] VARCHAR(8) NULL,
                    [altsize] INTEGER NULL,
                    [altcrc] VARCHAR(8) NULL,
                    [altsha1] VARCHAR(40) NULL,
                    [altmd5] VARCHAR(32) NULL
                );
            ");

            ExecuteNonQuery(@"
               CREATE TABLE IF NOT EXISTS [ROM] (
                    [RomId] INTEGER PRIMARY KEY NOT NULL,
                    [GameId] INTEGER NOT NULL,
                    [name] NVARCHAR(320) NOT NULL,
                    [type] INTEGER NULL,
                    [size] INTEGER NULL,
                    [crc] VARCHAR(8) NULL,
                    [sha1] VARCHAR(40) NULL,
                    [md5] VARCHAR(32) NULL,
                    [merge] VARCHAR(20) NULL,
                    [status] VARCHAR(20) NULL,
                    [FileId] INTEGER NULL,
                    [LocalFileHeader] BLOB NULL,
                    [LocalFileHeaderOffset] INTEGER NULL,
                    [LocalFileHeaderLength] INTEGER NULL,
                    FOREIGN KEY(GameId) REFERENCES Game(GameId),
                    FOREIGN KEY(FileId) REFERENCES Files(FileId)
                );");

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

            MakeIndex();
        }

        public void MakeIndex()
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
            ExecuteNonQuery("BEGIN");
        }

        public void Commit()
        {
            ExecuteNonQuery("COMMIT");
        }

        public void UpdateGotTotal()
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


            SQLiteCommand sqlNullCount = new SQLiteCommand(@"SELECT COUNT(1) FROM dir WHERE RomTotal IS null",Connection);

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
            sqlNullCount.Dispose();
        }

        private SQLiteCommand CommandRvDatWrite;
        private SQLiteCommand CommandRvDatRead;

        private SQLiteCommand CommandRvGameWrite;
        private SQLiteCommand CommandRvGameRead;
        private SQLiteCommand CommandRvGameReadDatGames;

        private SQLiteCommand CommandRvRomWrite;
        private SQLiteCommand CommandRvRomReader;

        private SQLiteCommand CommandRvFileWrite;
        private SQLiteCommand CommandRvFileUpdateRom;
        private SQLiteCommand CommandRvFileUpdateRomAlt;
        private SQLiteCommand CommandRvFileUpdateZeroRom;

        private SQLiteCommand CommandRvGameGridRowRead;


        private SQLiteCommand CommandClearfoundDirDATs;
        private SQLiteCommand CommandCleanupNotFoundDATs;
        private SQLiteCommand CommandCountDATs;

        private SQLiteCommand CommandSHA1;
        private SQLiteCommand CommandMD5;
        private SQLiteCommand CommandCRC;
        private SQLiteCommand CommandSize;

        private SQLiteCommand CommandSHA1Alt;
        private SQLiteCommand CommandMD5Alt;
        private SQLiteCommand CommandCRCAlt;


        private SQLiteCommand CommandFindDat;
        private SQLiteCommand CommandSetDatFound;


        private SQLiteCommand CommandFindInDir;
        private SQLiteCommand CommandSetDirFound;
        private SQLiteCommand CommandInsertIntoDir;


        private SQLiteCommand CommandFindInFiles;


        private SQLiteCommand CommandFindInROMs;
        private SQLiteCommand CommandFindInROMsAlt;
        private SQLiteCommand CommandFindInROMsZero;

        private SQLiteCommand CommandGetFile;

        private SQLiteCommand CommandReadTree;
        private SQLiteCommand CommandSetTreeExpanded;

        private SQLiteCommand CommandGetFirstExpanded;
        private SQLiteCommand CommandUpdateExpanded;

        private void InitializeSqlCommands()
        {
            CommandRvDatWrite = new SQLiteCommand(@"
                INSERT INTO DAT ( DirId, Filename, name, rootdir, description, category, version, date, author, email, homepage, url, comment,DatTimeStamp)
                VALUES            (@DirId,@Filename,@name,@rootdir,@description,@category,@version,@date,@author,@email,@homepage,@url,@comment,@DatTimeStamp);

                SELECT last_insert_rowid();",Connection);

            CommandRvDatWrite.Parameters.Add(new SQLiteParameter("DirId"));
            CommandRvDatWrite.Parameters.Add(new SQLiteParameter("Filename"));
            CommandRvDatWrite.Parameters.Add(new SQLiteParameter("name"));
            CommandRvDatWrite.Parameters.Add(new SQLiteParameter("rootdir"));
            CommandRvDatWrite.Parameters.Add(new SQLiteParameter("description"));
            CommandRvDatWrite.Parameters.Add(new SQLiteParameter("category"));
            CommandRvDatWrite.Parameters.Add(new SQLiteParameter("version"));
            CommandRvDatWrite.Parameters.Add(new SQLiteParameter("date"));
            CommandRvDatWrite.Parameters.Add(new SQLiteParameter("author"));
            CommandRvDatWrite.Parameters.Add(new SQLiteParameter("email"));
            CommandRvDatWrite.Parameters.Add(new SQLiteParameter("homepage"));
            CommandRvDatWrite.Parameters.Add(new SQLiteParameter("url"));
            CommandRvDatWrite.Parameters.Add(new SQLiteParameter("comment"));
            CommandRvDatWrite.Parameters.Add(new SQLiteParameter("DatTimeStamp"));


            CommandRvDatRead = new SQLiteCommand(@"
                SELECT DirId,Filename,name,rootdir,description,category,version,date,author,email,homepage,url,comment 
                FROM DAT WHERE DatId=@datId ORDER BY Filename",Connection);
            CommandRvDatRead.Parameters.Add(new SQLiteParameter("datId"));


            CommandRvGameWrite = new SQLiteCommand(@"
                INSERT INTO GAME ( DatId, name, description, manufacturer, cloneof, romof, sourcefile, isbios, board, year, istrurip, publisher, developer, edition, version, type, media, language, players, ratings, genre, peripheral, barcode, mediacatalognumber)
                          VALUES (@DatId,@Name,@Description,@Manufacturer,@CloneOf,@RomOf,@SourceFile,@IsBios,@Board,@Year,@IsTrurip,@Publisher,@Developer,@Edition,@Version,@Type,@Media,@Language,@Players,@Ratings,@Genre,@Peripheral,@BarCode,@MediaCatalogNumber);

                SELECT last_insert_rowid();",Connection);

            CommandRvGameWrite.Parameters.Add(new SQLiteParameter("DatId")); //DatId;
            CommandRvGameWrite.Parameters.Add(new SQLiteParameter("Name")); //Name;
            CommandRvGameWrite.Parameters.Add(new SQLiteParameter("Description")); //Description;
            CommandRvGameWrite.Parameters.Add(new SQLiteParameter("Manufacturer")); //Manufacturer;

            CommandRvGameWrite.Parameters.Add(new SQLiteParameter("CloneOf")); //CloneOf;
            CommandRvGameWrite.Parameters.Add(new SQLiteParameter("RomOf")); //RomOf;
            CommandRvGameWrite.Parameters.Add(new SQLiteParameter("SampleOf")); //SampleOf;
            CommandRvGameWrite.Parameters.Add(new SQLiteParameter("Sourcefile")); //SourceFile;
            CommandRvGameWrite.Parameters.Add(new SQLiteParameter("IsBios")); //IsBios;
            CommandRvGameWrite.Parameters.Add(new SQLiteParameter("Board")); //Board;
            CommandRvGameWrite.Parameters.Add(new SQLiteParameter("Year")); //Year;

            CommandRvGameWrite.Parameters.Add(new SQLiteParameter("IsTrurip")); //IsTrurip;
            CommandRvGameWrite.Parameters.Add(new SQLiteParameter("Publisher")); //Publisher;
            CommandRvGameWrite.Parameters.Add(new SQLiteParameter("Developer")); //Developer;
            CommandRvGameWrite.Parameters.Add(new SQLiteParameter("Edition")); //Edition;
            CommandRvGameWrite.Parameters.Add(new SQLiteParameter("Version")); //Version;
            CommandRvGameWrite.Parameters.Add(new SQLiteParameter("Type")); //Type;
            CommandRvGameWrite.Parameters.Add(new SQLiteParameter("Media")); //Media;
            CommandRvGameWrite.Parameters.Add(new SQLiteParameter("Language")); //Language;
            CommandRvGameWrite.Parameters.Add(new SQLiteParameter("Players")); //Players;
            CommandRvGameWrite.Parameters.Add(new SQLiteParameter("Ratings")); //Ratings;
            CommandRvGameWrite.Parameters.Add(new SQLiteParameter("Genre")); //Genre;
            CommandRvGameWrite.Parameters.Add(new SQLiteParameter("Peripheral")); //Peripheral;
            CommandRvGameWrite.Parameters.Add(new SQLiteParameter("BarCode")); //BarCode;
            CommandRvGameWrite.Parameters.Add(new SQLiteParameter("MediaCatalogNumber")); //MediaCatalogNumber;        


            CommandRvGameRead = new SQLiteCommand(@"
                SELECT GameId, DatId, name, description, manufacturer, cloneof, romof, sourcefile, isbios, board, year, istrurip, publisher, developer, edition, version, type, media, language, players, ratings, genre, peripheral, barcode, mediacatalognumber
                    FROM GAME WHERE GameId=@GameId ORDER BY name",Connection);
            CommandRvGameRead.Parameters.Add(new SQLiteParameter("GameId"));

            CommandRvGameReadDatGames = new SQLiteCommand(@"
                SELECT GameId, DatId, name, description, manufacturer, cloneof, romof, sourcefile, isbios, board, year, istrurip, publisher, developer, edition, version, type, media, language, players, ratings, genre, peripheral, barcode, mediacatalognumber
                    FROM GAME WHERE DatId=@DatId ORDER BY name",Connection);
            CommandRvGameReadDatGames.Parameters.Add(new SQLiteParameter("DatId"));


            CommandRvRomWrite = new SQLiteCommand(@"
                INSERT INTO ROM  ( GameId, name,type, size, crc, sha1, md5, merge, status,FileId)
                            VALUES (@GameId,@Name,@Type,@Size,@CRC,@SHA1,@MD5,@Merge,@Status,@FileId);

                SELECT last_insert_rowid();",Connection);

            CommandRvRomWrite.Parameters.Add(new SQLiteParameter("GameId"));
            CommandRvRomWrite.Parameters.Add(new SQLiteParameter("Name"));
            CommandRvRomWrite.Parameters.Add(new SQLiteParameter("Type"));
            CommandRvRomWrite.Parameters.Add(new SQLiteParameter("Size"));
            CommandRvRomWrite.Parameters.Add(new SQLiteParameter("CRC"));
            CommandRvRomWrite.Parameters.Add(new SQLiteParameter("SHA1"));
            CommandRvRomWrite.Parameters.Add(new SQLiteParameter("MD5"));
            CommandRvRomWrite.Parameters.Add(new SQLiteParameter("Merge"));
            CommandRvRomWrite.Parameters.Add(new SQLiteParameter("Status"));
            CommandRvRomWrite.Parameters.Add(new SQLiteParameter("FileId"));

            CommandRvRomReader = new SQLiteCommand(
                @"SELECT RomId,name,
                    type,
                    rom.size,
                    rom.crc,
                    rom.sha1,
                    rom.md5,
                    merge,status,
                    rom.FileId,
                    files.size as fileSize,
                    files.compressedsize as fileCompressedSize,
                    files.crc as filecrc,
                    files.sha1 as filesha1,
                    files.md5 as filemd5
                FROM rom LEFT OUTER JOIN files ON files.FileId=rom.FileId WHERE GameId=@GameId ORDER BY name",Connection);
            CommandRvRomReader.Parameters.Add(new SQLiteParameter("GameId"));


            CommandRvFileWrite = new SQLiteCommand(
    @"INSERT INTO FILES (size,compressedsize,crc,sha1,md5,alttype,altsize,altcrc,altsha1,altmd5)
                        VALUES (@Size,@compressedsize,@CRC,@SHA1,@MD5,@alttype,@altsize,@altcrc,@altsha1,@altmd5);

                SELECT last_insert_rowid();",Connection);

            CommandRvFileWrite.Parameters.Add(new SQLiteParameter("size"));
            CommandRvFileWrite.Parameters.Add(new SQLiteParameter("compressedsize"));
            CommandRvFileWrite.Parameters.Add(new SQLiteParameter("crc"));
            CommandRvFileWrite.Parameters.Add(new SQLiteParameter("sha1"));
            CommandRvFileWrite.Parameters.Add(new SQLiteParameter("md5"));
            CommandRvFileWrite.Parameters.Add(new SQLiteParameter("alttype"));
            CommandRvFileWrite.Parameters.Add(new SQLiteParameter("altsize"));
            CommandRvFileWrite.Parameters.Add(new SQLiteParameter("altcrc"));
            CommandRvFileWrite.Parameters.Add(new SQLiteParameter("altsha1"));
            CommandRvFileWrite.Parameters.Add(new SQLiteParameter("altmd5"));

            CommandRvFileUpdateRom = new SQLiteCommand(
                @"
                    UPDATE ROM SET 
	                    FileId = @FileId,
                        LocalFileHeader = null,
                        LocalFileHeaderOffset = null,
                        LocalFileHeaderLength=null
                    WHERE
	                    (                 sha1 = @sha1 ) AND
	                    ( md5  is NULL OR md5  = @md5  ) AND 
	                    ( crc  is NULL OR crc  = @crc  ) AND
	                    ( size is NULL OR size = @Size ) AND
	                    ( status != 'nodump' OR status is NULL) AND 
                        FileId IS NULL;
		
                    UPDATE ROM SET 
	                    FileId = @FileId,
                        LocalFileHeader = null,
                        LocalFileHeaderOffset = null,
                        LocalFileHeaderLength=null
                    WHERE
	                    (                 md5  = @md5  ) AND 
	                    ( sha1 is NULL OR sha1 = @sha1 ) AND
	                    ( crc  is NULL OR crc  = @crc  ) AND
	                    ( size is NULL OR size = @Size ) AND
	                    ( status != 'nodump' OR status is NULL) AND 
                        FileId IS NULL;
		
                    UPDATE ROM SET 
	                    FileId = @FileId,
                        LocalFileHeader = null,
                        LocalFileHeaderOffset = null,
                        LocalFileHeaderLength=null
                    WHERE
	                    (                 crc  = @crc  ) AND
	                    ( sha1 is NULL OR sha1 = @sha1 ) AND
	                    ( md5  is NULL OR md5  = @md5  ) AND 
	                    ( size is NULL OR size = @Size ) AND
	                    ( status != 'nodump' OR status is NULL) AND 
                        FileId IS NULL;
                ",Connection);
            CommandRvFileUpdateRom.Parameters.Add(new SQLiteParameter("FileId"));
            CommandRvFileUpdateRom.Parameters.Add(new SQLiteParameter("size"));
            CommandRvFileUpdateRom.Parameters.Add(new SQLiteParameter("crc"));
            CommandRvFileUpdateRom.Parameters.Add(new SQLiteParameter("sha1"));
            CommandRvFileUpdateRom.Parameters.Add(new SQLiteParameter("md5"));

            CommandRvFileUpdateRomAlt = new SQLiteCommand(
                @"
                    UPDATE ROM SET 
	                    FileId = @FileId,
                        LocalFileHeader = null,
                        LocalFileHeaderOffset = null,
                        LocalFileHeaderLength=null
                    WHERE
                        (                 type = @type ) AND
	                    (                 sha1 = @sha1 ) AND
	                    ( md5  is NULL OR md5  = @md5  ) AND 
	                    ( crc  is NULL OR crc  = @crc  ) AND
	                    ( size is NULL OR size = @Size ) AND
	                    ( status != 'nodump' OR status is NULL) AND 
                        FileId IS NULL;
		
                    UPDATE ROM SET 
	                    FileId = @FileId,
                        LocalFileHeader = null,
                        LocalFileHeaderOffset = null,
                        LocalFileHeaderLength=null
                    WHERE
                        (                 type = @type ) AND
	                    (                 md5  = @md5  ) AND 
	                    ( sha1 is NULL OR sha1 = @sha1 ) AND
	                    ( crc  is NULL OR crc  = @crc  ) AND
	                    ( size is NULL OR size = @Size ) AND
	                    ( status != 'nodump' OR status is NULL) AND 
                        FileId IS NULL;
		
                    UPDATE ROM SET 
	                    FileId = @FileId,
                        LocalFileHeader = null,
                        LocalFileHeaderOffset = null,
                        LocalFileHeaderLength=null
                    WHERE
                        (                 type = @type ) AND
	                    (                 crc  = @crc  ) AND
	                    ( sha1 is NULL OR sha1 = @sha1 ) AND
	                    ( md5  is NULL OR md5  = @md5  ) AND 
	                    ( size is NULL OR size = @Size ) AND
	                    ( status != 'nodump' OR status is NULL) AND 
                        FileId IS NULL;
                ",Connection);
            CommandRvFileUpdateRomAlt.Parameters.Add(new SQLiteParameter("FileId"));
            CommandRvFileUpdateRomAlt.Parameters.Add(new SQLiteParameter("type"));
            CommandRvFileUpdateRomAlt.Parameters.Add(new SQLiteParameter("size"));
            CommandRvFileUpdateRomAlt.Parameters.Add(new SQLiteParameter("crc"));
            CommandRvFileUpdateRomAlt.Parameters.Add(new SQLiteParameter("sha1"));
            CommandRvFileUpdateRomAlt.Parameters.Add(new SQLiteParameter("md5"));



            CommandRvFileUpdateZeroRom = new SQLiteCommand(
                @"
                    UPDATE ROM SET 
	                    FileId = @FileId,
                        LocalFileHeader = null,
                        LocalFileHeaderOffset = null,
                        LocalFileHeaderLength=null
                    WHERE
	                    ( Size=0 ) AND
	                    ( crc  is NULL OR crc  = @crc  ) AND
	                    ( sha1 is NULL OR sha1 = @sha1 ) AND
	                    ( md5  is NULL OR md5  = @md5  ) AND 
	                    ( status != 'nodump' OR status is NULL) AND 
                        FileId IS NULL;
                ",Connection);
            CommandRvFileUpdateZeroRom.Parameters.Add(new SQLiteParameter("FileId"));
            CommandRvFileUpdateZeroRom.Parameters.Add(new SQLiteParameter("crc"));
            CommandRvFileUpdateZeroRom.Parameters.Add(new SQLiteParameter("sha1"));
            CommandRvFileUpdateZeroRom.Parameters.Add(new SQLiteParameter("md5"));


            CommandRvGameGridRowRead = new SQLiteCommand(@"
                    SELECT GameId,Name,Description,RomTotal,RomGot,RomNoDump FROM game WHERE DatId=@datId ORDER BY Name",Connection);
            CommandRvGameGridRowRead.Parameters.Add(new SQLiteParameter("datId"));




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


            CommandSHA1 = new SQLiteCommand(@"
                       select FileId from memdb.FILESMEM
                            WHERE
	                                    (                  @sha1 = sha1 ) AND
	                                    ( @md5  is NULL OR @md5  = md5  ) AND
	                                    ( @crc  is NULL OR @crc  = crc  ) AND
	                                    ( @size is NULL OR @size = Size )
                            limit 1
                ", Connection);

            CommandSHA1.Parameters.Add(new SQLiteParameter("sha1"));
            CommandSHA1.Parameters.Add(new SQLiteParameter("md5"));
            CommandSHA1.Parameters.Add(new SQLiteParameter("crc"));
            CommandSHA1.Parameters.Add(new SQLiteParameter("size"));


            CommandSHA1Alt = new SQLiteCommand(@"
                       select FileId from memdb.FILESMEM
                            WHERE
                                        (               @alttype = alttype ) AND
	                                    (                  @sha1 = altsha1 ) AND
	                                    ( @md5  is NULL OR @md5  = altmd5  ) AND
	                                    ( @crc  is NULL OR @crc  = altcrc  ) AND
	                                    ( @size is NULL OR @size = altSize )
                            limit 1
                ", Connection);

            CommandSHA1Alt.Parameters.Add(new SQLiteParameter("alttype"));
            CommandSHA1Alt.Parameters.Add(new SQLiteParameter("sha1"));
            CommandSHA1Alt.Parameters.Add(new SQLiteParameter("md5"));
            CommandSHA1Alt.Parameters.Add(new SQLiteParameter("crc"));
            CommandSHA1Alt.Parameters.Add(new SQLiteParameter("size"));



            CommandMD5 = new SQLiteCommand(@"
                       select FileId from memdb.FILESMEM
                            WHERE
	                                    (                  @md5  = md5  ) AND
	                                    ( @crc  is NULL OR @crc  = crc  ) AND
	                                    ( @size is NULL OR @size = Size )
                            limit 1
                ", Connection);

            CommandMD5.Parameters.Add(new SQLiteParameter("md5"));
            CommandMD5.Parameters.Add(new SQLiteParameter("crc"));
            CommandMD5.Parameters.Add(new SQLiteParameter("size"));

            CommandMD5Alt = new SQLiteCommand(@"
                       select FileId from memdb.FILESMEM
                            WHERE
                                        (               @alttype = alttype ) AND
	                                    (                  @md5  = altmd5  ) AND
	                                    ( @crc  is NULL OR @crc  = altcrc  ) AND
	                                    ( @size is NULL OR @size = altSize )
                            limit 1
                ", Connection);

            CommandMD5Alt.Parameters.Add(new SQLiteParameter("alttype"));
            CommandMD5Alt.Parameters.Add(new SQLiteParameter("md5"));
            CommandMD5Alt.Parameters.Add(new SQLiteParameter("crc"));
            CommandMD5Alt.Parameters.Add(new SQLiteParameter("size"));


            CommandCRC = new SQLiteCommand(@"
                       select FileId from memdb.FILESMEM
                            WHERE
	                                    (                  @crc  = crc  ) AND
	                                    ( @size is NULL OR @size = Size )
                            limit 1
                ", Connection);

            CommandCRC.Parameters.Add(new SQLiteParameter("crc"));
            CommandCRC.Parameters.Add(new SQLiteParameter("size"));

            CommandCRCAlt = new SQLiteCommand(@"
                       select FileId from memdb.FILESMEM
                            WHERE
                                        (               @alttype = alttype ) AND
	                                    (                  @crc  = altcrc  ) AND
	                                    ( @size is NULL OR @size = altSize )
                            limit 1
                ", Connection);

            CommandCRCAlt.Parameters.Add(new SQLiteParameter("crc"));
            CommandCRCAlt.Parameters.Add(new SQLiteParameter("size"));



            CommandSize = new SQLiteCommand(@"
                       select FileId from memdb.FILESMEM
                            WHERE
	                                    ( @size = Size )
                            limit 1
                ", Connection);

            CommandSize.Parameters.Add(new SQLiteParameter("size"));



            CommandFindDat = new SQLiteCommand(@"
                SELECT DatId FROM Dat,Dir WHERE Dat.DirId=Dir.DirId AND fullname=@fullname AND Filename=@filename AND DatTimeStamp=@DatTimeStamp
            ",Connection);
            CommandFindDat.Parameters.Add(new SQLiteParameter("fullname"));
            CommandFindDat.Parameters.Add(new SQLiteParameter("filename"));
            CommandFindDat.Parameters.Add(new SQLiteParameter("DatTimeStamp"));


            CommandSetDatFound = new SQLiteCommand(@"
                Update Dat SET Found=1 WHERE DatId=@DatId
            ",Connection);
            CommandSetDatFound.Parameters.Add(new SQLiteParameter("DatId"));



            CommandFindInDir = new SQLiteCommand(@"SELECT DirId FROM dir WHERE fullname=@fullname LIMIT 1",Connection);
            CommandFindInDir.Parameters.Add(new SQLiteParameter("fullname"));

            CommandSetDirFound = new SQLiteCommand(@"Update Dir SET Found=1 WHERE DirId=@DirId",Connection);
            CommandSetDirFound.Parameters.Add(new SQLiteParameter("DirId"));

            CommandInsertIntoDir = new SQLiteCommand(@"
                    INSERT INTO DIR (ParentDirId,Name,FullName)
                         VALUES (@ParentDirId,@Name,@FullName);

                         SELECT last_insert_rowid();
                    ",Connection);

            CommandInsertIntoDir.Parameters.Add(new SQLiteParameter("ParentDirId"));
            CommandInsertIntoDir.Parameters.Add(new SQLiteParameter("Name"));
            CommandInsertIntoDir.Parameters.Add(new SQLiteParameter("FullName"));


            CommandFindInFiles = new SQLiteCommand(@"
                    SELECT COUNT(1) FROM FILES WHERE
                        size=@size AND crc=@CRC and sha1=@SHA1 and md5=@MD5",Connection);
            CommandFindInFiles.Parameters.Add(new SQLiteParameter("size"));
            CommandFindInFiles.Parameters.Add(new SQLiteParameter("crc"));
            CommandFindInFiles.Parameters.Add(new SQLiteParameter("sha1"));
            CommandFindInFiles.Parameters.Add(new SQLiteParameter("md5"));





            CommandFindInROMs = new SQLiteCommand(@"
                        SELECT
                        (
                            SELECT count(1) FROM ROM WHERE
                                ( sha1=@SHA1 ) AND 
                                ( md5=@MD5 OR md5 is NULL) AND
                                ( crc=@CRC OR crc is NULL ) AND
                                ( size=@size OR size is NULL ) AND
                                ( status!='nodump' or status is NULL)
                        ) +
                        (
                            SELECT count(1) FROM ROM WHERE
                                ( md5=@MD5 ) AND
                                ( sha1=@SHA1 OR sha1 is NULL ) AND 
                                ( crc=@CRC OR crc is NULL ) AND
                                ( size=@size OR size is NULL ) AND
                                ( status!='nodump' or status is NULL)
                        ) +
                        (
                            SELECT count(1) FROM ROM WHERE
                                ( crc=@CRC ) AND
                                ( sha1=@SHA1 OR sha1 is NULL ) AND 
                                ( md5=@MD5 OR md5 is NULL) AND
                                ( size=@size OR size is NULL ) AND
                                ( status!='nodump' or status is NULL)
                        ) 
                        AS TotalFound",Connection);
            CommandFindInROMs.Parameters.Add(new SQLiteParameter("size"));
            CommandFindInROMs.Parameters.Add(new SQLiteParameter("crc"));
            CommandFindInROMs.Parameters.Add(new SQLiteParameter("sha1"));
            CommandFindInROMs.Parameters.Add(new SQLiteParameter("md5"));

            CommandFindInROMsAlt = new SQLiteCommand(@"
                        SELECT
                        (
                            SELECT count(1) FROM ROM WHERE
                                ( type=@type ) AND
                                ( sha1=@SHA1 ) AND 
                                ( md5=@MD5 OR md5 is NULL) AND
                                ( crc=@CRC OR crc is NULL ) AND
                                ( size=@size OR size is NULL ) AND
                                ( status!='nodump' or status is NULL)
                        ) +
                        (
                            SELECT count(1) FROM ROM WHERE
                                ( type=@type ) AND
                                ( md5=@MD5 ) AND
                                ( sha1=@SHA1 OR sha1 is NULL ) AND 
                                ( crc=@CRC OR crc is NULL ) AND
                                ( size=@size OR size is NULL ) AND
                                ( status!='nodump' or status is NULL)
                        ) +
                        (
                            SELECT count(1) FROM ROM WHERE
                                ( type=@type ) AND
                                ( crc=@CRC ) AND
                                ( sha1=@SHA1 OR sha1 is NULL ) AND 
                                ( md5=@MD5 OR md5 is NULL) AND
                                ( size=@size OR size is NULL ) AND
                                ( status!='nodump' or status is NULL)
                        ) 
                        AS TotalFound",Connection);
            CommandFindInROMsAlt.Parameters.Add(new SQLiteParameter("type"));
            CommandFindInROMsAlt.Parameters.Add(new SQLiteParameter("size"));
            CommandFindInROMsAlt.Parameters.Add(new SQLiteParameter("crc"));
            CommandFindInROMsAlt.Parameters.Add(new SQLiteParameter("sha1"));
            CommandFindInROMsAlt.Parameters.Add(new SQLiteParameter("md5"));


            CommandFindInROMsZero = new SQLiteCommand(@"
                    SELECT count(1) AS TotalFound FROM ROM WHERE
                        ( sha1=@SHA1 OR sha1 is NULL ) AND 
                        ( md5=@MD5 OR md5 is NULL) AND
                        ( crc=@CRC OR crc is NULL ) AND
                        ( size=0 ) AND
                        ( status!='nodump' or status is NULL)",Connection);
            CommandFindInROMsZero.Parameters.Add(new SQLiteParameter("crc"));
            CommandFindInROMsZero.Parameters.Add(new SQLiteParameter("sha1"));
            CommandFindInROMsZero.Parameters.Add(new SQLiteParameter("md5"));

            CommandGetFile = new SQLiteCommand(@"
                    SELECT sha1 FROM FILES WHERE
                        fileId=@fileId",Connection);
            CommandGetFile.Parameters.Add(new SQLiteParameter("fileId"));



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
                    ORDER BY dir.Fullname,dat.Filename",Connection);

            CommandSetTreeExpanded = new SQLiteCommand(@"
                    UPDATE dir SET expanded=@expanded WHERE DirId=@dirId",Connection);
            CommandSetTreeExpanded.Parameters.Add(new SQLiteParameter("expanded"));
            CommandSetTreeExpanded.Parameters.Add(new SQLiteParameter("dirId"));


            CommandGetFirstExpanded=new SQLiteCommand(@"
                SELECT expanded FROM dir WHERE ParentDirId=@DirId ORDER BY fullname LIMIT 1
            ",Connection);
            CommandGetFirstExpanded.Parameters.Add(new SQLiteParameter("DirId"));
        }



        public uint RvDatWrite(RvDat dat)
        {
            CommandRvDatWrite.Parameters["DirId"].Value = dat.DirId;
            CommandRvDatWrite.Parameters["Filename"].Value = dat.Filename;
            CommandRvDatWrite.Parameters["name"].Value = dat.Name;
            CommandRvDatWrite.Parameters["rootdir"].Value = dat.RootDir;
            CommandRvDatWrite.Parameters["description"].Value = dat.Description;
            CommandRvDatWrite.Parameters["category"].Value = dat.Category;
            CommandRvDatWrite.Parameters["version"].Value = dat.Version;
            CommandRvDatWrite.Parameters["date"].Value = dat.Date;
            CommandRvDatWrite.Parameters["author"].Value = dat.Author;
            CommandRvDatWrite.Parameters["email"].Value = dat.Email;
            CommandRvDatWrite.Parameters["homepage"].Value = dat.Homepage;
            CommandRvDatWrite.Parameters["url"].Value = dat.URL;
            CommandRvDatWrite.Parameters["comment"].Value = dat.Comment;
            CommandRvDatWrite.Parameters["DatTimeStamp"].Value = dat.DatTimeStamp.ToString();
            object res = CommandRvDatWrite.ExecuteScalar();

            if (res == null || res == DBNull.Value)
                return 0;
            return Convert.ToUInt32(res);
        }

        public void RvDatRead(uint datId, RvDat dat)
        {
            CommandRvDatRead.Parameters["DatID"].Value = datId;

            using (DbDataReader dr = CommandRvDatRead.ExecuteReader())
            {
                if (dr.Read())
                {
                    dat.DatId = datId;
                    dat.DirId = Convert.ToUInt32(dr["DirId"]);
                    dat.Filename = dr["filename"].ToString();
                    dat.Name = dr["name"].ToString();
                    dat.RootDir = dr["rootdir"].ToString();
                    dat.Description = dr["description"].ToString();
                    dat.Category = dr["category"].ToString();
                    dat.Version = dr["version"].ToString();
                    dat.Date = dr["date"].ToString();
                    dat.Author = dr["author"].ToString();
                    dat.Email = dr["email"].ToString();
                    dat.Homepage = dr["homepage"].ToString();
                    dat.URL = dr["url"].ToString();
                    dat.Comment = dr["comment"].ToString();
                }
                dr.Close();
            }
        }

        public uint RvGameWrite(RvGame game)
        {
            CommandRvGameWrite.Parameters["DatId"].Value = game.DatId;
            CommandRvGameWrite.Parameters["Name"].Value = game.Name;
            CommandRvGameWrite.Parameters["Description"].Value = game.Description;
            CommandRvGameWrite.Parameters["Manufacturer"].Value = game.Manufacturer;

            CommandRvGameWrite.Parameters["CloneOf"].Value = game.CloneOf;
            CommandRvGameWrite.Parameters["RomOf"].Value = game.RomOf;
            CommandRvGameWrite.Parameters["SampleOf"].Value = game.SampleOf;
            CommandRvGameWrite.Parameters["sourcefile"].Value = game.SourceFile;
            CommandRvGameWrite.Parameters["IsBios"].Value = game.IsBios;
            CommandRvGameWrite.Parameters["Board"].Value = game.Board;
            CommandRvGameWrite.Parameters["Year"].Value = game.Year;

            CommandRvGameWrite.Parameters["IsTrurip"].Value = game.IsTrurip;
            CommandRvGameWrite.Parameters["Publisher"].Value = game.Publisher;
            CommandRvGameWrite.Parameters["Developer"].Value = game.Developer;
            CommandRvGameWrite.Parameters["Edition"].Value = game.Edition;
            CommandRvGameWrite.Parameters["Version"].Value = game.Version;
            CommandRvGameWrite.Parameters["Type"].Value = game.Type;
            CommandRvGameWrite.Parameters["Media"].Value = game.Media;
            CommandRvGameWrite.Parameters["Language"].Value = game.Language;
            CommandRvGameWrite.Parameters["Players"].Value = game.Players;
            CommandRvGameWrite.Parameters["Ratings"].Value = game.Ratings;
            CommandRvGameWrite.Parameters["Genre"].Value = game.Genre;
            CommandRvGameWrite.Parameters["Peripheral"].Value = game.Peripheral;
            CommandRvGameWrite.Parameters["BarCode"].Value = game.BarCode;
            CommandRvGameWrite.Parameters["MediaCatalogNumber"].Value = game.MediaCatalogNumber;

            object res = CommandRvGameWrite.ExecuteScalar();

            if (res == null || res == DBNull.Value)
                return 0;
            return Convert.ToUInt32(res);
        }

        public void RvGameRead(int gameId, RvGame game)
        {
            CommandRvGameRead.Parameters["GameId"].Value = gameId;

            using (DbDataReader dr = CommandRvGameRead.ExecuteReader())
            {
                if (dr.Read())
                {
                    RvGameReadFromReader(dr, game);
                }
                dr.Close();
            }
        }

        public List<RvGame> RvGamesRead(uint DatId)
        {
            List<RvGame> games = new List<RvGame>();
            CommandRvGameReadDatGames.Parameters["DatId"].Value = DatId;

            using (DbDataReader dr = CommandRvGameReadDatGames.ExecuteReader())
            {
                while (dr.Read())
                {
                    RvGame rvGame = new RvGame();
                    RvGameReadFromReader(dr, rvGame);
                    games.Add(rvGame);
                }
                dr.Close();
            }

            return games;
        }

        private void RvGameReadFromReader(DbDataReader dr, RvGame game)
        {
            game.GameId = Convert.ToUInt32(dr["GameId"]);
            game.DatId = Convert.ToUInt32(dr["DatId"]);
            game.Name = dr["name"].ToString();
            game.Description = dr["description"].ToString();
            game.Manufacturer = dr["manufacturer"].ToString();
            game.CloneOf = dr["cloneOf"].ToString();
            game.RomOf = dr["romof"].ToString();
            game.SourceFile = dr["sourcefile"].ToString();
            game.IsBios = dr["isbios"].ToString();
            game.Board = dr["board"].ToString();
            game.Year = dr["year"].ToString();
            game.IsTrurip = Convert.ToBoolean(dr["istrurip"]);
            game.Publisher = dr["publisher"].ToString();
            game.Developer = dr["developer"].ToString();
            game.Edition = dr["edition"].ToString();
            game.Version = dr["version"].ToString();
            game.Type = dr["type"].ToString();
            game.Media = dr["media"].ToString();
            game.Language = dr["language"].ToString();
            game.Players = dr["players"].ToString();
            game.Ratings = dr["ratings"].ToString();
            game.Genre = dr["genre"].ToString();
            game.Peripheral = dr["peripheral"].ToString();
            game.BarCode = dr["barcode"].ToString();
            game.MediaCatalogNumber = dr["mediacatalognumber"].ToString();
        }


        public void RvRomWrite(RvRom rom)
        {
            CommandRvRomWrite.Parameters["GameId"].Value = rom.GameId;
            CommandRvRomWrite.Parameters["name"].Value = rom.Name;
            CommandRvRomWrite.Parameters["type"].Value = (int)rom.altType;
            CommandRvRomWrite.Parameters["size"].Value = rom.Size;
            CommandRvRomWrite.Parameters["crc"].Value = VarFix.ToDBString(rom.CRC);
            CommandRvRomWrite.Parameters["sha1"].Value = VarFix.ToDBString(rom.SHA1);
            CommandRvRomWrite.Parameters["md5"].Value = VarFix.ToDBString(rom.MD5);
            CommandRvRomWrite.Parameters["merge"].Value = rom.Merge;
            CommandRvRomWrite.Parameters["status"].Value = rom.Status;
            CommandRvRomWrite.Parameters["FileID"].Value = rom.FileId;
            CommandRvRomWrite.ExecuteNonQuery();
        }


        public List<RvRom> RvRomsRead(uint gameId)
        {
            List<RvRom> roms = new List<RvRom>();
            CommandRvRomReader.Parameters["GameId"].Value = gameId;

            using (DbDataReader dr = CommandRvRomReader.ExecuteReader())
            {
                while (dr.Read())
                {
                    RvRom row = new RvRom
                    {
                        RomId = Convert.ToUInt32(dr["RomId"]),
                        GameId = gameId,
                        Name = dr["name"].ToString(),
                        altType = (FileType)FixLong(dr["type"]),
                        Size = FixLong(dr["size"]),
                        CRC = VarFix.CleanMD5SHA1(dr["CRC"].ToString(), 8),
                        SHA1 = VarFix.CleanMD5SHA1(dr["SHA1"].ToString(), 40),
                        MD5 = VarFix.CleanMD5SHA1(dr["MD5"].ToString(), 32),
                        Merge = dr["merge"].ToString(),
                        Status = dr["status"].ToString(),
                        FileId = FixLong(dr["FileId"]),
                        fileSize = FixLong(dr["fileSize"]),
                        fileCompressedSize = FixLong(dr["fileCompressedSize"]),
                        fileCRC = VarFix.CleanMD5SHA1(dr["fileCRC"].ToString(), 8),
                        fileSHA1 = VarFix.CleanMD5SHA1(dr["fileSHA1"].ToString(), 40),
                        fileMD5 = VarFix.CleanMD5SHA1(dr["fileMD5"].ToString(), 32)
                    };

                    roms.Add(row);
                }
                dr.Close();
            }
            return roms;
        }
        private static ulong? FixLong(object v)
        {
            return v == DBNull.Value ? null : (ulong?)Convert.ToInt64(v);
        }

        public uint RvFileWrite(RvFile file)
        {
            CommandRvFileWrite.Parameters["size"].Value = file.Size;
            CommandRvFileWrite.Parameters["compressedsize"].Value = file.CompressedSize;
            CommandRvFileWrite.Parameters["crc"].Value = VarFix.ToDBString(file.CRC);
            CommandRvFileWrite.Parameters["sha1"].Value = VarFix.ToDBString(file.SHA1);
            CommandRvFileWrite.Parameters["md5"].Value = VarFix.ToDBString(file.MD5);
            CommandRvFileWrite.Parameters["alttype"].Value = (int)file.AltType;
            CommandRvFileWrite.Parameters["altsize"].Value = file.AltSize;
            CommandRvFileWrite.Parameters["altcrc"].Value = VarFix.ToDBString(file.AltCRC);
            CommandRvFileWrite.Parameters["altsha1"].Value = VarFix.ToDBString(file.AltSHA1);
            CommandRvFileWrite.Parameters["altmd5"].Value = VarFix.ToDBString(file.AltMD5);

            object res = CommandRvFileWrite.ExecuteScalar();
            return Convert.ToUInt32(res);
        }

        public void RvFileUpdateRom(uint fileId, RvFile file)
        {
            CommandRvFileUpdateRom.Parameters["FileId"].Value = fileId;
            CommandRvFileUpdateRom.Parameters["size"].Value = file.Size;
            CommandRvFileUpdateRom.Parameters["crc"].Value = VarFix.ToDBString(file.CRC);
            CommandRvFileUpdateRom.Parameters["sha1"].Value = VarFix.ToDBString(file.SHA1);
            CommandRvFileUpdateRom.Parameters["md5"].Value = VarFix.ToDBString(file.MD5);
            CommandRvFileUpdateRom.ExecuteNonQuery();
        }
        public void RvFileUpdateRomAlt(uint fileId, RvFile file)
        {
            CommandRvFileUpdateRomAlt.Parameters["FileId"].Value = fileId;
            CommandRvFileUpdateRomAlt.Parameters["type"].Value = file.AltType;
            CommandRvFileUpdateRomAlt.Parameters["size"].Value = file.AltSize;
            CommandRvFileUpdateRomAlt.Parameters["crc"].Value = VarFix.ToDBString(file.AltCRC);
            CommandRvFileUpdateRomAlt.Parameters["sha1"].Value = VarFix.ToDBString(file.AltSHA1);
            CommandRvFileUpdateRomAlt.Parameters["md5"].Value = VarFix.ToDBString(file.AltMD5);
            CommandRvFileUpdateRomAlt.ExecuteNonQuery();
        }

        public void RvFileUpdateZeroRom(uint fileId, RvFile file)
        {
            CommandRvFileUpdateZeroRom.Parameters["FileId"].Value = fileId;
            CommandRvFileUpdateZeroRom.Parameters["crc"].Value = VarFix.ToDBString(file.CRC);
            CommandRvFileUpdateZeroRom.Parameters["sha1"].Value = VarFix.ToDBString(file.SHA1);
            CommandRvFileUpdateZeroRom.Parameters["md5"].Value = VarFix.ToDBString(file.MD5);
            CommandRvFileUpdateZeroRom.ExecuteNonQuery();
        }


        public List<RvGameGridRow> ReadGames(int datId)
        {
            List<RvGameGridRow> rows = new List<RvGameGridRow>();
            CommandRvGameGridRowRead.Parameters["DatId"].Value = datId;

            using (DbDataReader dr = CommandRvGameGridRowRead.ExecuteReader())
            {
                while (dr.Read())
                {
                    RvGameGridRow gridRow = new RvGameGridRow();
                    gridRow.GameId = System.Convert.ToInt32(dr["GameID"]); ;
                    gridRow.Name = dr["name"].ToString();
                    gridRow.Description = dr["description"].ToString();
                    gridRow.RomGot = System.Convert.ToInt32(dr["RomGot"]);
                    gridRow.RomTotal = System.Convert.ToInt32(dr["RomTotal"]);
                    gridRow.RomNoDump = System.Convert.ToInt32(dr["RomNoDump"]);
                    rows.Add(gridRow);
                }
                dr.Close();
            }
            return rows;
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
            ExecuteNonQuery(@"
              CREATE TABLE IF NOT EXISTS memdb.FILESMEM (
                    [FileId] INTEGER NOT NULL,
                    [size] INTEGER NOT NULL,
                    [crc] VARCHAR(8) NULL,
                    [sha1] VARCHAR(40) NULL,
                    [md5] VARCHAR(32) NULL,
                    [alttype] VARCHAR(8) NULL,
                    [altsize] INTEGER NULL,
                    [altcrc] VARCHAR(8) NULL,
                    [altsha1] VARCHAR(40) NULL,
                    [altmd5] VARCHAR(32) NULL
                );");

            ExecuteNonQuery(@"
                DELETE FROM memdb.FILESMEM;");

            ExecuteNonQuery(@"
                CREATE INDEX IF NOT EXISTS memdb.memFILESHA1 ON FILESMEM ([sha1] ASC);
                CREATE INDEX IF NOT EXISTS memdb.memFILEMD5 ON FILESMEM ([md5] ASC);
                CREATE INDEX IF NOT EXISTS memdb.memFILECRC ON FILESMEM ([crc] ASC);
                CREATE INDEX IF NOT EXISTS memdb.memFILESize ON FILESMEM ([size] ASC);");

            ExecuteNonQuery(@"
                INSERT INTO memdb.FILESMEM SELECT FileId,size,crc,sha1,md5,alttype,altsize,altcrc,altsha1,altmd5 FROM FILES");

            SQLiteCommand count = new SQLiteCommand("SELECT COUNT(1) FROM memdb.FILESMEM LIMIT 1", Connection);
            object res = count.ExecuteScalar();
            count.Dispose();
            if (res == null || res == DBNull.Value)
                return true;
            return Convert.ToInt32(res) == 0;
        }

        public uint? FindAFile(RvRom tFile)
        {
            if (tFile.SHA1 != null)
            {
                CommandSHA1.Parameters["sha1"].Value = VarFix.ToDBString(tFile.SHA1);
                CommandSHA1.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5);
                CommandSHA1.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
                CommandSHA1.Parameters["size"].Value = tFile.Size;

                object res = CommandSHA1.ExecuteScalar();

                if (res == null || res == DBNull.Value)
                    return null;
                return (uint?)Convert.ToInt32(res);
            }
            if (tFile.SHA1 != null && FileHeaderReader.AltHeaderFile(tFile.altType))
            {
                CommandSHA1Alt.Parameters["alttype"].Value = (int)tFile.altType;
                CommandSHA1Alt.Parameters["sha1"].Value = VarFix.ToDBString(tFile.SHA1);
                CommandSHA1Alt.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5);
                CommandSHA1Alt.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
                CommandSHA1Alt.Parameters["size"].Value = tFile.Size;

                object res = CommandSHA1Alt.ExecuteScalar();

                if (res == null || res == DBNull.Value)
                    return null;
                return (uint?)Convert.ToInt32(res);
            }
            if (tFile.MD5 != null)
            {
                CommandMD5.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5);
                CommandMD5.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
                CommandMD5.Parameters["size"].Value = tFile.Size;

                object res = CommandMD5.ExecuteScalar();

                if (res == null || res == DBNull.Value)
                    return null;
                return (uint?)Convert.ToInt32(res);
            }
            if (tFile.MD5 != null && FileHeaderReader.AltHeaderFile(tFile.altType))
            {
                CommandMD5Alt.Parameters["alttype"].Value = (int)tFile.altType;
                CommandMD5Alt.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5);
                CommandMD5Alt.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
                CommandMD5Alt.Parameters["size"].Value = tFile.Size;

                object res = CommandMD5Alt.ExecuteScalar();

                if (res == null || res == DBNull.Value)
                    return null;
                return (uint?)Convert.ToInt32(res);
            }


            if (tFile.CRC != null)
            {
                CommandCRC.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
                CommandCRC.Parameters["size"].Value = tFile.Size;

                object res = CommandCRC.ExecuteScalar();

                if (res == null || res == DBNull.Value)
                    return null;
                return (uint?)Convert.ToInt32(res);
            }
            if (tFile.CRC != null && FileHeaderReader.AltHeaderFile(tFile.altType))
            {
                CommandCRCAlt.Parameters["alttype"].Value = (int)tFile.altType;
                CommandCRCAlt.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
                CommandCRCAlt.Parameters["size"].Value = tFile.Size;

                object res = CommandCRCAlt.ExecuteScalar();

                if (res == null || res == DBNull.Value)
                    return null;
                return (uint?)Convert.ToInt32(res);
            }


            if (tFile.Size != null && tFile.Size == 0)
            {
                CommandSize.Parameters["size"].Value = tFile.Size;

                object res = CommandSize.ExecuteScalar();
                if (res == null || res == DBNull.Value)
                    return null;
                return (uint?)Convert.ToInt32(res);
            }

            return null;
        }


        public uint? FindDat(string fulldir, string filename, long DatTimeStamp)
        {
            CommandFindDat.Parameters["fullname"].Value = fulldir + "\\";
            CommandFindDat.Parameters["filename"].Value = filename;
            CommandFindDat.Parameters["DatTimeStamp"].Value = DatTimeStamp.ToString();

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


        public uint? FindInDir(string fullname)
        {
            CommandFindInDir.Parameters["FullName"].Value = fullname;
            object resFind = CommandFindInDir.ExecuteScalar();
            if (resFind == null || resFind == DBNull.Value)
                return null;

            return (uint?)Convert.ToInt32(resFind);
        }


        public void SetDirFound(uint foundDatId)
        {
            CommandSetDirFound.Parameters["DirId"].Value = foundDatId;
            CommandSetDirFound.ExecuteNonQuery();
        }

        public uint InsertIntoDir(uint parentDirId, string name, string fullName)
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

        public bool FindInFiles(RvFile tFile)
        {
            CommandFindInFiles.Parameters["size"].Value = tFile.Size;
            CommandFindInFiles.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
            CommandFindInFiles.Parameters["sha1"].Value = VarFix.ToDBString(tFile.SHA1);
            CommandFindInFiles.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5);

            object res = CommandFindInFiles.ExecuteScalar();
            if (res == null || res == DBNull.Value)
                return false;
            int count = Convert.ToInt32(res);

            return count > 0;
        }


        public bool FindInROMs(RvFile tFile)
        {
            if (tFile.Size == 0)
            {
                CommandFindInROMsZero.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
                CommandFindInROMsZero.Parameters["sha1"].Value = VarFix.ToDBString(tFile.SHA1);
                CommandFindInROMsZero.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5);

                object resZero = CommandFindInROMsZero.ExecuteScalar();

                if (resZero == null || resZero == DBNull.Value)
                    return false;
                int countZero = Convert.ToInt32(resZero);

                return countZero > 0;

            }

            CommandFindInROMs.Parameters["size"].Value = tFile.Size;
            CommandFindInROMs.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
            CommandFindInROMs.Parameters["sha1"].Value = VarFix.ToDBString(tFile.SHA1);
            CommandFindInROMs.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5);

            object res = CommandFindInROMs.ExecuteScalar();

            if (res == null || res == DBNull.Value)
                return false;
            int count = Convert.ToInt32(res);

            return count > 0;
        }


        public bool FindInROMsAlt(RvFile tFile)
        {
            CommandFindInROMsAlt.Parameters["type"].Value = (int)tFile.AltType;
            CommandFindInROMsAlt.Parameters["size"].Value = tFile.AltSize;
            CommandFindInROMsAlt.Parameters["crc"].Value = VarFix.ToDBString(tFile.AltCRC);
            CommandFindInROMsAlt.Parameters["sha1"].Value = VarFix.ToDBString(tFile.AltSHA1);
            CommandFindInROMsAlt.Parameters["md5"].Value = VarFix.ToDBString(tFile.AltMD5);

            object res = CommandFindInROMsAlt.ExecuteScalar();

            if (res == null || res == DBNull.Value)
                return false;
            int count = Convert.ToInt32(res);

            return count > 0;
        }


        public byte[] GetFile(uint fileId)
        {
            CommandGetFile.Parameters["fileId"].Value = fileId;

            byte[] sha1 = null;
            using (DbDataReader dr = CommandGetFile.ExecuteReader())
            {
                while (dr.Read())
                {
                    sha1 = VarFix.CleanMD5SHA1(dr["SHA1"].ToString(), 40);
                }
                dr.Close();
            }
            return sha1;
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

        public void UpdateSelectedFromList( List<uint> todo,int value)
        {
            string todoList = string.Join(",", todo);
            using (DbCommand SetStatus = new SQLiteCommand(@"UPDATE dir SET expanded=" + value + " WHERE ParentDirId in (" + todoList + ")"))
            {
                SetStatus.ExecuteNonQuery();
            }
        }

        public List<uint> UpdateSelectedGetChildList(List<uint> todo)
        {
            string todoList = string.Join(",", todo);
            List<uint> retList=new List<uint>();
            using (DbCommand GetChild = new SQLiteCommand(@"select DirId from dir where ParentDirId in (" + todoList + ")"))
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
    }
}
