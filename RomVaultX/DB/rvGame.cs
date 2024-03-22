using System;
using System.Collections.Generic;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using RomVaultX.Util;

namespace RomVaultX.DB
{
    public class RvGame
    {
        public static SqliteCommand CommandRead
        {
            get
            {
                if (_commandRead == null)
                {
                    _commandRead = new SqliteCommand(@"
                        SELECT *
                        FROM GAME
                        WHERE
                            GameId = @GameId
                        ORDER BY name",
                    Program.db.Connection);

                    _commandRead.Parameters.Add(new SqliteParameter("GameId", SqliteType.Integer));
                }

                return _commandRead;
            }
        }
        private static SqliteCommand? _commandRead;

        public static SqliteCommand CommandReadGames
        {
            get
            {
                if (_commandReadGames == null)
                {
                    _commandReadGames = new SqliteCommand(@"
                        SELECT *
                        FROM GAME
                        WHERE
                            DatId = @DatId
                        ORDER BY name",
                    Program.db.Connection);

                    _commandReadGames.Parameters.Add(new SqliteParameter("DatId", SqliteType.Integer));
                }

                return _commandReadGames;
            }
        }
        private static SqliteCommand? _commandReadGames;

        public static SqliteCommand CommandWrite
        {
            get
            {
                if (_commandWrite == null)
                {
                    _commandWrite = new SqliteCommand(@"
                        INSERT INTO GAME
                        (
                            DatId,
                            name,
                            description,
                            manufacturer,
                            cloneof,
                            romof,
                            sampleof,
                            sourcefile,
                            isbios,
                            board,
                            year,
                            istrurip,
                            publisher,
                            developer,
                            edition,
                            version,
                            type,
                            media,
                            language,
                            players,
                            ratings,
                            genre,
                            peripheral,
                            barcode,
                            mediacatalognumber
                        )
                        VALUES
                        (
                            @DatId,
                            @name,
                            @description,
                            @manufacturer,
                            @cloneof,
                            @romof,
                            @sampleof,
                            @sourcefile,
                            @isbios,
                            @board,
                            @year,
                            @istrurip,
                            @publisher,
                            @developer,
                            @edition,
                            @version,
                            @type,
                            @media,
                            @language,
                            @players,
                            @ratings,
                            @genre,
                            @peripheral,
                            @barcode,
                            @mediacatalognumber
                        );

                        SELECT last_insert_rowid();",
                    Program.db.Connection);

                    _commandWrite.Parameters.Add(new SqliteParameter("DatId", SqliteType.Integer));
                    _commandWrite.Parameters.Add(new SqliteParameter("name", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("description", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("manufacturer", SqliteType.Text));

                    _commandWrite.Parameters.Add(new SqliteParameter("cloneof", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("romof", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("sampleof", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("sourcefile", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("isbios", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("board", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("year", SqliteType.Text));

                    _commandWrite.Parameters.Add(new SqliteParameter("istrurip", SqliteType.Integer));
                    _commandWrite.Parameters.Add(new SqliteParameter("publisher", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("developer", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("edition", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("version", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("type", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("media", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("language", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("players", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("ratings", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("genre", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("peripheral", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("barcode", SqliteType.Text));
                    _commandWrite.Parameters.Add(new SqliteParameter("mediacatalognumber", SqliteType.Text));
                }

                return _commandWrite;
            }
        }
        private static SqliteCommand? _commandWrite;

        public uint GameId;
        public uint DatId;
        public string? Name;
        public string? Description;

        public string? Manufacturer;
        public string? CloneOf;
        public string? RomOf;
        public string? SampleOf;
        public string? SourceFile;
        public string? IsBios;
        public string? Board;
        public string? Year;

        public bool IsTrurip;
        public string? Publisher;
        public string? Developer;
        public string? Edition;
        public string? Version;
        public string? Type;
        public string? Media;
        public string? Language;
        public string? Players;
        public string? Ratings;
        public string? Genre;
        public string? Peripheral;
        public string? BarCode;
        public string? MediaCatalogNumber;

        public List<RvRom>? Roms;

        public int RomCount => Roms?.Count ?? 0;

        public static void CreateTable()
        {
            Program.db.ExecuteNonQuery(@"
                 CREATE TABLE IF NOT EXISTS [GAME] (
                    [GameId] INTEGER  PRIMARY KEY NOT NULL,
                    [DatId] INTEGER NOT NULL,
                    [DirId] INTEGER NULL,
                    [name] NVARCHAR(200) NOT NULL,
                    [description] NVARCHAR(220) NULL,
                    [manufacturer] NVARCHAR(200) NULL,
                    [cloneof] NVARCHAR(200) NULL,
                    [romof] NVARCHAR(200) NULL,
                    [sampleof] NVARCHAR(200) NULL,
                    [sourcefile] NVARCHAR(200) NULL,
                    [isbios] NVARCHAR(200) NULL,
                    [board] NVARCHAR(200) NULL,
                    [year] NVARCHAR(200) NULL,
                    [istrurip] BOOLEAN DEFAULT 0 NOT NULL,
                    [publisher] NVARCHAR(200) NULL,
                    [developer] NVARCHAR(200) NULL,
                    [edition] NVARCHAR(200) NULL,
                    [version] NVARCHAR(200) NULL,
                    [type] NVARCHAR(200) NULL,
                    [media] NVARCHAR(200) NULL,
                    [language] NVARCHAR(200) NULL,
                    [players] NVARCHAR(200) NULL,
                    [ratings] NVARCHAR(200) NULL,
                    [genre] NVARCHAR(200) NULL,
                    [peripheral] NVARCHAR(200) NULL,
                    [barcode] NVARCHAR(200) NULL,
                    [mediacatalognumber] NVARCHAR(200),
                    [RomTotal] INTEGER DEFAULT 0 NOT NULL,
                    [RomGot] INTEGER DEFAULT 0 NOT NULL,
                    [RomNoDump] INTEGER DEFAULT 0 NOT NULL,
                    [ZipFileLength] INTEGER NULL, 
                    [LastWriteTime] INTEGER NULL,
                    [CreationTime] INTEGER NULL,
                    [LastAccessTime] INTEGER NULL,
                    [CentralDirectory] BLOB NULL,
                    [CentralDirectoryOffset] INTEGER NULL,
                    [CentralDirectoryLength] INTEGER NULL,
                    FOREIGN KEY(DatId) REFERENCES DAT(DatId)
                    FOREIGN KEY(DirId) REFERENCES DIR(DirId)
                );");
        }

        public void DBRead(int gameId, bool readRoms = false)
        {
            CommandRead.Parameters["GameId"].Value = gameId;

            using (DbDataReader dr = CommandRead.ExecuteReader())
            {
                if (dr.Read())
                    RvGameReadFromReader(dr, this);

                dr.Close();
            }

            if (readRoms)
                Roms = RvRom.ReadRoms(GameId);
        }

        public static List<RvGame> ReadGames(uint datId, bool readRoms = false)
        {
            CommandReadGames.Parameters["DatId"].Value = datId;

            List<RvGame> games = [];
            using (DbDataReader dr = CommandReadGames.ExecuteReader())
            {
                while (dr.Read())
                {
                    var rvGame = new RvGame();
                    RvGameReadFromReader(dr, rvGame);
                    games.Add(rvGame);
                }

                dr.Close();
            }

            if (readRoms)
            {
                foreach (RvGame game in games)
                {
                    game.Roms = RvRom.ReadRoms(game.GameId);
                }
            }

            return games;
        }

        private static void RvGameReadFromReader(DbDataReader dr, RvGame game)
        {
            game.GameId = Convert.ToUInt32(dr["GameId"]);
            game.DatId = Convert.ToUInt32(dr["DatId"]);
            game.Name = dr["name"].ToString();
            game.Description = dr["description"].ToString();
            game.Manufacturer = dr["manufacturer"].ToString();
            game.CloneOf = dr["cloneof"].ToString();
            game.RomOf = dr["romof"].ToString();
            game.SampleOf = dr["sampleof"].ToString();
            game.SourceFile = dr["sourcefile"].ToString();
            game.IsBios = dr["isbios"].ToString();
            game.Board = dr["board"].ToString();
            game.Year = dr["year"].ToString();
            game.IsTrurip = Convert.ToBoolean(dr["istrurip"]);
            game.Publisher = dr["publisher"].ToString();
            game.Developer = dr["developer"].ToString();
            game.Edition = dr["edition"].ToString();
            game.Version = dr["version"].ToString();
            game.Type = dr["type"].ToString();
            game.Media = dr["media"].ToString();
            game.Language = dr["language"].ToString();
            game.Players = dr["players"].ToString();
            game.Ratings = dr["ratings"].ToString();
            game.Genre = dr["genre"].ToString();
            game.Peripheral = dr["peripheral"].ToString();
            game.BarCode = dr["barcode"].ToString();
            game.MediaCatalogNumber = dr["mediacatalognumber"].ToString();
        }

        public void DBWrite()
        {
            CommandWrite.Parameters["DatId"].Value = DatId;
            CommandWrite.Parameters["name"].Value = Name ?? string.Empty;
            CommandWrite.Parameters["description"].Value = Description ?? string.Empty;
            CommandWrite.Parameters["manufacturer"].Value = Manufacturer ?? string.Empty;

            CommandWrite.Parameters["cloneof"].Value = CloneOf ?? string.Empty;
            CommandWrite.Parameters["romof"].Value = RomOf ?? string.Empty;
            CommandWrite.Parameters["sampleof"].Value = SampleOf ?? string.Empty;
            CommandWrite.Parameters["sourcefile"].Value = SourceFile ?? string.Empty;
            CommandWrite.Parameters["isbios"].Value = IsBios ?? string.Empty;
            CommandWrite.Parameters["board"].Value = Board ?? string.Empty;
            CommandWrite.Parameters["year"].Value = Year ?? string.Empty;

            CommandWrite.Parameters["istrurip"].Value = IsTrurip;
            CommandWrite.Parameters["publisher"].Value = Publisher ?? string.Empty;
            CommandWrite.Parameters["developer"].Value = Developer ?? string.Empty;
            CommandWrite.Parameters["edition"].Value = Edition ?? string.Empty;
            CommandWrite.Parameters["version"].Value = Version ?? string.Empty;
            CommandWrite.Parameters["type"].Value = Type ?? string.Empty;
            CommandWrite.Parameters["media"].Value = Media ?? string.Empty;
            CommandWrite.Parameters["language"].Value = Language ?? string.Empty;
            CommandWrite.Parameters["players"].Value = Players ?? string.Empty;
            CommandWrite.Parameters["ratings"].Value = Ratings ?? string.Empty;
            CommandWrite.Parameters["genre"].Value = Genre ?? string.Empty;
            CommandWrite.Parameters["peripheral"].Value = Peripheral ?? string.Empty;
            CommandWrite.Parameters["barcode"].Value = BarCode ?? string.Empty;
            CommandWrite.Parameters["mediacatalognumber"].Value = MediaCatalogNumber ?? string.Empty;

            var res = CommandWrite.ExecuteScalar();

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
            Roms ??= [];
            ChildNameSearch(rvRom.Name, out int index);
            Roms.Insert(index, rvRom);
            return index;
        }

        private int ChildNameSearch(string lRomName, out int index)
        {
            int intBottom = 0;
            int intTop = Roms!.Count;
            int intMid = 0;
            int intRes = -1;

            //Binary chop to find the closest match
            while ((intBottom < intTop) && (intRes != 0))
            {
                intMid = (intBottom + intTop) / 2;

                intRes = VarFix.CompareName(lRomName, Roms[intMid].Name);
                if (intRes < 0)
                {
                    intTop = intMid;
                }
                else if (intRes > 0)
                {
                    intBottom = intMid + 1;
                }
            }
            index = intMid;

            // if match was found check up the list for the first match
            if (intRes == 0)
            {
                int intRes1 = 0;
                while ((index > 0) && (intRes1 == 0))
                {
                    intRes1 = VarFix.CompareName(lRomName, Roms[index - 1].Name);
                    if (intRes1 == 0)
                    {
                        index--;
                    }
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