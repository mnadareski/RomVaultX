using System;
using System.Data.SQLite;
using System.Diagnostics;
using RomVaultX.Util;

namespace RomVaultX.DB.DBAccess
{
    public static class FindInROMs
    {
        private static readonly SQLiteCommand Command;

        static FindInROMs()
        {
            Command = new SQLiteCommand(
               @"
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
                                ( sha1=@SHA1 OR sha1 is NULL ) AND 
                                ( md5=@MD5 ) AND
                                ( crc=@CRC OR crc is NULL ) AND
                                ( size=@size OR size is NULL ) AND
                                ( status!='nodump' or status is NULL)
                        ) +
                        (
                            SELECT count(1) FROM ROM WHERE
                                ( sha1=@SHA1 OR sha1 is NULL ) AND 
                                ( md5=@MD5 OR md5 is NULL) AND
                                ( crc=@CRC ) AND
                                ( size=@size OR size is NULL ) AND
                                ( status!='nodump' or status is NULL)
                        )
                        AS TotalFound"
               , DataAccessLayer.dbConnection);
            Command.Parameters.Add(new SQLiteParameter("size"));
            Command.Parameters.Add(new SQLiteParameter("crc"));
            Command.Parameters.Add(new SQLiteParameter("sha1"));
            Command.Parameters.Add(new SQLiteParameter("md5"));
        }

        public static bool Execute(rvFile tFile)
        {
            Command.Parameters["size"].Value = tFile.Size;
            Command.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
            Command.Parameters["sha1"].Value = VarFix.ToDBString(tFile.SHA1);
            Command.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5);

            Stopwatch sw = new Stopwatch();

            sw.Reset();
            sw.Start();
            object res = Command.ExecuteScalar();
            sw.Stop();

            if (res == null || res == DBNull.Value)
                return false;
            int count = Convert.ToInt32(res);

            Debug.WriteLine("Time =" + sw.ElapsedMilliseconds + " : Found " + count);

            return count > 0;
        }
    }
}
