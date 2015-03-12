using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using RomVaultX.DB;
using RomVaultX.DB.DBAccess;
using RomVaultX.SupportedFiles;
using RomVaultX.SupportedFiles.GZ;
using RomVaultX.SupportedFiles.Zip;
using RomVaultX.Util;
using Path = RomVaultX.IO.Path;

namespace RomVaultX
{
    public static class ReMakeZips
    {
        private static byte[] buffer = null;
        private static ulong BufferSize = 1024 * 1024;

        private static uint? _gameId;
        private static RvTreeRow _treeRow;
        private static string _outputdir;

        private static BackgroundWorker _bgw;
        public static void SetDatZipInfo(RvTreeRow treeRow, string outputDir)
        {
            _gameId = null;
            _treeRow = treeRow;
            _outputdir = outputDir;
        }
        public static void SetDatZipInfo(int gameId, string outputDir)
        {
            _gameId = (uint?)gameId;
            _treeRow = null;
            _outputdir = outputDir;
        }

        public static void MakeDatZips(object sender, DoWorkEventArgs e)
        {
            _bgw = sender as BackgroundWorker;
            Program.SyncCont = e.Argument as SynchronizationContext;
            if (Program.SyncCont == null)
            {
                _bgw = null;
                return;
            }

            if (!Directory.Exists(_outputdir))
                return;

            if (_treeRow != null)
                FindDats(_treeRow);
            else
            {
                RvGame tGame = new RvGame();
                tGame.DBRead((int)_gameId, true);
                ExtractGame(tGame, _outputdir);
            }
            _bgw.ReportProgress(0, new bgwText("Creating Zips Complete"));
            _bgw = null;
            Program.SyncCont = null;
        }

        private static void FindDats(RvTreeRow treeRow)
        {
            List<RvTreeRow> rows = RvTreeRow.ReadTreeFromDBZipRebuild(treeRow.dirFullName);
            for (int i = 0; i < rows.Count; i++)
            {
                if (_bgw.CancellationPending)
                    return;

                Debug.WriteLine(rows[i].dirName + " : " + rows[i].dirFullName);
                if (rows[i].DatId == null)
                    continue;

                string localdir = rows[i].dirFullName;
                localdir = localdir.Substring(treeRow.dirFullName.Length);
                if (rows[i].MultiDatDir)
                    localdir = Path.Combine(localdir, rows[i].datName);
                Debug.WriteLine(localdir);
                localdir = Path.Combine(_outputdir, localdir);

                ExtractZips((uint)rows[i].DatId, localdir);
            }
        }

        private static void ExtractZips(uint datId, string outDir)
        {
            if (buffer == null)
                buffer = new byte[BufferSize];

            RvDat tDat = new RvDat();
            tDat.DBRead(datId, true);

            _bgw.ReportProgress(0, new bgwSetRange(tDat.Games.Count));

            for (int gIndex = 0; gIndex < tDat.Games.Count; gIndex++)
            {
                if (_bgw.CancellationPending)
                    return;

                RvGame tGame = tDat.Games[gIndex];
                _bgw.ReportProgress(gIndex);
                _bgw.ReportProgress(0, new bgwText("Creating zip : " + tGame.Name + ".zip"));

                ExtractGame(tGame, outDir);
            }
        }
        
        private static void ExtractGame(RvGame tGame, string outDir)
        {
            if (buffer == null)
                buffer = new byte[BufferSize];

            ZipReturn zr;
            bool romGot = false;
            for (int rIndex = 0; rIndex < tGame.Roms.Count; rIndex++)
            {
                if (tGame.Roms[rIndex].FileId != null)
                {
                    romGot = true;
                    break;
                }
            }

            if (!romGot)
                return;

            // export the rom;

            ZipFile zipOut = new ZipFile();
            string filename = Path.Combine(outDir, tGame.Name + ".zip");
            filename = filename.Replace(@"/", @"\");
            if (!Directory.Exists(filename))
            {
                string dir = Path.GetDirectoryName(filename);
                Directory.CreateDirectory(dir);
            }
            zr = zipOut.ZipFileCreate(filename);
            if (zr != ZipReturn.ZipGood)
            {
                MessageBox.Show("Error creating " + Path.Combine(outDir, tGame.Name + ".zip") + " " + zr);
                return;
            }

            for (int rIndex = 0; rIndex < tGame.Roms.Count; rIndex++)
            {
                RvRom tRom = tGame.Roms[rIndex];
                if (tRom.FileId != null)
                {
                    GZip sourceGZip = new GZip();

                    string sha1 = Getfilename(GetFile.Execute((uint)tRom.FileId));

                    zr = sourceGZip.ReadGZip(sha1, false);

                    if (zr != ZipReturn.ZipGood)
                    {
                        sourceGZip.Close();
                        continue;
                    }

                    Stream outStream;
                    zipOut.ZipFileOpenWriteStream(true, true, tRom.Name, sourceGZip.uncompressedSize, 8, out outStream);

                    Stream gZipStream;
                    zr = sourceGZip.GetRawStream(out gZipStream);
                    if (zr == ZipReturn.ZipGood)
                    {
                        // write the gzip stream to the zipstream
                        ulong sizetogo = sourceGZip.compressedSize;

                        while (sizetogo > 0)
                        {
                            int sizenow = sizetogo > BufferSize ? (int)BufferSize : (int)sizetogo;

                            gZipStream.Read(buffer, 0, sizenow);
                            outStream.Write(buffer, 0, sizenow);

                            sizetogo = sizetogo - (ulong)sizenow;

                        }
                    }
                    sourceGZip.Close();

                    zipOut.ZipFileCloseWriteStream(sourceGZip.crc);
                }
            }
            zipOut.ZipFileClose();


        }

        private static string Getfilename(byte[] SHA1)
        {
            return @"RomRoot\" + VarFix.ToString(SHA1[0]) + @"\" +
                         VarFix.ToString(SHA1[1]) + @"\" +
                         VarFix.ToString(SHA1) + ".gz";

        }
    }
}
