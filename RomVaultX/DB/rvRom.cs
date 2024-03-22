using System;
using System.Collections.Generic;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using FileHeaderReader;
using RomVaultX.Util;

namespace RomVaultX.DB
{
    public class RvRom
    {
        public static SqliteCommand CommandRead
        {
            get
            {
                if (_commandRead == null)
                {
                    _commandRead = new SqliteCommand(@"
                        SELECT
                            RomId,
                            name,
                            type,
                            ROM.size,
                            ROM.crc,
                            ROM.sha1,
                            ROM.md5,
                            merge,
                            status,
                            putinzip,
                            ROM.FileId,
                            FILES.size as fileSize,
                            FILES.compressedsize as fileCompressedSize,
                            FILES.crc as filecrc,
                            FILES.sha1 as filesha1,
                            FILES.md5 as filemd5
                        FROM ROM
                        LEFT OUTER JOIN FILES
                        ON
                            FILES.FileId = ROM.FileId
                        WHERE
                            GameId = @GameId
                        ORDER BY RomId",
                    Program.db.Connection);

                    _commandRead.Parameters.Add(new SqliteParameter("GameId", SqliteType.Integer));
                }

                return _commandRead;
            }
        }
        private static SqliteCommand? _commandRead;

        public static SqliteCommand CommandWrite
        {
            get
            {
                if (_commandWrite == null)
                {
                    _commandWrite = new SqliteCommand(@"
                        INSERT INTO ROM 
                        (
                            GameId,
                            name,
                            type,
                            size,
                            crc,
                            sha1,
                            md5,
                            merge,
                            status,
                            putinzip,
                            FileId
                        )
                        VALUES
                        (
                            @GameId,
                            @name,
                            @type,
                            @size,
                            @crc,
                            @sha1,
                            @md5,
                            @merge,
                            @status,
                            @putinzip,
                            @FileId
                        );

                        SELECT last_insert_rowid();",
                    Program.db.Connection);

                    _commandWrite.Parameters.Add(new SqliteParameter("GameId", SqliteType.Integer));
                    _commandWrite.Parameters.Add(new SqliteParameter("name", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("type", SqliteType.Integer));
                    _commandWrite.Parameters.Add(new SqliteParameter("size", SqliteType.Integer));
                    _commandWrite.Parameters.Add(new SqliteParameter("crc", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("sha1", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("md5", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("merge", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("status", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("putinzip", SqliteType.Integer));
                    _commandWrite.Parameters.Add(new SqliteParameter("FileId", SqliteType.Integer));
                }

                return _commandWrite;
            }
        }
        private static SqliteCommand? _commandWrite;

        public static SqliteCommand CommandWriteNoFiles
        {
            get
            {
                if (_commandWriteNoFiles == null)
                {
                    _commandWriteNoFiles = new SqliteCommand(@"
                        INSERT INTO ROM 
                        (
                            GameId,
                            name,
                            type,
                            size,
                            crc,
                            sha1,
                            md5,
                            merge,
                            status,
                            putinzip
                        )
                        VALUES
                        (
                            @GameId,
                            @name,
                            @type,
                            @size,
                            @crc,
                            @sha1,
                            @md5,
                            @merge,
                            @status,
                            @putinzip
                        );

                        SELECT last_insert_rowid();",
                    Program.db.Connection);

                    _commandWriteNoFiles.Parameters.Add(new SqliteParameter("GameId", SqliteType.Integer));
                    _commandWriteNoFiles.Parameters.Add(new SqliteParameter("name", SqliteType.Text));
                    _commandWriteNoFiles.Parameters.Add(new SqliteParameter("type", SqliteType.Integer));
                    _commandWriteNoFiles.Parameters.Add(new SqliteParameter("size", SqliteType.Integer));
                    _commandWriteNoFiles.Parameters.Add(new SqliteParameter("crc", SqliteType.Text));
                    _commandWriteNoFiles.Parameters.Add(new SqliteParameter("sha1", SqliteType.Text));
                    _commandWriteNoFiles.Parameters.Add(new SqliteParameter("md5", SqliteType.Text));
                    _commandWriteNoFiles.Parameters.Add(new SqliteParameter("merge", SqliteType.Text));
                    _commandWriteNoFiles.Parameters.Add(new SqliteParameter("status", SqliteType.Text));
                    _commandWriteNoFiles.Parameters.Add(new SqliteParameter("putinzip", SqliteType.Integer));
                }

                return _commandWriteNoFiles;
            }
        }
        private static SqliteCommand? _commandWriteNoFiles;

        public uint RomId;
        public uint GameId;
        public string? Name;
        public ulong? Size;
        public HeaderFileType AltType;
        public byte[]? CRC;
        public byte[]? SHA1;
        public byte[]? MD5;
        public string? Merge;
        public string? Status;
        public string? Date;
        public bool PutInZip;
        public ulong? FileId;

        public ulong? FileSize;
        public ulong? FileCompressedSize;
        public byte[]? FileCRC;
        public byte[]? FileSHA1;
        public byte[]? FileMD5;

        public static void CreateTable()
        {
            Program.db.ExecuteNonQuery(@"
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
                    [putinzip] BOOLEAN,
                    [FileId] INTEGER NULL,
                    [LocalFileHeader] BLOB NULL,
                    [LocalFileHeaderOffset] INTEGER NULL,
                    [LocalFileHeaderLength] INTEGER NULL,
                    [LocalFileSha1] VARCHAR(40) NULL,
                    [LocalFileCompressedSize] INTEGER NULL,
                    FOREIGN KEY(GameId) REFERENCES Game(GameId),
                    FOREIGN KEY(FileId) REFERENCES Files(FileId)
                );");
        }

        public static List<RvRom> ReadRoms(uint gameId)
        {
            CommandRead.Parameters["GameId"].Value = gameId;

            List<RvRom> roms = [];
            using (DbDataReader dr = CommandRead.ExecuteReader())
            {
                while (dr.Read())
                {
                    var row = new RvRom
                    {
                        RomId = Convert.ToUInt32(dr["RomId"]),
                        GameId = gameId,
                        Name = dr["name"].ToString(),
                        AltType = VarFix.FixFileType(dr["type"]),
                        Size = VarFix.FixLong(dr["size"]),
                        CRC = VarFix.CleanMD5SHA1(dr["crc"].ToString(), 8),
                        SHA1 = VarFix.CleanMD5SHA1(dr["sha1"].ToString(), 40),
                        MD5 = VarFix.CleanMD5SHA1(dr["md5"].ToString(), 32),
                        Merge = dr["merge"].ToString(),
                        Status = dr["status"].ToString(),
                        PutInZip = (bool)dr["putinzip"],
                        FileId = VarFix.FixLong(dr["FileId"]),
                        FileSize = VarFix.FixLong(dr["fileSize"]),
                        FileCompressedSize = VarFix.FixLong(dr["fileCompressedSize"]),
                        FileCRC = VarFix.CleanMD5SHA1(dr["filecrc"].ToString(), 8),
                        FileSHA1 = VarFix.CleanMD5SHA1(dr["filesha1"].ToString(), 40),
                        FileMD5 = VarFix.CleanMD5SHA1(dr["filemd5"].ToString(), 32)
                    };

                    roms.Add(row);
                }

                dr.Close();
            }
            return roms;
        }

        public void DBWrite()
        {
            FileId = DatUpdate.NoFilesInDb ? null : RvRomFileMatchup.FindAFile(this);

            if (DatUpdate.NoFilesInDb)
            {
                CommandWriteNoFiles.Parameters["GameId"].Value = GameId;
                CommandWriteNoFiles.Parameters["name"].Value = Name ?? string.Empty;
                CommandWriteNoFiles.Parameters["type"].Value = (int)AltType;
                CommandWriteNoFiles.Parameters["size"].Value = Size;
                CommandWriteNoFiles.Parameters["crc"].Value = VarFix.ToDBString(CRC) ?? string.Empty;
                CommandWriteNoFiles.Parameters["sha1"].Value = VarFix.ToDBString(SHA1) ?? string.Empty;
                CommandWriteNoFiles.Parameters["md5"].Value = VarFix.ToDBString(MD5) ?? string.Empty;
                CommandWriteNoFiles.Parameters["merge"].Value = Merge ?? string.Empty;
                CommandWriteNoFiles.Parameters["status"].Value = Status ?? string.Empty;
                CommandWriteNoFiles.Parameters["putinzip"].Value = PutInZip;

                CommandWriteNoFiles.ExecuteNonQuery();
            }
            else
            {
                CommandWrite.Parameters["GameId"].Value = GameId;
                CommandWrite.Parameters["name"].Value = Name ?? string.Empty;
                CommandWrite.Parameters["type"].Value = (int)AltType;
                CommandWrite.Parameters["size"].Value = Size;
                CommandWrite.Parameters["crc"].Value = VarFix.ToDBString(CRC) ?? string.Empty;
                CommandWrite.Parameters["sha1"].Value = VarFix.ToDBString(SHA1) ?? string.Empty;
                CommandWrite.Parameters["md5"].Value = VarFix.ToDBString(MD5) ?? string.Empty;
                CommandWrite.Parameters["merge"].Value = Merge ?? string.Empty;
                CommandWrite.Parameters["status"].Value = Status ?? string.Empty;
                CommandWrite.Parameters["putinzip"].Value = PutInZip;
                CommandWrite.Parameters["FileId"].Value = FileId;

                CommandWrite.ExecuteNonQuery();
            }
        }
    }
}