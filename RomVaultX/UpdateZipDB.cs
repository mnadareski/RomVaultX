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
                        }
                    }

                    byte[] centeralDir;
                    memZip.ZipFileCloseFake(fileOffset, out centeralDir);

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

        /*

                public static void WriteOutZips()
                {
                    DbCommand findRoms = Program.db.Command(
                        @"SELECT
                            FILES.sha1,
                            LocalFileHeader,
                            LocalFileHeaderOffset,
                            LocalFileHeaderLength
                         FROM ROM,FILES WHERE ROM.FileId=FILES.FileId AND ROM.GameId=@GameId");
                    findRoms.Parameters.Add(Program.db.Parameter("GameId"));

                    DbCommand findGames = Program.db.Command(
                        @"select 
                            gameid,
                            fullname,
                            game.name,
                            CentralDirectory,
                            CentralDirectoryOffset,
                            CentralDirectoryLength
                        from game,dat,dir where game.RomGot>0 and game.DatId=dat.DatId and dat.DirId=dir.dirid");

                    DbDataReader drGame = findGames.ExecuteReader();

                    int commitCount = 0;
                    DataAccessLayer.Begin();

                    while (drGame.Read())
                    {
                        int GameId = Convert.ToInt32(drGame["GameId"]);
                        string GameName = drGame["name"].ToString();
                        string Directory = drGame["fullname"].ToString();
                        Debug.WriteLine("Game " + GameId + " Name: " + Directory + GameName);

                        Stream _zipFs;
                        string outZipFilename = @"D:\tmpout\" + Directory + GameName + @".Zip";
                        ZipFile.CreateDirForFile(outZipFilename);
                        IO.FileStream.OpenFileWrite(outZipFilename, out _zipFs);

                        findRoms.Parameters["GameId"].Value = GameId;
                        DbDataReader drRom = findRoms.ExecuteReader();

                        while (drRom.Read())
                        {
                            byte[] SHA1 = VarFix.CleanMD5SHA1(drRom["sha1"].ToString(), 20);
                            byte[] localheader = (byte[])drRom["LocalFileHeader"];

                            string strFilename = Getfilename(SHA1);


                            GZip gzFile = new GZip();
                            gzFile.ReadGZip(strFilename, false);

                            Debug.WriteLine(gzFile.compressedSize);

                            _zipFs.Write(localheader, 0, localheader.Length);

                            Stream coms;
                            gzFile.GetRawStream(out coms);
                            byte[] buffer = new byte[1024];
                            ulong sizetogo = gzFile.compressedSize;

                            while (sizetogo > 0)
                            {
                                int sizenow = sizetogo > 1024 ? 1024 : (int)sizetogo;

                                coms.Read(buffer, 0, sizenow);
                                _zipFs.Write(buffer, 0, sizenow);

                                sizetogo = sizetogo - (ulong)sizenow;
                            }
                            coms.Close();

                            gzFile.Close();
                        }
                        drRom.Close();
                        drRom.Dispose();

                        byte[] centraldir = (byte[])drGame["CentralDirectory"];
                        _zipFs.Write(centraldir, 0, centraldir.Length);
                        _zipFs.Flush();
                        _zipFs.Close();
                        _zipFs.Dispose();
                    }
                }

                private static string Getfilename(byte[] SHA1)
                {
                    return @"RomRoot\" + VarFix.ToString(SHA1[0]) + @"\" +
                                 VarFix.ToString(SHA1[1]) + @"\" +
                                 VarFix.ToString(SHA1) + ".gz";

                }
          */
    }
}
