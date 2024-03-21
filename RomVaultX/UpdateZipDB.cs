using System;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.Windows.Forms;
using Compress.ZipFile;
using RomVaultX.Util;

namespace RomVaultX
{
    public static class UpdateZipDB
    {
        private static SQLiteCommand CommandWriteLocalHeaderToRom;
        private static SQLiteCommand CommandWriteCentralDirToGame;
        private static SQLiteCommand CommandGetAllGamesWithRoms;
        private static SQLiteCommand CommandFindRomsInGame;

        public static void UpdateDB()
        {
            SetupSQLCommands();

            Program.db.ExecuteNonQuery(@"update game set dirid=(select dirId from DAT where game.Datid=dat.datid) where dirid is null;");

            using (DbDataReader drGame = ZipSetGetAllGames())
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
                    using (DbDataReader drRom = ZipSetGetRomsInGame(GameId))
                    {
                        while (drRom.Read())
                        {
                            int RomId = Convert.ToInt32(drRom["RomId"]);
                            string RomName = drRom["name"].ToString();
                            ulong size = Convert.ToUInt64(drRom["size"]);
                            ulong compressedSize = Convert.ToUInt64(drRom["compressedsize"]);
                            byte[] CRC = VarFix.CleanMD5SHA1(drRom["crc"].ToString(), 8);
                            byte[] SHA1 = VarFix.CleanMD5SHA1(drRom["sha1"].ToString(), 40);
                            Debug.WriteLine("    Rom " + RomId + " Name: " + RomName + "  Size: " + size + "  Compressed: " + compressedSize + "  CRC: " + VarFix.ToString(CRC));

                            byte[] localHeader;
                            memZip.ZipFileAddFake(RomName, fileOffset, size, compressedSize, CRC, out localHeader);

                            ZipSetLocalFileHeader(RomId, localHeader, fileOffset, compressedSize, SHA1);

                            fileOffset += (ulong)localHeader.Length + compressedSize;
                            commitCount += 1;
                            romCount += 1;
                        }
                    }

                    byte[] centeralDir;
                    memZip.ZipFileCloseFake(fileOffset, out centeralDir);

                    if (romCount > 0)
                    {
                        ZipSetCentralFileHeader(GameId, fileOffset + (ulong)centeralDir.Length, DateTime.UtcNow.Ticks, centeralDir, fileOffset);
                    }

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

        private static void SetupSQLCommands()
        {
            // just check one as the rest should be the same
            if (CommandGetAllGamesWithRoms != null)
            {
                return;
            }

            CommandGetAllGamesWithRoms = new SQLiteCommand(@"SELECT GameId,name FROM game WHERE RomGot>0 AND ZipFileLength is null", Program.db.Connection);


            CommandFindRomsInGame = new SQLiteCommand(
                @"SELECT
                    ROM.RomId, ROM.name, FILES.size, FILES.compressedsize, FILES.crc,FILES.sha1
                 FROM ROM,FILES WHERE ROM.FileId=FILES.FileId AND ROM.GameId=@GameId AND ROM.PutInZip ORDER BY ROM.RomId", Program.db.Connection);
            CommandFindRomsInGame.Parameters.Add(new SQLiteParameter("GameId"));

            CommandWriteLocalHeaderToRom = new SQLiteCommand(
                @"UPDATE ROM SET 
                    LocalFileHeader=@LocalFileHeader,
                    LocalFileHeaderOffset=@LocalFileHeaderOffset,
                    LocalFileHeaderLength=@LocalFileHeaderLength,
                    LocalFileSha1=@LocalFileSha1,
                    LocalFileCompressedSize=@LocalFileCompressedSize
                WHERE
                    RomId=@RomId", Program.db.Connection);
            CommandWriteLocalHeaderToRom.Parameters.Add(new SQLiteParameter("LocalFileHeader"));
            CommandWriteLocalHeaderToRom.Parameters.Add(new SQLiteParameter("LocalFileHeaderOffset"));
            CommandWriteLocalHeaderToRom.Parameters.Add(new SQLiteParameter("LocalFileHeaderLength"));
            CommandWriteLocalHeaderToRom.Parameters.Add(new SQLiteParameter("LocalFileSha1"));
            CommandWriteLocalHeaderToRom.Parameters.Add(new SQLiteParameter("LocalFileCompressedSize"));
            CommandWriteLocalHeaderToRom.Parameters.Add(new SQLiteParameter("RomId"));


            CommandWriteCentralDirToGame = new SQLiteCommand(
                @"UPDATE GAME SET 
                    ZipFileLength=@ZipFileLength,
                    LastWriteTime=@ZipFileTimeStamp,
                    CreationTime=@ZipFileTimeStamp,
                    LastAccessTime=@ZipFileTimeStamp,
                    CentralDirectory=@CentralDirectory,
                    CentralDirectoryOffset=@CentralDirectoryOffset,
                    CentralDirectoryLength=@CentralDirectoryLength
                WHERE
                    GameId=@GameId", Program.db.Connection);
            CommandWriteCentralDirToGame.Parameters.Add(new SQLiteParameter("ZipFileLength"));
            CommandWriteCentralDirToGame.Parameters.Add(new SQLiteParameter("ZipFileTimeStamp"));
            CommandWriteCentralDirToGame.Parameters.Add(new SQLiteParameter("CentralDirectory"));
            CommandWriteCentralDirToGame.Parameters.Add(new SQLiteParameter("CentralDirectoryOffset"));
            CommandWriteCentralDirToGame.Parameters.Add(new SQLiteParameter("CentralDirectoryLength"));
            CommandWriteCentralDirToGame.Parameters.Add(new SQLiteParameter("GameId"));
        }

        private static DbDataReader ZipSetGetAllGames()
        {
            return CommandGetAllGamesWithRoms.ExecuteReader();
        }

        private static DbDataReader ZipSetGetRomsInGame(int GameId)
        {
            CommandFindRomsInGame.Parameters["GameId"].Value = GameId;
            return CommandFindRomsInGame.ExecuteReader();
        }

        private static void ZipSetLocalFileHeader(int RomId, byte[] localHeader, ulong fileOffset, ulong compressedSize, byte[] sha1)
        {
            CommandWriteLocalHeaderToRom.Parameters["LocalFileHeader"].Value = localHeader;
            CommandWriteLocalHeaderToRom.Parameters["LocalFileHeaderOffset"].Value = fileOffset;
            CommandWriteLocalHeaderToRom.Parameters["LocalFileHeaderLength"].Value = localHeader.Length;
            CommandWriteLocalHeaderToRom.Parameters["LocalFileSha1"].Value = VarFix.ToString(sha1);
            CommandWriteLocalHeaderToRom.Parameters["LocalFileCompressedSize"].Value = compressedSize;

            CommandWriteLocalHeaderToRom.Parameters["RomId"].Value = RomId;
            CommandWriteLocalHeaderToRom.ExecuteNonQuery();
        }

        private static void ZipSetCentralFileHeader(int GameId, ulong ZipFileLength, long timestamp, byte[] centeralDir, ulong fileOffset)
        {
            CommandWriteCentralDirToGame.Parameters["ZipFileLength"].Value = ZipFileLength;
            CommandWriteCentralDirToGame.Parameters["ZipFileTimeStamp"].Value = timestamp;
            CommandWriteCentralDirToGame.Parameters["CentralDirectory"].Value = centeralDir;
            CommandWriteCentralDirToGame.Parameters["CentralDirectoryOffset"].Value = fileOffset;
            CommandWriteCentralDirToGame.Parameters["CentralDirectoryLength"].Value = centeralDir.Length;
            CommandWriteCentralDirToGame.Parameters["GameId"].Value = GameId;
            CommandWriteCentralDirToGame.ExecuteNonQuery();
        }
    }
}