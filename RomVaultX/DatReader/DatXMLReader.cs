using System.Xml;
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

            if (head == null)
                return false;
            string name = VarFix.CleanFileName(head.SelectSingleNode("name"));
            string rootdir = VarFix.CleanFileName(head.SelectSingleNode("rootdir"));
            string description = VarFix.String(head.SelectSingleNode("description"));
            string category = VarFix.String(head.SelectSingleNode("category"));
            string version = VarFix.String(head.SelectSingleNode("version"));
            string date = VarFix.String(head.SelectSingleNode("date"));
            string author = VarFix.String(head.SelectSingleNode("author"));
            string email = VarFix.String(head.SelectSingleNode("email"));
            string homepage = VarFix.String(head.SelectSingleNode("homepage"));
            string url = VarFix.String(head.SelectSingleNode("url"));
            string comment = VarFix.String(head.SelectSingleNode("comment"));

            DatId = DataAccessLayer.InsertIntoDat(DirId, Filename, name, rootdir, description, category, version, date, author, email, homepage, url, comment);
            /*
            string superDAT = VarFix.String(head.SelectSingleNode("type"));
            _cleanFileNames = superDAT.ToLower() != "superdat" && superDAT.ToLower() != "gigadat";
            if (!_cleanFileNames) tDat.AddData(DatReader.DatData.SuperDat, "superdat");

            // Look for:   <romvault forcepacking="unzip"/>
            XmlNode packingNode = head.SelectSingleNode("romvault");
            if (packingNode == null)
                // Look for:   <clrmamepro forcepacking="unzip"/>
                packingNode = head.SelectSingleNode("clrmamepro");
            if (packingNode != null)
            {
                if (packingNode.Attributes != null)
                {
                    string val = VarFix.String(packingNode.Attributes.GetNamedItem("forcepacking")).ToLower();
                    switch (val.ToLower())
                    {
                        case "zip":
                            tDat.AddData(DatReader.DatData.FileType, "zip");
                            break;
                        case "unzip":
                        case "file":
                            tDat.AddData(DatReader.DatData.FileType, "file");
                            break;
                        default:
                            break;
                    }

                    val = VarFix.String(packingNode.Attributes.GetNamedItem("forcemerging")).ToLower();
                    switch (val.ToLower())
                    {
                        case "split":
                            tDat.AddData(DatReader.DatData.MergeType, "split");
                            break;
                        case "full":
                            tDat.AddData(DatReader.DatData.MergeType, "full");
                            break;
                        default:
                            tDat.AddData(DatReader.DatData.MergeType, "split");
                            break;
                    }
                    val = VarFix.String(packingNode.Attributes.GetNamedItem("dir")).ToLower(); // noautodir , nogame
                    if (!String.IsNullOrEmpty(val))
                        tDat.AddData(DatReader.DatData.DirSetup, val);
                }
            }

            // Look for: <notzipped>true</notzipped>
            string notzipped = VarFix.String(head.SelectSingleNode("notzipped"));
            if (notzipped.ToLower() == "true" || notzipped.ToLower() == "yes") thisFileType = FileType.File;
            */

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
            string name = VarFix.CleanFileName(head.Attributes.GetNamedItem("build"));
            string description = VarFix.String(head.Attributes.GetNamedItem("build"));

            DatId = DataAccessLayer.InsertIntoDat(DirId, Filename, name, "", description, "", "", "", "", "", "", "", "");

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


            string name = VarFix.CleanFullFileName(gameNode.Attributes.GetNamedItem("name"));
            string romof = VarFix.CleanFileName(gameNode.Attributes.GetNamedItem("romof"));
            string description = VarFix.String(gameNode.SelectSingleNode("description"));
            string sourcefile = VarFix.String(gameNode.Attributes.GetNamedItem("sourcefile"));
            string isbios = VarFix.String(gameNode.Attributes.GetNamedItem("isbios"));
            string cloneof = VarFix.CleanFileName(gameNode.Attributes.GetNamedItem("cloneof"));
            string sampleof = VarFix.CleanFileName(gameNode.Attributes.GetNamedItem("sampleof"));
            string board = VarFix.String(gameNode.Attributes.GetNamedItem("board"));
            string year = VarFix.String(gameNode.SelectSingleNode("year"));
            string manufacturer = VarFix.String(gameNode.SelectSingleNode("manufacturer"));

            XmlNode trurip = gameNode.SelectSingleNode("trurip");
            if (trurip != null)
            {
                //string year= VarFix.String(trurip.SelectSingleNode("year"));
                string publisher = VarFix.String(trurip.SelectSingleNode("publisher"));
                string developer = VarFix.String(trurip.SelectSingleNode("developer"));
                string edition = VarFix.String(trurip.SelectSingleNode("edition"));
                string version = VarFix.String(trurip.SelectSingleNode("version"));
                string type = VarFix.String(trurip.SelectSingleNode("type"));
                string media = VarFix.String(trurip.SelectSingleNode("media"));
                string language = VarFix.String(trurip.SelectSingleNode("language"));
                string players = VarFix.String(trurip.SelectSingleNode("players"));
                string ratings = VarFix.String(trurip.SelectSingleNode("ratings"));
                string peripheral = VarFix.String(trurip.SelectSingleNode("peripheral"));
                string genre = VarFix.String(trurip.SelectSingleNode("genre"));
                string mediacatalognumber = VarFix.String(trurip.SelectSingleNode("mediacatalognumber"));
                string barcode = VarFix.String(trurip.SelectSingleNode("barcode"));
            }

            name = IO.Path.Combine(rootDir, name);

            int GameId = DataAccessLayer.InsertIntoGame(DatId, name, romof, description, sourcefile);

            XmlNodeList romNodeList = gameNode.SelectNodes("rom");
            if (romNodeList != null)
                for (int i = 0; i < romNodeList.Count; i++)
                    LoadRomFromDat(GameId, romNodeList[i]);

            XmlNodeList diskNodeList = gameNode.SelectNodes("disk");
            if (diskNodeList != null)
                for (int i = 0; i < diskNodeList.Count; i++)
                    LoadDiskFromDat(GameId, diskNodeList[i]);
        }

        private static void LoadRomFromDat(int GameId, XmlNode romNode)
        {
            if (romNode.Attributes == null)
                return;

            string Name = VarFix.CleanFullFileName(romNode.Attributes.GetNamedItem("name"));
            ulong? Size = VarFix.ULong(romNode.Attributes.GetNamedItem("size"));
            byte[] CRC = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("crc"), 8);
            byte[] SHA1 = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("sha1"), 40);
            byte[] MD5 = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("md5"), 32);
            string Merge = VarFix.CleanFullFileName(romNode.Attributes.GetNamedItem("merge"));
            string Status = VarFix.ToLower(romNode.Attributes.GetNamedItem("status"));

            DataAccessLayer.InsertIntoRom(GameId, Name, Size,CRC,SHA1,MD5);
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
