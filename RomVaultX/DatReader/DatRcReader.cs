using System;
using System.ComponentModel;

using RomVaultX.DB;
using RomVaultX.Util;

using Alphaleonis.Win32.Filesystem;

using Stream = System.IO.Stream;
using StreamReader = System.IO.StreamReader;

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

			string filename = Path.GetFileName(strFilename);

			DatFileLoader.Gn();
			if (DatFileLoader.EndOfStream())
			{
				return false;
			}
			if (DatFileLoader.Next.ToLower() == "[credits]")
			{
				if (!LoadHeaderFromDat(filename, rvDat, out datFileType, DatFileLoader.Next.ToLower()))
				{
					return false;
				}
				DatFileLoader.Gn();
			}
			else if (DatFileLoader.Next.ToLower() == "[dat]")
			{
				if (!LoadHeaderFromDat(filename, rvDat, out datFileType, DatFileLoader.Next.ToLower()))
				{
					return false;
				}
				DatFileLoader.Gn();
			}
			else if (DatFileLoader.Next.ToLower() == "[emulator]")
			{
				if (!LoadHeaderFromDat(filename, rvDat, out datFileType, DatFileLoader.Next.ToLower()))
				{
					return false;
				}
				DatFileLoader.Gn();
			}

			// Everything else if a rom/game
			string lastgame = "";
			bool foundgame = false;
			RvGame rvGame = new RvGame();
			while (!DatFileLoader.EndOfStream())
			{
				// Set loop variables
				foundgame = true;
				string game = "", description = "", romof = "", cloneof = "";
				RvRom rvRom = new RvRom();

				if (!LoadRomFromDat("", datFileType, out rvRom, out game, out description, out romof, out cloneof))
				{
					return false;
				}
				DatFileLoader.Gn();
				
				// If we have a new game finally, add the last one
				if (lastgame != game && lastgame != "")
				{
					rvDat.AddGame(rvGame);
					foundgame = false;
					rvGame = new RvGame();
				}

				// For everything else, add to the new rvGame
				rvGame.Name = (String.IsNullOrEmpty(rvGame.Name) ? game : rvGame.Name);
				rvGame.Description = (String.IsNullOrEmpty(rvGame.Description) ? description : rvGame.Description);
				rvGame.CloneOf = (String.IsNullOrEmpty(rvGame.CloneOf) ? cloneof : rvGame.CloneOf);
				rvGame.RomOf = (String.IsNullOrEmpty(rvGame.RomOf) ? romof : rvGame.RomOf);
				rvGame.AddRom(rvRom);
				lastgame = game;
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

			while (DatFileLoader.Next.ToLower() != "[games]")
			{
				// Split the line by '='
				string key = DatFileLoader.Next.Split('=')[0];
				string value = DatFileLoader.Next.Remove(0, key.Length + (DatFileLoader.Next.Contains("=") ? 1 : 0));
				string block = blockstart;

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
					case "url":
						rvDat.URL = value;
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
						DatFileLoader.Gn();
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
						DatFileLoader.Gn();
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
						DatUpdate.SendAndShowDat("Error on line " + DatFileLoader.LineNumber + ": key word '" + key  + "' not known in romcenter", DatFileLoader.Filename);
						DatFileLoader.Gn();
						break;
				}
			}

			return true;
		}

		private static bool LoadRomFromDat(string rootName, FileType datFileType, out RvRom rvRom, out string game, out string description, out string romof, out string cloneof)
		{
			// Set the current out vars to blank for the time being
			rvRom = new RvRom();
			game = "";
			description = "";
			romof = "";
			cloneof = "";

			if (!DatFileLoader.Next.Contains("¬"))
			{
				DatUpdate.SendAndShowDat("¬ not found in the rom definition on line " + DatFileLoader.LineNumber, DatFileLoader.Filename);
				return false;
			}

			// Some old RC DATs have this behavior
			if (DatFileLoader.Next.Contains("¬N¬O"))
			{
				DatFileLoader.Next = DatFileLoader.Next.Replace("¬N¬O", "") + "¬¬";
			}

			string[] split = DatFileLoader.Next.Split('¬');
			game = split[3];
			description = split[4];
			romof = split[1];
			cloneof = split[1];

			rvRom = new RvRom
			{
				Name = split[5],
				CRC = VarFix.CleanMD5SHA1(split[6], 8),
				Size = Int64.TryParse(split[7], out var temp) ? VarFix.FixLong(split[7]) : null,
				Merge = split[9],
			};

			return true;
		}

		private static class DatFileLoader
		{
			public static string Filename { get; private set; }
			private static Stream _fileStream;
			private static StreamReader _streamReader;
			private static string _line = "";
			public static string Next;
			public static long LineNumber = 0;

			public static int LoadDat(string strFilename)
			{
				Filename = strFilename;
				_streamReader = null;
				try
				{
					_fileStream = File.OpenRead(strFilename);
					_streamReader = new StreamReader(_fileStream, Program.Enc);
				}
				catch
				{
					return 1; // Mock error code
				}
				return 0;
			}

			public static void Close()
			{
				_streamReader.Dispose();
				_fileStream.Dispose();
			}

			public static bool EndOfStream()
			{
				return _streamReader.EndOfStream;
			}

			public static string Gn()
			{
				_line = "";
				while ((_line.Trim().Length == 0) && (!_streamReader.EndOfStream))
				{
					_line = _streamReader.ReadLine();
					LineNumber++;
				}

				Next = _line;
				return _line;
			}
		}
	}
}
