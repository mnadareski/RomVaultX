using System;
using System.Data.Common;
using RomVaultX.Util;

namespace RomVaultX.DB.DBAccess
{
    public static class FindInFiles
    {
        private static readonly DbCommand Command;

        static FindInFiles()
        {
            Command = Program.db.Command(
              @"
                    SELECT COUNT(1) FROM FILES WHERE
                        size=@size AND crc=@CRC and sha1=@SHA1 and md5=@MD5");
            Command.Parameters.Add(Program.db.Parameter("size"));
            Command.Parameters.Add(Program.db.Parameter("crc"));
            Command.Parameters.Add(Program.db.Parameter("sha1"));
            Command.Parameters.Add(Program.db.Parameter("md5"));
        }

        public static bool Execute(RvFile tFile)
        {
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

    }
}
