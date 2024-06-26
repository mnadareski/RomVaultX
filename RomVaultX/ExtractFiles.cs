﻿using System;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Microsoft.Data.Sqlite;
using Compress.ZipFile;
using RomVaultX.SupportedFiles.GZ;
using RomVaultX.Util;
using FileStream = RVIO.FileStream;

namespace RomVaultX
{
    internal static class ExtractFiles
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
                            FILES.size,
                            FILES.compressedsize,
                            FILES.crc,
                            FILES.sha1
                        FROM ROM, FILES
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

        public static void Extract(string dirName)
        {
            FolderBrowserDialog folderBrowserDialog1 = new FolderBrowserDialog();
            DialogResult result = folderBrowserDialog1.ShowDialog();
            if (result != DialogResult.OK) return;
            string outPath = folderBrowserDialog1.SelectedPath;

            byte[] buff = new byte[1024];

            Debug.WriteLine(dirName);

            var getfiles = new SqliteCommand(@"
                SELECT
                    DIR.fullname,
                    GameId,
                    GAME.Name
                FROM DIR, DAT, GAME
                WHERE
                    DAT.DirId = DIR.DirId
                    AND GAME.DatId = DAT.DatId
                    AND DIR.fullname LIKE '" + dirName + "%'",
            Program.db.Connection);

            DbDataReader reader = getfiles.ExecuteReader();

            while (reader.Read())
            {
                string outputFile = reader["fullname"].ToString() + reader["Name"].ToString() + ".zip";
                outputFile = outputFile.Substring(dirName.Length);

                outputFile = Path.Combine(outPath, outputFile).Replace(@"/", @"\");

                Debug.WriteLine(outputFile);

                int GameId = Convert.ToInt32(reader["GameId"]);
                string GameName = reader["name"].ToString();
                Debug.WriteLine("Game " + GameId + " Name: " + GameName);

                var memZip = new ZipFile();
                memZip.ZipCreateFake();

                ulong fileOffset = 0;

                Stream? _zipFs = null;

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
                        byte[] sha1 = VarFix.CleanMD5SHA1(drRom["sha1"].ToString(), 32);

                        Debug.WriteLine("    Rom " + RomId + " Name: " + RomName + "  Size: " + size + "  Compressed: " + compressedSize + "  CRC: " + VarFix.ToString(CRC));

                        memZip.ZipFileAddFake(RomName, fileOffset, size, compressedSize, CRC, out byte[] localHeader);

                        //ZipSetLocalFileHeader(RomId, localHeader, fileOffset);
                        if (romCount == 0)
                        {
                            ZipFile.CreateDirForFile(outputFile);
                            int errorCode = FileStream.OpenFileWrite(outputFile, out _zipFs);
                        }

                        _zipFs!.Write(localHeader, 0, localHeader.Length);

                        var GZip = new GZip();
                        string strFilename = RomRootDir.GetFilename(sha1, true);
                        GZip.ReadGZip(strFilename, false);
                        GZip.GetRawStream(out Stream oStr);

                        ulong sizetogo = compressedSize;
                        while (sizetogo > 0)
                        {
                            ulong sizenow = sizetogo > 1024 ? 1024 : sizetogo;
                            oStr.Read(buff, 0, (int)sizenow);
                            _zipFs.Write(buff, 0, (int)sizenow);
                            sizetogo -= sizenow;
                        }

                        oStr.Dispose();
                        GZip.Close();

                        fileOffset += (ulong)localHeader.Length + compressedSize;
                        _zipFs.Position = (long)fileOffset;

                        romCount += 1;
                    }
                }

                memZip.ZipFileCloseFake(fileOffset, out byte[] centeralDir);

                if (romCount > 0)
                {
                    _zipFs!.Write(centeralDir, 0, centeralDir.Length);
                    _zipFs.Flush();
                    _zipFs.Close();
                    _zipFs.Dispose();
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