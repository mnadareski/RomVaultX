using System.Collections.Generic;
using RomVaultX.Util;

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
        public string Path;
        public long DatTimeStamp;
        public bool ExtraDir;
        public string MergeType;

        public List<RvGame> Games;


        public void DbRead(uint datId, bool readGames = false)
        {
            Program.db.RvDatRead(datId, this);

            if (readGames)
                Games = RvGame.ReadGames(DatId, true);
        }

        public void DbWrite()
        {
            DatId = Program.db.RvDatWrite(this);
            if (DatId == 0)
                return;

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
            if (Games == null)
                Games = new List<RvGame>();

            int index;
            ChildNameSearch(rvGame.Name, out index);
            Games.Insert(index, rvGame);
        }

        public string GetExtraDirName()
        {
            if (!string.IsNullOrWhiteSpace(Description))
                return Description;
            return "-unknown-";
        }



        public int ChildNameSearch(string lGameName, out int index)
        {
            int intBottom = 0;
            int intTop = Games.Count;
            int intMid = 0;
            int intRes = -1;

            //Binary chop to find the closest match
            while (intBottom < intTop && intRes != 0)
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
                while (index > 0 && intRes1 == 0)
                {
                    intRes1 = VarFix.CompareName(lGameName, Games[index - 1].Name);
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
