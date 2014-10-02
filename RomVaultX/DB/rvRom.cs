using System;
using System.Collections.Generic;
using System.Data.SQLite;
using RomVaultX.DB.DBAccess;
using RomVaultX.Util;

namespace RomVaultX.DB
{
    public class RvRom
    {
        public uint RomId;
        public uint GameId;
        public string Name;
        public ulong? Size;
        public byte[] CRC;
        public byte[] SHA1;
        public byte[] MD5;
        public string Merge;
        public string Status;
        public ulong? FileId;

        public ulong? fileSize;
        public byte[] fileCRC;
        public byte[] fileSHA1;
        public byte[] fileMD5;


        private static readonly SQLiteCommand SqlWrite;
        private static readonly SQLiteCommand SqlRead;

        static RvRom()
        {
            SqlWrite = new SQLiteCommand(
                @"INSERT INTO ROM  ( GameId, name, size, crc, sha1, md5, merge, status,FileId)
                            VALUES (@GameId,@Name,@Size,@CRC,@SHA1,@MD5,@Merge,@Status,@FileId);

                SELECT last_insert_rowid();", DataAccessLayer.dbConnection);

            SqlWrite.Parameters.Add(new SQLiteParameter("GameId"));
            SqlWrite.Parameters.Add(new SQLiteParameter("Name"));
            SqlWrite.Parameters.Add(new SQLiteParameter("Size"));
            SqlWrite.Parameters.Add(new SQLiteParameter("CRC"));
            SqlWrite.Parameters.Add(new SQLiteParameter("SHA1"));
            SqlWrite.Parameters.Add(new SQLiteParameter("MD5"));
            SqlWrite.Parameters.Add(new SQLiteParameter("Merge"));
            SqlWrite.Parameters.Add(new SQLiteParameter("Status"));
            SqlWrite.Parameters.Add(new SQLiteParameter("FileId"));

            SqlRead = new SQLiteCommand(
                @"SELECT RomId,name,
                    rom.size,
                    rom.crc,
                    rom.sha1,
                    rom.md5,
                    merge,status,
                    rom.FileId,
                    files.size as fileSize,
                    files.crc as filecrc,
                    files.sha1 as filesha1,
                    files.md5 as filemd5
                FROM rom LEFT OUTER JOIN files ON files.FileId=rom.FileId WHERE GameId=@GameId", DataAccessLayer.dbConnection);
            SqlRead.Parameters.Add(new SQLiteParameter("GameId"));
        }

        public static void MakeDB()
        {

            DataAccessLayer.ExecuteNonQuery(@"
               CREATE TABLE IF NOT EXISTS [ROM] (
                    [RomId] INTEGER PRIMARY KEY NOT NULL,
                    [GameId] INTEGER NOT NULL,
                    [name] NVARCHAR(320) NOT NULL,
                    [size] INTEGER NULL,
                    [crc] VARCHAR(8) NULL,
                    [sha1] VARCHAR(40) NULL,
                    [md5] VARCHAR(32) NULL,
                    [merge] VARCHAR(20) NULL,
                    [status] VARCHAR(20) NULL,
                    [FileId] INTEGER NULL,
                    FOREIGN KEY(GameId) REFERENCES Game(GameId),
                    FOREIGN KEY(FileId) REFERENCES File(FileId)
                );");
        }

        public static List<RvRom> ReadRoms(uint gameId)
        {
            List<RvRom> roms = new List<RvRom>();
            SqlRead.Parameters["GameId"].Value = gameId;

            using (SQLiteDataReader dr = SqlRead.ExecuteReader())
            {
                while (dr.Read())
                {
                    RvRom row = new RvRom
                    {
                        RomId = Convert.ToUInt32(dr["RomId"]),
                        GameId = gameId,
                        Name = dr["name"].ToString(),
                        Size = FixLong(dr["size"]),
                        CRC = VarFix.CleanMD5SHA1(dr["CRC"].ToString(), 8), 
                        SHA1 = VarFix.CleanMD5SHA1(dr["SHA1"].ToString(), 40),
                        MD5 = VarFix.CleanMD5SHA1(dr["MD5"].ToString(), 32),
                        Merge = dr["merge"].ToString(), 
                        Status = dr["status"].ToString(),
                        FileId = FixLong(dr["FileId"]), 
                        fileSize = FixLong(dr["fileSize"]),
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

        public void DBWrite()
        {
            FileId = FindAFile.Execute(this);

            SqlWrite.Parameters["GameId"].Value = GameId;
            SqlWrite.Parameters["name"].Value = Name;
            SqlWrite.Parameters["size"].Value = Size;
            SqlWrite.Parameters["crc"].Value = VarFix.ToDBString(CRC);
            SqlWrite.Parameters["sha1"].Value = VarFix.ToDBString(SHA1);
            SqlWrite.Parameters["md5"].Value = VarFix.ToDBString(MD5);
            SqlWrite.Parameters["merge"].Value = Merge;
            SqlWrite.Parameters["status"].Value = Status;
            SqlWrite.Parameters["FileID"].Value = FileId;
            SqlWrite.ExecuteNonQuery();
        }
    }
}
