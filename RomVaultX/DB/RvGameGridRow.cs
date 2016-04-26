using System.Collections.Generic;

namespace RomVaultX.DB
{

    public class RvGameGridRow
    {
        public int GameId;
        public string Name;
        public string Description;
        public int RomGot;
        public int RomTotal;
        public int RomNoDump;

        
        public static List<RvGameGridRow> ReadGames(int datId)
        {
            return Program.db.ReadGames(datId);
        }

        public bool HasCorrect()
        {
            return RomGot > 0;
        }

        public bool HasMissing()
        {
            return RomTotal - RomNoDump - RomGot > 0;
        }
    }
}