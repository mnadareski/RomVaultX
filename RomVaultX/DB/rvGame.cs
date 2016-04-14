using System;
using System.Collections.Generic;
using System.Data.Common;

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

        private static readonly DbCommand SqlWrite;
        private static readonly DbCommand SqlRead;
        private static readonly DbCommand SqlReadGames;

        static RvGame()
        {
            SqlWrite = Program.db.Command(
              @"INSERT INTO GAME ( DatId, name, description, manufacturer, cloneof, romof, sourcefile, isbios, board, year, istrurip, publisher, developer, edition, version, type, media, language, players, ratings, genre, peripheral, barcode, mediacatalognumber)
                          VALUES (@DatId,@Name,@Description,@Manufacturer,@CloneOf,@RomOf,@SourceFile,@IsBios,@Board,@Year,@IsTrurip,@Publisher,@Developer,@Edition,@Version,@Type,@Media,@Language,@Players,@Ratings,@Genre,@Peripheral,@BarCode,@MediaCatalogNumber);

                SELECT last_insert_rowid();");

            SqlWrite.Parameters.Add(Program.db.Parameter("DatId")); //DatId;
            SqlWrite.Parameters.Add(Program.db.Parameter("Name")); //Name;
            SqlWrite.Parameters.Add(Program.db.Parameter("Description")); //Description;
            SqlWrite.Parameters.Add(Program.db.Parameter("Manufacturer")); //Manufacturer;

            SqlWrite.Parameters.Add(Program.db.Parameter("CloneOf")); //CloneOf;
            SqlWrite.Parameters.Add(Program.db.Parameter("RomOf")); //RomOf;
            SqlWrite.Parameters.Add(Program.db.Parameter("SampleOf")); //SampleOf;
            SqlWrite.Parameters.Add(Program.db.Parameter("Sourcefile")); //SourceFile;
            SqlWrite.Parameters.Add(Program.db.Parameter("IsBios")); //IsBios;
            SqlWrite.Parameters.Add(Program.db.Parameter("Board")); //Board;
            SqlWrite.Parameters.Add(Program.db.Parameter("Year")); //Year;

            SqlWrite.Parameters.Add(Program.db.Parameter("IsTrurip")); //IsTrurip;
            SqlWrite.Parameters.Add(Program.db.Parameter("Publisher")); //Publisher;
            SqlWrite.Parameters.Add(Program.db.Parameter("Developer")); //Developer;
            SqlWrite.Parameters.Add(Program.db.Parameter("Edition")); //Edition;
            SqlWrite.Parameters.Add(Program.db.Parameter("Version")); //Version;
            SqlWrite.Parameters.Add(Program.db.Parameter("Type")); //Type;
            SqlWrite.Parameters.Add(Program.db.Parameter("Media")); //Media;
            SqlWrite.Parameters.Add(Program.db.Parameter("Language")); //Language;
            SqlWrite.Parameters.Add(Program.db.Parameter("Players")); //Players;
            SqlWrite.Parameters.Add(Program.db.Parameter("Ratings")); //Ratings;
            SqlWrite.Parameters.Add(Program.db.Parameter("Genre")); //Genre;
            SqlWrite.Parameters.Add(Program.db.Parameter("Peripheral")); //Peripheral;
            SqlWrite.Parameters.Add(Program.db.Parameter("BarCode")); //BarCode;
            SqlWrite.Parameters.Add(Program.db.Parameter("MediaCatalogNumber")); //MediaCatalogNumber;        


            SqlRead = Program.db.Command(
                @"SELECT GameId, DatId, name, description, manufacturer, cloneof, romof, sourcefile, isbios, board, year, istrurip, publisher, developer, edition, version, type, media, language, players, ratings, genre, peripheral, barcode, mediacatalognumber
                    FROM GAME WHERE GameId=@GameId ORDER BY name");
            SqlRead.Parameters.Add(Program.db.Parameter("GameId"));

            SqlReadGames = Program.db.Command(
                @"SELECT GameId, DatId, name, description, manufacturer, cloneof, romof, sourcefile, isbios, board, year, istrurip, publisher, developer, edition, version, type, media, language, players, ratings, genre, peripheral, barcode, mediacatalognumber
                    FROM GAME WHERE DatId=@DatId ORDER BY name");
            SqlReadGames.Parameters.Add(Program.db.Parameter("DatId"));

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
                    [RomTotal] INTEGER DEFAULT 0 NOT NULL,
                    [RomGot] INTEGER DEFAULT 0 NOT NULL,
                    [RomNoDump] INTEGER DEFAULT 0 NOT NULL,
                    [ZipFileLength] INTEGER NULL, 
                    [ZipFileTimeStamp] INTEGER NULL,
                    [CentralDirectory] BLOB NULL,
                    [CentralDirectoryOffset] INTEGER NULL,
                    [CentralDirectoryLength] INTEGER NULL,
                    FOREIGN KEY(DatId) REFERENCES DAT(DatId)
                );");
        }

        public void DBRead(int gameId, bool readRoms = false)
        {
            SqlRead.Parameters["GameId"].Value = gameId;

            using (DbDataReader dr = SqlRead.ExecuteReader())
            {
                if (dr.Read())
                {
                    ReadFromReader(dr, readRoms);
                }
                dr.Close();
            }
        }

        public static List<RvGame> ReadGames(uint DatId, bool readRoms = false)
        {
            List<RvGame> games = new List<RvGame>();
            SqlReadGames.Parameters["DatId"].Value = DatId;

            using (DbDataReader dr = SqlReadGames.ExecuteReader())
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

        private void ReadFromReader(DbDataReader dr, bool readRoms = false)
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

        public int AddRom(RvRom rvRom)
        {
            if (Roms == null)
                Roms = new List<RvRom>();

            Roms.Add(rvRom);
            return Roms.Count - 1;
        }

        public int RomCount
        {
            get { return Roms == null ? 0 : Roms.Count; }
        }

        public RvRom Get(int index)
        {
            return Roms[index];
        }
    }


}
