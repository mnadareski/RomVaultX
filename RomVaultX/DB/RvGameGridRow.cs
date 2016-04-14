﻿using System.Collections.Generic;
using System.Data.Common;

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

        private static readonly DbCommand SQLRead;

        static RvGameGridRow()
        {
            SQLRead = Program.db.Command(
                @"
                    SELECT GameId,Name,Description,RomTotal,RomGot,RomNoDump FROM game WHERE DatId=@datId ORDER BY Name");
            SQLRead.Parameters.Add(Program.db.Parameter("datId"));

        }

        public static List<RvGameGridRow> ReadGames(int datId)
        {
            List<RvGameGridRow> rows = new List<RvGameGridRow>();
            SQLRead.Parameters["DatId"].Value = datId;

            using (DbDataReader dr = SQLRead.ExecuteReader())
            {
                while (dr.Read())
                {
                    RvGameGridRow gridRow = new RvGameGridRow();
                    gridRow.GameId = System.Convert.ToInt32(dr["GameID"]);;
                    gridRow.Name = dr["name"].ToString();
                    gridRow.Description =dr["description"].ToString();
                    gridRow.RomGot = System.Convert.ToInt32(dr["RomGot"]);
                    gridRow.RomTotal = System.Convert.ToInt32(dr["RomTotal"]);
                    gridRow.RomNoDump = System.Convert.ToInt32(dr["RomNoDump"]);
                    rows.Add(gridRow);
                }
                dr.Close();
            }
            return rows;
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