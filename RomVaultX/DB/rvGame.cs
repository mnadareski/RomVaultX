using System.Collections.Generic;

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


        public void DBRead(int gameId, bool readRoms = false)
        {
            Program.db.RvGameRead(gameId, this);
            if (readRoms)
                Roms = RvRom.ReadRoms(GameId);
        }

        public static List<RvGame> ReadGames(uint DatId, bool readRoms = false)
        {
            List<RvGame> games = Program.db.RvGamesRead(DatId);

            if (readRoms)
                foreach (RvGame game in games)
                    game.Roms = RvRom.ReadRoms(game.GameId);

            return games;
        }


        public void DBWrite()
        {

            GameId = Program.db.RvGameWrite(this);
            if (GameId == 0)
                return;

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
