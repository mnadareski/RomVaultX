using System.Collections.Generic;
using System.Data.SQLite;

namespace RomVaultX.DB
{

    public class RvGameRow
    {
        public int GameId;
        public string Name;
        public string Description;
        public int TotalGot;
        public int TotalMissing;

        private static readonly SQLiteCommand SQLRead;

        static RvGameRow()
        {
            SQLRead = new SQLiteCommand(
                @"
                    SELECT GameId,Name,Description,RomTotal,RomGot FROM game WHERE DatId=@datId ORDER BY Name");
            SQLRead.Parameters.Add(new SQLiteParameter("datId"));

        }

        public static void setConnection(SQLiteConnection Connection)
        {
            SQLRead.Connection = Connection;
        }

        public static List<RvGameRow> ReadGames(int datId)
        {
            List<RvGameRow> rows = new List<RvGameRow>();
            SQLRead.Parameters["DatId"].Value = datId;

            using (SQLiteDataReader dr = SQLRead.ExecuteReader())
            {
                while (dr.Read())
                {
                    RvGameRow row = new RvGameRow();
                    row.GameId = System.Convert.ToInt32(dr["GameID"]);;
                    row.Name = dr["name"].ToString();
                    row.Description =dr["description"].ToString();
                    row.TotalGot = System.Convert.ToInt32(dr["RomGot"]);
                    row.TotalMissing = System.Convert.ToInt32(dr["RomTotal"]) - row.TotalGot;
                    rows.Add(row);
                }
                dr.Close();
            }
            return rows;
        }
    }
}