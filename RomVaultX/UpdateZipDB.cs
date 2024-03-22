using System;
using System.Data.Common;
using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.Data.Sqlite;
using Compress.ZipFile;
using RomVaultX.Util;

namespace RomVaultX
{
    public static class UpdateZipDB
    {
        public static SqliteCommand CommandWriteLocalHeaderToRom
        {
            get
            {
                if (_commandWriteLocalHeaderToRom == null)
                {
                    _commandWriteLocalHeaderToRom = new SqliteCommand(@"
                        UPDATE ROM
                        SET 
                            LocalFileHeader = @LocalFileHeader,
                            LocalFileHeaderOffset = @LocalFileHeaderOffset,
                            LocalFileHeaderLength = @LocalFileHeaderLength,
                            LocalFileSha1 = @LocalFileSha1,
                            LocalFileCompressedSize = @LocalFileCompressedSize
                        WHERE
                            RomId = @RomId",
                    Program.db.Connection);

                    _commandWriteLocalHeaderToRom.Parameters.Add(new SqliteParameter("LocalFileHeader", SqliteType.Blob));
                    _commandWriteLocalHeaderToRom.Parameters.Add(new SqliteParameter("LocalFileHeaderOffset", SqliteType.Integer));
                    _commandWriteLocalHeaderToRom.Parameters.Add(new SqliteParameter("LocalFileHeaderLength", SqliteType.Integer));
                    _commandWriteLocalHeaderToRom.Parameters.Add(new SqliteParameter("LocalFileSha1", SqliteType.Text));
                    _commandWriteLocalHeaderToRom.Parameters.Add(new SqliteParameter("LocalFileCompressedSize", SqliteType.Integer));
                    _commandWriteLocalHeaderToRom.Parameters.Add(new SqliteParameter("RomId", SqliteType.Integer));
                }

                return _commandWriteLocalHeaderToRom;
            }
        }
        private static SqliteCommand? _commandWriteLocalHeaderToRom;

        public static SqliteCommand CommandWriteCentralDirToGame
        {
            get
            {
                if (_commandWriteCentralDirToGame == null)
                {
                    _commandWriteCentralDirToGame = new SqliteCommand(@"
                        UPDATE GAME
                        SET 
                            ZipFileLength = @ZipFileLength,
                            LastWriteTime = @ZipFileTimeStamp,
                            CreationTime = @ZipFileTimeStamp,
                            LastAccessTime = @ZipFileTimeStamp,
                            CentralDirectory = @CentralDirectory,
                            CentralDirectoryOffset = @CentralDirectoryOffset,
                            CentralDirectoryLength = @CentralDirectoryLength
                        WHERE
                            GameId = @GameId",
                    Program.db.Connection);

                    _commandWriteCentralDirToGame.Parameters.Add(new SqliteParameter("ZipFileLength", SqliteType.Integer));
                    _commandWriteCentralDirToGame.Parameters.Add(new SqliteParameter("ZipFileTimeStamp", SqliteType.Integer));
                    _commandWriteCentralDirToGame.Parameters.Add(new SqliteParameter("CentralDirectory", SqliteType.Blob));
                    _commandWriteCentralDirToGame.Parameters.Add(new SqliteParameter("CentralDirectoryOffset", SqliteType.Integer));
                    _commandWriteCentralDirToGame.Parameters.Add(new SqliteParameter("CentralDirectoryLength", SqliteType.Integer));
                    _commandWriteCentralDirToGame.Parameters.Add(new SqliteParameter("GameId", SqliteType.Integer));
                }

                return _commandWriteCentralDirToGame;
            }
        }
        private static SqliteCommand? _commandWriteCentralDirToGame;

        public static SqliteCommand CommandGetAllGamesWithRoms
        {
            get
            {
                if (_commandGetAllGamesWithRoms == null)
                {
                    _commandGetAllGamesWithRoms = new SqliteCommand(@"
                        SELECT
                            GameId,
                            name
                        FROM GAME
                            WHERE RomGot > 0
                            AND ZipFileLength IS null",
                    Program.db.Connection);
                }

                return _commandGetAllGamesWithRoms;
            }
        }
        private static SqliteCommand? _commandGetAllGamesWithRoms;

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
                            FILES.size,
                            FILES.compressedsize,
                            FILES.crc,
                            FILES.sha1
                        FROM ROM ,FILES
                        WHERE
                            ROM.FileId = FILES.FileId
                            AND ROM.GameId = @GameId
                            AND ROM.putinzip
                        ORDER BY ROM.RomId",
                    Program.db.Connection);

                    _commandFindRomsInGame.Parameters.Add(new SqliteParameter("GameId", SqliteType.Integer));
                }

                return _commandFindRomsInGame;
            }
        }
        private static SqliteCommand? _commandFindRomsInGame;

        public static void UpdateDB()
        {
            Program.db.ExecuteNonQuery(@"UPDATE GAME SET DirId = (SELECT DirId FROM DAT WHERE GAME.DatId = DAT.DatId) WHERE DirId IS null;");

            using (DbDataReader drGame = ZipSetGetAllGames())
            {
                int commitCount = 0;
                Program.db.Begin();

                while (drGame.Read())
                {
                    int GameId = Convert.ToInt32(drGame["GameId"]);
                    string GameName = drGame["name"].ToString();
                    Debug.WriteLine("Game " + GameId + " Name: " + GameName);

                    var memZip = new ZipFile();
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

                            memZip.ZipFileAddFake(RomName, fileOffset, size, compressedSize, CRC, out byte[] localHeader);

                            ZipSetLocalFileHeader(RomId, localHeader, fileOffset, compressedSize, SHA1);

                            fileOffset += (ulong)localHeader.Length + compressedSize;
                            commitCount += 1;
                            romCount += 1;
                        }
                    }

                    memZip.ZipFileCloseFake(fileOffset, out byte[] centeralDir);

                    if (romCount > 0)
                        ZipSetCentralFileHeader(GameId, fileOffset + (ulong)centeralDir.Length, DateTime.UtcNow.Ticks, centeralDir, fileOffset);

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