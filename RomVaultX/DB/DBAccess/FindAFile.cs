using System;
using System.Data.SQLite;
using RomVaultX.Util;

namespace RomVaultX.DB.DBAccess
{
    public static class FindAFile
    {
        private static readonly SQLiteCommand Command;
        
        static FindAFile()
        {
            Command = new SQLiteCommand(
            @"

                    select (coalesce(

                       (select FileId from memdb.FILESMEM
                            WHERE
	                                    (                  @sha1 = sha1 ) AND
	                                    ( @md5  is NULL OR @md5  = md5  ) AND
	                                    ( @crc  is NULL OR @crc  = crc  ) AND
	                                    ( @size is NULL OR @size = Size )
                            limit 1)
                       ,
                       (select FileId from memdb.FILESMEM
                            WHERE
	                                    (                  @md5  = md5  ) AND
	                                    ( @sha1 is NULL OR @sha1 = sha1 ) AND
	                                    ( @crc  is NULL OR @crc  = crc  ) AND
	                                    ( @size is NULL OR @size = Size )
                            limit 1)
                       ,
                       (select FileId from memdb.FILESMEM
                            WHERE
	                                    (                  @crc  = crc  ) AND
	                                    ( @sha1 is NULL OR @sha1 = sha1 ) AND
	                                    ( @md5  is NULL OR @md5  = md5  ) AND
	                                    ( @size is NULL OR @size = Size )
                            limit 1)
                       )) as FileId limit 1;
                ", DataAccessLayer.dbConnection);

            Command.Parameters.Add(new SQLiteParameter("sha1"));
            Command.Parameters.Add(new SQLiteParameter("md5"));
            Command.Parameters.Add(new SQLiteParameter("crc"));
            Command.Parameters.Add(new SQLiteParameter("size"));

        }

        private static SQLiteConnection _memoryConnection;

        public static void copyDBtoMem()
        {
            DataAccessLayer.ExecuteNonQuery(@"
              CREATE TABLE IF NOT EXISTS memdb.FILESMEM (
                    [FileId] INTEGER NOT NULL,
                    [size] INTEGER NOT NULL,
                    [crc] VARCHAR(8) NULL,
                    [sha1] VARCHAR(40) NULL,
                    [md5] VARCHAR(32) NULL
                );");

            DataAccessLayer.ExecuteNonQuery(@"
                DELETE FROM memdb.FILESMEM;");


            DataAccessLayer.ExecuteNonQuery(@"
                CREATE INDEX IF NOT EXISTS memdb.memFILESHA1 ON FILESMEM ([sha1] ASC);
                CREATE INDEX IF NOT EXISTS memdb.memFILEMD5 ON FILESMEM ([md5] ASC);
                CREATE INDEX IF NOT EXISTS memdb.memFILECRC ON FILESMEM ([crc] ASC);");

            DataAccessLayer.ExecuteNonQuery(@"
                INSERT INTO memdb.FILESMEM SELECT * FROM FILES");
        }

        public static uint? Execute(RvRom tFile)
        {
            Command.Parameters["size"].Value = tFile.Size;
            Command.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
            Command.Parameters["sha1"].Value = VarFix.ToDBString(tFile.SHA1);
            Command.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5);

            object res = Command.ExecuteScalar();

            if (res == null || res == DBNull.Value)
                return null;
            return (uint?)Convert.ToInt32(res);
        }
    }
}
