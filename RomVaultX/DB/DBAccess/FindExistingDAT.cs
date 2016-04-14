using System;
using System.Data.Common;

namespace RomVaultX.DB.DBAccess
{
    public static class FindDAT
    {

        private static class FindExistingDat
        {
            private static readonly DbCommand Command;

            static FindExistingDat()
            {
                Command = Program.db.Command(
           @"SELECT DatId FROM Dat,Dir WHERE Dat.DirId=Dir.DirId AND fullname=@fullname AND Filename=@filename AND DatTimeStamp=@DatTimeStamp");
                Command.Parameters.Add(Program.db.Parameter("fullname"));
                Command.Parameters.Add(Program.db.Parameter("filename"));
                Command.Parameters.Add(Program.db.Parameter("DatTimeStamp"));
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
            private static readonly DbCommand Command;

            static SetDatFound()
            {
                Command = Program.db.Command(
                     @"Update Dat SET Found=1 WHERE DatId=@DatId");
                Command.Parameters.Add(Program.db.Parameter("DatId"));

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
