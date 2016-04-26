using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using RomVaultX.Util;

namespace RomVaultX.DB.NewDB
{
    public class DBSqlServer : iDB
    {
        private const int DBVersion = 6;

        private SqlConnection Connection;

        public void ConnectToDB()
        {
            Connection = new SqlConnection();
            Connection.ConnectionString = "Data Source=GORDONS-PC\\SQLEXPRESS; Initial Catalog=RomVaultX1; User id=sa; Password=Welcome1; Connection Timeout=60;";
            Connection.Open();

            bool datFound = true;
            CheckDbVersion(ref datFound);

            InitializeSqlCommands();

            //if (!datFound)
                MakeDB();

        }
        private void CheckDbVersion(ref bool datFound)
        {
            if (!datFound)
                return;

            int testVersion = 0;
            try
            {

                DbCommand dbVersionCommand = new SqlCommand(@"SELECT TOP(1) version from version", Connection);
                object res = dbVersionCommand.ExecuteScalar();

                if (res != null && res != DBNull.Value)
                    testVersion = System.Convert.ToInt32(res);

                if (testVersion == DBVersion)
                    return;
            }
            catch (Exception)
            {
            }

            //Connection.Close();
            datFound = false;
        }

        private void ExecuteNonQuery(string query, params object[] args)
        {
            using (SqlCommand command = new SqlCommand(query, Connection))
            {
                command.CommandTimeout = 600;
                for (int i = 0; i < args.Length; i += 2)
                    command.Parameters.Add(new SqlParameter(args[i].ToString(), args[i + 1]));

                command.ExecuteNonQuery();
            }
        }

        private void ExecuteNonQueryAsync(string query, params object[] args)
        {
            using (SqlCommand command = new SqlCommand(query, Connection))
            {
                command.CommandTimeout = 600;
                for (int i = 0; i < args.Length; i += 2)
                    command.Parameters.Add(new SqlParameter(args[i].ToString(), args[i + 1]));

                command.ExecuteNonQueryAsync();
            }
        }


        private void MakeDB()
        {
            /******** Create Tables ***********/

            ExecuteNonQuery(@"
            if not exists (select * from sysobjects where name='VERSION' and xtype='U')
                BEGIN
                CREATE TABLE VERSION (
                    Version INTEGER NOT NULL);
                INSERT INTO VERSION (version) VALUES (@Version);
                END",
                "version", DBVersion);

            ExecuteNonQuery(@"
            if not exists (select * from sysobjects where name='DIR' and xtype='U')
                CREATE TABLE [DIR] (
                    [DirId] INTEGER IDENTITY(1,1) NOT NULL,
                    [ParentDirId] INTEGER NULL,
                    [name] NVARCHAR(300) NOT NULL,
                    [fullname] NVARCHAR(300) NOT NULL,
                    [expanded] BIT DEFAULT 1 NOT NULL,
                    [found] BIT DEFAULT 1,
                    [RomTotal] INTEGER NULL,
                    [RomGot] iNTEGER NULL,
                    [RomNoDump] INTEGER NULL,
                 CONSTRAINT [PK_DIR] PRIMARY KEY CLUSTERED ( [DirId] )
                )
           ");

            ExecuteNonQuery(@"
            if not exists (select * from sysobjects where name='DAT' and xtype='U')
                 CREATE TABLE [DAT] (
                    [DatId] INTEGER  IDENTITY(1,1) NOT NULL,
                    [DirId] INTEGER  NOT NULL,
                    [Filename] NVARCHAR(300)  NULL,
                    [name] NVARCHAR(100)  NULL,
                    [rootdir] NVARCHAR(100)  NULL,
                    [description] NVARCHAR(100)  NULL,
                    [category] NVARCHAR(100)  NULL,
                    [version] NVARCHAR(100)  NULL,
                    [date] NVARCHAR(100)  NULL,
                    [author] NVARCHAR(100)  NULL,
                    [email] NVARCHAR(100)  NULL,
                    [homepage] NVARCHAR(100)  NULL,
                    [url] NVARCHAR(100)  NULL,
                    [comment] NVARCHAR(100) NULL,
                    [RomTotal] INTEGER DEFAULT 0 NOT NULL,
                    [RomGot] INTEGER DEFAULT 0 NOT NULL,
                    [RomNoDump] INTEGER DEFAULT 0 NOT NULL,
                    [DatTimeStamp] NVARCHAR(20)  NOT NULL,
                    [found] BIT DEFAULT 1,            
                    CONSTRAINT [PK_DAT] PRIMARY KEY CLUSTERED ( [DatId] ) ,
                    FOREIGN KEY(DirId) REFERENCES DIR(DirId)
                );");

            ExecuteNonQuery(@"
            if not exists (select * from sysobjects where name='GAME' and xtype='U')
                 CREATE TABLE [GAME] (
                    [GameId] INTEGER  IDENTITY(1,1) NOT NULL,
                    [DatId] INTEGER NOT NULL,
                    [name] NVARCHAR(200) NOT NULL,
                    [description] NVARCHAR(220) NULL,
                    [manufacturer] NVARCHAR(200) NULL,
                    [cloneof] NVARCHAR(200) NULL,
                    [romof] NVARCHAR(200) NULL,
                    [sampleof] NVARCHAR(200) NULL,
                    [sourcefile] NVARCHAR(200) NULL,
                    [isbios] NVARCHAR(200) NULL,
                    [board] NVARCHAR(200) NULL,
                    [year] NVARCHAR(200) NULL,
                    [istrurip] BIT DEFAULT 0 NOT NULL,
                    [publisher] NVARCHAR(200) NULL,
                    [developer] NVARCHAR(200) NULL,
                    [edition] NVARCHAR(200) NULL,
                    [version] NVARCHAR(200) NULL,
                    [type] NVARCHAR(200) NULL,
                    [media] NVARCHAR(200) NULL,
                    [language] NVARCHAR(200) NULL,
                    [players] NVARCHAR(200) NULL,
                    [ratings] NVARCHAR(200) NULL,
                    [genre] NVARCHAR(200) NULL,
                    [peripheral] NVARCHAR(200) NULL,
                    [barcode] NVARCHAR(200) NULL,
                    [mediacatalognumber] NVARCHAR(200),
                    [RomTotal] INTEGER DEFAULT 0 NOT NULL,
                    [RomGot] INTEGER DEFAULT 0 NOT NULL,
                    [RomNoDump] INTEGER DEFAULT 0 NOT NULL,
                    [ZipFileLength] INTEGER NULL, 
                    [ZipFileTimeStamp] INTEGER NULL,
                    [CentralDirectory] VARBINARY(MAX) NULL,
                    [CentralDirectoryOffset] INTEGER NULL,
                    [CentralDirectoryLength] INTEGER NULL,
                    CONSTRAINT [PK_GAME] PRIMARY KEY CLUSTERED ( [GameId] ) ,
                    FOREIGN KEY(DatId) REFERENCES DAT(DatId)
                );");

            ExecuteNonQuery(@"
            if not exists (select * from sysobjects where name='FILES' and xtype='U')
                CREATE TABLE [FILES] (
                    [FileId] INTEGER IDENTITY(1,1) NOT NULL,
                    [size] INTEGER NOT NULL,
                    [compressedsize] INTEGER NULL,
                    [crc] VARCHAR(8) NULL,
                    [sha1] VARCHAR(40) NULL,
                    [md5] VARCHAR(32) NULL,
                    [alttype] VARCHAR(8) NULL,
                    [altsize] INTEGER NULL,
                    [altcrc] VARCHAR(8) NULL,
                    [altsha1] VARCHAR(40) NULL,
                    [altmd5] VARCHAR(32) NULL,
                    CONSTRAINT [PK_FILES] PRIMARY KEY CLUSTERED ( [FileId] )

                );
            ");

            ExecuteNonQuery(@"
            if not exists (select * from sysobjects where name='ROM' and xtype='U')
               CREATE TABLE [ROM] (
                    [RomId] INTEGER IDENTITY(1,1) NOT NULL,
                    [GameId] INTEGER NOT NULL,
                    [name] NVARCHAR(320) NOT NULL,
                    [type] INTEGER NULL,
                    [size] INTEGER NULL,
                    [crc] VARCHAR(8) NULL,
                    [sha1] VARCHAR(40) NULL,
                    [md5] VARCHAR(32) NULL,
                    [merged] VARCHAR(320) NULL,
                    [status] VARCHAR(20) NULL,
                    [FileId] INTEGER NULL,
                    [LocalFileHeader] VARBINARY(MAX) NULL,
                    [LocalFileHeaderOffset] INTEGER NULL,
                    [LocalFileHeaderLength] INTEGER NULL,
                    CONSTRAINT [PK_ROM] PRIMARY KEY CLUSTERED ( [RomId] ) ,
                    FOREIGN KEY(GameId) REFERENCES Game(GameId),
                    FOREIGN KEY(FileId) REFERENCES Files(FileId)
                );");



            MakeIndex();
            return;

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
        public void MakeIndex()
        {
            ExecuteNonQuery(@"if not exists (select * from sys.indexes where name='ROMSHA1Index')   CREATE INDEX  [ROMSHA1Index]   ON [ROM]   ([sha1]        ASC);");
            ExecuteNonQuery(@"if not exists (select * from sys.indexes where name='ROMMD5Index')    CREATE INDEX  [ROMMD5Index]    ON [ROM]   ([md5]         ASC);");
            ExecuteNonQuery(@"if not exists (select * from sys.indexes where name='ROMCRCIndex')    CREATE INDEX  [ROMCRCIndex]    ON [ROM]   ([crc]         ASC);");
            ExecuteNonQuery(@"if not exists (select * from sys.indexes where name='ROMSizeIndex')   CREATE INDEX  [ROMSizeIndex]   ON [ROM]   ([size]        ASC);");
            ExecuteNonQuery(@"if not exists (select * from sys.indexes where name='ROMFileIdIndex') CREATE INDEX  [ROMFileIdIndex] ON [ROM]   ([FileId]      ASC);");
            ExecuteNonQuery(@"if not exists (select * from sys.indexes where name='ROMGameId')      CREATE INDEX  [ROMGameId]      ON [ROM]   ([GameId]      ASC,[name] ASC);");

            ExecuteNonQuery(@"if not exists (select * from sys.indexes where name='GameDatId')      CREATE INDEX  [GameDatId]      ON [GAME]  ([DatId]       ASC,[name] ASC);");

            ExecuteNonQuery(@"if not exists (select * from sys.indexes where name='FILESHA1')       CREATE INDEX  [FILESHA1]       ON [FILES] ([sha1]        ASC);");
            ExecuteNonQuery(@"if not exists (select * from sys.indexes where name='FILEMD5')        CREATE INDEX  [FILEMD5]        ON [FILES] ([md5]         ASC);");
            ExecuteNonQuery(@"if not exists (select * from sys.indexes where name='FILECRC')        CREATE INDEX  [FILECRC]        ON [FILES] ([crc]         ASC);");

            ExecuteNonQuery(@"if not exists (select * from sys.indexes where name='DATDIRID')       CREATE INDEX  [DATDIRID]       ON [DAT]   ([DirId]       ASC);");
            ExecuteNonQuery(@"if not exists (select * from sys.indexes where name='DIRPARENTDIRID') CREATE INDEX  [DIRPARENTDIRID] ON [DIR]   ([ParentDirId] ASC);");
        }
        
        public void DropIndex()
        {
            /*
                ExecuteNonQuery(@"
                    DROP INDEX  [ROMSHA1Index] ON ROM;
                    DROP INDEX  [ROMMD5Index] ON ROM;
                    DROP INDEX  [ROMCRCIndex] ON ROM;
                    DROP INDEX  [ROMSizeIndex] ON ROM;
                    DROP INDEX  [ROMFileIdIndex] ON ROM;
                    DROP INDEX  [ROMGameId] ON ROM;");
            */
        }


        public void Begin()
        {
            ExecuteNonQuery("BEGIN TRAN T1");
        }

        public void Commit()
        {
            ExecuteNonQuery("COMMIT TRAN T1");
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


            SqlCommand sqlNullCount = new SqlCommand(@"SELECT COUNT(1) FROM dir WHERE RomTotal IS null", Connection);

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

        private SqlCommand CommandRvDatWrite;
        private SqlCommand CommandRvDatRead;

        private SqlCommand CommandRvGameWrite;
        private SqlCommand CommandRvGameRead;
        private SqlCommand CommandRvGameReadDatGames;

        private SqlCommand CommandRvRomWrite;
        private SqlCommand CommandRvRomReader;

        private SqlCommand CommandRvFileWrite;
        private SqlCommand CommandRvFileUpdateRom;
        private SqlCommand CommandRvFileUpdateRomAlt;
        private SqlCommand CommandRvFileUpdateZeroRom;

        private SqlCommand CommandRvGameGridRowRead;


        private SqlCommand CommandClearfoundDirDATs;
        private SqlCommand CommandCleanupNotFoundDATs;
        private SqlCommand CommandCountDATs;

        private SqlCommand CommandSHA1;
        private SqlCommand CommandMD5;
        private SqlCommand CommandCRC;
        private SqlCommand CommandSize;

        private SqlCommand CommandSHA1Alt;
        private SqlCommand CommandMD5Alt;
        private SqlCommand CommandCRCAlt;


        private SqlCommand CommandFindDat;
        private SqlCommand CommandSetDatFound;


        private SqlCommand CommandFindInDir;
        private SqlCommand CommandSetDirFound;
        private SqlCommand CommandInsertIntoDir;


        private SqlCommand CommandFindInFiles;


        private SqlCommand CommandFindInROMs;
        private SqlCommand CommandFindInROMsAlt;
        private SqlCommand CommandFindInROMsZero;

        private SqlCommand CommandGetFile;

        private SqlCommand CommandReadTree;
        private SqlCommand CommandSetTreeExpanded;

        private SqlCommand CommandGetFirstExpanded;
        private SqlCommand CommandUpdateExpanded;

        private void InitializeSqlCommands()
        {
            CommandRvDatWrite = new SqlCommand(@"
                INSERT INTO DAT ( DirId, Filename, name, rootdir, description, category, version, date, author, email, homepage, url, comment,DatTimeStamp)
                VALUES            (@DirId,@Filename,@name,@rootdir,@description,@category,@version,@date,@author,@email,@homepage,@url,@comment,@DatTimeStamp);

                SELECT Scope_Identity();", Connection);

            CommandRvDatWrite.Parameters.Add(new SqlParameter("DirId", SqlDbType.Int));
            CommandRvDatWrite.Parameters.Add(new SqlParameter("Filename", SqlDbType.VarChar));
            CommandRvDatWrite.Parameters.Add(new SqlParameter("name", SqlDbType.VarChar));
            CommandRvDatWrite.Parameters.Add(new SqlParameter("rootdir", SqlDbType.VarChar));
            CommandRvDatWrite.Parameters.Add(new SqlParameter("description", SqlDbType.VarChar));
            CommandRvDatWrite.Parameters.Add(new SqlParameter("category", SqlDbType.VarChar));
            CommandRvDatWrite.Parameters.Add(new SqlParameter("version", SqlDbType.VarChar));
            CommandRvDatWrite.Parameters.Add(new SqlParameter("date", SqlDbType.VarChar));
            CommandRvDatWrite.Parameters.Add(new SqlParameter("author", SqlDbType.VarChar));
            CommandRvDatWrite.Parameters.Add(new SqlParameter("email", SqlDbType.VarChar));
            CommandRvDatWrite.Parameters.Add(new SqlParameter("homepage", SqlDbType.VarChar));
            CommandRvDatWrite.Parameters.Add(new SqlParameter("url", SqlDbType.VarChar));
            CommandRvDatWrite.Parameters.Add(new SqlParameter("comment", SqlDbType.VarChar));
            CommandRvDatWrite.Parameters.Add(new SqlParameter("DatTimeStamp", SqlDbType.BigInt));


            CommandRvDatRead = new SqlCommand(@"
                SELECT DirId,Filename,name,rootdir,description,category,version,date,author,email,homepage,url,comment 
                FROM DAT WHERE DatId=@datId ORDER BY Filename", Connection);
            CommandRvDatRead.Parameters.Add(new SqlParameter("datId", SqlDbType.Int));


            CommandRvGameWrite = new SqlCommand(@"
                INSERT INTO GAME ( DatId, name, description, manufacturer, cloneof, romof, sourcefile, isbios, board, year, istrurip, publisher, developer, edition, version, type, media, language, players, ratings, genre, peripheral, barcode, mediacatalognumber)
                          VALUES (@DatId,@Name,@Description,@Manufacturer,@CloneOf,@RomOf,@SourceFile,@IsBios,@Board,@Year,@IsTrurip,@Publisher,@Developer,@Edition,@Version,@Type,@Media,@Language,@Players,@Ratings,@Genre,@Peripheral,@BarCode,@MediaCatalogNumber);

                SELECT Scope_Identity();", Connection);

            CommandRvGameWrite.Parameters.Add(new SqlParameter("DatId", SqlDbType.Int)); //DatId;
            CommandRvGameWrite.Parameters.Add(new SqlParameter("Name", SqlDbType.VarChar)); //Name;
            CommandRvGameWrite.Parameters.Add(new SqlParameter("Description", SqlDbType.VarChar)); //Description;
            CommandRvGameWrite.Parameters.Add(new SqlParameter("Manufacturer", SqlDbType.VarChar)); //Manufacturer;

            CommandRvGameWrite.Parameters.Add(new SqlParameter("CloneOf", SqlDbType.VarChar)); //CloneOf;
            CommandRvGameWrite.Parameters.Add(new SqlParameter("RomOf", SqlDbType.VarChar)); //RomOf;
            CommandRvGameWrite.Parameters.Add(new SqlParameter("SampleOf", SqlDbType.VarChar)); //SampleOf;
            CommandRvGameWrite.Parameters.Add(new SqlParameter("Sourcefile", SqlDbType.VarChar)); //SourceFile;
            CommandRvGameWrite.Parameters.Add(new SqlParameter("IsBios", SqlDbType.VarChar)); //IsBios;
            CommandRvGameWrite.Parameters.Add(new SqlParameter("Board", SqlDbType.VarChar)); //Board;
            CommandRvGameWrite.Parameters.Add(new SqlParameter("Year", SqlDbType.VarChar)); //Year;

            CommandRvGameWrite.Parameters.Add(new SqlParameter("IsTrurip", SqlDbType.Bit)); //IsTrurip;
            CommandRvGameWrite.Parameters.Add(new SqlParameter("Publisher", SqlDbType.VarChar)); //Publisher;
            CommandRvGameWrite.Parameters.Add(new SqlParameter("Developer", SqlDbType.VarChar)); //Developer;
            CommandRvGameWrite.Parameters.Add(new SqlParameter("Edition", SqlDbType.VarChar)); //Edition;
            CommandRvGameWrite.Parameters.Add(new SqlParameter("Version", SqlDbType.VarChar)); //Version;
            CommandRvGameWrite.Parameters.Add(new SqlParameter("Type", SqlDbType.VarChar)); //Type;
            CommandRvGameWrite.Parameters.Add(new SqlParameter("Media", SqlDbType.VarChar)); //Media;
            CommandRvGameWrite.Parameters.Add(new SqlParameter("Language", SqlDbType.VarChar)); //Language;
            CommandRvGameWrite.Parameters.Add(new SqlParameter("Players", SqlDbType.VarChar)); //Players;
            CommandRvGameWrite.Parameters.Add(new SqlParameter("Ratings", SqlDbType.VarChar)); //Ratings;
            CommandRvGameWrite.Parameters.Add(new SqlParameter("Genre", SqlDbType.VarChar)); //Genre;
            CommandRvGameWrite.Parameters.Add(new SqlParameter("Peripheral", SqlDbType.VarChar)); //Peripheral;
            CommandRvGameWrite.Parameters.Add(new SqlParameter("BarCode", SqlDbType.VarChar)); //BarCode;
            CommandRvGameWrite.Parameters.Add(new SqlParameter("MediaCatalogNumber", SqlDbType.VarChar)); //MediaCatalogNumber;        


            CommandRvGameRead = new SqlCommand(@"
                SELECT GameId, DatId, name, description, manufacturer, cloneof, romof, sourcefile, isbios, board, year, istrurip, publisher, developer, edition, version, type, media, language, players, ratings, genre, peripheral, barcode, mediacatalognumber
                    FROM GAME WHERE GameId=@GameId ORDER BY name", Connection);
            CommandRvGameRead.Parameters.Add(new SqlParameter("GameId", SqlDbType.Int));

            CommandRvGameReadDatGames = new SqlCommand(@"
                SELECT GameId, DatId, name, description, manufacturer, cloneof, romof, sourcefile, isbios, board, year, istrurip, publisher, developer, edition, version, type, media, language, players, ratings, genre, peripheral, barcode, mediacatalognumber
                    FROM GAME WHERE DatId=@DatId ORDER BY name", Connection);
            CommandRvGameReadDatGames.Parameters.Add(new SqlParameter("DatId", SqlDbType.Int));


            CommandRvRomWrite = new SqlCommand(@"
                INSERT INTO ROM  ( GameId, name, type, size, crc, sha1, md5, merged, status, FileId)
                          VALUES (@GameId,@Name,@Type,@Size,@CRC,@SHA1,@MD5,@Merged,@Status,@FileId);

                SELECT Scope_Identity();", Connection);


            CommandRvRomWrite.Parameters.Add(new SqlParameter("GameId", SqlDbType.Int));
            CommandRvRomWrite.Parameters.Add(new SqlParameter("Name", SqlDbType.VarChar));
            CommandRvRomWrite.Parameters.Add(new SqlParameter("Type", SqlDbType.Int));
            CommandRvRomWrite.Parameters.Add(new SqlParameter("Size", SqlDbType.BigInt));
            CommandRvRomWrite.Parameters.Add(new SqlParameter("CRC", SqlDbType.VarChar));
            CommandRvRomWrite.Parameters.Add(new SqlParameter("SHA1", SqlDbType.VarChar));
            CommandRvRomWrite.Parameters.Add(new SqlParameter("MD5", SqlDbType.VarChar));
            CommandRvRomWrite.Parameters.Add(new SqlParameter("Merged", SqlDbType.VarChar));
            CommandRvRomWrite.Parameters.Add(new SqlParameter("Status", SqlDbType.VarChar));
            CommandRvRomWrite.Parameters.Add(new SqlParameter("FileId", SqlDbType.Int));

            CommandRvRomReader = new SqlCommand(
                @"SELECT RomId,name,
                    type,
                    rom.size,
                    rom.crc,
                    rom.sha1,
                    rom.md5,
                    merged,status,
                    rom.FileId,
                    files.size as fileSize,
                    files.compressedsize as fileCompressedSize,
                    files.crc as filecrc,
                    files.sha1 as filesha1,
                    files.md5 as filemd5
                FROM rom LEFT OUTER JOIN files ON files.FileId=rom.FileId WHERE GameId=@GameId ORDER BY name", Connection);
            CommandRvRomReader.Parameters.Add(new SqlParameter("GameId", SqlDbType.Int));


            CommandRvFileWrite = new SqlCommand(
    @"INSERT INTO FILES (size,compressedsize,crc,sha1,md5,alttype,altsize,altcrc,altsha1,altmd5)
                        VALUES (@Size,@compressedsize,@CRC,@SHA1,@MD5,@alttype,@altsize,@altcrc,@altsha1,@altmd5);

                SELECT Scope_Identity();", Connection);

            CommandRvFileWrite.Parameters.Add(new SqlParameter("size", SqlDbType.BigInt));
            CommandRvFileWrite.Parameters.Add(new SqlParameter("compressedsize", SqlDbType.BigInt));
            CommandRvFileWrite.Parameters.Add(new SqlParameter("crc", SqlDbType.VarChar));
            CommandRvFileWrite.Parameters.Add(new SqlParameter("sha1", SqlDbType.VarChar));
            CommandRvFileWrite.Parameters.Add(new SqlParameter("md5", SqlDbType.VarChar));
            CommandRvFileWrite.Parameters.Add(new SqlParameter("alttype", SqlDbType.VarChar));
            CommandRvFileWrite.Parameters.Add(new SqlParameter("altsize", SqlDbType.VarChar));
            CommandRvFileWrite.Parameters.Add(new SqlParameter("altcrc", SqlDbType.VarChar));
            CommandRvFileWrite.Parameters.Add(new SqlParameter("altsha1", SqlDbType.VarChar));
            CommandRvFileWrite.Parameters.Add(new SqlParameter("altmd5", SqlDbType.VarChar));

            CommandRvFileUpdateRom = new SqlCommand(
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
                ", Connection);
            CommandRvFileUpdateRom.Parameters.Add(new SqlParameter("FileId", SqlDbType.Int));
            CommandRvFileUpdateRom.Parameters.Add(new SqlParameter("size", SqlDbType.BigInt));
            CommandRvFileUpdateRom.Parameters.Add(new SqlParameter("crc", SqlDbType.VarChar));
            CommandRvFileUpdateRom.Parameters.Add(new SqlParameter("sha1", SqlDbType.VarChar));
            CommandRvFileUpdateRom.Parameters.Add(new SqlParameter("md5", SqlDbType.VarChar));

            CommandRvFileUpdateRomAlt = new SqlCommand(
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
                ", Connection);
            CommandRvFileUpdateRomAlt.Parameters.Add(new SqlParameter("FileId", SqlDbType.Int));
            CommandRvFileUpdateRomAlt.Parameters.Add(new SqlParameter("type", SqlDbType.Int));
            CommandRvFileUpdateRomAlt.Parameters.Add(new SqlParameter("size", SqlDbType.BigInt));
            CommandRvFileUpdateRomAlt.Parameters.Add(new SqlParameter("crc", SqlDbType.VarChar));
            CommandRvFileUpdateRomAlt.Parameters.Add(new SqlParameter("sha1", SqlDbType.VarChar));
            CommandRvFileUpdateRomAlt.Parameters.Add(new SqlParameter("md5", SqlDbType.VarChar));



            CommandRvFileUpdateZeroRom = new SqlCommand(
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
                ", Connection);
            CommandRvFileUpdateZeroRom.Parameters.Add(new SqlParameter("FileId", SqlDbType.Int));
            CommandRvFileUpdateZeroRom.Parameters.Add(new SqlParameter("crc", SqlDbType.VarChar));
            CommandRvFileUpdateZeroRom.Parameters.Add(new SqlParameter("sha1", SqlDbType.VarChar));
            CommandRvFileUpdateZeroRom.Parameters.Add(new SqlParameter("md5", SqlDbType.VarChar));


            CommandRvGameGridRowRead = new SqlCommand(@"
                    SELECT GameId,Name,Description,RomTotal,RomGot,RomNoDump FROM game WHERE DatId=@datId ORDER BY Name", Connection);
            CommandRvGameGridRowRead.Parameters.Add(new SqlParameter("datId", SqlDbType.Int));




            CommandClearfoundDirDATs = new SqlCommand(@"
                    UPDATE DIR SET Found=0;
                    UPDATE DAT SET Found=0;
                ", Connection);

            CommandCleanupNotFoundDATs = new SqlCommand(@"
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


            CommandCountDATs = new SqlCommand(@"
                select count(1) from dat
            ", Connection);


            CommandSHA1 = new SqlCommand(@"
                       select top(1) FileId from FILES
                            WHERE
	                                    (                  @sha1 = sha1 ) AND
	                                    ( @md5  is NULL OR @md5  = md5  ) AND
	                                    ( @crc  is NULL OR @crc  = crc  ) AND
	                                    ( @size is NULL OR @size = Size )
                ", Connection);

            CommandSHA1.Parameters.Add(new SqlParameter("sha1", SqlDbType.VarChar));
            CommandSHA1.Parameters.Add(new SqlParameter("md5", SqlDbType.VarChar));
            CommandSHA1.Parameters.Add(new SqlParameter("crc", SqlDbType.VarChar));
            CommandSHA1.Parameters.Add(new SqlParameter("size", SqlDbType.BigInt));


            CommandSHA1Alt = new SqlCommand(@"
                       select top(1) FileId from FILES
                            WHERE
                                        (               @alttype = alttype ) AND
	                                    (                  @sha1 = altsha1 ) AND
	                                    ( @md5  is NULL OR @md5  = altmd5  ) AND
	                                    ( @crc  is NULL OR @crc  = altcrc  ) AND
	                                    ( @size is NULL OR @size = altSize )
                ", Connection);

            CommandSHA1Alt.Parameters.Add(new SqlParameter("alttype", SqlDbType.Int));
            CommandSHA1Alt.Parameters.Add(new SqlParameter("sha1", SqlDbType.VarChar));
            CommandSHA1Alt.Parameters.Add(new SqlParameter("md5", SqlDbType.VarChar));
            CommandSHA1Alt.Parameters.Add(new SqlParameter("crc", SqlDbType.VarChar));
            CommandSHA1Alt.Parameters.Add(new SqlParameter("size", SqlDbType.BigInt));



            CommandMD5 = new SqlCommand(@"
                       select top(1) FileId from FILES
                            WHERE
	                                    (                  @md5  = md5  ) AND
	                                    ( @crc  is NULL OR @crc  = crc  ) AND
	                                    ( @size is NULL OR @size = Size )
                ", Connection);

            CommandMD5.Parameters.Add(new SqlParameter("md5", SqlDbType.VarChar));
            CommandMD5.Parameters.Add(new SqlParameter("crc", SqlDbType.VarChar));
            CommandMD5.Parameters.Add(new SqlParameter("size", SqlDbType.BigInt));

            CommandMD5Alt = new SqlCommand(@"
                       select top(1) FileId from FILES
                            WHERE
                                        (               @alttype = alttype ) AND
	                                    (                  @md5  = altmd5  ) AND
	                                    ( @crc  is NULL OR @crc  = altcrc  ) AND
	                                    ( @size is NULL OR @size = altSize )
                ", Connection);

            CommandMD5Alt.Parameters.Add(new SqlParameter("alttype", SqlDbType.Int));
            CommandMD5Alt.Parameters.Add(new SqlParameter("md5", SqlDbType.VarChar));
            CommandMD5Alt.Parameters.Add(new SqlParameter("crc", SqlDbType.VarChar));
            CommandMD5Alt.Parameters.Add(new SqlParameter("size", SqlDbType.BigInt));


            CommandCRC = new SqlCommand(@"
                       select top(1) FileId from FILES
                            WHERE
	                                    (                  @crc  = crc  ) AND
	                                    ( @size is NULL OR @size = Size )
                ", Connection);

            CommandCRC.Parameters.Add(new SqlParameter("crc", SqlDbType.VarChar));
            CommandCRC.Parameters.Add(new SqlParameter("size", SqlDbType.BigInt));

            CommandCRCAlt = new SqlCommand(@"
                       select top(1) FileId from FILES
                            WHERE
                                        (               @alttype = alttype ) AND
	                                    (                  @crc  = altcrc  ) AND
	                                    ( @size is NULL OR @size = altSize )
                ", Connection);

            CommandCRCAlt.Parameters.Add(new SqlParameter("crc", SqlDbType.VarChar));
            CommandCRCAlt.Parameters.Add(new SqlParameter("size", SqlDbType.BigInt));



            CommandSize = new SqlCommand(@"
                       select top(1) FileId from FILES
                            WHERE
	                                    ( @size = Size )
                ", Connection);

            CommandSize.Parameters.Add(new SqlParameter("size", SqlDbType.BigInt));



            CommandFindDat = new SqlCommand(@"
                SELECT DatId FROM Dat,Dir WHERE Dat.DirId=Dir.DirId AND fullname=@fullname AND Filename=@filename AND DatTimeStamp=@DatTimeStamp
            ", Connection);
            CommandFindDat.Parameters.Add(new SqlParameter("fullname", SqlDbType.VarChar));
            CommandFindDat.Parameters.Add(new SqlParameter("filename", SqlDbType.VarChar));
            CommandFindDat.Parameters.Add(new SqlParameter("DatTimeStamp", SqlDbType.NVarChar));


            CommandSetDatFound = new SqlCommand(@"
                Update Dat SET Found=1 WHERE DatId=@DatId
            ", Connection);
            CommandSetDatFound.Parameters.Add(new SqlParameter("DatId", SqlDbType.Int));



            CommandFindInDir = new SqlCommand(@"SELECT TOP(1) DirId FROM dir WHERE fullname=@fullname", Connection);
            CommandFindInDir.Parameters.Add(new SqlParameter("fullname", SqlDbType.VarChar));

            CommandSetDirFound = new SqlCommand(@"Update Dir SET Found=1 WHERE DirId=@DirId", Connection);
            CommandSetDirFound.Parameters.Add(new SqlParameter("DirId", SqlDbType.Int));

            CommandInsertIntoDir = new SqlCommand(@"
                    INSERT INTO DIR (ParentDirId,Name,FullName)
                         VALUES (@ParentDirId,@Name,@FullName);

                         SELECT Scope_Identity();
                    ", Connection);

            CommandInsertIntoDir.Parameters.Add(new SqlParameter("ParentDirId", SqlDbType.Int));
            CommandInsertIntoDir.Parameters.Add(new SqlParameter("Name", SqlDbType.VarChar));
            CommandInsertIntoDir.Parameters.Add(new SqlParameter("FullName", SqlDbType.VarChar));


            CommandFindInFiles = new SqlCommand(@"
                    SELECT COUNT(1) FROM FILES WHERE
                        size=@size AND crc=@CRC and sha1=@SHA1 and md5=@MD5", Connection);
            CommandFindInFiles.Parameters.Add(new SqlParameter("size", SqlDbType.BigInt));
            CommandFindInFiles.Parameters.Add(new SqlParameter("crc", SqlDbType.VarChar));
            CommandFindInFiles.Parameters.Add(new SqlParameter("sha1", SqlDbType.VarChar));
            CommandFindInFiles.Parameters.Add(new SqlParameter("md5", SqlDbType.VarChar));





            CommandFindInROMs = new SqlCommand(@"
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
                        AS TotalFound", Connection);
            CommandFindInROMs.Parameters.Add(new SqlParameter("size", SqlDbType.BigInt));
            CommandFindInROMs.Parameters.Add(new SqlParameter("crc", SqlDbType.VarChar));
            CommandFindInROMs.Parameters.Add(new SqlParameter("sha1", SqlDbType.VarChar));
            CommandFindInROMs.Parameters.Add(new SqlParameter("md5", SqlDbType.VarChar));

            CommandFindInROMsAlt = new SqlCommand(@"
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
                        AS TotalFound", Connection);
            CommandFindInROMsAlt.Parameters.Add(new SqlParameter("type", SqlDbType.Int));
            CommandFindInROMsAlt.Parameters.Add(new SqlParameter("size", SqlDbType.BigInt));
            CommandFindInROMsAlt.Parameters.Add(new SqlParameter("crc", SqlDbType.VarChar));
            CommandFindInROMsAlt.Parameters.Add(new SqlParameter("sha1", SqlDbType.VarChar));
            CommandFindInROMsAlt.Parameters.Add(new SqlParameter("md5", SqlDbType.VarChar));


            CommandFindInROMsZero = new SqlCommand(@"
                    SELECT count(1) AS TotalFound FROM ROM WHERE
                        ( sha1=@SHA1 OR sha1 is NULL ) AND 
                        ( md5=@MD5 OR md5 is NULL) AND
                        ( crc=@CRC OR crc is NULL ) AND
                        ( size=0 ) AND
                        ( status!='nodump' or status is NULL)", Connection);
            CommandFindInROMsZero.Parameters.Add(new SqlParameter("crc", SqlDbType.VarChar));
            CommandFindInROMsZero.Parameters.Add(new SqlParameter("sha1", SqlDbType.VarChar));
            CommandFindInROMsZero.Parameters.Add(new SqlParameter("md5", SqlDbType.VarChar));

            CommandGetFile = new SqlCommand(@"
                    SELECT sha1 FROM FILES WHERE
                        fileId=@fileId", Connection);
            CommandGetFile.Parameters.Add(new SqlParameter("fileId", SqlDbType.Int));



            CommandReadTree = new SqlCommand(@"
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

            CommandSetTreeExpanded = new SqlCommand(@"
                    UPDATE dir SET expanded=@expanded WHERE DirId=@dirId", Connection);
            CommandSetTreeExpanded.Parameters.Add(new SqlParameter("expanded", SqlDbType.Bit));
            CommandSetTreeExpanded.Parameters.Add(new SqlParameter("dirId", SqlDbType.Int));


            CommandGetFirstExpanded = new SqlCommand(@"
                SELECT top(1) expanded FROM dir WHERE ParentDirId=@dirId ORDER BY fullname
            ", Connection);
            CommandGetFirstExpanded.Parameters.Add(new SqlParameter("dirId", SqlDbType.Int));

        }



        public uint RvDatWrite(RvDat dat)
        {
            CommandRvDatWrite.Parameters["DirId"].Value = dat.DirId;
            CommandRvDatWrite.Parameters["Filename"].Value = dat.Filename;
            CommandRvDatWrite.Parameters["name"].Value = dat.Name;
            CommandRvDatWrite.Parameters["rootdir"].Value =dbnull( dat.RootDir);
            CommandRvDatWrite.Parameters["description"].Value = dbnull(dat.Description);
            CommandRvDatWrite.Parameters["category"].Value = dbnull(dat.Category);
            CommandRvDatWrite.Parameters["version"].Value = dat.Version;
            CommandRvDatWrite.Parameters["date"].Value =dbnull( dat.Date);
            CommandRvDatWrite.Parameters["author"].Value = dbnull(dat.Author);
            CommandRvDatWrite.Parameters["email"].Value = dbnull(dat.Email);
            CommandRvDatWrite.Parameters["homepage"].Value = dbnull(dat.Homepage);
            CommandRvDatWrite.Parameters["url"].Value = dbnull(dat.URL);
            CommandRvDatWrite.Parameters["comment"].Value = dbnull(dat.Comment);
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
            CommandRvGameWrite.Parameters["Description"].Value = dbnull(game.Description);
            CommandRvGameWrite.Parameters["Manufacturer"].Value = dbnull(game.Manufacturer);

            CommandRvGameWrite.Parameters["CloneOf"].Value = dbnull(game.CloneOf);
            CommandRvGameWrite.Parameters["RomOf"].Value = dbnull(game.RomOf);
            CommandRvGameWrite.Parameters["SampleOf"].Value = dbnull(game.SampleOf);
            CommandRvGameWrite.Parameters["sourcefile"].Value = dbnull(game.SourceFile);
            CommandRvGameWrite.Parameters["IsBios"].Value = dbnull(game.IsBios);
            CommandRvGameWrite.Parameters["Board"].Value = dbnull(game.Board);
            CommandRvGameWrite.Parameters["Year"].Value = dbnull(game.Year);

            CommandRvGameWrite.Parameters["IsTrurip"].Value = game.IsTrurip;
            CommandRvGameWrite.Parameters["Publisher"].Value = dbnull(game.Publisher);
            CommandRvGameWrite.Parameters["Developer"].Value = dbnull(game.Developer);
            CommandRvGameWrite.Parameters["Edition"].Value = dbnull(game.Edition);
            CommandRvGameWrite.Parameters["Version"].Value = dbnull(game.Version);
            CommandRvGameWrite.Parameters["Type"].Value = dbnull(game.Type);
            CommandRvGameWrite.Parameters["Media"].Value = dbnull(game.Media);
            CommandRvGameWrite.Parameters["Language"].Value = dbnull(game.Language);
            CommandRvGameWrite.Parameters["Players"].Value = dbnull(game.Players);
            CommandRvGameWrite.Parameters["Ratings"].Value = dbnull(game.Ratings);
            CommandRvGameWrite.Parameters["Genre"].Value = dbnull(game.Genre);
            CommandRvGameWrite.Parameters["Peripheral"].Value = dbnull(game.Peripheral);
            CommandRvGameWrite.Parameters["BarCode"].Value = dbnull(game.BarCode);
            CommandRvGameWrite.Parameters["MediaCatalogNumber"].Value = dbnull(game.MediaCatalogNumber);

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
            CommandRvRomWrite.Parameters["size"].Value = dbnull(rom.Size);
            CommandRvRomWrite.Parameters["crc"].Value = VarFix.ToDBString(rom.CRC);
            CommandRvRomWrite.Parameters["sha1"].Value = VarFix.ToDBString(rom.SHA1);
            CommandRvRomWrite.Parameters["md5"].Value = VarFix.ToDBString(rom.MD5);
            CommandRvRomWrite.Parameters["merged"].Value = dbnull(rom.Merge);
            CommandRvRomWrite.Parameters["status"].Value = dbnull(rom.Status);
            CommandRvRomWrite.Parameters["FileId"].Value = dbnull(rom.FileId);
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
                        Merge = dr["merged"].ToString(),
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
            /*
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

            SqlCommand count = new SqlCommand("SELECT COUNT(1) FROM memdb.FILESMEM LIMIT 1", Connection);
            object res = count.ExecuteScalar();
            count.Dispose();
            if (res == null || res == DBNull.Value)
                return true;
            return Convert.ToInt32(res) == 0;
        */
            return false;
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
                CommandMD5.Parameters["size"].Value =dbnull( tFile.Size);

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
                CommandCRC.Parameters["size"].Value = dbnull(tFile.Size);

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

        public void UpdateSelectedFromList(List<uint> todo, int value)
        {
            string todoList = string.Join(",", todo);
            using (DbCommand SetStatus = new SqlCommand(@"UPDATE dir SET expanded=" + value + " WHERE ParentDirId in (" + todoList + ")",Connection))
            {
                SetStatus.ExecuteNonQuery();
            }
        }

        public List<uint> UpdateSelectedGetChildList(List<uint> todo)
        {
            string todoList = string.Join(",", todo);
            List<uint> retList = new List<uint>();
            using (DbCommand GetChild = new SqlCommand(@"select DirId from dir where ParentDirId in (" + todoList + ")", Connection))
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

        private object dbnull(object value)
        {
            if (value == null) return DBNull.Value;
            return value;
        }
    }
}
