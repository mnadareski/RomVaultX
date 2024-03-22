using System;
using System.Collections.Generic;
using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace RomVaultX.DB
{
    public class rvGameGridRow
    {
        public static SqliteCommand CommandRead
        {
            get
            {
                if (_commandRead == null)
                {
                    _commandRead = new SqliteCommand(@"
                        SELECT
                            GameId,
                            name,
                            description,
                            RomTotal,
                            RomGot,
                            RomNoDump
                        FROM GAME
                        WHERE
                            DatId = @DatId
                        ORDER BY name",
                    Program.db.Connection);

                    _commandRead.Parameters.Add(new SqliteParameter("DatId", SqliteType.Integer));
                }

                return _commandRead;
            }
        }
        private static SqliteCommand? _commandRead;

        public int GameId;
        public string? Name;
        public string? Description;
        public int RomGot;
        public int RomTotal;
        public int RomNoDump;

        public static List<rvGameGridRow> ReadGames(int datId)
        {
            CommandRead.Parameters["DatId"].Value = datId;

            List<rvGameGridRow> rows = [];
            using (DbDataReader dr = CommandRead.ExecuteReader())
            {
                while (dr.Read())
                {
                    var gridRow = new rvGameGridRow
                    {
                        GameId = Convert.ToInt32(dr["GameId"]),
                        Name = dr["name"].ToString(),
                        Description = dr["description"].ToString(),
                        RomGot = Convert.ToInt32(dr["RomGot"]),
                        RomTotal = Convert.ToInt32(dr["RomTotal"]),
                        RomNoDump = Convert.ToInt32(dr["RomNoDump"])
                    };

                    rows.Add(gridRow);
                }
                dr.Close();
            }
            return rows;
        }

        public bool HasCorrect() => RomGot > 0;

        public bool HasMissing() => RomTotal - RomNoDump - RomGot > 0;
    }
}