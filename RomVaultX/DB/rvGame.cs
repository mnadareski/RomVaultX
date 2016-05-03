using System.Collections.Generic;
using RomVaultX.Util;

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

            int index;
            ChildNameSearch(rvRom.Name, out index);
            Roms.Insert(index, rvRom);
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




        public int ChildNameSearch(string lRomName, out int index)
        {
            int intBottom = 0;
            int intTop = Roms.Count;
            int intMid = 0;
            int intRes = -1;

            //Binary chop to find the closest match
            while (intBottom < intTop && intRes != 0)
            {
                intMid = (intBottom + intTop) / 2;

                intRes = VarFix.CompareName(lRomName, Roms[intMid].Name);
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
                while (index > 0 && intRes1 == 0)
                {
                    intRes1 = VarFix.CompareName(lRomName, Roms[index - 1].Name);
                    if (intRes1 == 0)
                        index--;
                }
            }
            // if the search is greater than the closest match move one up the list
            else if (intRes > 0)
                index++;

            return intRes;
        }
    }


}
