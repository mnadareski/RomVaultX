using System;
using System.ComponentModel;
using System.Data.Common;
using System.Data.SQLite;

using Alphaleonis.Win32.Filesystem;

namespace RomVaultX.DB
{
	public class DBSqlite
	{
		private const int DBVersion = 7;
		private string _dbFilename;
		public SQLiteConnection Connection;

		public string ConnectToDB()
		{
			_dbFilename = AppSettings.ReadSetting("DBFileName");
			if (_dbFilename == null)
			{
				AppSettings.AddUpdateAppSettings("DBFileName", "rom");
				_dbFilename = AppSettings.ReadSetting("DBFileName");
			}
			string dbMemCacheSize = AppSettings.ReadSetting("DBMemCacheSize");
			if (dbMemCacheSize == null)
			{
				// I use 8000000
				AppSettings.AddUpdateAppSettings("DBMemCacheSize", "2000");
				dbMemCacheSize = AppSettings.ReadSetting("DBMemCacheSize");
			}

			_dbFilename += DBVersion + ".db3";

			bool datFound = File.Exists(_dbFilename);

			Connection = new SQLiteConnection(@"data source=" + _dbFilename + ";Version=3");
			Connection.Open();

			ExecuteNonQuery("PRAGMA temp_store = MEMORY");
			ExecuteNonQuery("PRAGMA cache_size = -" + dbMemCacheSize);
			//ExecuteNonQuery("PRAGMA journal_mode = MEMORY");
			ExecuteNonQuery("PRAGMA journal_mode = PERSIST");
			ExecuteNonQuery("PRAGMA threads = 7");

			string dbCheckOnStartup = AppSettings.ReadSetting("DBCheckOnStartup");
			if (dbCheckOnStartup == null)
			{
				AppSettings.AddUpdateAppSettings("DBCheckOnStartup", "false");
				dbCheckOnStartup = AppSettings.ReadSetting("DBCheckOnStartup");
			}

			if (dbCheckOnStartup.ToLower() == "true")
			{
				DbCommand dbCheck = new SQLiteCommand(@"PRAGMA quick_check;", Connection);
				object res = dbCheck.ExecuteScalar();
				string sRes = res.ToString();

				if (sRes != "ok")
				{
					return sRes;
				}
			}

			CheckDbVersion(ref datFound);
			if (!datFound)
			{
				MakeDB();
			}
			MakeIndex();

			return null;
		}

		private void CheckDbVersion(ref bool datFound)
		{
			if (!datFound)
			{
				return;
			}

			int testVersion = 0;
			try
			{
				DbCommand dbVersionCommand = new SQLiteCommand(@"SELECT version from version limit 1", Connection);
				object res = dbVersionCommand.ExecuteScalar();

				if (res != null && res != DBNull.Value)
				{
					testVersion = System.Convert.ToInt32(res);
				}

				if (testVersion == DBVersion)
				{
					return;
				}
			}
			catch (Exception)
			{
			}

			Connection.Close();
			File.Delete(_dbFilename);
			Connection.Open();
			datFound = false;
		}

		public void ExecuteNonQuery(string query, params object[] args)
		{
			using (SQLiteCommand command = new SQLiteCommand(query, Connection))
			{
				for (int i = 0; i < args.Length; i += 2)
				{
					command.Parameters.Add(new SQLiteParameter(args[i].ToString(), args[i + 1]));
				}

				command.ExecuteNonQuery();
			}
		}

		private void MakeDB()
		{
			/******** Create Tables ***********/

			ExecuteNonQuery(@"
				CREATE TABLE IF NOT EXISTS [VERSION] (
					[Version] INTEGER NOT NULL);
				INSERT INTO VERSION (version) VALUES (@Version);",
				"version", DBVersion);

			RvDir.CreateTable();
			RvDat.CreateTable();
			RvGame.CreateTable();
			RvFile.CreateTable();
			RvRom.CreateTable();

			/******** Create Triggers ***********/

			/**** FILE Triggers ****/
			/*INSERT*/
			ExecuteNonQuery(@"
				DROP TRIGGER IF EXISTS [FileInsert];
				");

			/*DELETE*/
			ExecuteNonQuery(@"
				DROP TRIGGER IF EXISTS [FileDelete];
				CREATE TRIGGER IF NOT EXISTS [FileDelete] 
				AFTER DELETE ON [FILES] 
				FOR EACH ROW 
				BEGIN 
					UPDATE ROM SET 
						FileId=null,
						LocalFileHeader=null,
						LocalFileHeaderOffset=null,
						LocalFileHeaderLength=null 
					WHERE 
						FileId=OLD.FileId;
				END;
			");

			//**** ROM Triggers ****
			//INSERT
			ExecuteNonQuery(@"
				DROP TRIGGER IF EXISTS [RomInsert];
				CREATE TRIGGER IF NOT EXISTS [RomInsert] 
				AFTER INSERT ON [ROM] 
				FOR EACH ROW
				BEGIN 
					UPDATE GAME SET
						RomTotal = RomTotal + 1,
						RomGot = RomGot + (IFNULL(New.FileId,0)>0),
						RomNoDump = RomNoDump + (IFNULL(New.status ='nodump' and New.crc is null and New.sha1 is null and New.md5 is null,0)),
						ZipFileLength=null,
						LastWriteTime=null,
						CreationTime=null,
						LastAccessTime=null,
						CentralDirectory=null,
						CentralDirectoryOffset=null,
						CentralDirectoryLength=null
					WHERE 
						Game.GameId = New.GameId;
				END;
			");
			//DELETE
			ExecuteNonQuery(@"
				DROP TRIGGER IF EXISTS [RomDelete];
				CREATE TRIGGER IF NOT EXISTS [RomDelete] 
				AFTER DELETE ON [ROM] 
				FOR EACH ROW
				BEGIN 
					UPDATE GAME SET
						RomTotal = RomTotal - 1,
						RomGot = RomGot - (IFNULL(Old.FileId,0)>0),
						RomNoDump = RomNoDump - (IFNULL(Old.status ='nodump' and Old.crc is null and Old.sha1 is null and Old.md5 is null,0)),
						ZipFileLength=null,
						LastWriteTime=null,
						CreationTime=null,
						LastAccessTime=null,
						CentralDirectory=null,
						CentralDirectoryOffset=null,
						CentralDirectoryLength=null
					WHERE 
						Game.GameId = Old.GameId;
				END;
			");
			//UPDATE
			ExecuteNonQuery(@"
				DROP TRIGGER IF EXISTS [RomUpdate];
				CREATE TRIGGER IF NOT EXISTS [RomUpdate]
				AFTER UPDATE ON [ROM]
				FOR EACH ROW WHEN (IFNULL(Old.FileId,0)>0) != (IFNULL(New.FileId,0)>0)
				BEGIN 
					UPDATE GAME SET
						RomGot = RomGot - (IFNULL(Old.FileId,0)>0) + (IFNULL(New.FileId,0)>0),
						ZipFileLength=null,
						LastWriteTime=null,
						CreationTime=null,
						LastAccessTime=null,
						CentralDirectory=null,
						CentralDirectoryOffset=null,
						CentralDirectoryLength=null
					WHERE 
						Game.GameId = New.GameId;
				END;
			");

			//**** GAME Triggers ****
			//INSERT
			ExecuteNonQuery(@"
				DROP TRIGGER IF EXISTS [GameInsert];
				CREATE TRIGGER IF NOT EXISTS [GameInsert]
				AFTER INSERT ON [GAME]
				FOR EACH ROW
				BEGIN
					UPDATE DAT SET
							RomTotal   =RomTotal  + New.RomTotal  , 
							RomGot	 =RomGot	+ New.RomGot	,
							RomNoDump  =RomNoDump + New.RomNoDump
					WHERE
							DatId= New.DatId;
				END;
			");
			//DELETE
			ExecuteNonQuery(@"
				DROP TRIGGER IF EXISTS [GameDelete];
				CREATE TRIGGER IF NOT EXISTS [GameDelete]
				AFTER DELETE ON [GAME]
				FOR EACH ROW
				BEGIN
					UPDATE DAT SET 
							RomTotal   =RomTotal  - Old.RomTotal  ,
							RomGot	 =RomGot	- Old.RomGot	,
							RomNoDump  =RomNoDump - Old.RomNoDump
					WHERE
							DatId=Old.DatId;
				END;
			");
			//UPDATE
			ExecuteNonQuery(@"
				DROP TRIGGER IF EXISTS [GameUpdate];
				CREATE TRIGGER IF NOT EXISTS [GameUpdate] 
				AFTER UPDATE ON [GAME] 
				FOR EACH ROW WHEN Old.RomTotal!=New.RomTotal OR Old.RomGot!=New.RomGot 
				BEGIN 
				  UPDATE DAT SET
							RomTotal   =RomTotal  - Old.RomTotal  + New.RomTotal ,
							RomGot	 =RomGot	- Old.RomGot	+ New.RomGot ,
							RomNoDump  =RomNoDump - Old.RomNoDump + New.RomNoDump
					WHERE
							DatId=New.DatId;
				END;
			");

		}

		public void MakeIndex(BackgroundWorker bgw = null)
		{
			if (bgw == null)
			{
				ConsoleManager.Show();
			}

			if (bgw != null)
			{
				bgw.ReportProgress(0, new bgwRange2Visible(true));
				bgw.ReportProgress(0, new bgwSetRange2(12));
			}
			if (bgw != null)
			{
				bgw.ReportProgress(0, new bgwValue2(0));
				bgw.ReportProgress(0, new bgwText2("Creating Index ROM-SHA1"));
			}
			Console.WriteLine("Creating Index ROM-SHA1");
			ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [ROMSHA1Index]   ON [ROM]   ([sha1]		ASC);");

			if (bgw != null)
			{
				bgw.ReportProgress(0, new bgwValue2(1));
				bgw.ReportProgress(0, new bgwText2("Creating Index ROM-MD5"));
			}
			Console.WriteLine("Creating Index ROM-MD5");
			ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [ROMMD5Index]	ON [ROM]   ([md5]		 ASC); ");

			if (bgw != null)
			{
				bgw.ReportProgress(0, new bgwValue2(2));
				bgw.ReportProgress(0, new bgwText2("Creating Index ROM-CRC"));
			}
			Console.WriteLine("Creating Index ROM-CRC");
			ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [ROMCRCIndex]	ON [ROM]   ([crc]		 ASC); ");

			if (bgw != null)
			{
				bgw.ReportProgress(0, new bgwValue2(3));
				bgw.ReportProgress(0, new bgwText2("Creating Index ROM-Size"));
			}
			Console.WriteLine("Creating Index ROM-Size");
			ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [ROMSizeIndex]   ON [ROM]   ([size]		ASC); ");

			if (bgw != null)
			{
				bgw.ReportProgress(0, new bgwValue2(4));
				bgw.ReportProgress(0, new bgwText2("Creating Index ROM-FileId"));
			}
			Console.WriteLine("Creating Index ROM-FileId");
			ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [ROMFileIdIndex] ON [ROM]   ([FileId]	  ASC); ");

			if (bgw != null)
			{
				bgw.ReportProgress(0, new bgwValue2(5));
				bgw.ReportProgress(0, new bgwText2("Creating Index ROM-GameId-Name"));
			}
			Console.WriteLine("Creating Index ROM-GameId-Name");
			ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [ROMGameId]	  ON [ROM]   ([GameId]	  ASC,[name] ASC);");

			if (bgw != null)
			{
				bgw.ReportProgress(0, new bgwValue2(6));
				bgw.ReportProgress(0, new bgwText2("Creating Index Game-DatId"));
			}
			Console.WriteLine("Creating Index Game-DatId");
			ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [GameDatId]	  ON [GAME]  ([DatId]	   ASC,[name] ASC);");

			if (bgw != null)
			{
				bgw.ReportProgress(0, new bgwValue2(7));
				bgw.ReportProgress(0, new bgwText2("Creating Index FILE-SHA1"));
			}
			Console.WriteLine("Creating Index FILE-SHA1");
			ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [FILESHA1]	   ON [FILES] ([sha1]		ASC);");

			if (bgw != null)
			{
				bgw.ReportProgress(0, new bgwValue2(8));
				bgw.ReportProgress(0, new bgwText2("Creating Index FILE-MD5"));
			}
			Console.WriteLine("Creating Index FILE-MD5");
			ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [FILEMD5]		ON [FILES] ([md5]		 ASC);");

			if (bgw != null)
			{
				bgw.ReportProgress(0, new bgwValue2(9));
				bgw.ReportProgress(0, new bgwText2("Creating Index FILE-CRC"));
			}
			Console.WriteLine("Creating Index FILE-CRC");
			ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [FILECRC]		ON [FILES] ([crc]		 ASC);");

			if (bgw != null)
			{
				bgw.ReportProgress(0, new bgwValue2(10));
				bgw.ReportProgress(0, new bgwText2("Creating Index DAT-DirId"));
			}
			Console.WriteLine("Creating Index DAT-DirId");
			ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [DATDIRID]	   ON [DAT]   ([DirId]	   ASC);");

			if (bgw != null)
			{
				bgw.ReportProgress(0, new bgwValue2(11));
				bgw.ReportProgress(0, new bgwText2("Creating Index Dir-ParentDirId"));
			}
			Console.WriteLine("Creating Index Dir-ParentDirId");
			ExecuteNonQuery(@"CREATE INDEX IF NOT EXISTS [DIRPARENTDIRID] ON [DIR]   ([ParentDirId] ASC);");

			if (bgw != null)
			{
				bgw.ReportProgress(0, new bgwValue2(12));
				bgw.ReportProgress(0, new bgwText2("Indexing Complete"));
			}
			Console.WriteLine("Indexing Complete");

			if (bgw == null)
			{
				ConsoleManager.Hide();
			}
		}

		public void DropIndex()
		{
			ExecuteNonQuery(@"
				DROP INDEX IF EXISTS [ROMSHA1Index];
				DROP INDEX IF EXISTS [ROMMD5Index];
				DROP INDEX IF EXISTS [ROMCRCIndex];
				DROP INDEX IF EXISTS [ROMSizeIndex];
				DROP INDEX IF EXISTS [ROMFileIdIndex];
				DROP INDEX IF EXISTS [ROMGameId];");

		}

		public void Begin()
		{
			ExecuteNonQuery("BEGIN TRANSACTION");
		}

		public void Commit()
		{
			ExecuteNonQuery("COMMIT TRANSACTION");
		}

		public DbCommand Command(string command)
		{
			return new SQLiteCommand(command, Connection);
		}

		public DbParameter Parameter(string param, object value)
		{
			return new SQLiteParameter(param, value);
		}
	}
}
