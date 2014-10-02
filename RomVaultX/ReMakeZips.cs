using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
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

        public static void MakeDatZips(uint DatId)
        {
            if (buffer == null)
                buffer = new byte[BufferSize];


            ZipReturn zr;

            RvDat tDat = new RvDat();
            tDat.DBRead(DatId, true);

            string outDir = @"D:\outroms";

            for (int gIndex = 0; gIndex < tDat.Games.Count; gIndex++)
            {
                RvGame tGame = tDat.Games[gIndex];

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
                    continue;

                // export the rom;

                ZipFile zipOut = new ZipFile();
                string filename = Path.Combine(outDir, tGame.Name + ".zip");
                filename=filename.Replace(@"/",@"\");
                if (!Directory.Exists(filename))
                {
                    string dir = Path.GetDirectoryName(filename);
                    Directory.CreateDirectory(dir);
                }
                zr= zipOut.ZipFileCreate(filename);
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
                        zipOut.ZipFileOpenWriteStream(true, false, tRom.Name, sourceGZip.uncompressedSize, 8, out outStream);

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

        }

        private static string Getfilename(byte[] SHA1)
        {
            return @"RomRoot\" + VarFix.ToString(SHA1[0]) + @"\" +
                         VarFix.ToString(SHA1[1]) + @"\" +
                         VarFix.ToString(SHA1) + ".gz";

        }
    }
}
