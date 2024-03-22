using System;
using System.Collections.Generic;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using RomVaultX.Util;

namespace RomVaultX.DB
{
    public class RvDat
    {
        public static SqliteCommand CommandRead
        {
            get
            {
                if (_commandRead == null)
                {
                    _commandRead = new SqliteCommand(@"
                        SELECT *
                        FROM DAT
                        WHERE
                            DatId = @DatId
                        ORDER BY Filename",
                    Program.db.Connection);

                    _commandRead.Parameters.Add(new SqliteParameter("DatId", SqliteType.Integer));
                }

                return _commandRead;
            }
        }
        private static SqliteCommand? _commandRead;

        public static SqliteCommand CommandWrite
        {
            get
            {
                if (_commandWrite == null)
                {
                    _commandWrite = new SqliteCommand(@"
                        INSERT INTO DAT
                        (
                            DirId,
                            Filename,
                            name,
                            rootdir,
                            description,
                            category,
                            version,
                            date,
                            author,
                            email,
                            homepage,
                            url,
                            comment,
                            mergetype,
                            Path,
                            DatTimeStamp,
                            ExtraDir
                        )
                        VALUES
                        (
                            @DirId,
                            @Filename,
                            @name,
                            @rootdir,
                            @description,
                            @category,
                            @version,
                            @date,
                            @author,
                            @email,
                            @homepage,
                            @url,
                            @comment,
                            @mergetype,
                            @Path,
                            @DatTimeStamp,
                            @ExtraDir
                        );

                        SELECT last_insert_rowid();",
                    Program.db.Connection);

                    _commandWrite.Parameters.Add(new SqliteParameter("DirId", SqliteType.Integer));
                    _commandWrite.Parameters.Add(new SqliteParameter("Filename", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("name", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("rootdir", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("description", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("category", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("version", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("date", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("author", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("email", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("homepage", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("url", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("comment", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("mergetype", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("Path", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("DatTimeStamp", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("ExtraDir", SqliteType.Integer));
                }

                return _commandWrite;
            }
        }
        private static SqliteCommand? _commandWrite;

        public uint DatId;
        public uint DirId;
        public string? Filename;
        public string? Name;
        public string? RootDir;
        public string? Description;
        public string? Category;
        public string? Version;
        public string? Date;
        public string? Author;
        public string? Email;
        public string? Homepage;
        public string? URL;
        public string? Comment;
        public string? Path;
        public long DatTimeStamp;
        public bool ExtraDir;
        public string? MergeType;

        public List<RvGame>? Games;

        public static void CreateTable()
        {
            Program.db.ExecuteNonQuery(@"
                 CREATE TABLE IF NOT EXISTS [DAT] (
                    [DatId] INTEGER  PRIMARY KEY NOT NULL,
                    [DirId] INTEGER  NOT NULL,
                    [Filename] NVARCHAR(300)  NULL,

                    [name] NVARCHAR(100)  NULL,
                    [rootdir] NVARCHAR(10)  NULL,
                    [description] NVARCHAR(10)  NULL,
                    [category] NVARCHAR(10)  NULL,
                    [version] NVARCHAR(10)  NULL,
                    [date] NVARCHAR(10)  NULL,
                    [author] NVARCHAR(10)  NULL,
                    [email] NVARCHAR(10)  NULL,
                    [homepage] NVARCHAR(10)  NULL,
                    [url] NVARCHAR(10)  NULL,
                    [comment] NVARCHAR(10) NULL,
                    [mergetype] NVARCHAR(10) NULL,
                    [RomTotal] INTEGER DEFAULT 0 NOT NULL,
                    [RomGot] INTEGER DEFAULT 0 NOT NULL,
                    [RomNoDump] INTEGER DEFAULT 0 NOT NULL,
                    [Path] NVARCHAR(10)  NOT NULL,
                    [DatTimeStamp] NVARCHAR(20)  NOT NULL,
                    [ExtraDir] BOOLEAN DEFAULT 0,
                    [found] BOOLEAN DEFAULT 1,            
                    FOREIGN KEY(DirId) REFERENCES DIR(DirId)
                );");
        }

        public void DbRead(uint datId, bool readGames = false)
        {
            CommandRead.Parameters["DatId"].Value = datId;

            using (DbDataReader dr = CommandRead.ExecuteReader())
            {
                if (dr.Read())
                {
                    DatId = datId;
                    DirId = Convert.ToUInt32(dr["DirId"]);
                    Filename = dr["Filename"].ToString();
                    Name = dr["name"].ToString();
                    RootDir = dr["rootdir"].ToString();
                    Description = dr["description"].ToString();
                    Category = dr["category"].ToString();
                    Version = dr["version"].ToString();
                    Date = dr["date"].ToString();
                    Author = dr["author"].ToString();
                    Email = dr["email"].ToString();
                    Homepage = dr["homepage"].ToString();
                    URL = dr["url"].ToString();
                    Comment = dr["comment"].ToString();
                    MergeType = dr["mergetype"].ToString();
                }
                dr.Close();
            }

            if (readGames)
                Games = RvGame.ReadGames(DatId, true);
        }

        public void DbWrite()
        {
            CommandWrite.Parameters["DirId"].Value = DirId;
            CommandWrite.Parameters["Filename"].Value = Filename;
            CommandWrite.Parameters["name"].Value = Name;
            CommandWrite.Parameters["rootdir"].Value = RootDir;
            CommandWrite.Parameters["description"].Value = Description;
            CommandWrite.Parameters["category"].Value = Category;
            CommandWrite.Parameters["version"].Value = Version;
            CommandWrite.Parameters["date"].Value = Date;
            CommandWrite.Parameters["author"].Value = Author;
            CommandWrite.Parameters["email"].Value = Email;
            CommandWrite.Parameters["homepage"].Value = Homepage;
            CommandWrite.Parameters["url"].Value = URL;
            CommandWrite.Parameters["comment"].Value = Comment;
            CommandWrite.Parameters["mergetype"].Value = MergeType;
            CommandWrite.Parameters["Path"].Value = Path;
            CommandWrite.Parameters["DatTimeStamp"].Value = DatTimeStamp.ToString();
            CommandWrite.Parameters["ExtraDir"].Value = ExtraDir;

            var res = CommandWrite.ExecuteScalar();

            if (res == null || res == DBNull.Value)
                return;

            DatId = Convert.ToUInt32(res);

            if (Games == null)
                return;

            foreach (RvGame rvGame in Games)
            {
                rvGame.DatId = DatId;
                rvGame.DBWrite();
            }
        }

        public void AddGame(RvGame rvGame)
        {
            Games ??= [];

            ChildNameSearch(rvGame.Name, out int index);
            Games.Insert(index, rvGame);
        }

        public string GetExtraDirName()
        {
            if (!string.IsNullOrWhiteSpace(Description))
                return Description!;

            if (!string.IsNullOrWhiteSpace(Name))
                return Name!;

            return "-unknown-";
        }

        public int ChildNameSearch(string lGameName, out int index)
        {
            index = -1;
            if (Games == null)
                return -1;

            int intBottom = 0;
            int intTop = Games.Count;
            int intMid = 0;
            int intRes = -1;

            //Binary chop to find the closest match
            while ((intBottom < intTop) && (intRes != 0))
            {
                intMid = (intBottom + intTop) / 2;

                intRes = VarFix.CompareName(lGameName, Games[intMid].Name);
                if (intRes < 0)
                    intTop = intMid;
                else if (intRes > 0)
                    intBottom = intMid + 1;
            }
            index = intMid;

            // if match was found check up the list for the first match
            if (intRes == 0)
            {
                int intRes1 = 0;
                while ((index > 0) && (intRes1 == 0))
                {
                    intRes1 = VarFix.CompareName(lGameName, Games[index - 1].Name);
                    if (intRes1 == 0)
                        index--;
                }
            }

            // if the search is greater than the closest match move one up the list
            else if (intRes > 0)
            {
                index++;
            }

            return intRes;
        }
    }
}