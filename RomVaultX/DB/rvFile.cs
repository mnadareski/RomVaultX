using System;
using Microsoft.Data.Sqlite;
using FileHeaderReader;
using RomVaultX.Util;

namespace RomVaultX.DB
{
    public class RvFile
    {
        public static SqliteCommand CommandWrite
        {
            get
            {
                if (_commandWrite == null)
                {
                    _commandWrite = new SqliteCommand(@"
                        INSERT INTO FILES
                        (
                            size,
                            compressedsize,
                            crc,
                            sha1,
                            md5,
                            alttype,
                            altsize,
                            altcrc,
                            altsha1,
                            altmd5
                        )
                        VALUES
                        (
                            @size,
                            @compressedsize,
                            @crc,
                            @sha1,
                            @md5,
                            @alttype,
                            @altsize,
                            @altcrc,
                            @altsha1,
                            @altmd5
                        );

                        SELECT last_insert_rowid();",
                    Program.db.Connection);

                    _commandWrite.Parameters.Add(new SqliteParameter("size", SqliteType.Integer));
                    _commandWrite.Parameters.Add(new SqliteParameter("compressedsize", SqliteType.Integer));
                    _commandWrite.Parameters.Add(new SqliteParameter("crc", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("sha1", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("md5", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("alttype", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("altsize", SqliteType.Integer));
                    _commandWrite.Parameters.Add(new SqliteParameter("altcrc", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("altsha1", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("altmd5", SqliteType.Text));
                }

                return _commandWrite;
            }
        }
        private static SqliteCommand? _commandWrite;

        public static SqliteCommand CommandCheckForAnyFiles
        {
            get
            {
                _commandCheckForAnyFiles ??= new SqliteCommand("SELECT COUNT(1) FROM FILES LIMIT 1", Program.db.Connection);
                return _commandCheckForAnyFiles;
            }
        }
        private static SqliteCommand? _commandCheckForAnyFiles;

        public uint FileId;
        public ulong Size;
        public ulong CompressedSize;
        public byte[]? CRC;
        public byte[]? SHA1;
        public byte[]? MD5;

        public HeaderFileType AltType;
        public ulong? AltSize;
        public byte[]? AltCRC;
        public byte[]? AltSHA1;
        public byte[]? AltMD5;

        public static void CreateTable()
        {
            Program.db.ExecuteNonQuery(@"
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
        }

        public void DBWrite()
        {
            Program.db.Begin();
            RvFileWrite();
            RvRomFileMatchup.MatchFiletoRoms(this);
            Program.db.Commit();
        }

        private void RvFileWrite()
        {
            CommandWrite.Parameters["size"].Value = Size;
            CommandWrite.Parameters["compressedsize"].Value = CompressedSize;
            CommandWrite.Parameters["crc"].Value = VarFix.ToDBString(CRC);
            CommandWrite.Parameters["sha1"].Value = VarFix.ToDBString(SHA1);
            CommandWrite.Parameters["md5"].Value = VarFix.ToDBString(MD5);
            CommandWrite.Parameters["alttype"].Value = ((int)AltType).ToString();
            CommandWrite.Parameters["altsize"].Value = AltSize;
            CommandWrite.Parameters["altcrc"].Value = VarFix.ToDBString(AltCRC);
            CommandWrite.Parameters["altsha1"].Value = VarFix.ToDBString(AltSHA1);
            CommandWrite.Parameters["altmd5"].Value = VarFix.ToDBString(AltMD5);

            var res = CommandWrite.ExecuteScalar();

            FileId = Convert.ToUInt32(res);
        }

        public static bool FilesinDBCheck()
        {
            var res = CommandCheckForAnyFiles.ExecuteScalar();
            if (res == null || res == DBNull.Value)
                return true;

            return Convert.ToInt32(res) == 0;
        }
    }
}