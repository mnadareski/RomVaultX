using System;
using FileHeaderReader;
using Microsoft.Data.Sqlite;
using RomVaultX.Util;

namespace RomVaultX.DB
{
    public class RvFile
    {
        public static SqliteCommand CommandWrite
        {
            get
            {
                if (field == null)
                {
                    field = new SqliteCommand(@"
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

                    field.Parameters.Add(new SqliteParameter("size", SqliteType.Integer));
                    field.Parameters.Add(new SqliteParameter("compressedsize", SqliteType.Integer));
                    field.Parameters.Add(new SqliteParameter("crc", SqliteType.Text));
                    field.Parameters.Add(new SqliteParameter("sha1", SqliteType.Text));
                    field.Parameters.Add(new SqliteParameter("md5", SqliteType.Text));
                    field.Parameters.Add(new SqliteParameter("alttype", SqliteType.Text));
                    field.Parameters.Add(new SqliteParameter("altsize", SqliteType.Integer));
                    field.Parameters.Add(new SqliteParameter("altcrc", SqliteType.Text));
                    field.Parameters.Add(new SqliteParameter("altsha1", SqliteType.Text));
                    field.Parameters.Add(new SqliteParameter("altmd5", SqliteType.Text));
                }

                return field;
            }
        }

        public static SqliteCommand CommandCheckForAnyFiles
        {
            get
            {
                field ??= new SqliteCommand("SELECT COUNT(1) FROM FILES LIMIT 1", Program.db.Connection);
                return field;
            }
        }

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
            CommandWrite.Parameters["crc"].Value = VarFix.ToDBString(CRC) ?? string.Empty;
            CommandWrite.Parameters["sha1"].Value = VarFix.ToDBString(SHA1) ?? string.Empty;
            CommandWrite.Parameters["md5"].Value = VarFix.ToDBString(MD5) ?? string.Empty;
            CommandWrite.Parameters["alttype"].Value = ((int)AltType).ToString() ?? string.Empty;
            CommandWrite.Parameters["altsize"].Value = AltSize ?? 0;
            CommandWrite.Parameters["altcrc"].Value = VarFix.ToDBString(AltCRC) ?? string.Empty;
            CommandWrite.Parameters["altsha1"].Value = VarFix.ToDBString(AltSHA1) ?? string.Empty;
            CommandWrite.Parameters["altmd5"].Value = VarFix.ToDBString(AltMD5) ?? string.Empty;

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
