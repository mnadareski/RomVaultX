using System;
using System.Data.Common;
using System.Data.Common;
using RomVaultX.Util;

namespace RomVaultX.DB.DBAccess
{
    public static class FindInROMs
    {
        private static readonly DbCommand Command;
        private static readonly DbCommand CommandAlt;
        private static readonly DbCommand CommandZero;

        static FindInROMs()
        {
            Command = Program.db.Command(
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
                                ( md5=@MD5 ) AND
                                ( sha1=@SHA1 OR sha1 is NULL ) AND 
                                ( crc=@CRC OR crc is NULL ) AND
                                ( size=@size OR size is NULL ) AND
                                ( status!='nodump' or status is NULL)
                        ) +
                        (
                            SELECT count(1) FROM ROM WHERE
                                ( crc=@CRC ) AND
                                ( sha1=@SHA1 OR sha1 is NULL ) AND 
                                ( md5=@MD5 OR md5 is NULL) AND
                                ( size=@size OR size is NULL ) AND
                                ( status!='nodump' or status is NULL)
                        ) 
                        AS TotalFound"
               );
            Command.Parameters.Add(Program.db.Parameter("size"));
            Command.Parameters.Add(Program.db.Parameter("crc"));
            Command.Parameters.Add(Program.db.Parameter("sha1"));
            Command.Parameters.Add(Program.db.Parameter("md5"));

            CommandAlt = Program.db.Command(
              @"
                        SELECT
                        (
                            SELECT count(1) FROM ROM WHERE
                                ( type=@type ) AND
                                ( sha1=@SHA1 ) AND 
                                ( md5=@MD5 OR md5 is NULL) AND
                                ( crc=@CRC OR crc is NULL ) AND
                                ( size=@size OR size is NULL ) AND
                                ( status!='nodump' or status is NULL)
                        ) +
                        (
                            SELECT count(1) FROM ROM WHERE
                                ( type=@type ) AND
                                ( md5=@MD5 ) AND
                                ( sha1=@SHA1 OR sha1 is NULL ) AND 
                                ( crc=@CRC OR crc is NULL ) AND
                                ( size=@size OR size is NULL ) AND
                                ( status!='nodump' or status is NULL)
                        ) +
                        (
                            SELECT count(1) FROM ROM WHERE
                                ( type=@type ) AND
                                ( crc=@CRC ) AND
                                ( sha1=@SHA1 OR sha1 is NULL ) AND 
                                ( md5=@MD5 OR md5 is NULL) AND
                                ( size=@size OR size is NULL ) AND
                                ( status!='nodump' or status is NULL)
                        ) 
                        AS TotalFound"
              );
            CommandAlt.Parameters.Add(Program.db.Parameter("type"));
            CommandAlt.Parameters.Add(Program.db.Parameter("size"));
            CommandAlt.Parameters.Add(Program.db.Parameter("crc"));
            CommandAlt.Parameters.Add(Program.db.Parameter("sha1"));
            CommandAlt.Parameters.Add(Program.db.Parameter("md5"));


            CommandZero = Program.db.Command(
               @"
                    SELECT count(1) AS TotalFound FROM ROM WHERE
                        ( sha1=@SHA1 OR sha1 is NULL ) AND 
                        ( md5=@MD5 OR md5 is NULL) AND
                        ( crc=@CRC OR crc is NULL ) AND
                        ( size=0 ) AND
                        ( status!='nodump' or status is NULL)"
               );
            CommandZero.Parameters.Add(Program.db.Parameter("crc"));
            CommandZero.Parameters.Add(Program.db.Parameter("sha1"));
            CommandZero.Parameters.Add(Program.db.Parameter("md5"));

        }


        public static bool Execute(RvFile tFile)
        {
            if (tFile.Size == 0)
            {
                CommandZero.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
                CommandZero.Parameters["sha1"].Value = VarFix.ToDBString(tFile.SHA1);
                CommandZero.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5);

                object resZero = CommandZero.ExecuteScalar();

                if (resZero == null || resZero == DBNull.Value)
                    return false;
                int countZero = Convert.ToInt32(resZero);

                return countZero > 0;
                
            }
            
            Command.Parameters["size"].Value = tFile.Size;
            Command.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC);
            Command.Parameters["sha1"].Value = VarFix.ToDBString(tFile.SHA1);
            Command.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5);

            object res = Command.ExecuteScalar();

            if (res == null || res == DBNull.Value)
                return false;
            int count = Convert.ToInt32(res);

            return count > 0;
        }


        public static bool ExecuteAlt(RvFile tFile)
        {
            CommandAlt.Parameters["type"].Value =(int) tFile.AltType;
            CommandAlt.Parameters["size"].Value = tFile.AltSize;
            CommandAlt.Parameters["crc"].Value = VarFix.ToDBString(tFile.AltCRC);
            CommandAlt.Parameters["sha1"].Value = VarFix.ToDBString(tFile.AltSHA1);
            CommandAlt.Parameters["md5"].Value = VarFix.ToDBString(tFile.AltMD5);

            object res = CommandAlt.ExecuteScalar();

            if (res == null || res == DBNull.Value)
                return false;
            int count = Convert.ToInt32(res);

            return count > 0;
        }
    }
}
