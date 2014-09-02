using System.Collections.Generic;
using System.Data.SQLite;

namespace RomVaultX.DB
{

    public class RvGameGridRow
    {
        public int GameId;
        public string Name;
        public string Description;
        public int TotalGot;
        public int TotalMissing;

        private static readonly SQLiteCommand SQLRead;

        static RvGameGridRow()
        {
            SQLRead = new SQLiteCommand(
                @"
                    SELECT GameId,Name,Description,RomTotal,RomGot FROM game WHERE DatId=@datId ORDER BY Name",DataAccessLayer.dbConnection);
            SQLRead.Parameters.Add(new SQLiteParameter("datId"));

        }

        public static List<RvGameGridRow> ReadGames(int datId)
        {
            List<RvGameGridRow> rows = new List<RvGameGridRow>();
            SQLRead.Parameters["DatId"].Value = datId;

            using (SQLiteDataReader dr = SQLRead.ExecuteReader())
            {
                while (dr.Read())
                {
                    RvGameGridRow gridRow = new RvGameGridRow();
                    gridRow.GameId = System.Convert.ToInt32(dr["GameID"]);;
                    gridRow.Name = dr["name"].ToString();
                    gridRow.Description =dr["description"].ToString();
                    gridRow.TotalGot = System.Convert.ToInt32(dr["RomGot"]);
                    gridRow.TotalMissing = System.Convert.ToInt32(dr["RomTotal"]) - gridRow.TotalGot;
                    rows.Add(gridRow);
                }
                dr.Close();
            }
            return rows;
        }
    }
}