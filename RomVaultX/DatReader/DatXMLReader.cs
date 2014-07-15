using System.Security.Policy;
using System.Xml;
using RomVaultX.DB;
using RomVaultX.Util;

namespace RomVaultX.DatReader
{
    public static class DatXmlReader
    {
        public static bool ReadDat(XmlDocument doc,int DirId, string strFilename)
        {
            string Filename = IO.Path.GetFileName(strFilename);

            int DatId;
            if (!LoadHeaderFromDat(doc, DirId, Filename, out DatId))
                return false;

            if (doc.DocumentElement == null)
                return false;

            XmlNodeList dirNodeList = doc.DocumentElement.SelectNodes("dir");
            if (dirNodeList != null)
            {
                for (int i = 0; i < dirNodeList.Count; i++)
                {
                    LoadDirFromDat(DatId, dirNodeList[i], "");
                }
            }

            XmlNodeList gameNodeList = doc.DocumentElement.SelectNodes("game");

            if (gameNodeList != null)
            {
                for (int i = 0; i < gameNodeList.Count; i++)
                {
                    LoadGameFromDat(DatId, gameNodeList[i], "");
                }
            }

            return true;
        }

        public static bool ReadMameDat(XmlDocument doc, int DirId, string strFilename)
        {
            string Filename = IO.Path.GetFileName(strFilename);

            int DatId;
            if (!LoadMameHeaderFromDat(doc, DirId, Filename, out DatId))
                return false;

            if (doc.DocumentElement == null)
                return false;

            XmlNodeList dirNodeList = doc.DocumentElement.SelectNodes("dir");
            if (dirNodeList != null)
            {
                for (int i = 0; i < dirNodeList.Count; i++)
                {
                    LoadDirFromDat(DatId, dirNodeList[i], "");
                }
            }

            XmlNodeList gameNodeList = doc.DocumentElement.SelectNodes("game");

            if (gameNodeList != null)
            {
                for (int i = 0; i < gameNodeList.Count; i++)
                {
                    LoadGameFromDat(DatId, gameNodeList[i], "");
                }
            }

            return true;
        }



        private static bool LoadHeaderFromDat(XmlDocument doc, int DirId, string Filename, out int DatId)
        {
            DatId = 0;
            if (doc.DocumentElement == null)
                return false;
            XmlNode head = doc.DocumentElement.SelectSingleNode("header");

            rvDat tDat=new rvDat();
            tDat.DirId = DirId;
            tDat.Filename = Filename;

            if (head == null)
                return false;
            tDat.Name = VarFix.CleanFileName(head.SelectSingleNode("name"));
            tDat.RootDir = VarFix.CleanFileName(head.SelectSingleNode("rootdir"));
            tDat.Description = VarFix.String(head.SelectSingleNode("description"));
            tDat.Category = VarFix.String(head.SelectSingleNode("category"));
            tDat.Version = VarFix.String(head.SelectSingleNode("version"));
            tDat.Date = VarFix.String(head.SelectSingleNode("date"));
            tDat.Author = VarFix.String(head.SelectSingleNode("author"));
            tDat.Email = VarFix.String(head.SelectSingleNode("email"));
            tDat.Homepage = VarFix.String(head.SelectSingleNode("homepage"));
            tDat.URL = VarFix.String(head.SelectSingleNode("url"));
            tDat.Comment = VarFix.String(head.SelectSingleNode("comment"));

            tDat.DbWrite();
            DatId = tDat.DatId;

            return true;
        }

        private static bool LoadMameHeaderFromDat(XmlDocument doc, int DirId, string Filename, out int DatId)
        {
            DatId = 0;
            if (doc.DocumentElement == null)
                return false;
            XmlNode head = doc.SelectSingleNode("mame");

            if (head == null || head.Attributes == null)
                return false;


            rvDat tDat = new rvDat();
            tDat.DirId = DirId;
            tDat.Filename = Filename;
            tDat.Name = VarFix.CleanFileName(head.Attributes.GetNamedItem("build"));
            tDat.Description = VarFix.String(head.Attributes.GetNamedItem("build"));

            tDat.DbWrite();
            DatId = tDat.DatId;

            return true;
        }


        private static void LoadDirFromDat(int DatId, XmlNode dirNode, string rootDir)
        {
            if (dirNode.Attributes == null)
                return;

            string fullname = VarFix.CleanFullFileName(dirNode.Attributes.GetNamedItem("name"));

            XmlNodeList dirNodeList = dirNode.SelectNodes("dir");
            if (dirNodeList != null)
            {
                for (int i = 0; i < dirNodeList.Count; i++)
                {
                    LoadDirFromDat(DatId, dirNodeList[i], IO.Path.Combine(rootDir, fullname));
                }
            }

            XmlNodeList gameNodeList = dirNode.SelectNodes("game");
            if (gameNodeList != null)
            {
                for (int i = 0; i < gameNodeList.Count; i++)
                {
                    LoadGameFromDat(DatId, gameNodeList[i], IO.Path.Combine(rootDir, fullname));
                }
            }
        }

        private static void LoadGameFromDat(int DatId, XmlNode gameNode, string rootDir)
        {
            if (gameNode.Attributes == null)
                return;

            RvGame gInfo=new RvGame();
            gInfo.DatId = DatId;
            gInfo.Name = VarFix.CleanFullFileName(gameNode.Attributes.GetNamedItem("name"));
            gInfo.RomOf = VarFix.CleanFileName(gameNode.Attributes.GetNamedItem("romof"));
            gInfo.CloneOf = VarFix.CleanFileName(gameNode.Attributes.GetNamedItem("cloneof"));
            gInfo.SampleOf = VarFix.CleanFileName(gameNode.Attributes.GetNamedItem("sampleof"));
            gInfo.Description = VarFix.String(gameNode.SelectSingleNode("description"));
            gInfo.SourceFile = VarFix.String(gameNode.Attributes.GetNamedItem("sourcefile"));
            gInfo.IsBios = VarFix.String(gameNode.Attributes.GetNamedItem("isbios"));
            gInfo.Board = VarFix.String(gameNode.Attributes.GetNamedItem("board"));
            gInfo.Year = VarFix.String(gameNode.SelectSingleNode("year"));
            gInfo.Manufacturer = VarFix.String(gameNode.SelectSingleNode("manufacturer"));

            XmlNode trurip = gameNode.SelectSingleNode("trurip");
            if (trurip != null)
            {
                gInfo.IsTrurip = true;
                gInfo.Publisher = VarFix.String(trurip.SelectSingleNode("publisher"));
                gInfo.Developer = VarFix.String(trurip.SelectSingleNode("developer"));
                gInfo.Edition = VarFix.String(trurip.SelectSingleNode("edition"));
                gInfo.Version = VarFix.String(trurip.SelectSingleNode("version"));
                gInfo.Type = VarFix.String(trurip.SelectSingleNode("type"));
                gInfo.Media = VarFix.String(trurip.SelectSingleNode("media"));
                gInfo.Language = VarFix.String(trurip.SelectSingleNode("language"));
                gInfo.Players = VarFix.String(trurip.SelectSingleNode("players"));
                gInfo.Ratings = VarFix.String(trurip.SelectSingleNode("ratings"));
                gInfo.Peripheral = VarFix.String(trurip.SelectSingleNode("peripheral"));
                gInfo.Genre = VarFix.String(trurip.SelectSingleNode("genre"));
                gInfo.MediaCatalogNumber = VarFix.String(trurip.SelectSingleNode("mediacatalognumber"));
                gInfo.BarCode = VarFix.String(trurip.SelectSingleNode("barcode"));
            }

            gInfo.Name = IO.Path.Combine(rootDir,gInfo.Name);

            gInfo.DBWrite();

            XmlNodeList romNodeList = gameNode.SelectNodes("rom");
            if (romNodeList != null)
                for (int i = 0; i < romNodeList.Count; i++)
                    LoadRomFromDat(gInfo.GameId, romNodeList[i]);

            XmlNodeList diskNodeList = gameNode.SelectNodes("disk");
            if (diskNodeList != null)
                for (int i = 0; i < diskNodeList.Count; i++)
                    LoadDiskFromDat(gInfo.GameId, diskNodeList[i]);
        }

        private static void LoadRomFromDat(int GameId, XmlNode romNode)
        {
            if (romNode.Attributes == null)
                return;

            RvRom tRom=new RvRom();
            tRom.GameId = GameId;

            tRom.Name = VarFix.CleanFullFileName(romNode.Attributes.GetNamedItem("name"));
            tRom.Size = VarFix.ULong(romNode.Attributes.GetNamedItem("size"));
            tRom.CRC = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("crc"), 8);
            tRom.SHA1 = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("sha1"), 40);
            tRom.MD5 = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("md5"), 32);
            tRom.Merge = VarFix.CleanFullFileName(romNode.Attributes.GetNamedItem("merge"));
            tRom.Status = VarFix.ToLower(romNode.Attributes.GetNamedItem("status"));

            tRom.DBWrite();
        }

        private static void LoadDiskFromDat(int GameId, XmlNode romNode)
        {
            if (romNode.Attributes == null)
                return;

            string Name = VarFix.CleanFullFileName(romNode.Attributes.GetNamedItem("name")) + ".chd";
            byte[] SHA1CHD = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("sha1"), 40);
            byte[] MD5CHD = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("md5"), 32);
            string Merge = VarFix.CleanFullFileName(romNode.Attributes.GetNamedItem("merge"));
            string Status = VarFix.ToLower(romNode.Attributes.GetNamedItem("status"));
        }

    }
}
