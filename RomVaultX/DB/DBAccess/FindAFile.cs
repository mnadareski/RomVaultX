using System;
using System.Data.Common;
using RomVaultX.Util;

namespace RomVaultX.DB.DBAccess
{
    public static class FindAFile
    {
        private static readonly DbCommand CommandSHA1;
        private static readonly DbCommand CommandMD5;
        private static readonly DbCommand CommandCRC;
        private static readonly DbCommand CommandSize;

        private static readonly DbCommand CommandSHA1Alt;
        private static readonly DbCommand CommandMD5Alt;
        private static readonly DbCommand CommandCRCAlt;



        static FindAFile()
        {
            CommandSHA1 = Program.db.Command(
                @"
                       select FileId from memdb.FILESMEM
                            WHERE
	                                    (                  @sha1 = sha1 ) AND
	                                    ( @md5  is NULL OR @md5  = md5  ) AND
	                                    ( @crc  is NULL OR @crc  = crc  ) AND
	                                    ( @size is NULL OR @size = Size )
                            limit 1
                ");

            CommandSHA1.Parameters.Add(Program.db.Parameter("sha1"));
            CommandSHA1.Parameters.Add(Program.db.Parameter("md5"));
            CommandSHA1.Parameters.Add(Program.db.Parameter("crc"));
            CommandSHA1.Parameters.Add(Program.db.Parameter("size"));


            CommandSHA1Alt = Program.db.Command(
                @"
                       select FileId from memdb.FILESMEM
                            WHERE
                                        (               @alttype = alttype ) AND
	                                    (                  @sha1 = altsha1 ) AND
	                                    ( @md5  is NULL OR @md5  = altmd5  ) AND
	                                    ( @crc  is NULL OR @crc  = altcrc  ) AND
	                                    ( @size is NULL OR @size = altSize )
                            limit 1
                ");

            CommandSHA1Alt.Parameters.Add(Program.db.Parameter("alttype"));
            CommandSHA1Alt.Parameters.Add(Program.db.Parameter("sha1"));
            CommandSHA1Alt.Parameters.Add(Program.db.Parameter("md5"));
            CommandSHA1Alt.Parameters.Add(Program.db.Parameter("crc"));
            CommandSHA1Alt.Parameters.Add(Program.db.Parameter("size"));



            CommandMD5 = Program.db.Command(
                @"
                       select FileId from memdb.FILESMEM
                            WHERE
	                                    (                  @md5  = md5  ) AND
	                                    ( @crc  is NULL OR @crc  = crc  ) AND
	                                    ( @size is NULL OR @size = Size )
                            limit 1
                ");

            CommandMD5.Parameters.Add(Program.db.Parameter("md5"));
            CommandMD5.Parameters.Add(Program.db.Parameter("crc"));
            CommandMD5.Parameters.Add(Program.db.Parameter("size"));

            CommandMD5Alt = Program.db.Command(
                @"
                       select FileId from memdb.FILESMEM
                            WHERE
                                        (               @alttype = alttype ) AND
	                                    (                  @md5  = altmd5  ) AND
	                                    ( @crc  is NULL OR @crc  = altcrc  ) AND
	                                    ( @size is NULL OR @size = altSize )
                            limit 1
                ");

            CommandMD5Alt.Parameters.Add(Program.db.Parameter("alttype"));
            CommandMD5Alt.Parameters.Add(Program.db.Parameter("md5"));
            CommandMD5Alt.Parameters.Add(Program.db.Parameter("crc"));
            CommandMD5Alt.Parameters.Add(Program.db.Parameter("size"));


            CommandCRC = Program.db.Command(
                @"
                       select FileId from memdb.FILESMEM
                            WHERE
	                                    (                  @crc  = crc  ) AND
	                                    ( @size is NULL OR @size = Size )
                            limit 1
                ");

            CommandCRC.Parameters.Add(Program.db.Parameter("crc"));
            CommandCRC.Parameters.Add(Program.db.Parameter("size"));

            CommandCRCAlt = Program.db.Command(
    @"
                       select FileId from memdb.FILESMEM
                            WHERE
                                        (               @alttype = alttype ) AND
	                                    (                  @crc  = altcrc  ) AND
	                                    ( @size is NULL OR @size = altSize )
                            limit 1
                ");

            CommandCRCAlt.Parameters.Add(Program.db.Parameter("crc"));
            CommandCRCAlt.Parameters.Add(Program.db.Parameter("size"));



            CommandSize = Program.db.Command(
                @"
                       select FileId from memdb.FILESMEM
                            WHERE
	                                    ( @size = Size )
                            limit 1
                ");

            CommandSize.Parameters.Add(Program.db.Parameter("size"));

        }

        public static bool copyDBtoMem()
        {
            DataAccessLayer.ExecuteNonQuery(@"
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

            DataAccessLayer.ExecuteNonQuery(@"
                DELETE FROM memdb.FILESMEM;");


            DataAccessLayer.ExecuteNonQuery(@"
                CREATE INDEX IF NOT EXISTS memdb.memFILESHA1 ON FILESMEM ([sha1] ASC);
                CREATE INDEX IF NOT EXISTS memdb.memFILEMD5 ON FILESMEM ([md5] ASC);
                CREATE INDEX IF NOT EXISTS memdb.memFILECRC ON FILESMEM ([crc] ASC);
                CREATE INDEX IF NOT EXISTS memdb.memFILESize ON FILESMEM ([size] ASC);");

            DataAccessLayer.ExecuteNonQuery(@"
                INSERT INTO memdb.FILESMEM SELECT FileId,size,crc,sha1,md5,alttype,altsize,altcrc,altsha1,altmd5 FROM FILES");

            DbCommand count = Program.db.Command("SELECT COUNT(1) FROM memdb.FILESMEM LIMIT 1");
            object res = count.ExecuteScalar();
            if (res == null || res == DBNull.Value)
                return true;
            return Convert.ToInt32(res) == 0;
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
    }
}
