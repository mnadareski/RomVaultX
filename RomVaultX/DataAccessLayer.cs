using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using RomVaultX.IO;
using RomVaultX.Util;
using Convert = System.Convert;

namespace RomVaultX
{
    public static class DataAccessLayer
    {
        private static readonly SQLiteConnection _connection;

        private static readonly SQLiteCommand _insertIntoDir;

        private static readonly SQLiteCommand _insertIntoDat;

        private static readonly SQLiteCommand _insertIntoGame;

        private static readonly SQLiteCommand _insertIntoRom;

        private static readonly SQLiteCommand _insertIntoCHD;

        private static readonly SQLiteCommand _readTree;

        static DataAccessLayer()
        {
            bool datFound = File.Exists("rom.db");

            _connection = new SQLiteConnection("data source=rom.db;Version=3");
            _connection.Open();

            if (!datFound)
                MakeDB();

            _insertIntoDir = new SQLiteCommand(
                @"INSERT INTO DIR (ParentDirId,Name,FullName)
                VALUES (@ParentDirId,@Name,@FullName);

                SELECT last_insert_rowid();", _connection);

            _insertIntoDir.Parameters.Add(new SQLiteParameter("ParentDirId"));
            _insertIntoDir.Parameters.Add(new SQLiteParameter("Name"));
            _insertIntoDir.Parameters.Add(new SQLiteParameter("FullName"));


            _insertIntoDat = new SQLiteCommand(
                @"INSERT INTO DAT (DirId,Filename,name,rootdir,description,category,version,date,author,email,homepage,url,comment)
                VALUES (@DirId,@Filename,@name,@rootdir,@description,@category,@version,@date,@author,@email,@homepage,@url,@comment);

                SELECT last_insert_rowid();", _connection);

            _insertIntoDat.Parameters.Add(new SQLiteParameter("DirId"));
            _insertIntoDat.Parameters.Add(new SQLiteParameter("Filename"));
            _insertIntoDat.Parameters.Add(new SQLiteParameter("name"));
            _insertIntoDat.Parameters.Add(new SQLiteParameter("rootdir"));
            _insertIntoDat.Parameters.Add(new SQLiteParameter("description"));
            _insertIntoDat.Parameters.Add(new SQLiteParameter("category"));
            _insertIntoDat.Parameters.Add(new SQLiteParameter("version"));
            _insertIntoDat.Parameters.Add(new SQLiteParameter("date"));
            _insertIntoDat.Parameters.Add(new SQLiteParameter("author"));
            _insertIntoDat.Parameters.Add(new SQLiteParameter("email"));
            _insertIntoDat.Parameters.Add(new SQLiteParameter("homepage"));
            _insertIntoDat.Parameters.Add(new SQLiteParameter("url"));
            _insertIntoDat.Parameters.Add(new SQLiteParameter("comment"));

            _insertIntoGame = new SQLiteCommand(
                @"INSERT INTO GAME (DatId,name,romof,description,sourcefile)
                VALUES (@DatId,@name,@romof,@description,@sourcefile);

                SELECT last_insert_rowid();", _connection);

            _insertIntoGame.Parameters.Add(new SQLiteParameter("DatId"));
            _insertIntoGame.Parameters.Add(new SQLiteParameter("name"));
            _insertIntoGame.Parameters.Add(new SQLiteParameter("romof"));
            _insertIntoGame.Parameters.Add(new SQLiteParameter("description"));
            _insertIntoGame.Parameters.Add(new SQLiteParameter("sourcefile"));

            _insertIntoRom = new SQLiteCommand(
                @"INSERT INTO ROM (GameId,name,size,crc,sha1,md5)
                VALUES (@GameId,@name,@size,@crc,@sha1,@md5);", _connection);

            _insertIntoRom.Parameters.Add(new SQLiteParameter("GameId"));
            _insertIntoRom.Parameters.Add(new SQLiteParameter("name"));
            _insertIntoRom.Parameters.Add(new SQLiteParameter("size"));
            _insertIntoRom.Parameters.Add(new SQLiteParameter("crc"));
            _insertIntoRom.Parameters.Add(new SQLiteParameter("sha1"));
            _insertIntoRom.Parameters.Add(new SQLiteParameter("md5"));


            _readTree = new SQLiteCommand(
                @"
                    SELECT 
                        dir.DirId as DirId,
                        dir.name as dirname,
                        dir.fullname,
                        dat.DatId,
                        dat.name as datname
                    FROM dir LEFT JOIN dat ON dir.DirId=dat.dirid
                    ORDER BY fullname,filename", _connection);
        }

        public static void close()
        {
            _insertIntoDat.Dispose();
            _connection.Close();
            _connection.Dispose();
        }

        public static void ExecuteNonQuery(string query, params object[] args)
        {
            using (SQLiteCommand command = new SQLiteCommand(query, _connection))
            {
                for (int i = 0; i < args.Length; i += 2)
                    command.Parameters.Add(new SQLiteParameter(args[i].ToString(), args[i + 1]));

                command.ExecuteNonQuery();
            }
        }

        public static void MakeDB()
        {
            ExecuteNonQuery(@"
                                
                CREATE TABLE IF NOT EXISTS [DIR] (
                    [DirId] INTEGER PRIMARY KEY NOT NULL,
                    [ParentDirId] INTEGER NULL,
                    [name] NVARCHAR(300) NOT NULL,
                    [fullname] NVARCHAR(300) NOT NULL,
                    [expanded] BOOLEAN DEFAULT '1' NOT NULL
                );

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
                    [comment] NVARCHAR(10) NULL
                );

                CREATE TABLE IF NOT EXISTS [GAME] (
                    [GameId] INTEGER  PRIMARY KEY NOT NULL,
                    [DatId] INTEGER NOT NULL,
                    [name] NVARCHAR(200) NOT NULL,
                    [romof] NVARCHAR(20) NULL,
                    [description] NVARCHAR(220) NULL,
                    [sourcefile] NVARCHAR(20) NULL
                );

                CREATE TABLE IF NOT EXISTS [ROM] (
                    [RomId] INTEGER PRIMARY KEY NOT NULL,
                    [GameId] INTEGER NOT NULL,
                    [name] NVARCHAR(320) NOT NULL,
                    [size] INTEGER NULL,
                    [crc] VARCHAR(8) NULL,
                    [sha1] VARCHAR(40) NULL,
                    [md5] VARCHAR(32) NULL
                );

                CREATE TABLE IF NOT EXISTS [FILES] (
                    [FileId] INTEGER PRIMARY KEY NOT NULL,
                    [size] INTEGER NOT NULL,
                    [compressedSize] INTEGER NOT NULL,
                    [crc] VARCHAR(8) NULL,
                    [sha1] VARCHAR(40) NULL,
                    [md5] VARCHAR(32) NULL
                );

        ");

        }



        public static void DeleteAll()
        {
            ExecuteNonQuery(@"
                DELETE FROM [ROM];
                DELETE FROM [GAME];
                DELETE FROM [DAT];
                DELETE FROM [DIR];
                DELETE FROM [FILES];
            ");
        }

        public static void DropIndex()
        {
            ExecuteNonQuery(@"
                DROP INDEX IF EXISTS [SHA1Index];
            ");

        }

        public static void MakeIndex()
        {
            ExecuteNonQuery(@"
                CREATE INDEX IF NOT EXISTS [SHA1Index] ON [ROM](
                [sha1]  ASC,
                [crc] ASC,
                [md5] ASC,
                [size] ASC
                )
            ");

        }

        public static int InsertIntoDir(
            int ParentDirId,
            string Name,
            string FullName)
        {
            _insertIntoDir.Parameters["ParentDirId"].Value = ParentDirId;
            _insertIntoDir.Parameters["Name"].Value = Name;
            _insertIntoDir.Parameters["FullName"].Value = FullName;

            object res = _insertIntoDir.ExecuteScalar();

            if (res == null || res == DBNull.Value)
                return 0;
            return Convert.ToInt32(res);
        }

        public static int InsertIntoDat(
            int DirId,
            string Filename,
            string name,
            string rootdir,
            string description,
            string category,
            string version,
            string date,
            string author,
            string email,
            string homepage,
            string url,
            string comment)
        {
            _insertIntoDat.Parameters["DirId"].Value = DirId;
            _insertIntoDat.Parameters["Filename"].Value = Filename;
            _insertIntoDat.Parameters["name"].Value = name;
            _insertIntoDat.Parameters["rootdir"].Value = rootdir;
            _insertIntoDat.Parameters["description"].Value = description;
            _insertIntoDat.Parameters["category"].Value = category;
            _insertIntoDat.Parameters["version"].Value = version;
            _insertIntoDat.Parameters["date"].Value = date;
            _insertIntoDat.Parameters["author"].Value = author;
            _insertIntoDat.Parameters["email"].Value = email;
            _insertIntoDat.Parameters["homepage"].Value = homepage;
            _insertIntoDat.Parameters["url"].Value = url;
            _insertIntoDat.Parameters["comment"].Value = comment;
            object res = _insertIntoDat.ExecuteScalar();

            if (res == null || res == DBNull.Value)
                return 0;
            return Convert.ToInt32(res);

        }

        public static int InsertIntoGame(
            int DatId,
            string name,
            string romof,
            string description,
            string sourcefile
            )
        {
            _insertIntoGame.Parameters["DatId"].Value = DatId;
            _insertIntoGame.Parameters["name"].Value = name;
            _insertIntoGame.Parameters["romof"].Value = romof;
            _insertIntoGame.Parameters["description"].Value = description;
            _insertIntoGame.Parameters["sourcefile"].Value = sourcefile;

            object res = _insertIntoGame.ExecuteScalar();

            if (res == null || res == DBNull.Value)
                return 0;
            return Convert.ToInt32(res);
        }

        public static void InsertIntoRom(
            int GameId,
            string name,
            ulong? size,
            byte[] crc,
            byte[] sha1,
            byte[] md5)
        {
            _insertIntoRom.Parameters["GameId"].Value = GameId;
            _insertIntoRom.Parameters["name"].Value = name;
            _insertIntoRom.Parameters["size"].Value = size;
            _insertIntoRom.Parameters["crc"].Value = VarFix.ToString(crc);
            _insertIntoRom.Parameters["sha1"].Value = VarFix.ToString(sha1);
            _insertIntoRom.Parameters["md5"].Value = VarFix.ToString(md5);
            _insertIntoRom.ExecuteNonQuery();
        }

        public static List<RvTreeRow> ReadTreeFromDB()
        {
            List<RvTreeRow> rows = new List<RvTreeRow>();

            using (SQLiteDataReader dr = _readTree.ExecuteReader())
            {
                int iDirId = dr.GetOrdinal("DirId");
                int iDirName = dr.GetOrdinal("dirname");
                int iFullName = dr.GetOrdinal("fullname");
                int iDatId = dr.GetOrdinal("DatId");
                int iDatName = dr.GetOrdinal("datname");

                bool multiDatDirFound = false;

                RvTreeRow lastTree = null;
                while (dr.Read())
                {
                    // a single DAT is a directory is just displayed in the tree at the same level as the directory
                    RvTreeRow pTree = new RvTreeRow
                    {
                        DirId = dr.GetInt32(iDirId),
                        dirName = dr.GetString(iDirName),
                        dirFullName = dr.GetString(iFullName),
                        DatId = dr.IsDBNull(iDatId) ? null : (int?)dr.GetInt32(iDatId),
                        datName = dr.IsDBNull(iDatName) ? null : dr.GetString(iDatName)
                    };
                    rows.Add(pTree);

                    if (lastTree != null)
                    {
                        // if multiple DAT's are in the same directory then we should add another level in the tree to display the directory
                        bool thisMultiDatDirFound = (lastTree.DirId == pTree.DirId);
                        if (thisMultiDatDirFound && !multiDatDirFound)
                        {
                            // found a new multidat
                            RvTreeRow dirTree = new RvTreeRow
                            {
                                DirId = lastTree.DirId,
                                dirName = lastTree.dirName,
                                dirFullName = lastTree.dirFullName,
                                DatId = null,
                                datName = null
                            };
                            rows.Insert(rows.Count - 2, dirTree);
                            lastTree.MultiDatDir = true;
                        }
                        if (thisMultiDatDirFound)
                            pTree.MultiDatDir = true;

                        multiDatDirFound = thisMultiDatDirFound;
                    }


                    lastTree = pTree;
                }
            }

            return rows;
        }


    }
}
