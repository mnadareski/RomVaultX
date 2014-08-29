using System.Configuration;
using System.IO;
using RomVaultX.DB;
using RomVaultX.SupportedFiles;
using RomVaultX.SupportedFiles.GZ;
using RomVaultX.SupportedFiles.Zip;
using Path = RomVaultX.IO.Path;

namespace RomVaultX
{
    public static class ReMakeZips
    {
        public static void MakeDatZips(uint DatId)
        {
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
                    break;

                // export the rom;

                ZipFile zipOut=new ZipFile();
                zipOut.ZipFileCreate(Path.Combine(outDir, tGame.Name + ".zip"));

                for (int rIndex = 0; rIndex < tGame.Roms.Count; rIndex++)
                {
                    RvRom tRom = tGame.Roms[rIndex];
                    if (tRom.FileId != null)
                    {
                        GZip sourceGZip=new GZip();

                        string sourceFilename = "1234";
                        zr = sourceGZip.ReadGZip(sourceFilename, false);

                        if (zr != ZipReturn.ZipGood)
                        {
                            sourceGZip.Close();
                            continue;
                        }

                        Stream outStream;
                        zipOut.ZipFileOpenWriteStream(true, false, tRom.Name, sourceGZip.uncompressedSize, 8, out outStream);

                        Stream gZipStream;
                        zr=sourceGZip.GetStream(out gZipStream);
                        if (zr == ZipReturn.ZipGood)
                        {
                            // write the gzip stream to the zipstream
                            
                        }


                        zipOut.ZipFileCloseWriteStream(sourceGZip.crc);
                    }
                }
                zipOut.ZipFileClose();

            }

        }
    }
}
