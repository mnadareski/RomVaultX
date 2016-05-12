using System;
using System.Data.Common;
using System.Diagnostics;
using RomVaultX.SupportedFiles.Zip;
using RomVaultX.Util;
using System.Windows.Forms;

namespace RomVaultX
{
    public static class UpdateZipDB
    {
        public static void UpdateDB()
        {
            using (DbDataReader drGame = Program.db.ZipSetGetAllGames())
            {
                int commitCount = 0;
                Program.db.Begin();

                while (drGame.Read())
                {
                    int GameId = Convert.ToInt32(drGame["GameId"]);
                    string GameName = drGame["name"].ToString();
                    Debug.WriteLine("Game " + GameId + " Name: " + GameName);

                    ZipFile memZip = new ZipFile();
                    memZip.ZipCreateFake();

                    ulong fileOffset = 0;

                    int romCount = 0;
                    using (DbDataReader drRom = Program.db.ZipSetGetRomsInGame(GameId))
                    {

                        while (drRom.Read())
                        {
                            int RomId = Convert.ToInt32(drRom["RomId"]);
                            string RomName = drRom["name"].ToString();
                            ulong size = Convert.ToUInt64(drRom["size"]);
                            ulong compressedSize = Convert.ToUInt64(drRom["compressedsize"]);
                            byte[] CRC = VarFix.CleanMD5SHA1(drRom["crc"].ToString(), 8);
                            Debug.WriteLine("    Rom " + RomId + " Name: " + RomName + "  Size: " + size + "  Compressed: " + compressedSize + "  CRC: " + VarFix.ToString(CRC));

                            byte[] localHeader;
                            memZip.ZipFileAddFake(RomName, fileOffset, size, compressedSize, CRC, out localHeader);

                            Program.db.ZipSetLocalFileHeader(RomId, localHeader, fileOffset);

                            fileOffset += (ulong)localHeader.Length + compressedSize;
                            commitCount += 1;
                            romCount += 1;
                        }
                    }

                    byte[] centeralDir;
                    memZip.ZipFileCloseFake(fileOffset, out centeralDir);

                    if (romCount > 0)
                        Program.db.ZipSetCentralFileHeader(GameId, fileOffset + (ulong)centeralDir.Length, DateTime.UtcNow.Ticks, centeralDir, fileOffset);

                    if (commitCount >= 100)
                    {
                        Program.db.Commit();
                        Program.db.Begin();
                        commitCount = 0;
                    }

                }
            }
            Program.db.Commit();

            MessageBox.Show("Zip Header Database Update Complete");
        }

    }
}
