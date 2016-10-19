using System;
using System.ComponentModel;
using System.IO;
using RomVaultX.DB;
using RomVaultX.Util;

namespace RomVaultX.DatReader
{
	public static class DatRcReader
	{
		public static bool ReadDat(string strFilename, out RvDat rvDat)
		{
			FileType datFileType = FileType.Nothing;
			rvDat = new RvDat();
			int errorCode = DatFileLoader.LoadDat(strFilename);
			if (errorCode != 0)
			{
				DatUpdate.ShowDat(new Win32Exception(errorCode).Message, strFilename);
				return false;
			}

			string filename = IO.Path.GetFileName(strFilename);

			DatFileLoader.Gn();
			if (DatFileLoader.EndOfStream())
			{
				return false;
			}
			if (DatFileLoader.Next.ToLower() == "[credits]")
			{
				DatFileLoader.Gn();
				if (!LoadHeaderFromDat(filename, rvDat, out datFileType, DatFileLoader.Next.ToLower()))
				{
					return false;
				}
				DatFileLoader.Gn();
			}
			else if (DatFileLoader.Next.ToLower() == "[dat]")
			{
				DatFileLoader.Gn();
				if (!LoadHeaderFromDat(filename, rvDat, out datFileType, DatFileLoader.Next.ToLower()))
				{
					return false;
				}
				DatFileLoader.Gn();
			}
			else if (DatFileLoader.Next.ToLower() == "[emulator]")
			{
				DatFileLoader.Gn();
				if (!LoadHeaderFromDat(filename, rvDat, out datFileType, DatFileLoader.Next.ToLower()))
				{
					return false;
				}
				DatFileLoader.Gn();
			}

			// Everything else if a rom/game
			string lastgame = ""; bool foundgame = false;
			RvGame rvGame = new RvGame();
			while (!DatFileLoader.EndOfStream())
			{
				foundgame = true;
				string game = "";
				if (!LoadRomFromDat(rvGame, "", datFileType, out game))
				{
					return false;
				}
				DatFileLoader.Gn();
				
				// If we have a new game finally, add the last one
				if (lastgame != game)
				{
					rvDat.AddGame(rvGame);
					lastgame = ""; foundgame = false;
					rvGame = new RvGame();
				}
			}

			// If we had a lingering game, add it
			if (foundgame)
			{
				rvDat.AddGame(rvGame);
			}

			DatFileLoader.Close();

			return true;
		}

		private static bool LoadHeaderFromDat(string filename, RvDat rvDat, out FileType datFileType, string blockstart)
		{
			datFileType = FileType.Nothing;
			rvDat.Filename = filename;

			// Split the line by '='
			string key = DatFileLoader.Next.Split('=')[0];
			string value = DatFileLoader.Next.Remove(0, key.Length + 1);
			string block = blockstart;

			while (DatFileLoader.Next.ToLower() != "[games]")
			{
				switch (key.ToLower())
				{
					// CREDITS block
					case "[credits]":
						block = key;
						DatFileLoader.Gn();
						break;
					case "author":
						rvDat.Author = value;
						DatFileLoader.Gn();
						break;
					case "version":
						if (block == "[credits]")
						{
							rvDat.Date = value;
						}
						else if (block == "[dat]")
						{
							rvDat.Version = value;
						}
						DatFileLoader.Gn();
						break;
					case "comment":
						rvDat.Name = value;
						rvDat.Description = value;
						rvDat.Comment = value;
						DatFileLoader.Gn();
						break;

					// DAT block
					case "[dat]":
						block = key;
						DatFileLoader.Gn();
						break;
					case "plugin":
						DatFileLoader.Gn();
						break;
					case "split":
						switch (value)
						{
							case "0":
								rvDat.MergeType = (String.IsNullOrEmpty(rvDat.MergeType) ? "" : rvDat.MergeType);
								break;
							case "1":
								rvDat.MergeType = (String.IsNullOrEmpty(rvDat.MergeType) ? "split" : rvDat.MergeType);
								break;
						}
						break;
					case "merge":
						switch (value)
						{
							case "0":
								rvDat.MergeType = (String.IsNullOrEmpty(rvDat.MergeType) ? "" : rvDat.MergeType);
								break;
							case "1":
								rvDat.MergeType = (String.IsNullOrEmpty(rvDat.MergeType) ? "merge" : rvDat.MergeType);
								break;
						}
						break;

					// EMULATOR block
					case "[emulator]":
						block = key;
						DatFileLoader.Gn();
						break;
					case "refname":
						DatFileLoader.Gn();
						break;

					default:
						DatUpdate.SendAndShowDat("Error: key word '" + key  + "' not known in romcenter", DatFileLoader.Filename);
						DatFileLoader.Gn();
						break;
				}
			}

			return true;
		}

		private static bool LoadRomFromDat(RvGame rvGame, string rootName, FileType datFileType, out string game)
		{
			// Set the current game name to "" for the time being
			game = "";

			if (!DatFileLoader.Next.Contains("¬"))
			{
				DatUpdate.SendAndShowDat("¬ not found in the rom definition", DatFileLoader.Filename);
				return false;
			}

			string[] split = DatFileLoader.Next.Split('¬');
			if (String.IsNullOrEmpty(rvGame.Name))
			{
				rvGame.Name = split[3];
				rvGame.Description = split[4];
				rvGame.RomOf = split[1];
				rvGame.CloneOf = split[1];
			}

			RvRom rvRom = new RvRom
			{
				Name = split[5],
				CRC = VarFix.CleanMD5SHA1(split[6], 8),
				Size = VarFix.FixLong(split[7]),
				Merge = split[9],
			};
			rvGame.AddRom(rvRom);

			return true;
		}

		private static class DatFileLoader
		{
			public static String Filename { get; private set; }
			private static Stream _fileStream;
			private static StreamReader _streamReader;
			private static string _line = "";
			public static string Next;

			public static int LoadDat(string strFilename)
			{
				Filename = strFilename;
				_streamReader = null;
				int errorCode = IO.FileStream.OpenFileRead(strFilename, out _fileStream);
				if (errorCode != 0)
					return errorCode;
				_streamReader = new StreamReader(_fileStream, Program.Enc);
				return 0;
			}
			public static void Close()
			{
				_streamReader.Close();
				_fileStream.Close();
				_streamReader.Dispose();
				_fileStream.Dispose();
			}

			public static bool EndOfStream()
			{
				return _streamReader.EndOfStream;
			}

			public static string Gn()
			{
				while ((_line.Trim().Length == 0) && (!_streamReader.EndOfStream))
				{
					_line = _streamReader.ReadLine();
				}

				Next = _line;
				return _line; ;
			}
		}
	}
}
