using System;
using System.Data.SQLite;
using RomVaultX.Util;

namespace RomVaultX.DB.DBAccess
{
    public static class FindAFile
    {
        private static readonly SQLiteCommand CommandSHA1;
        private static readonly SQLiteCommand CommandMD5;
        private static readonly SQLiteCommand CommandCRC;
        private static readonly SQLiteCommand CommandSize;



        static FindAFile()
        {
            CommandSHA1 = new SQLiteCommand(
                @"
                       select FileId from memdb.FILESMEM
                            WHERE
	                                    (                  @sha1 = sha1 ) AND
	                                    ( @md5  is NULL OR @md5  = md5  ) AND
	                                    ( @crc  is NULL OR @crc  = crc  ) AND
	                                    ( @size is NULL OR @size = Size )
                            limit 1
                ", DataAccessLayer.DBConnection);

            CommandSHA1.Parameters.Add(new SQLiteParameter("sha1"));
            CommandSHA1.Parameters.Add(new SQLiteParameter("md5"));
            CommandSHA1.Parameters.Add(new SQLiteParameter("crc"));
            CommandSHA1.Parameters.Add(new SQLiteParameter("size"));

            CommandMD5 = new SQLiteCommand(
                @"
                       select FileId from memdb.FILESMEM
                            WHERE
	                                    (                  @md5  = md5  ) AND
	                                    ( @crc  is NULL OR @crc  = crc  ) AND
	                                    ( @size is NULL OR @size = Size )
                            limit 1
                ", DataAccessLayer.DBConnection);

            CommandMD5.Parameters.Add(new SQLiteParameter("md5"));
            CommandMD5.Parameters.Add(new SQLiteParameter("crc"));
            CommandMD5.Parameters.Add(new SQLiteParameter("size"));

            CommandCRC = new SQLiteCommand(
                @"
                       select FileId from memdb.FILESMEM
                            WHERE
	                                    (                  @crc  = crc  ) AND
	                                    ( @size is NULL OR @size = Size )
                            limit 1
                ", DataAccessLayer.DBConnection);

            CommandCRC.Parameters.Add(new SQLiteParameter("crc"));
            CommandCRC.Parameters.Add(new SQLiteParameter("size"));

            CommandSize = new SQLiteCommand(
                @"
                       select FileId from memdb.FILESMEM
                            WHERE
	                                    ( @size = Size )
                            limit 1
                ", DataAccessLayer.DBConnection);

            CommandSize.Parameters.Add(new SQLiteParameter("size"));

        }

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
                CREATE INDEX IF NOT EXISTS memdb.memFILECRC ON FILESMEM ([crc] ASC);
                CREATE INDEX IF NOT EXISTS memdb.memFILESize ON FILESMEM ([size] ASC);");

            DataAccessLayer.ExecuteNonQuery(@"
                INSERT INTO memdb.FILESMEM SELECT FileId,size,crc,sha1,md5 FROM FILES");
        }

        public static uint? Execute(RvRom tFile)
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
            if (tFile.CRC != null)
            {
                CommandCRC.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
                CommandCRC.Parameters["size"].Value = tFile.Size;

                object res = CommandCRC.ExecuteScalar();

                if (res == null || res == DBNull.Value)
                    return null;
                return (uint?)Convert.ToInt32(res);
            }
            if (tFile.Size != null && tFile.Size==0)
            {
                CommandSize.Parameters["size"].Value = tFile.Size;

                object res = CommandSize.ExecuteScalar();

                if (res == null || res == DBNull.Value)
                    return null;
                return (uint?)Convert.ToInt32(res);
            }

            return null;
        }
    }
}
