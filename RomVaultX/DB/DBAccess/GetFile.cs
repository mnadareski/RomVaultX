using System.Data.SQLite;
using RomVaultX.Util;

namespace RomVaultX.DB.DBAccess
{
    public static class GetFile
    {
        private static readonly SQLiteCommand Command;

        static GetFile()
        {
            Command = new SQLiteCommand(
              @"
                    SELECT sha1 FROM FILES WHERE
                        fileId=@fileId", DataAccessLayer.dbConnection);
            Command.Parameters.Add(new SQLiteParameter("fileId"));
        }

        public static byte[] Execute(uint fileId)
        {
            Command.Parameters["fileId"].Value = fileId;

            byte[] sha1=null;
            using (SQLiteDataReader dr = Command.ExecuteReader())
            {
                while (dr.Read())
                {
                    sha1 = VarFix.CleanMD5SHA1(dr["SHA1"].ToString(), 40);
                }
                dr.Close();
            }
            return sha1;
        }

    }
}
