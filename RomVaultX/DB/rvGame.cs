using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace RomVaultX.DB
{
    public class RvGame
    {
        public uint GameId;
        public uint DatId;
        public string Name;
        public string Description;

        public string Manufacturer;
        public string CloneOf;
        public string RomOf;
        public string SampleOf;
        public string SourceFile;
        public string IsBios;
        public string Board;
        public string Year;

        public bool IsTrurip;
        public string Publisher;
        public string Developer;
        public string Edition;
        public string Version;
        public string Type;
        public string Media;
        public string Language;
        public string Players;
        public string Ratings;
        public string Genre;
        public string Peripheral;
        public string BarCode;
        public string MediaCatalogNumber;

        public List<RvRom> Roms = null;

        private static readonly SQLiteCommand SqlWrite;
        private static readonly SQLiteCommand SqlRead;
        private static readonly SQLiteCommand SqlReadGames;

        static RvGame()
        {
            SqlWrite = new SQLiteCommand(
              @"INSERT INTO GAME ( DatId, name, description, manufacturer, cloneof, romof, sourcefile, isbios, board, year, istrurip, publisher, developer, edition, version, type, media, language, players, ratings, genre, peripheral, barcode, mediacatalognumber)
                          VALUES (@DatId,@Name,@Description,@Manufacturer,@CloneOf,@RomOf,@SourceFile,@IsBios,@Board,@Year,@IsTrurip,@Publisher,@Developer,@Edition,@Version,@Type,@Media,@Language,@Players,@Ratings,@Genre,@Peripheral,@BarCode,@MediaCatalogNumber);

                SELECT last_insert_rowid();");

            SqlWrite.Parameters.Add(new SQLiteParameter("DatId")); //DatId;
            SqlWrite.Parameters.Add(new SQLiteParameter("Name")); //Name;
            SqlWrite.Parameters.Add(new SQLiteParameter("Description")); //Description;
            SqlWrite.Parameters.Add(new SQLiteParameter("Manufacturer")); //Manufacturer;

            SqlWrite.Parameters.Add(new SQLiteParameter("CloneOf")); //CloneOf;
            SqlWrite.Parameters.Add(new SQLiteParameter("RomOf")); //RomOf;
            SqlWrite.Parameters.Add(new SQLiteParameter("SampleOf")); //SampleOf;
            SqlWrite.Parameters.Add(new SQLiteParameter("Sourcefile")); //SourceFile;
            SqlWrite.Parameters.Add(new SQLiteParameter("IsBios")); //IsBios;
            SqlWrite.Parameters.Add(new SQLiteParameter("Board")); //Board;
            SqlWrite.Parameters.Add(new SQLiteParameter("Year")); //Year;

            SqlWrite.Parameters.Add(new SQLiteParameter("IsTrurip")); //IsTrurip;
            SqlWrite.Parameters.Add(new SQLiteParameter("Publisher")); //Publisher;
            SqlWrite.Parameters.Add(new SQLiteParameter("Developer")); //Developer;
            SqlWrite.Parameters.Add(new SQLiteParameter("Edition")); //Edition;
            SqlWrite.Parameters.Add(new SQLiteParameter("Version")); //Version;
            SqlWrite.Parameters.Add(new SQLiteParameter("Type")); //Type;
            SqlWrite.Parameters.Add(new SQLiteParameter("Media")); //Media;
            SqlWrite.Parameters.Add(new SQLiteParameter("Language")); //Language;
            SqlWrite.Parameters.Add(new SQLiteParameter("Players")); //Players;
            SqlWrite.Parameters.Add(new SQLiteParameter("Ratings")); //Ratings;
            SqlWrite.Parameters.Add(new SQLiteParameter("Genre")); //Genre;
            SqlWrite.Parameters.Add(new SQLiteParameter("Peripheral")); //Peripheral;
            SqlWrite.Parameters.Add(new SQLiteParameter("BarCode")); //BarCode;
            SqlWrite.Parameters.Add(new SQLiteParameter("MediaCatalogNumber")); //MediaCatalogNumber;        


            SqlRead = new SQLiteCommand(
                @"SELECT GameId, DatId, name, description, manufacturer, cloneof, romof, sourcefile, isbios, board, year, istrurip, publisher, developer, edition, version, type, media, language, players, ratings, genre, peripheral, barcode, mediacatalognumber
                    FROM GAME WHERE GameId=@GameId");
            SqlRead.Parameters.Add(new SQLiteParameter("GameId"));

            SqlReadGames = new SQLiteCommand(
                @"SELECT GameId, DatId, name, description, manufacturer, cloneof, romof, sourcefile, isbios, board, year, istrurip, publisher, developer, edition, version, type, media, language, players, ratings, genre, peripheral, barcode, mediacatalognumber
                    FROM GAME WHERE DatId=@DatId");
            SqlReadGames.Parameters.Add(new SQLiteParameter("DatId"));

        }

        public static void SetConnection(SQLiteConnection connection)
        {
            SqlWrite.Connection = connection;
            SqlRead.Connection = connection;
        }

        public static void MakeDB()
        {
            DataAccessLayer.ExecuteNonQuery(@"
                 CREATE TABLE IF NOT EXISTS [GAME] (
                    [GameId] INTEGER  PRIMARY KEY NOT NULL,
                    [DatId] INTEGER NOT NULL,
                    [name] NVARCHAR(200) NOT NULL,
                    [description] NVARCHAR(220) NULL,
                    [manufacturer] NVARCHAR(20) NULL,
                    [cloneof] NVARCHAR(20) NULL,
                    [romof] NVARCHAR(20) NULL,
                    [sampleof] NVARCHAR(20) NULL,
                    [sourcefile] NVARCHAR(20) NULL,
                    [isbios] NVARCHAR(20) NULL,
                    [board] NVARCHAR(20) NULL,
                    [year] NVARCHAR(20) NULL,
                    [istrurip] BOOLEAN DEFAULT '0' NOT NULL,
                    [publisher] NVARCHAR(20) NULL,
                    [developer] NVARCHAR(20) NULL,
                    [edition] NVARCHAR(20) NULL,
                    [version] NVARCHAR(20) NULL,
                    [type] NVARCHAR(20) NULL,
                    [media] NVARCHAR(20) NULL,
                    [language] NVARCHAR(20) NULL,
                    [players] NVARCHAR(20) NULL,
                    [ratings] NVARCHAR(20) NULL,
                    [genre] NVARCHAR(20) NULL,
                    [peripheral] NVARCHAR(20) NULL,
                    [barcode] NVARCHAR(20) NULL,
                    [mediacatalognumber] NVARCHAR(20),
                    [RomTotal] INTEGER DEFAULT '0' NOT NULL,
                    [RomGot] INTEGER DEFAULT '0' NOT NULL,
                    FOREIGN KEY(DatId) REFERENCES DAT(DatId)
                );");
        }

        public void DBRead(int gameId)
        {
            SqlRead.Parameters["GameId"].Value = gameId;

            using (SQLiteDataReader dr = SqlRead.ExecuteReader())
            {
                if (dr.Read())
                {
                    ReadFromReader(dr);
                }
                dr.Close();
            }
        }

        public static List<RvGame> ReadGames(uint DatId, bool readRoms = false)
        {
            List<RvGame> games = new List<RvGame>();
            SqlReadGames.Parameters["DatId"].Value = DatId;

            using (SQLiteDataReader dr = SqlReadGames.ExecuteReader())
            {
                while (dr.Read())
                {
                    RvGame rvGame = new RvGame();
                    rvGame.ReadFromReader(dr, readRoms);
                    games.Add(rvGame);
                }
                dr.Close();
            }

            return games;
        }

        private void ReadFromReader(SQLiteDataReader dr, bool readRoms = false)
        {
            GameId = Convert.ToUInt32(dr["GameId"]);
            DatId = Convert.ToUInt32(dr["DatId"]);
            Name = dr["name"].ToString();
            Description = dr["description"].ToString();
            Manufacturer = dr["manufacturer"].ToString();
            CloneOf = dr["cloneOf"].ToString();
            RomOf = dr["romof"].ToString();
            SourceFile = dr["sourcefile"].ToString();
            IsBios = dr["isbios"].ToString();
            Board = dr["board"].ToString();
            Year = dr["year"].ToString();
            IsTrurip = Convert.ToBoolean(dr["istrurip"]);
            Publisher = dr["publisher"].ToString();
            Developer = dr["developer"].ToString();
            Edition = dr["edition"].ToString();
            Version = dr["version"].ToString();
            Type = dr["type"].ToString();
            Media = dr["media"].ToString();
            Language = dr["language"].ToString();
            Players = dr["players"].ToString();
            Ratings = dr["ratings"].ToString();
            Genre = dr["genre"].ToString();
            Peripheral = dr["peripheral"].ToString();
            BarCode = dr["barcode"].ToString();
            MediaCatalogNumber = dr["mediacatalognumber"].ToString();

            if (readRoms)
                Roms = RvRom.ReadRoms(GameId);
        }

        public void DBWrite()
        {
            SqlWrite.Parameters["DatId"].Value = DatId;
            SqlWrite.Parameters["Name"].Value = Name;
            SqlWrite.Parameters["Description"].Value = Description;
            SqlWrite.Parameters["Manufacturer"].Value = Manufacturer;

            SqlWrite.Parameters["CloneOf"].Value = CloneOf;
            SqlWrite.Parameters["RomOf"].Value = RomOf;
            SqlWrite.Parameters["SampleOf"].Value = SampleOf;
            SqlWrite.Parameters["sourcefile"].Value = SourceFile;
            SqlWrite.Parameters["IsBios"].Value = IsBios;
            SqlWrite.Parameters["Board"].Value = Board;
            SqlWrite.Parameters["Year"].Value = Year;

            SqlWrite.Parameters["IsTrurip"].Value = IsTrurip;
            SqlWrite.Parameters["Publisher"].Value = Publisher;
            SqlWrite.Parameters["Developer"].Value = Developer;
            SqlWrite.Parameters["Edition"].Value = Edition;
            SqlWrite.Parameters["Version"].Value = Version;
            SqlWrite.Parameters["Type"].Value = Type;
            SqlWrite.Parameters["Media"].Value = Media;
            SqlWrite.Parameters["Language"].Value = Language;
            SqlWrite.Parameters["Players"].Value = Players;
            SqlWrite.Parameters["Ratings"].Value = Ratings;
            SqlWrite.Parameters["Genre"].Value = Genre;
            SqlWrite.Parameters["Peripheral"].Value = Peripheral;
            SqlWrite.Parameters["BarCode"].Value = BarCode;
            SqlWrite.Parameters["MediaCatalogNumber"].Value = MediaCatalogNumber;

            object res = SqlWrite.ExecuteScalar();

            if (res == null || res == DBNull.Value)
                return;
            GameId = Convert.ToUInt32(res);

            if (Roms != null)
            {
                foreach (RvRom rvRom in Roms)
                {
                    rvRom.GameId = GameId;
                    rvRom.DBWrite();
                }
            }
        }

        public void AddRom(RvRom rvRom)
        {
            if (Roms == null)
                Roms = new List<RvRom>();

            Roms.Add(rvRom);
        }


    }


}
