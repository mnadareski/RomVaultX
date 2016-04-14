using System.Data.Common;
using RomVaultX.Util;

namespace RomVaultX.DB.DBAccess
{
    public static class GetFile
    {
        private static readonly DbCommand Command;

        static GetFile()
        {
            Command = Program.db.Command(
              @"
                    SELECT sha1 FROM FILES WHERE
                        fileId=@fileId");
            Command.Parameters.Add(Program.db.Parameter("fileId"));
        }

        public static byte[] Execute(uint fileId)
        {
            Command.Parameters["fileId"].Value = fileId;

            byte[] sha1=null;
            using (DbDataReader dr = Command.ExecuteReader())
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
