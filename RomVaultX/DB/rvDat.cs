using System.Collections.Generic;

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

        public List<RvGame> Games; 

        
        public void DbRead(uint datId,bool readGames=false)
        {
            Program.db.RvDatRead(datId, this);

            if (readGames)
                Games = RvGame.ReadGames(DatId,true);
        }

        public void DbWrite()
        {
            DatId = Program.db.RvDatWrite(this);
            if (DatId == 0)
                return;

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
