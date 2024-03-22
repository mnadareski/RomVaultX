using System;
using Microsoft.Data.Sqlite;

namespace RomVaultX.DB
{
    public class RvDir
    {
        public static SqliteCommand CommandFindInDir
        {
            get
            {
                if (_commandFindInDir == null)
                {
                    _commandFindInDir = new SqliteCommand(@"
                        SELECT DirId
                        FROM DIR
                        WHERE
                            fullname = @fullname
                        LIMIT 1",
                    Program.db.Connection);

                    _commandFindInDir.Parameters.Add(new SqliteParameter("fullname", SqliteType.Text));
                }

                return _commandFindInDir;
            }
        }
        private static SqliteCommand? _commandFindInDir;

        public static SqliteCommand CommandSetDirFound
        {
            get
            {
                if (_commandSetDirFound == null)
                {
                    _commandSetDirFound = new SqliteCommand(@"
                        UPDATE DIR
                        SET
                            found = 1
                        WHERE
                            DirId = @DirId",
                    Program.db.Connection);

                    _commandSetDirFound.Parameters.Add(new SqliteParameter("DirId", SqliteType.Integer));
                }

                return _commandSetDirFound;
            }
        }
        private static SqliteCommand? _commandSetDirFound;

        public static SqliteCommand CommandInsertIntoDir
        {
            get
            {
                if (_commandInsertIntoDir == null)
                {
                    _commandInsertIntoDir = new SqliteCommand(@"
                        INSERT INTO DIR
                        (
                            ParentDirId,
                            name,
                            fullname,
                            CreationTime,
                            LastAccessTime,
                            LastWriteTime
                        )
                        VALUES
                        (
                            @ParentDirId,
                            @name,
                            @fullname,
                            @TimeStamp,
                            @TimeStamp,
                            @TimeStamp
                        );

                        SELECT last_insert_rowid();",
                    Program.db.Connection);

                    _commandInsertIntoDir.Parameters.Add(new SqliteParameter("ParentDirId", SqliteType.Integer));
                    _commandInsertIntoDir.Parameters.Add(new SqliteParameter("name", SqliteType.Text));
                    _commandInsertIntoDir.Parameters.Add(new SqliteParameter("fullname", SqliteType.Text));
                    _commandInsertIntoDir.Parameters.Add(new SqliteParameter("TimeStamp", SqliteType.Integer));
                }

                return _commandInsertIntoDir;
            }
        }
        private static SqliteCommand? _commandInsertIntoDir;

        public static void CreateTable()
        {
            Program.db.ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS [DIR] (
                    [DirId] INTEGER PRIMARY KEY NOT NULL,
                    [ParentDirId] INTEGER NULL,
                    [name] NVARCHAR(300) NOT NULL,
                    [fullname] NVARCHAR(300) NOT NULL,
                    [expanded] BOOLEAN DEFAULT 1 NOT NULL,
                    [found] BOOLEAN DEFAULT 1,
                    [CreationTime] INTEGER,
                    [LastAccessTime] INTEGER,
                    [LastWriteTime] INTEGER,
                    [RomTotal] INTEGER NULL,
                    [RomGot] INTEGER NULL,
                    [RomNoDump] INTEGER NULL
                );");
        }

        public static uint FindOrInsertIntoDir(uint parentDirId, string name, string fullName)
        {
            uint? foundDatId = FindInDir(fullName);
            if (foundDatId == null)
                return InsertIntoDir(parentDirId, name, fullName);

            SetDirFound((uint)foundDatId);
            return (uint)foundDatId;
        }

        private static uint? FindInDir(string fullname)
        {
            CommandFindInDir.Parameters["fullname"].Value = fullname ?? string.Empty;

            var resFind = CommandFindInDir.ExecuteScalar();

            if (resFind == null || resFind == DBNull.Value)
                return null;

            return (uint?)Convert.ToInt32(resFind);
        }

        private static void SetDirFound(uint foundDatId)
        {
            CommandSetDirFound.Parameters["DirId"].Value = foundDatId;

            CommandSetDirFound.ExecuteNonQuery();
        }

        private static uint InsertIntoDir(uint parentDirId, string name, string fullName)
        {
            CommandInsertIntoDir.Parameters["ParentDirId"].Value = parentDirId;
            CommandInsertIntoDir.Parameters["name"].Value = name ?? string.Empty;
            CommandInsertIntoDir.Parameters["fullname"].Value = fullName ?? string.Empty;
            CommandInsertIntoDir.Parameters["TimeStamp"].Value = DateTime.UtcNow.Ticks;

            var res = CommandInsertIntoDir.ExecuteScalar();

            if (res == null || res == DBNull.Value)
                return 0;

            return Convert.ToUInt32(res);
        }
    }
}