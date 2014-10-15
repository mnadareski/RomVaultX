using System;
using System.Data.SQLite;

namespace RomVaultX.DB.DBAccess
{
    public static class FindOrInsert
    {

        private static class FindInDir
        {
            private static readonly SQLiteCommand Command;

            static FindInDir()
            {
                Command = new SQLiteCommand(@"SELECT DirId FROM dir WHERE fullname=@fullname LIMIT 1", DataAccessLayer.DBConnection);
                Command.Parameters.Add(new SQLiteParameter("fullname"));
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
            private static readonly SQLiteCommand Command;

            static SetDirFound()
            {
                Command = new SQLiteCommand(
                    @"Update Dir SET Found=1 WHERE DirId=@DirId", DataAccessLayer.DBConnection);
                Command.Parameters.Add(new SQLiteParameter("DirId"));
            }

            public static void Execute(uint foundDatId)
            {
                Command.Parameters["DirId"].Value = foundDatId;
                Command.ExecuteNonQuery();
        
            }

        }

        private static class InsertIntoDir
        {
            private static readonly SQLiteCommand Command;

            static InsertIntoDir()
            {
                Command = new SQLiteCommand(
                    @"INSERT INTO DIR (ParentDirId,Name,FullName)
                         VALUES (@ParentDirId,@Name,@FullName);

                         SELECT last_insert_rowid();", DataAccessLayer.DBConnection);

                Command.Parameters.Add(new SQLiteParameter("ParentDirId"));
                Command.Parameters.Add(new SQLiteParameter("Name"));
                Command.Parameters.Add(new SQLiteParameter("FullName"));
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
