using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace RomVaultX.DB
{
    public class RvDat
    {
        public uint DatId;
        public uint DirId;
        public string Filename;
        public string Name;
        public string RootDir;
        public string Description;
        public string Category;
        public string Version;
        public string Date;
        public string Author;
        public string Email;
        public string Homepage;
        public string URL;
        public string Comment;
        public long DatTimeStamp;

        public List<RvGame> Games = null; 

        private static readonly SQLiteCommand SqlWrite;
        private static readonly SQLiteCommand SqlRead;

        static RvDat()
        {
            SqlWrite = new SQLiteCommand(
               @"INSERT INTO DAT ( DirId, Filename, name, rootdir, description, category, version, date, author, email, homepage, url, comment,DatTimeStamp)
                VALUES            (@DirId,@Filename,@name,@rootdir,@description,@category,@version,@date,@author,@email,@homepage,@url,@comment,@DatTimeStamp);

                SELECT last_insert_rowid();", DataAccessLayer.DBConnection);

            SqlWrite.Parameters.Add(new SQLiteParameter("DirId"));
            SqlWrite.Parameters.Add(new SQLiteParameter("Filename"));
            SqlWrite.Parameters.Add(new SQLiteParameter("name"));
            SqlWrite.Parameters.Add(new SQLiteParameter("rootdir"));
            SqlWrite.Parameters.Add(new SQLiteParameter("description"));
            SqlWrite.Parameters.Add(new SQLiteParameter("category"));
            SqlWrite.Parameters.Add(new SQLiteParameter("version"));
            SqlWrite.Parameters.Add(new SQLiteParameter("date"));
            SqlWrite.Parameters.Add(new SQLiteParameter("author"));
            SqlWrite.Parameters.Add(new SQLiteParameter("email"));
            SqlWrite.Parameters.Add(new SQLiteParameter("homepage"));
            SqlWrite.Parameters.Add(new SQLiteParameter("url"));
            SqlWrite.Parameters.Add(new SQLiteParameter("comment"));
            SqlWrite.Parameters.Add(new SQLiteParameter("DatTimeStamp"));


            SqlRead = new SQLiteCommand(
             @"SELECT DirId,Filename,name,rootdir,description,category,version,date,author,email,homepage,url,comment 
                FROM DAT WHERE DatId=@datId", DataAccessLayer.DBConnection);
            SqlRead.Parameters.Add(new SQLiteParameter("datId"));

        }
      
        public static void MakeDB()
        {

            DataAccessLayer.ExecuteNonQuery(@"
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
                    [RomTotal] INTEGER DEFAULT '0' NOT NULL,
                    [RomGot] INTEGER DEFAULT '0' NOT NULL,
                    [DatTimeStamp] NVARCHAR(20)  NOT NULL,
                    [found] BOOLEAN DEFAULT '1',            
                    FOREIGN KEY(DirId) REFERENCES DIR(DirId)
                );");
        }

        public void DBRead(uint datId,bool readGames=false)
        {
            SqlRead.Parameters["DatID"].Value = datId;

            using (SQLiteDataReader dr = SqlRead.ExecuteReader())
            {
                if (dr.Read())
                {
                    DatId = datId;
                    DirId = Convert.ToUInt32(dr["DirId"]);
                    Filename = dr["filename"].ToString();
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
                }
                dr.Close();
            }

            if (readGames)
                Games = RvGame.ReadGames(DatId,true);
        }

        public void DbWrite()
        {
            SqlWrite.Parameters["DirId"].Value = DirId;
            SqlWrite.Parameters["Filename"].Value = Filename;
            SqlWrite.Parameters["name"].Value = Name;
            SqlWrite.Parameters["rootdir"].Value = RootDir;
            SqlWrite.Parameters["description"].Value = Description;
            SqlWrite.Parameters["category"].Value = Category;
            SqlWrite.Parameters["version"].Value = Version;
            SqlWrite.Parameters["date"].Value = Date;
            SqlWrite.Parameters["author"].Value = Author;
            SqlWrite.Parameters["email"].Value = Email;
            SqlWrite.Parameters["homepage"].Value = Homepage;
            SqlWrite.Parameters["url"].Value = URL;
            SqlWrite.Parameters["comment"].Value = Comment;
            SqlWrite.Parameters["DatTimeStamp"].Value = DatTimeStamp.ToString();
            object res = SqlWrite.ExecuteScalar();

            if (res == null || res == DBNull.Value)
                return;
            DatId = Convert.ToUInt32(res);

            if (Games==null)
                return;

            foreach (RvGame rvGame in Games)
            {
                rvGame.DatId = DatId;
                rvGame.DBWrite();
            }
        }

        public void AddGame(RvGame rvGame)
        {
            if (Games==null)
                Games=new List<RvGame>();

            Games.Add(rvGame);
        }
    }
}
