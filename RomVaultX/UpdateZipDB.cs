using System;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using RomVaultX.DB;
using RomVaultX.SupportedFiles.GZ;
using RomVaultX.SupportedFiles.Zip;
using RomVaultX.Util;
using System.Windows.Forms;

namespace RomVaultX
{
    public static class UpdateZipDB
    {

        public static void UpdateDB()
        {
            SQLiteCommand writeLocalHeaderToRom = new SQLiteCommand(
                @"UPDATE ROM SET 
                    LocalFileHeader=@localFileHeader,
                    LocalFileHeaderOffset=@localFileHeaderOffset,
                    LocalFileHeaderLength=@localFileHeaderLength
                WHERE
                    RomId=@romID", DataAccessLayer.DBConnection);
            writeLocalHeaderToRom.Parameters.Add(new SQLiteParameter("localFileHeader", DbType.Binary));
            writeLocalHeaderToRom.Parameters.Add(new SQLiteParameter("localFileHeaderOffset"));
            writeLocalHeaderToRom.Parameters.Add(new SQLiteParameter("localFileHeaderLength"));
            writeLocalHeaderToRom.Parameters.Add(new SQLiteParameter("RomId"));

            SQLiteCommand writeCentralDirToGame = new SQLiteCommand(
                @"UPDATE GAME SET 
                    ZipFileLength=@zipFileLength,
                    ZipFileTimeStamp=@zipFileTimeStamp,
                    CentralDirectory=@centralDirectory,
                    CentralDirectoryOffset=@centralDirectoryOffset,
                    CentralDirectoryLength=@centralDirectoryLength
                WHERE
                    GameId=@gameID", DataAccessLayer.DBConnection);
            writeCentralDirToGame.Parameters.Add(new SQLiteParameter("zipFileLength"));
            writeCentralDirToGame.Parameters.Add(new SQLiteParameter("zipFileTimeStamp"));
            writeCentralDirToGame.Parameters.Add(new SQLiteParameter("centralDirectory", DbType.Binary));
            writeCentralDirToGame.Parameters.Add(new SQLiteParameter("centralDirectoryOffset"));
            writeCentralDirToGame.Parameters.Add(new SQLiteParameter("centralDirectoryLength"));
            writeCentralDirToGame.Parameters.Add(new SQLiteParameter("GameId"));



            SQLiteCommand findRoms = new SQLiteCommand(
                @"SELECT
                    ROM.RomId, 
                    ROM.name,
                    FILES.size,
                    FILES.compressedsize,
                    FILES.crc
                 FROM ROM,FILES WHERE ROM.FileId=FILES.FileId AND ROM.GameId=@GameId ORDER BY ROM.name", DataAccessLayer.DBConnection);
            findRoms.Parameters.Add(new SQLiteParameter("GameId"));

            SQLiteCommand findGames = new SQLiteCommand(
                @"SELECT GameId,name FROM game WHERE RomGot>0 AND ZipFileLength is null", DataAccessLayer.DBConnection);

            SQLiteDataReader drGame = findGames.ExecuteReader();

            int commitCount = 0;
            DataAccessLayer.Begin();

            while (drGame.Read())
            {
                int GameId = Convert.ToInt32(drGame["GameId"]);
                string GameName = drGame["name"].ToString();
                Debug.WriteLine("Game " + GameId + " Name: " + GameName);

                ZipFile memZip = new ZipFile();
                memZip.ZipCreateFake();

                findRoms.Parameters["GameId"].Value = GameId;
                SQLiteDataReader drRom = findRoms.ExecuteReader();

                ulong fileOffset = 0;

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

                    writeLocalHeaderToRom.Parameters["localFileHeader"].Value = localHeader;
                    writeLocalHeaderToRom.Parameters["localFileHeaderOffset"].Value = fileOffset;
                    writeLocalHeaderToRom.Parameters["localFileHeaderLength"].Value = localHeader.Length;
                    writeLocalHeaderToRom.Parameters["RomId"].Value = RomId;
                    writeLocalHeaderToRom.ExecuteNonQuery();

                    fileOffset += (ulong)localHeader.Length + compressedSize;
                    commitCount += 1;
                }
                drRom.Close();
                drRom.Dispose();

                byte[] centeralDir;
                memZip.ZipFileCloseFake(fileOffset, out centeralDir);

                writeCentralDirToGame.Parameters["zipFileLength"].Value = fileOffset+(ulong)centeralDir.Length;
                writeCentralDirToGame.Parameters["zipFileTimeStamp"].Value = DateTime.UtcNow.Ticks;
                writeCentralDirToGame.Parameters["centralDirectory"].Value = centeralDir;
                writeCentralDirToGame.Parameters["centralDirectoryOffset"].Value = fileOffset;
                writeCentralDirToGame.Parameters["centralDirectoryLength"].Value = centeralDir.Length;
                writeCentralDirToGame.Parameters["GameId"].Value = GameId;
                writeCentralDirToGame.ExecuteNonQuery();

                if (commitCount >= 100)
                {
                    DataAccessLayer.Commit();
                    DataAccessLayer.Begin();
                    commitCount = 0;
                }

            }
            drGame.Close();
            drGame.Dispose();

            DataAccessLayer.Commit();

            MessageBox.Show("Zip Header Database Update Complete");


        }



        public static void WriteOutZips()
        {
            SQLiteCommand findRoms = new SQLiteCommand(
                @"SELECT
                    FILES.sha1,
                    LocalFileHeader,
                    LocalFileHeaderOffset,
                    LocalFileHeaderLength
                 FROM ROM,FILES WHERE ROM.FileId=FILES.FileId AND ROM.GameId=@GameId", DataAccessLayer.DBConnection);
            findRoms.Parameters.Add(new SQLiteParameter("GameId"));

            SQLiteCommand findGames = new SQLiteCommand(
                @"select 
                    gameid,
                    fullname,
                    game.name,
                    CentralDirectory,
                    CentralDirectoryOffset,
                    CentralDirectoryLength
                from game,dat,dir where game.RomGot>0 and game.DatId=dat.DatId and dat.DirId=dir.dirid", DataAccessLayer.DBConnection);

            SQLiteDataReader drGame = findGames.ExecuteReader();

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
                SQLiteDataReader drRom = findRoms.ExecuteReader();

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
    }
}
