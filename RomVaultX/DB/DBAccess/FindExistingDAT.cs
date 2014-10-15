using System;
using System.Data.SQLite;

namespace RomVaultX.DB.DBAccess
{
    public static class FindDAT
    {

        private static class FindExistingDat
        {
            private static readonly SQLiteCommand Command;

            static FindExistingDat()
            {
                Command = new SQLiteCommand(
           @"SELECT DatId FROM Dat,Dir WHERE Dat.DirId=Dir.DirId AND fullname=@fullname AND Filename=@filename AND DatTimeStamp=@DatTimeStamp", DataAccessLayer.DBConnection);
                Command.Parameters.Add(new SQLiteParameter("fullname"));
                Command.Parameters.Add(new SQLiteParameter("filename"));
                Command.Parameters.Add(new SQLiteParameter("DatTimeStamp"));
            }

            public static uint? Execute(string fulldir, string filename, long DatTimeStamp)
            {
                Command.Parameters["fullname"].Value = fulldir + "\\";
                Command.Parameters["filename"].Value = filename;
                Command.Parameters["DatTimeStamp"].Value = DatTimeStamp.ToString();

                object res = Command.ExecuteScalar();

                if (res == null || res == DBNull.Value)
                    return null;
                return Convert.ToUInt32(res);
                
            }
        }

        public static class SetDatFound
        {
            private static readonly SQLiteCommand Command;

            static SetDatFound()
            {
                Command = new SQLiteCommand(
                     @"Update Dat SET Found=1 WHERE DatId=@DatId", DataAccessLayer.DBConnection);
                Command.Parameters.Add(new SQLiteParameter("DatId"));

            }

            public static void Execute(uint datId)
            {
                Command.Parameters["DatId"].Value = datId;
                Command.ExecuteNonQuery();
                
            }
        }


        public static uint Execute(string fulldir, string filename, long DatTimeStamp)
        {
            uint? datId = FindExistingDat.Execute(fulldir, filename, DatTimeStamp);
            if (datId == null)
                return 0;
            
            SetDatFound.Execute((uint)datId);
            return (uint)datId;

        }
    }
}
