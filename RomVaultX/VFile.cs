using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;

using RomVaultX.SupportedFiles.GZ;
using RomVaultX.Util;

using DokanNet;

namespace RomVaultX
{
	public class VFile
	{
		public string FileName;
		public long Length;
		public int FileId;
		public bool IsDirectory;
		private DateTime CreationTime;
		private DateTime LastAccessTime;
		private DateTime LastWriteTime;

		public List<VZipFile> Files;

		public class VZipFile
		{
			public long LocalHeaderOffset;
			public long LocalHeaderLength;
			public byte[] LocalHeader;

			public byte[] GZipSHA1;
			public long CompressedDataOffset;
			public long CompressedDataLength;

			public GZip GZip;
		}

		public static explicit operator FileInformation(VFile v)
		{
			FileInformation fi = new FileInformation
			{
				FileName = v.FileName + (v.IsDirectory ? "" : ".zip"),
				Length = v.Length,
				Attributes = v.IsDirectory ? FileAttributes.Directory | FileAttributes.ReadOnly : FileAttributes.Normal | FileAttributes.ReadOnly,
				CreationTime = v.CreationTime,
				LastAccessTime = v.LastAccessTime,
				LastWriteTime = v.LastWriteTime
			};
			return fi;
		}

		private static string path = @"D:\tmp";
		private static string GetPath(string fileName)
		{
			return path + fileName;
		}

		public static VFile DirInfo(string dirFullname)
		{
			if (dirFullname == "\\")
			{
				VFile vfile = new VFile
				{
					FileName = dirFullname,
					IsDirectory = true,
					FileId = 0,
					CreationTime = DateTime.Today,
					LastWriteTime = DateTime.Today,
					LastAccessTime = DateTime.Today
				};
				return vfile;
			}

			string testName = dirFullname.Substring(1) + @"\";
			using (DbCommand getDirectoryId = Program.db.Command(@"select DirId,CreationTime,LastAccessTime,LastWriteTime From DIR where fullname=@FName"))
			{
				DbParameter pFName = Program.db.Parameter("FName", testName);
				getDirectoryId.Parameters.Add(pFName);

				using (DbDataReader reader = getDirectoryId.ExecuteReader())
				{
					while (reader.Read())
					{
						VFile vDir = new VFile
						{
							FileName = dirFullname,
							IsDirectory = true,
							FileId = Convert.ToInt32(reader["DirId"]),
							CreationTime = new DateTime(Convert.ToInt64(reader["CreationTime"])),
							LastAccessTime = new DateTime(Convert.ToInt64(reader["LastAccessTime"])),
							LastWriteTime = new DateTime(Convert.ToInt64(reader["LastWriteTime"]))
						};
						return vDir;
					}
				}
			}

			Alphaleonis.Win32.Filesystem.DirectoryInfo di = new Alphaleonis.Win32.Filesystem.DirectoryInfo(GetPath(dirFullname));
			if (di.Exists)
			{
				VFile vDir = new VFile
				{
					FileName = dirFullname,
					IsDirectory = true,
					FileId = -1,
					CreationTime = di.CreationTime,
					LastWriteTime = di.LastWriteTime,
					LastAccessTime = di.LastAccessTime
				};
				return vDir;
			}

			return null;
		}

		public static VFile CreateDir(string dirFullname)
		{
			Alphaleonis.Win32.Filesystem.DirectoryInfo di = Alphaleonis.Win32.Filesystem.Directory.CreateDirectory(GetPath(dirFullname));
			if (di.Exists)
			{
				VFile vDir = new VFile
				{
					FileName = dirFullname,
					IsDirectory = true,
					FileId = -1,
					CreationTime = di.CreationTime,
					LastWriteTime = di.LastWriteTime,
					LastAccessTime = di.LastAccessTime
				};
				return vDir;
			}
			return null;
		}

		public static List<VFile> DirGetSubDirs(int dirId)
		{
			List<VFile> dirs = new List<VFile>();
			using (DbCommand getDirectory = Program.db.Command(@"select DirId,name,CreationTime,LastAccessTime,LastWriteTime From DIR where ParentDirId=@ParentId"))
			{
				DbParameter pParentDirId = Program.db.Parameter("ParentId", dirId);
				getDirectory.Parameters.Add(pParentDirId);
				using (DbDataReader dr = getDirectory.ExecuteReader())
				{
					while (dr.Read())
					{
						dirs.Add(
							new VFile
							{
								IsDirectory = true,
								FileId = Convert.ToInt32(dr["DirId"]),
								FileName = (string)dr["name"],
								CreationTime = new DateTime(Convert.ToInt64(dr["CreationTime"])),
								LastAccessTime = new DateTime(Convert.ToInt64(dr["LastAccessTime"])),
								LastWriteTime = new DateTime(Convert.ToInt64(dr["LastWriteTime"]))
							}
							);
					}
				}
			}
			return dirs;
		}

		public static List<VFile> DirGetFiles(int dirId)
		{
			List<VFile> files = new List<VFile>();
			using (DbCommand getFilesInDirectory = Program.db.Command(@"select game.gameId, game.name,ZipFileLength,LastWriteTime,CreationTime,LastAccessTime from Dat,game where dat.DatId=game.datId and ZipFileLength>0 and dirId=@dirId"))
			{
				DbParameter pDirId = Program.db.Parameter("DirId", dirId);
				getFilesInDirectory.Parameters.Add(pDirId);
				using (DbDataReader dr = getFilesInDirectory.ExecuteReader())
				{
					while (dr.Read())
					{
						// Here, we want to do a check if the name contains AltDirSepChar
						// This means the file is from a SuperDAT
						// Try to add a directory at each level down until the last one
						// Then add the file to that last dir
						// Only problem is how that would be opened since it wouldn't be a valid dirid

						files.Add(
							new VFile
							{
								IsDirectory = false,
								FileId = Convert.ToInt32(dr["GameId"]),
								FileName = ((string)dr["name"]).Replace(Path.AltDirectorySeparatorChar, '¬'),
								Length = Convert.ToInt64(dr["ZipFileLength"]),
								CreationTime = new DateTime(Convert.ToInt64(dr["CreationTime"])),
								LastAccessTime = new DateTime(Convert.ToInt64(dr["LastAccessTime"])),
								LastWriteTime = new DateTime(Convert.ToInt64(dr["LastWriteTime"]))
							}
							);
					}
				}
			}
			return files;
		}

		public static VFile GetFile(string filename)
		{
			string dirPart = Alphaleonis.Win32.Filesystem.Path.GetDirectoryName(filename);
			string filePart = Alphaleonis.Win32.Filesystem.Path.GetFileNameWithoutExtension(filename);

			int? dirId = DirFind(dirPart);
			if (dirId == null)
			{
				return null;
			}

			VFile retFile = DirGetFile((int)dirId, filePart);
			if (retFile != null)
			{
				retFile.FileName = filename;
			}

			return retFile;

		}

		private static int? DirFind(string dirPart)
		{
			if (string.IsNullOrEmpty(dirPart))
			{
				return 0;
			}

			string testName = dirPart.Substring(1) + @"\";
			using (DbCommand getDirectoryId = Program.db.Command(@"select DirId From DIR where fullname=@FName"))
			{
				DbParameter pFName = Program.db.Parameter("FName", testName);
				getDirectoryId.Parameters.Add(pFName);

				object ret = getDirectoryId.ExecuteScalar();
				if (ret == null || ret == DBNull.Value)
				{
					return null;
				}
				return Convert.ToInt32(ret);
			}
		}

		private static VFile DirGetFile(int dirId, string filePart)
		{
			using (DbCommand getFileInDirectory = Program.db.Command(@"select game.gameId, ZipFileLength,LastWriteTime,CreationTime,LastAccessTime from Dat, game where dat.DatId = game.datId and ZipFileLength > 0 and dirid = @dirId and game.name = @name"))
			{
				DbParameter pDirId = Program.db.Parameter("DirId", dirId);
				getFileInDirectory.Parameters.Add(pDirId);
				DbParameter pName = Program.db.Parameter("Name", filePart.Replace('¬', Path.AltDirectorySeparatorChar));
				getFileInDirectory.Parameters.Add(pName);
				using (DbDataReader dr = getFileInDirectory.ExecuteReader())
				{
					while (dr.Read())
					{
						VFile vFile = new VFile
						{
							IsDirectory = false,
							FileId = Convert.ToInt32(dr["GameId"]),
							//FileName = filePart,
							Length = Convert.ToInt64(dr["ZipFileLength"]),
							LastWriteTime = new DateTime(Convert.ToInt64(dr["LastWriteTime"])),
							CreationTime = new DateTime(Convert.ToInt64(dr["CreationTime"])),
							LastAccessTime = new DateTime(Convert.ToInt64(dr["LastAccessTime"]))
						};
						return vFile;
					}
				}
			}
			return null;
		}

		public bool LoadVFile()
		{
			Files = new List<VZipFile>();

			using (DbCommand getRoms = Program.db.Command(
				@"SELECT
					FILES.sha1,
					FILES.compressedsize,
					LocalFileHeader,
					LocalFileHeaderOffset,
					LocalFileHeaderLength
				FROM ROM,FILES WHERE ROM.FileId=FILES.FileId AND ROM.GameId=@GameId AND putinzip
				ORDER BY Rom.RomId"))
			{
				DbParameter pGameId = Program.db.Parameter("GameId", FileId);
				getRoms.Parameters.Add(pGameId);
				using (DbDataReader dr = getRoms.ExecuteReader())
				{
					while (dr.Read())
					{
						VZipFile gf = new VZipFile
						{
							LocalHeaderOffset = Convert.ToInt64(dr["LocalFileHeaderOffset"]),
							LocalHeaderLength = Convert.ToInt64(dr["LocalFileHeaderLength"]),
							LocalHeader = (byte[])dr["LocalFileHeader"],
							GZipSHA1 = VarFix.CleanMD5SHA1(dr["sha1"].ToString(), 40),
							CompressedDataOffset = Convert.ToInt64(dr["LocalFileHeaderOffset"]) + Convert.ToInt64(dr["LocalFileHeaderLength"]),
							CompressedDataLength = Convert.ToInt64(dr["compressedsize"]),
							GZip = null // opened as needed
						};
						Files.Add(gf);
					}
				}
			}

			// the central directory is now added on to the end of the file list, like is another file with zero bytes of compressed data.
			using (DbCommand getCentralDir = Program.db.Command(
				@"SELECT
					CentralDirectory, 
					CentralDirectoryOffset, 
					CentralDirectoryLength 
				FROM game WHERE GameId=@gameId"))
			{
				DbParameter pGameId = Program.db.Parameter("GameId", FileId);
				getCentralDir.Parameters.Add(pGameId);
				using (DbDataReader dr = getCentralDir.ExecuteReader())
				{
					if (!dr.Read())
					{
						return false;
					}

					VZipFile gf = new VZipFile
					{
						LocalHeaderOffset = Convert.ToInt64(dr["CentralDirectoryOffset"]),
						LocalHeaderLength = Convert.ToInt64(dr["CentralDirectoryLength"]),
						LocalHeader = (byte[])dr["CentralDirectory"],
						GZipSHA1 = null,
						CompressedDataOffset = Convert.ToInt64(dr["CentralDirectoryOffset"]) + Convert.ToInt64(dr["CentralDirectoryLength"]),
						CompressedDataLength = 0,
						GZip = null  // not used
					};
					Files.Add(gf);
				}
			}

			return true;
		}
	}
}
