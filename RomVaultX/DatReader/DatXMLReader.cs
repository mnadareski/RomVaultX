using System.Xml;

using RomVaultX.DB;
using RomVaultX.Util;

using Alphaleonis.Win32.Filesystem;

namespace RomVaultX.DatReader
{
	public static class DatXmlReader
	{
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

			XmlNodeList dirNodeList = doc.DocumentElement.SelectNodes("dir");
			if (dirNodeList != null)
			{
				for (int i = 0; i < dirNodeList.Count; i++)
				{
					LoadDirFromDat(rvDat, dirNodeList[i], "");
				}
			}

			XmlNodeList gameNodeList = doc.DocumentElement.SelectNodes("game");
			if (gameNodeList != null)
			{
				for (int i = 0; i < gameNodeList.Count; i++)
				{
					LoadGameFromDat(rvDat, gameNodeList[i], "");
				}
			}

			XmlNodeList machineNodeList = doc.DocumentElement.SelectNodes("machine");
			if (machineNodeList != null)
			{
				for (int i = 0; i < machineNodeList.Count; i++)
				{
					LoadGameFromDat(rvDat, machineNodeList[i], "");
				}
			}

			return true;
		}

		public static bool ReadMameDat(XmlDocument doc, string strFilename, out RvDat rvDat)
		{
			rvDat = new RvDat();
			string filename = Path.GetFileName(strFilename);

			if (!LoadMameHeaderFromDat(doc, rvDat, filename))
			{
				return false;
			}

			if (doc.DocumentElement == null)
			{
				return false;
			}

			XmlNodeList dirNodeList = doc.DocumentElement.SelectNodes("dir");
			if (dirNodeList != null)
			{
				for (int i = 0; i < dirNodeList.Count; i++)
				{
					LoadDirFromDat(rvDat, dirNodeList[i], "");
				}
			}

			XmlNodeList gameNodeList = doc.DocumentElement.SelectNodes("game");
			if (gameNodeList != null)
			{
				for (int i = 0; i < gameNodeList.Count; i++)
				{
					LoadGameFromDat(rvDat, gameNodeList[i], "");
				}
			}

			XmlNodeList machineNodeList = doc.DocumentElement.SelectNodes("machine");
			if (machineNodeList != null)
			{
				for (int i = 0; i < machineNodeList.Count; i++)
				{
					LoadGameFromDat(rvDat, machineNodeList[i], "");
				}
			}

			return true;
		}

		private static bool LoadHeaderFromDat(XmlDocument doc, RvDat rvDat, string filename)
		{
			if (doc.DocumentElement == null)
			{
				return false;
			}
			XmlNode head = doc.DocumentElement.SelectSingleNode("header");

			rvDat.Filename = filename;

			if (head == null)
			{
				return false;
			}
			rvDat.Name = VarFix.CleanFileName(head.SelectSingleNode("name"));
			rvDat.RootDir = VarFix.CleanFileName(head.SelectSingleNode("rootdir"));
			rvDat.Description = VarFix.StringFromXmlNode(head.SelectSingleNode("description"));
			rvDat.Category = VarFix.StringFromXmlNode(head.SelectSingleNode("category"));
			rvDat.Version = VarFix.StringFromXmlNode(head.SelectSingleNode("version"));
			rvDat.Date = VarFix.StringFromXmlNode(head.SelectSingleNode("date"));
			rvDat.Author = VarFix.StringFromXmlNode(head.SelectSingleNode("author"));
			rvDat.Email = VarFix.StringFromXmlNode(head.SelectSingleNode("email"));
			rvDat.Homepage = VarFix.StringFromXmlNode(head.SelectSingleNode("homepage"));
			rvDat.URL = VarFix.StringFromXmlNode(head.SelectSingleNode("url"));
			rvDat.Comment = VarFix.StringFromXmlNode(head.SelectSingleNode("comment"));

			XmlNode packingNode = head.SelectSingleNode("romvault") ?? head.SelectSingleNode("clrmamepro");

			if (packingNode?.Attributes != null)
			{
				rvDat.MergeType = VarFix.StringFromXmlNode(packingNode.Attributes.GetNamedItem("forcemerging")).ToLower();
			}

			return true;
		}

		private static bool LoadMameHeaderFromDat(XmlDocument doc, RvDat rvDat, string filename)
		{
			if (doc.DocumentElement == null)
			{
				return false;
			}
			XmlNode head = doc.SelectSingleNode("mame");

			if (head?.Attributes == null)
			{
				return false;
			}

			rvDat.Filename = filename;
			rvDat.Name = VarFix.CleanFileName(head.Attributes.GetNamedItem("build"));   /// ?? is this correct should it be Name & Description??
			rvDat.Description = VarFix.StringFromXmlNode(head.Attributes.GetNamedItem("build"));

			return true;
		}

		private static void LoadDirFromDat(RvDat rvDat, XmlNode dirNode, string rootDir)
		{
			if (dirNode.Attributes == null)
			{
				return;
			}

			string fullname = VarFix.CleanFullFileName(dirNode.Attributes.GetNamedItem("name"));

			XmlNodeList dirNodeList = dirNode.SelectNodes("dir");
			if (dirNodeList != null)
			{
				for (int i = 0; i < dirNodeList.Count; i++)
				{
					LoadDirFromDat(rvDat, dirNodeList[i], Path.Combine(rootDir, fullname));
				}
			}

			XmlNodeList gameNodeList = dirNode.SelectNodes("game");
			if (gameNodeList != null)
			{
				for (int i = 0; i < gameNodeList.Count; i++)
				{
					LoadGameFromDat(rvDat, gameNodeList[i], Path.Combine(rootDir, fullname));
				}
			}
		}

		private static void LoadGameFromDat(RvDat rvDat, XmlNode gameNode, string rootDir)
		{
			if (gameNode.Attributes == null)
			{
				return;
			}

			RvGame rvGame = new RvGame
			{
				Name = VarFix.CleanFullFileName(gameNode.Attributes.GetNamedItem("name")),
				RomOf = VarFix.CleanFileName(gameNode.Attributes.GetNamedItem("romof")),
				CloneOf = VarFix.CleanFileName(gameNode.Attributes.GetNamedItem("cloneof")),
				SampleOf = VarFix.CleanFileName(gameNode.Attributes.GetNamedItem("sampleof")),
				Description = VarFix.StringFromXmlNode(gameNode.SelectSingleNode("description")),
				SourceFile = VarFix.StringFromXmlNode(gameNode.Attributes.GetNamedItem("sourcefile")),
				IsBios = VarFix.StringFromXmlNode(gameNode.Attributes.GetNamedItem("isbios")),
				Board = VarFix.StringFromXmlNode(gameNode.Attributes.GetNamedItem("board")),
				Year = VarFix.StringFromXmlNode(gameNode.SelectSingleNode("year")),
				Manufacturer = VarFix.StringFromXmlNode(gameNode.SelectSingleNode("manufacturer"))
			};

			XmlNode trurip = gameNode.SelectSingleNode("trurip");
			if (trurip != null)
			{
				rvGame.IsTrurip = true;
				rvGame.Publisher = VarFix.StringFromXmlNode(trurip.SelectSingleNode("publisher"));
				rvGame.Developer = VarFix.StringFromXmlNode(trurip.SelectSingleNode("developer"));
				rvGame.Edition = VarFix.StringFromXmlNode(trurip.SelectSingleNode("edition"));
				rvGame.Version = VarFix.StringFromXmlNode(trurip.SelectSingleNode("version"));
				rvGame.Type = VarFix.StringFromXmlNode(trurip.SelectSingleNode("type"));
				rvGame.Media = VarFix.StringFromXmlNode(trurip.SelectSingleNode("media"));
				rvGame.Language = VarFix.StringFromXmlNode(trurip.SelectSingleNode("language"));
				rvGame.Players = VarFix.StringFromXmlNode(trurip.SelectSingleNode("players"));
				rvGame.Ratings = VarFix.StringFromXmlNode(trurip.SelectSingleNode("ratings"));
				rvGame.Peripheral = VarFix.StringFromXmlNode(trurip.SelectSingleNode("peripheral"));
				rvGame.Genre = VarFix.StringFromXmlNode(trurip.SelectSingleNode("genre"));
				rvGame.MediaCatalogNumber = VarFix.StringFromXmlNode(trurip.SelectSingleNode("mediacatalognumber"));
				rvGame.BarCode = VarFix.StringFromXmlNode(trurip.SelectSingleNode("barcode"));
			}

			rvGame.Name = Path.Combine(rootDir, rvGame.Name);

			rvDat.AddGame(rvGame);

			XmlNodeList romNodeList = gameNode.SelectNodes("rom");
			if (romNodeList != null)
			{
				for (int i = 0; i < romNodeList.Count; i++)
				{
					LoadRomFromDat(rvGame, romNodeList[i]);
				}
			}

			XmlNodeList diskNodeList = gameNode.SelectNodes("disk");
			if (diskNodeList != null)
			{
				for (int i = 0; i < diskNodeList.Count; i++)
				{
					LoadDiskFromDat(rvGame, diskNodeList[i]);
				}
			}
		}

		private static void LoadRomFromDat(RvGame rvGame, XmlNode romNode)
		{
			if (romNode.Attributes == null)
			{
				return;
			}

			RvRom rvRom = new RvRom
			{
				Name = VarFix.CleanFullFileName(romNode.Attributes.GetNamedItem("name")),
				Size = VarFix.ULong(romNode.Attributes.GetNamedItem("size")),
				CRC = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("crc"), 8),
				SHA1 = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("sha1"), 40),
				MD5 = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("md5"), 32),
				Merge = VarFix.CleanFullFileName(romNode.Attributes.GetNamedItem("merge")),
				Status = VarFix.ToLower(romNode.Attributes.GetNamedItem("status"))
			};

			rvGame.AddRom(rvRom);
		}

		private static void LoadDiskFromDat(RvGame rvGame, XmlNode romNode)
		{
			if (romNode.Attributes == null)
			{
				return;
			}

			RvRom rvRom = new RvRom
			{
				Name = VarFix.CleanFullFileName(romNode.Attributes.GetNamedItem("name")) + ".chd",
				Size = VarFix.ULong(romNode.Attributes.GetNamedItem("size")),
				SHA1CHD = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("sha1"), 40),
				FileSHA1 = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("sha1"), 40),
				MD5CHD = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("md5"), 32),
				FileMD5 = VarFix.CleanMD5SHA1(romNode.Attributes.GetNamedItem("md5"), 32),
				Merge = VarFix.CleanFullFileName(romNode.Attributes.GetNamedItem("merge")),
				Status = VarFix.ToLower(romNode.Attributes.GetNamedItem("status")),
				AltType = FileType.CHD,
			};

			rvGame.AddRom(rvRom);
		}
	}
}
