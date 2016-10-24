using System.Xml;
using RomVaultX.DB;
using RomVaultX.Util;
using Alphaleonis.Win32.Filesystem;

namespace RomVaultX.DatReader
{
	public static class DatMessXmlReader
	{
		private static int _indexContinue;

		public static bool ReadDat(XmlDocument doc, string strFilename, out RvDat rvDat)
		{
			rvDat = new RvDat();
			string filename = Path.GetFileName(strFilename);
			if (!LoadHeaderFromDat(doc, rvDat, filename))
			{
				return false;
			}

			if (doc.DocumentElement == null)
			{
				return false;
			}
			XmlNodeList gameNodeList = doc.DocumentElement.SelectNodes("software");

			if (gameNodeList == null)
			{
				return false;
			}
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
			{
				return false;
			}

			if (head.Count == 0)
			{
				return false;
			}

			if (head[0].Attributes == null)
			{
				return false;
			}

			rvDat.Filename = filename;
			rvDat.Name = VarFix.CleanFileName(head[0].Attributes.GetNamedItem("name"));
			rvDat.Description = VarFix.StringFromXmlNode(head[0].Attributes.GetNamedItem("description"));

			return true;
		}

		private static void LoadGameFromDat(RvDat rvDat, XmlNode gameNode)
		{
			if (gameNode.Attributes == null)
			{
				return;
			}

			RvGame rvGame = new RvGame
			{
				Name = VarFix.CleanFileName(gameNode.Attributes.GetNamedItem("name")),
				Description = VarFix.StringFromXmlNode(gameNode.SelectSingleNode("description")),
				RomOf = VarFix.CleanFileName(gameNode.Attributes.GetNamedItem("cloneof")),
				CloneOf = VarFix.CleanFileName(gameNode.Attributes.GetNamedItem("cloneof")),
				Year = VarFix.CleanFileName(gameNode.SelectSingleNode("year")),
				Manufacturer = VarFix.CleanFileName(gameNode.SelectSingleNode("publisher"))
			};

			XmlNodeList partNodeList = gameNode.SelectNodes("part");
			if (partNodeList == null)
			{
				return;
			}

			for (int iP = 0; iP < partNodeList.Count; iP++)
			{
				_indexContinue = -1;
				XmlNodeList dataAreaNodeList = partNodeList[iP].SelectNodes("dataarea");
				if (dataAreaNodeList == null)
				{
					continue;
				}
				for (int iD = 0; iD < dataAreaNodeList.Count; iD++)
				{
					XmlNodeList romNodeList = dataAreaNodeList[iD].SelectNodes("rom");
					if (romNodeList == null)
					{
						continue;
					}
					for (int iR = 0; iR < romNodeList.Count; iR++)
					{
						LoadRomFromDat(rvGame, romNodeList[iR]);
					}
				}
			}

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
								LoadDiskFromDat(rvGame, romNodeList[iR]);
							}
					}
			}

			if (rvGame.RomCount > 0)
			{
				rvDat.AddGame(rvGame);
			}
		}

		private static void LoadRomFromDat(RvGame rvGame, XmlNode romNode)
		{
			if (romNode.Attributes == null)
			{
				return;
			}

			XmlNode name = romNode.Attributes.GetNamedItem("name");
			string loadflag = VarFix.StringFromXmlNode(romNode.Attributes.GetNamedItem("loadflag"));
			if (name != null)
			{
				RvRom rvRom = new RvRom();
				rvRom.Name = VarFix.CleanFullFileName(name);
				rvRom.Size = VarFix.ULong(romNode.Attributes.GetNamedItem("size"));
				rvRom.CRC = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("crc"), 8);
				rvRom.SHA1 = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("sha1"), 40);
				rvRom.Status = VarFix.ToLower(romNode.Attributes.GetNamedItem("status"));

				_indexContinue = rvGame.AddRom(rvRom);
			}
			else if (loadflag.ToLower() == "continue")
			{
				RvRom tROM = rvGame.Roms[_indexContinue];
				tROM.Size += VarFix.ULong(romNode.Attributes.GetNamedItem("size"));
			}
		}

		private static void LoadDiskFromDat(RvGame rvGame, XmlNode romNode)
		{
			if (romNode.Attributes == null)
			{
				return;
			}

			string Name = VarFix.CleanFullFileName(romNode.Attributes.GetNamedItem("name")) + ".chd";
			byte[] SHA1CHD = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("sha1"), 40);
			byte[] MD5CHD = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("md5"), 32);
			string Merge = VarFix.CleanFullFileName(romNode.Attributes.GetNamedItem("merge"));
			string Status = VarFix.ToLower(romNode.Attributes.GetNamedItem("status"));
		}
	}
}
