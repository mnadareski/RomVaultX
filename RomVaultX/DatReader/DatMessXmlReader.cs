using System;
using System.ComponentModel;
using System.IO;
using System.Xml;
using RomVaultX.DB;
using RomVaultX.Util;

namespace RomVaultX.DatReader
{
    public static class DatMessXmlReader
    {
        private static int _indexContinue;

        public static bool ReadDat(XmlDocument doc, string strFilename,  out RvDat rvDat)
        {
            rvDat = new RvDat();
            string filename = IO.Path.GetFileName(strFilename);
            if (!LoadHeaderFromDat(doc, rvDat, filename))
                return false;

            if (doc.DocumentElement == null)
                return false;
            XmlNodeList gameNodeList = doc.DocumentElement.SelectNodes("software");

            if (gameNodeList == null)
                return false;
            for (int i = 0; i < gameNodeList.Count; i++)
            {
                LoadGameFromDat(rvDat, gameNodeList[i]);
            }

            return true;
        }

        private static bool LoadHeaderFromDat(XmlDocument doc, RvDat rvDat, string filename)
        {
            XmlNodeList head = doc.SelectNodes("softwarelist");
            if (head == null)
                return false;

            if (head.Count == 0)
                return false;

            if (head[0].Attributes == null)
                return false;

            rvDat.Filename = filename;
            rvDat.Name= VarFix.CleanFileName(head[0].Attributes.GetNamedItem("name"));
            rvDat.Description= VarFix.String(head[0].Attributes.GetNamedItem("description"));

            /*
            string val = VarFix.String(head[0].Attributes.GetNamedItem("forcemerging")).ToLower();
            switch (val.ToLower())
            {
                case "split":
                    rvDat.MergeType= "split";
                    break;
                case "full":
                    rvDat.MergeType= "full";
                    break;
                default:
                    rvDat.MergeType= "split";
                    break;
            }
            */

            return true;
        }

        private static void LoadGameFromDat(RvDat rvDat, XmlNode gameNode)
        {
            if (gameNode.Attributes == null)
                return;

            RvGame rvGame=new RvGame();
            rvGame.Name = VarFix.CleanFileName(gameNode.Attributes.GetNamedItem("name"));
            rvGame.Description= VarFix.String(gameNode.SelectSingleNode("description"));
            rvGame.RomOf=VarFix.CleanFileName(gameNode.Attributes.GetNamedItem("cloneof"));
            rvGame.CloneOf=VarFix.CleanFileName(gameNode.Attributes.GetNamedItem("cloneof"));
            rvGame.Year= VarFix.CleanFileName(gameNode.SelectSingleNode("year"));
            rvGame.Manufacturer=VarFix.CleanFileName(gameNode.SelectSingleNode("publisher"));

            XmlNodeList partNodeList = gameNode.SelectNodes("part");
            if (partNodeList == null) return;

            for (int iP = 0; iP < partNodeList.Count; iP++)
            {
                _indexContinue = -1;
                XmlNodeList dataAreaNodeList = partNodeList[iP].SelectNodes("dataarea");
                if (dataAreaNodeList != null)
                    for (int iD = 0; iD < dataAreaNodeList.Count; iD++)
                    {
                        XmlNodeList romNodeList = dataAreaNodeList[iD].SelectNodes("rom");
                        if (romNodeList != null)
                            for (int iR = 0; iR < romNodeList.Count; iR++)
                            {
                                LoadRomFromDat(rvGame, romNodeList[iR]);
                            }
                    }
            }
            
            /*
            for (int iP = 0; iP < partNodeList.Count; iP++)
            {
                XmlNodeList diskAreaNodeList = partNodeList[iP].SelectNodes("diskarea");
                if (diskAreaNodeList != null)
                    for (int iD = 0; iD < diskAreaNodeList.Count; iD++)
                    {
                        XmlNodeList romNodeList = diskAreaNodeList[iD].SelectNodes("disk");
                        if (romNodeList != null)
                            for (int iR = 0; iR < romNodeList.Count; iR++)
                            {
                                LoadDiskFromDat(rvGameCHD, romNodeList[iR]);
                            }
                    }
            }
            */

            if (rvGame.RomCount > 0)
                rvDat.AddGame(rvGame);
            
            /*
            if (tDirCHD.ChildCount > 0)
                rvDat.AddGame(rvGameCHD, index1);
            */

        }

        private static void LoadRomFromDat(RvGame rvGame, XmlNode romNode)
        {
            if (romNode.Attributes == null)
                return;

            XmlNode name = romNode.Attributes.GetNamedItem("name");
            string loadflag = VarFix.String(romNode.Attributes.GetNamedItem("loadflag"));
            if (name != null)
            {
                RvRom rvRom=new RvRom();
                rvRom.Name = VarFix.CleanFullFileName(name);
                rvRom.Size = VarFix.ULong(romNode.Attributes.GetNamedItem("size"));
                rvRom.CRC = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("crc"), 8);
                rvRom.SHA1 = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("sha1"), 40);
                rvRom.Status = VarFix.ToLower(romNode.Attributes.GetNamedItem("status"));

                _indexContinue = rvGame.AddRom(rvRom);
            }
            else if (loadflag.ToLower() == "continue")
            {
                RvRom tROM = rvGame.Get(_indexContinue);
                tROM.Size += VarFix.ULong(romNode.Attributes.GetNamedItem("size"));
            }

        }

        /*
        private static void LoadDiskFromDat(ref RvDir tGame, XmlNode romNode)
        {
            if (romNode.Attributes == null)
                return;

            XmlNode name = romNode.Attributes.GetNamedItem("name");
            RvFile tRom = new RvFile(FileType.File)
            {
                Name = VarFix.CleanFullFileName(name) + ".chd",
                SHA1CHD = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("sha1"), 40),
                Status = VarFix.ToLower(romNode.Attributes.GetNamedItem("status")),

                Dat = tGame.Dat
            };

            if (tRom.SHA1CHD != null) tRom.FileStatusSet(FileStatus.SHA1CHDFromDAT);

            tGame.ChildAdd(tRom);
        }
        */

    }
}
