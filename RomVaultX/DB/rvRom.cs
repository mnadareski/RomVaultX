using System;
using System.Collections.Generic;
using System.Data.SQLite;
using RomVaultX.Util;

namespace RomVaultX.DB
{
    public class RvRom
    {
        public int RomId;
        public int GameId;
        public string Name;
        public ulong? Size;
        public byte[] CRC;
        public byte[] SHA1;
        public byte[] MD5;
        public string Merge;
        public string Status;


        private static readonly SQLiteCommand SqlWrite;
        private static readonly SQLiteCommand SqlRead;

        static RvRom()
        {
            SqlWrite = new SQLiteCommand(
                @"INSERT INTO ROM  ( GameId, name, size, crc, sha1, md5, merge, status)
                            VALUES (@GameId,@Name,@Size,@CRC,@SHA1,@MD5,@Merge,@Status);

                SELECT last_insert_rowid();");

            SqlWrite.Parameters.Add(new SQLiteParameter("GameId"));
            SqlWrite.Parameters.Add(new SQLiteParameter("Name"));
            SqlWrite.Parameters.Add(new SQLiteParameter("Size"));
            SqlWrite.Parameters.Add(new SQLiteParameter("CRC"));
            SqlWrite.Parameters.Add(new SQLiteParameter("SHA1"));
            SqlWrite.Parameters.Add(new SQLiteParameter("MD5"));
            SqlWrite.Parameters.Add(new SQLiteParameter("Merge"));
            SqlWrite.Parameters.Add(new SQLiteParameter("Status"));

            SqlRead = new SQLiteCommand(
                @"SELECT RomId,name,size,crc,sha1,md5,merge,status
                    FROM ROM WHERE GameId=@GameId");
            SqlRead.Parameters.Add(new SQLiteParameter("GameId"));
        }
        public static void SetConnection(SQLiteConnection connection)
        {
            SqlWrite.Connection = connection;
            SqlRead.Connection = connection;
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
                    [status] VARCHAR(20) NULL
                );");
        }

        public static IEnumerable<RvRom> ReadRoms(int gameId)
        {
            List<RvRom> rows = new List<RvRom>();
            SqlRead.Parameters["GameId"].Value = gameId;

            using (SQLiteDataReader dr = SqlRead.ExecuteReader())
            {
                while (dr.Read())
                {
                    RvRom row = new RvRom
                    {
                        RomId = Convert.ToInt32(dr["RomId"]), 
                        GameId = gameId, 
                        Name = dr["name"].ToString(),
                        Size = (ulong?) Convert.ToInt64(dr["size"]),
                        CRC = VarFix.CleanMD5SHA1(dr["CRC"].ToString(), 8), 
                        SHA1 = VarFix.CleanMD5SHA1(dr["SHA1"].ToString(), 40),
                        MD5 = VarFix.CleanMD5SHA1(dr["MD5"].ToString(), 32),
                        Merge = dr["merge"].ToString(), 
                        Status = dr["status"].ToString()
                    };

                    rows.Add(row);
                }
                dr.Close();
            }
            return rows;
        }

        public void DBWrite()
        {
            SqlWrite.Parameters["GameId"].Value = GameId;
            SqlWrite.Parameters["name"].Value = Name;
            SqlWrite.Parameters["size"].Value = Size;
            SqlWrite.Parameters["crc"].Value = VarFix.ToString(CRC);
            SqlWrite.Parameters["sha1"].Value = VarFix.ToString(SHA1);
            SqlWrite.Parameters["md5"].Value = VarFix.ToString(MD5);
            SqlWrite.Parameters["merge"].Value = Merge;
            SqlWrite.Parameters["status"].Value = Status;
            SqlWrite.ExecuteNonQuery();
        }
    }
}
