using System;
using System.Data.SQLite;
using RomVaultX.Util;

namespace RomVaultX.DB.DBAccess
{
    public static class FindInFiles
    {
        private static readonly SQLiteCommand Command;

        static FindInFiles()
        {
            Command = new SQLiteCommand(
              @"
                    SELECT COUNT(1) FROM FILES WHERE
                        size=@size AND crc=@CRC and sha1=@SHA1 and md5=@MD5", DataAccessLayer.DBConnection);
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

            object res = Command.ExecuteScalar();
            if (res == null || res == DBNull.Value)
                return false;
            int count = Convert.ToInt32(res);

            return count > 0;
        }

    }
}
