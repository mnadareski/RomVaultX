using System;
using System.Data.Common;

namespace RomVaultX.DB.DBAccess
{
    public static class FindOrInsert
    {

        private static class FindInDir
        {
            private static readonly DbCommand Command;

            static FindInDir()
            {
                Command = Program.db.Command(@"SELECT DirId FROM dir WHERE fullname=@fullname LIMIT 1");
                Command.Parameters.Add(Program.db.Parameter("fullname"));
            }

            public static uint? Execute(string fullname)
            {
                Command.Parameters["FullName"].Value = fullname;
                object resFind = Command.ExecuteScalar();
                if (resFind == null || resFind == DBNull.Value)
                    return null;

                return (uint?)Convert.ToInt32(resFind);
            }
        }

        private static class SetDirFound
        {
            private static readonly DbCommand Command;

            static SetDirFound()
            {
                Command = Program.db.Command(
                    @"Update Dir SET Found=1 WHERE DirId=@DirId");
                Command.Parameters.Add(Program.db.Parameter("DirId"));
            }

            public static void Execute(uint foundDatId)
            {
                Command.Parameters["DirId"].Value = foundDatId;
                Command.ExecuteNonQuery();
        
            }

        }

        private static class InsertIntoDir
        {
            private static readonly DbCommand Command;

            static InsertIntoDir()
            {
                Command = Program.db.Command(
                    @"INSERT INTO DIR (ParentDirId,Name,FullName)
                         VALUES (@ParentDirId,@Name,@FullName);

                         SELECT last_insert_rowid();");

                Command.Parameters.Add(Program.db.Parameter("ParentDirId"));
                Command.Parameters.Add(Program.db.Parameter("Name"));
                Command.Parameters.Add(Program.db.Parameter("FullName"));
            }

            public static uint Execute(uint parentDirId, string name, string fullName)
            {
                Command.Parameters["ParentDirId"].Value = parentDirId;
                Command.Parameters["Name"].Value = name;
                Command.Parameters["FullName"].Value = fullName;

                object res = Command.ExecuteScalar();

                if (res == null || res == DBNull.Value)
                    return 0;
                return Convert.ToUInt32(res);
                
            }
        }

        public static uint FindOrInsertIntoDir(uint parentDirId, string name, string fullName)
        {

            uint? foundDatId = FindInDir.Execute(fullName);
            if (foundDatId != null)
            {
                SetDirFound.Execute((uint)foundDatId);
                return (uint)foundDatId;
            }

            return InsertIntoDir.Execute(parentDirId, name, fullName);
        }
    }
}
