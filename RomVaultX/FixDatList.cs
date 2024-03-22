using System;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using RomVaultX.Util;

namespace RomVaultX
{
    internal static class FixDatList
    {
        public static SqliteCommand CommandFindRomsInGame
        {
            get
            {
                if (_commandFindRomsInGame == null)
                {
                    _commandFindRomsInGame = new SqliteCommand(@"
                        SELECT
                            ROM.RomId,
                            ROM.name,
                            ROM.size,
                            ROM.crc,
                            ROM.SHA1
                        FROM ROM
                        WHERE
                            ROM.FileId IS null
                            AND ROM.GameId = @GameId
                        ORDER BY ROM.RomId",
                    Program.db.Connection);

                    _commandFindRomsInGame.Parameters.Add(new SqliteParameter("GameId", SqliteType.Integer));
                }

                return _commandFindRomsInGame;
            }
        }
        private static SqliteCommand? _commandFindRomsInGame;

        public static void Extract(string dirName)
        {
            Debug.WriteLine(dirName);

            var getfiles = new SqliteCommand(@"
                SELECT
                    DAT.DatId,
                    DIR.fullname,
                    GameId,
                    GAME.name
                FROM DIR, DAT, GAME
                WHERE
                    DAT.DirId = DIR.DirId
                    AND GAME.DatId = DAT.DatId
                    AND DIR.fullname like '" + dirName + @"%' 
                    AND (SELECT COUNT(1) FROM ROM WHERE ROM.FileId IS null AND ROM.GameId = GAME.GameId) > 0
                ORDER BY DAT.DatId, GameId",
            Program.db.Connection);

            DbDataReader reader = getfiles.ExecuteReader();

            int DatId = -1;

            while (reader.Read())
            {
                int thisDatId = Convert.ToInt32(reader["datId"]);
                if (thisDatId == DatId)
                {
                    string dirFullName = reader["FullName"].ToString();
                    Debug.WriteLine(dirFullName);
                    DatId = thisDatId;
                }

                int GameId = Convert.ToInt32(reader["GameId"]);
                string GameName = reader["name"].ToString();
                Debug.WriteLine("Game " + GameId + " Name: " + GameName);

                int romCount = 0;
                using DbDataReader drRom = ZipSetGetRomsInGame(GameId);
                while (drRom.Read())
                {
                    int RomId = Convert.ToInt32(drRom["RomId"]);
                    string RomName = drRom["name"].ToString();
                    ulong size = Convert.ToUInt64(drRom["size"]);
                    byte[] CRC = VarFix.CleanMD5SHA1(drRom["crc"].ToString(), 8);
                    byte[] sha1 = VarFix.CleanMD5SHA1(drRom["sha1"].ToString(), 32);

                    Debug.WriteLine("    Rom " + RomId + " Name: " + RomName + "  Size: " + size + "  CRC: " + VarFix.ToString(CRC));

                    romCount += 1;
                }
            }
        }

        private static DbDataReader ZipSetGetRomsInGame(int GameId)
        {
            CommandFindRomsInGame.Parameters["GameId"].Value = GameId;
            return CommandFindRomsInGame.ExecuteReader();
        }
    }
}