using System;
using System.Data.SQLite;
using RomVaultX.Util;

namespace RomVaultX.DB
{
    public class RvFile
    {
        public ulong Size;
        public ulong CompressedSize;
        public byte[] CRC;
        public byte[] SHA1;
        public byte[] MD5;

        public FileType AltType;
        public ulong? AltSize;
        public byte[] AltCRC;
        public byte[] AltSHA1;
        public byte[] AltMD5;

        private static readonly SQLiteCommand SqlWrite;
        private static readonly SQLiteCommand SqlUpdateRom;
        private static readonly SQLiteCommand SqlUpdateZeroRom;

        static RvFile()
        {
            SqlWrite = new SQLiteCommand(
                @"INSERT INTO FILES (size,compressedsize,crc,sha1,md5)
                        VALUES (@Size,@compressedsize,@CRC,@SHA1,@MD5);

                SELECT last_insert_rowid();",DataAccessLayer.DBConnection);

            SqlWrite.Parameters.Add(new SQLiteParameter("size"));
            SqlWrite.Parameters.Add(new SQLiteParameter("compressedsize"));
            SqlWrite.Parameters.Add(new SQLiteParameter("crc"));
            SqlWrite.Parameters.Add(new SQLiteParameter("sha1"));
            SqlWrite.Parameters.Add(new SQLiteParameter("md5"));

            SqlUpdateRom=new SQLiteCommand(
                @"
                    UPDATE ROM SET 
	                    FileId = @FileId
                    WHERE
	                    (                 sha1 = @sha1 ) AND
	                    ( md5  is NULL OR md5  = @md5  ) AND 
	                    ( crc  is NULL OR crc  = @crc  ) AND
	                    ( size is NULL OR size = @Size ) AND
	                    ( status != 'nodump' OR status is NULL) AND 
                        FileId IS NULL;
		
                    UPDATE ROM SET 
	                    FileId = @FileId
                    WHERE
	                    (                 md5  = @md5  ) AND 
	                    ( sha1 is NULL OR sha1 = @sha1 ) AND
	                    ( crc  is NULL OR crc  = @crc  ) AND
	                    ( size is NULL OR size = @Size ) AND
	                    ( status != 'nodump' OR status is NULL) AND 
                        FileId IS NULL;
		
                    UPDATE ROM SET 
	                    FileId = @FileId
                    WHERE
	                    (                 crc  = @crc  ) AND
	                    ( sha1 is NULL OR sha1 = @sha1 ) AND
	                    ( md5  is NULL OR md5  = @md5  ) AND 
	                    ( size is NULL OR size = @Size ) AND
	                    ( status != 'nodump' OR status is NULL) AND 
                        FileId IS NULL;
                ", DataAccessLayer.DBConnection);
            SqlUpdateRom.Parameters.Add(new SQLiteParameter("FileId"));
            SqlUpdateRom.Parameters.Add(new SQLiteParameter("size"));
            SqlUpdateRom.Parameters.Add(new SQLiteParameter("crc"));
            SqlUpdateRom.Parameters.Add(new SQLiteParameter("sha1"));
            SqlUpdateRom.Parameters.Add(new SQLiteParameter("md5"));

            SqlUpdateZeroRom = new SQLiteCommand(
                @"
                    UPDATE ROM SET 
	                    FileId = @FileId
                    WHERE
	                    ( Size=0 ) AND
	                    ( crc  is NULL OR crc  = @crc  ) AND
	                    ( sha1 is NULL OR sha1 = @sha1 ) AND
	                    ( md5  is NULL OR md5  = @md5  ) AND 
	                    ( status != 'nodump' OR status is NULL) AND 
                        FileId IS NULL;
                ", DataAccessLayer.DBConnection);
            SqlUpdateZeroRom.Parameters.Add(new SQLiteParameter("FileId"));
            SqlUpdateZeroRom.Parameters.Add(new SQLiteParameter("crc"));
            SqlUpdateZeroRom.Parameters.Add(new SQLiteParameter("sha1"));
            SqlUpdateZeroRom.Parameters.Add(new SQLiteParameter("md5"));

        }

        public static void MakeDB()
        {
            DataAccessLayer.ExecuteNonQuery(@"
              
                CREATE TABLE IF NOT EXISTS [FILES] (
                    [FileId] INTEGER PRIMARY KEY NOT NULL,
                    [size] INTEGER NOT NULL,
                    [compressedsize] INTEGER NULL,
                    [crc] VARCHAR(8) NULL,
                    [sha1] VARCHAR(40) NULL,
                    [md5] VARCHAR(32) NULL
                );
            ");
        }
        public void DBWrite()
        {
            DataAccessLayer.ExecuteNonQuery("BEGIN");

            SqlWrite.Parameters["size"].Value = Size;
            SqlWrite.Parameters["compressedsize"].Value = CompressedSize;
            SqlWrite.Parameters["crc"].Value = VarFix.ToDBString(CRC);
            SqlWrite.Parameters["sha1"].Value = VarFix.ToDBString(SHA1);
            SqlWrite.Parameters["md5"].Value = VarFix.ToDBString(MD5);

            object res = SqlWrite.ExecuteScalar();
            UInt32 fileId= Convert.ToUInt32(res);

            if (Size != 0)
            {
                SqlUpdateRom.Parameters["FileId"].Value = fileId;
                SqlUpdateRom.Parameters["size"].Value = Size;
                SqlUpdateRom.Parameters["crc"].Value = VarFix.ToDBString(CRC);
                SqlUpdateRom.Parameters["sha1"].Value = VarFix.ToDBString(SHA1);
                SqlUpdateRom.Parameters["md5"].Value = VarFix.ToDBString(MD5);
                SqlUpdateRom.ExecuteNonQuery();
            }
            else
            {
                SqlUpdateZeroRom.Parameters["FileId"].Value = fileId;
                SqlUpdateZeroRom.Parameters["crc"].Value = VarFix.ToDBString(CRC);
                SqlUpdateZeroRom.Parameters["sha1"].Value = VarFix.ToDBString(SHA1);
                SqlUpdateZeroRom.Parameters["md5"].Value = VarFix.ToDBString(MD5);
                SqlUpdateZeroRom.ExecuteNonQuery();                
            }
            DataAccessLayer.ExecuteNonQuery("COMMIT");

        }
    }
}
