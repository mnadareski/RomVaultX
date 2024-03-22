using System;
using Microsoft.Data.Sqlite;
using RomVaultX.DB;
using RomVaultX.Util;

namespace RomVaultX
{
    public enum FindStatus
    {
        FileUnknown,
        FoundFileInArchive,
        FileNeededInArchive
    }

    public static class RvRomFileMatchup
    {
        #region FileNeededTest

        public static SqliteCommand CommandFindInFiles
        {
            get
            {
                if (_commandFindInFiles == null)
                {
                    _commandFindInFiles = new SqliteCommand(@"
                        SELECT COUNT(1)
                        FROM FILES
                        WHERE
                            size = @size
                            AND crc = @crc
                            AND sha1 = @sha1
                            AND md5 = @md5",
                    Program.db.Connection);

                    _commandFindInFiles.Parameters.Add(new SqliteParameter("size", SqliteType.Integer));
                    _commandFindInFiles.Parameters.Add(new SqliteParameter("crc", SqliteType.Text));
                    _commandFindInFiles.Parameters.Add(new SqliteParameter("sha1", SqliteType.Text));
                    _commandFindInFiles.Parameters.Add(new SqliteParameter("md5", SqliteType.Text));
                }

                return _commandFindInFiles;
            }
        }
        private static SqliteCommand? _commandFindInFiles;

        public static SqliteCommand CommandFindInRoms
        {
            get
            {
                if (_commandFindInRoms == null)
                {
                    _commandFindInRoms = new SqliteCommand(@"
                        SELECT
                        (
                            SELECT COUNT(1) FROM ROM WHERE
                                (sha1 = @sha1)
                                AND (md5 = @md5 OR md5 IS null)
                                AND (crc = @crc OR crc IS null)
                                AND (size = @size OR size IS null)
                        ) +
                        (
                            SELECT COUNT(1) FROM ROM WHERE
                                (md5 = @md5)
                                AND (sha1 = @sha1 OR sha1 IS null)
                                AND (crc = @crc OR crc IS null)
                                AND (size = @size OR size IS null)
                        ) +
                        (
                            SELECT COUNT(1) FROM ROM WHERE
                                (crc = @crc)
                                AND (sha1 = @sha1 OR sha1 IS null)
                                AND (md5 = @md5 OR md5 IS null)
                                AND (size = @size OR size IS null)
                        ) 
                        AS TotalFound", Program.db.Connection);

                    _commandFindInRoms.Parameters.Add(new SqliteParameter("size", SqliteType.Integer));
                    _commandFindInRoms.Parameters.Add(new SqliteParameter("crc", SqliteType.Text));
                    _commandFindInRoms.Parameters.Add(new SqliteParameter("sha1", SqliteType.Text));
                    _commandFindInRoms.Parameters.Add(new SqliteParameter("md5", SqliteType.Text));
                }

                return _commandFindInRoms;
            }
        }
        private static SqliteCommand? _commandFindInRoms;

        public static SqliteCommand CommandFindInRomsAlt
        {
            get
            {
                if (_commandFindInRomsAlt == null)
                {
                    _commandFindInRomsAlt = new SqliteCommand(@"
                        SELECT
                        (
                            SELECT COUNT(1) FROM ROM WHERE
                                (type = @type)
                                AND (sha1 = @sha1)
                                AND (md5 = @md5 OR md5 IS null)
                                AND (crc = @crc OR crc IS null)
                                AND (size = @size OR size IS null)
                        ) +
                        (
                            SELECT COUNT(1) FROM ROM WHERE
                                (type = @type)
                                AND (md5 = @md5)
                                AND (sha1 = @sha1 OR sha1 IS null)
                                AND (crc = @crc OR crc IS null)
                                AND (size = @size OR size IS null)
                        ) +
                        (
                            SELECT COUNT(1) FROM ROM WHERE
                                (type = @type)
                                AND (crc = @crc)
                                AND (sha1 = @sha1 OR sha1 IS null)
                                AND (md5 = @md5 OR md5 IS null)
                                AND (size = @size OR size IS null)
                        ) 
                        AS TotalFound", Program.db.Connection);

                    _commandFindInRomsAlt.Parameters.Add(new SqliteParameter("type", SqliteType.Integer));
                    _commandFindInRomsAlt.Parameters.Add(new SqliteParameter("size", SqliteType.Integer));
                    _commandFindInRomsAlt.Parameters.Add(new SqliteParameter("crc", SqliteType.Text));
                    _commandFindInRomsAlt.Parameters.Add(new SqliteParameter("sha1", SqliteType.Text));
                    _commandFindInRomsAlt.Parameters.Add(new SqliteParameter("md5", SqliteType.Text));
                }

                return _commandFindInRomsAlt;
            }
        }
        private static SqliteCommand? _commandFindInRomsAlt;

        public static SqliteCommand CommandFindInRomsZero
        {
            get
            {
                if (_commandFindInRomsZero == null)
                {
                    _commandFindInRomsZero = new SqliteCommand(@"
                        SELECT COUNT(1) AS TotalFound
                        FROM ROM
                        WHERE
                            (sha1 = @sha1 OR sha1 IS null)
                            AND (md5 = @md5 OR md5 IS null)
                            AND (crc = @crc OR crc IS null)
                            AND (size = 0 AND (status != 'nodump' OR status IS null)) ",
                    Program.db.Connection);

                    _commandFindInRomsZero.Parameters.Add(new SqliteParameter("crc", SqliteType.Text));
                    _commandFindInRomsZero.Parameters.Add(new SqliteParameter("sha1", SqliteType.Text));
                    _commandFindInRomsZero.Parameters.Add(new SqliteParameter("md5", SqliteType.Text));
                }

                return _commandFindInRomsZero;
            }
        }
        private static SqliteCommand? _commandFindInRomsZero;

        public static FindStatus FileneededTest(RvFile tFile)
        {
            // first check to see if we already have it in the file table
            bool inFileDB = FindInFiles(tFile); // returns true if found in File table
            if (inFileDB)
                return FindStatus.FoundFileInArchive;

            // now check if needed in any ROMs
            if (FindInROMs(tFile))
                return FindStatus.FileNeededInArchive;

            if (FileHeaderReader.FileHeaderReader.AltHeaderFile(tFile.AltType) && FindInROMsAlt(tFile))
                return FindStatus.FileNeededInArchive;

            return FindStatus.FileUnknown;
        }

        public static bool FindInFiles(RvFile tFile)
        {
            CommandFindInFiles.Parameters["size"].Value = tFile.Size;
            CommandFindInFiles.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC) ?? string.Empty;
            CommandFindInFiles.Parameters["sha1"].Value = VarFix.ToDBString(tFile.SHA1) ?? string.Empty;
            CommandFindInFiles.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5) ?? string.Empty;

            var res = CommandFindInFiles.ExecuteScalar();
            if (res == null || res == DBNull.Value)
                return false;

            int count = Convert.ToInt32(res);
            return count > 0;
        }

        private static bool FindInROMs(RvFile tFile)
        {
            if (tFile.Size == 0)
            {
                CommandFindInRomsZero.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC) ?? string.Empty;
                CommandFindInRomsZero.Parameters["sha1"].Value = VarFix.ToDBString(tFile.SHA1) ?? string.Empty;
                CommandFindInRomsZero.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5) ?? string.Empty;

                var resZero = CommandFindInRomsZero.ExecuteScalar();
                if (resZero == null || resZero == DBNull.Value)
                    return false;

                int countZero = Convert.ToInt32(resZero);
                return countZero > 0;
            }

            CommandFindInRoms.Parameters["size"].Value = tFile.Size;
            CommandFindInRoms.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC) ?? string.Empty;
            CommandFindInRoms.Parameters["sha1"].Value = VarFix.ToDBString(tFile.SHA1) ?? string.Empty;
            CommandFindInRoms.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5) ?? string.Empty;

            var res = CommandFindInRoms.ExecuteScalar();
            if (res == null || res == DBNull.Value)
                return false;

            int count = Convert.ToInt32(res);
            return count > 0;
        }

        private static bool FindInROMsAlt(RvFile tFile)
        {
            CommandFindInRomsAlt.Parameters["type"].Value = (int)tFile.AltType;
            CommandFindInRomsAlt.Parameters["size"].Value = tFile.AltSize;
            CommandFindInRomsAlt.Parameters["crc"].Value = VarFix.ToDBString(tFile.AltCRC) ?? string.Empty;
            CommandFindInRomsAlt.Parameters["sha1"].Value = VarFix.ToDBString(tFile.AltSHA1) ?? string.Empty;
            CommandFindInRomsAlt.Parameters["md5"].Value = VarFix.ToDBString(tFile.AltMD5) ?? string.Empty;

            var res = CommandFindInRomsAlt.ExecuteScalar();
            if (res == null || res == DBNull.Value)
                return false;

            int count = Convert.ToInt32(res);
            return count > 0;
        }

        #endregion

        #region MatchFileToRoms

        public static SqliteCommand CommandUpdateRom
        {
            get
            {
                if (_commandUpdateRom == null)
                {
                    _commandUpdateRom = new SqliteCommand(@"
                        UPDATE ROM SET 
	                        FileId = @FileId,
                            LocalFileHeader = null,
                            LocalFileHeaderOffset = null,
                            LocalFileHeaderLength = null
                        WHERE
	                        (                 sha1 = @sha1 ) AND
	                        ( md5  IS null OR md5  = @md5  ) AND 
	                        ( crc  IS null OR crc  = @crc  ) AND
	                        ( size IS null OR size = @size ) AND
                            FileId IS null;
		
                        UPDATE ROM SET 
	                        FileId = @FileId,
                            LocalFileHeader = null,
                            LocalFileHeaderOffset = null,
                            LocalFileHeaderLength = null
                        WHERE
	                        (                 md5  = @md5  ) AND 
	                        ( sha1 IS null OR sha1 = @sha1 ) AND
	                        ( crc  IS null OR crc  = @crc  ) AND
	                        ( size IS null OR size = @size ) AND
                            FileId IS null;
		
                        UPDATE ROM SET 
	                        FileId = @FileId,
                            LocalFileHeader = null,
                            LocalFileHeaderOffset = null,
                            LocalFileHeaderLength = null
                        WHERE
	                        (                 crc  = @crc  ) AND
	                        ( sha1 IS null OR sha1 = @sha1 ) AND
	                        ( md5  IS null OR md5  = @md5  ) AND 
	                        ( size IS null OR size = @size ) AND
                            FileId IS null;",
                    Program.db.Connection);

                    _commandUpdateRom.Parameters.Add(new SqliteParameter("FileId", SqliteType.Integer));
                    _commandUpdateRom.Parameters.Add(new SqliteParameter("size", SqliteType.Integer));
                    _commandUpdateRom.Parameters.Add(new SqliteParameter("crc", SqliteType.Text));
                    _commandUpdateRom.Parameters.Add(new SqliteParameter("sha1", SqliteType.Text));
                    _commandUpdateRom.Parameters.Add(new SqliteParameter("md5", SqliteType.Text));
                }

                return _commandUpdateRom;
            }
        }
        private static SqliteCommand? _commandUpdateRom;

        public static SqliteCommand CommandUpdateRomAlt
        {
            get
            {
                if (_commandUpdateRomAlt == null)
                {
                    _commandUpdateRomAlt = new SqliteCommand(@"
                        UPDATE ROM SET 
	                        FileId = @FileId,
                            LocalFileHeader = null,
                            LocalFileHeaderOffset = null,
                            LocalFileHeaderLength = null
                        WHERE
                            (                 type = @type ) AND
	                        (                 sha1 = @sha1 ) AND
	                        ( md5  IS null OR md5  = @md5  ) AND 
	                        ( crc  IS null OR crc  = @crc  ) AND
	                        ( size IS null OR size = @size ) AND
                            FileId IS null;
		
                        UPDATE ROM SET 
	                        FileId = @FileId,
                            LocalFileHeader = null,
                            LocalFileHeaderOffset = null,
                            LocalFileHeaderLength = null
                        WHERE
                            (                 type = @type ) AND
	                        (                 md5  = @md5  ) AND 
	                        ( sha1 IS null OR sha1 = @sha1 ) AND
	                        ( crc  IS null OR crc  = @crc  ) AND
	                        ( size IS null OR size = @size ) AND
                            FileId IS null;
		
                        UPDATE ROM SET 
	                        FileId = @FileId,
                            LocalFileHeader = null,
                            LocalFileHeaderOffset = null,
                            LocalFileHeaderLength = null
                        WHERE
                            (                 type = @type ) AND
	                        (                 crc  = @crc  ) AND
	                        ( sha1 IS null OR sha1 = @sha1 ) AND
	                        ( md5  IS null OR md5  = @md5  ) AND 
	                        ( size IS null OR size = @size ) AND
                            FileId IS null;",
                    Program.db.Connection);

                    _commandUpdateRomAlt.Parameters.Add(new SqliteParameter("FileId", SqliteType.Integer));
                    _commandUpdateRomAlt.Parameters.Add(new SqliteParameter("type", SqliteType.Integer));
                    _commandUpdateRomAlt.Parameters.Add(new SqliteParameter("size", SqliteType.Integer));
                    _commandUpdateRomAlt.Parameters.Add(new SqliteParameter("crc", SqliteType.Text));
                    _commandUpdateRomAlt.Parameters.Add(new SqliteParameter("sha1", SqliteType.Text));
                    _commandUpdateRomAlt.Parameters.Add(new SqliteParameter("md5", SqliteType.Text));
                }

                return _commandUpdateRomAlt;
            }
        }
        private static SqliteCommand? _commandUpdateRomAlt;

        public static SqliteCommand CommandUpdateRomZero
        {
            get
            {
                if (_commandUpdateRomZero == null)
                {
                    _commandUpdateRomZero = new SqliteCommand(@"
                        UPDATE ROM SET 
	                        FileId = @FileId,
                            LocalFileHeader = null,
                            LocalFileHeaderOffset = null,
                            LocalFileHeaderLength = null
                        WHERE
	                        ( Size=0 ) AND
	                        ( crc  IS null OR crc  = @crc  ) AND
	                        ( sha1 IS null OR sha1 = @sha1 ) AND
	                        ( md5  IS null OR md5  = @md5  ) AND 
                            FileId IS null;",
                    Program.db.Connection);

                    _commandUpdateRomZero.Parameters.Add(new SqliteParameter("FileId", SqliteType.Integer));
                    _commandUpdateRomZero.Parameters.Add(new SqliteParameter("crc", SqliteType.Text));
                    _commandUpdateRomZero.Parameters.Add(new SqliteParameter("sha1", SqliteType.Text));
                    _commandUpdateRomZero.Parameters.Add(new SqliteParameter("md5", SqliteType.Text));
                }

                return _commandUpdateRomZero;
            }
        }
        private static SqliteCommand? _commandUpdateRomZero;

        public static void MatchFiletoRoms(RvFile file)
        {
            if (file.Size != 0)
            {
                FileUpdateRom(file);
                if (FileHeaderReader.FileHeaderReader.AltHeaderFile(file.AltType))
                    FileUpdateRomAlt(file);
            }
            else
            {
                FileUpdateZeroRom(file);
            }
        }

        private static void FileUpdateRom(RvFile file)
        {
            CommandUpdateRom.Parameters["FileId"].Value = file.FileId;
            CommandUpdateRom.Parameters["size"].Value = file.Size;
            CommandUpdateRom.Parameters["crc"].Value = VarFix.ToDBString(file.CRC) ?? string.Empty;
            CommandUpdateRom.Parameters["sha1"].Value = VarFix.ToDBString(file.SHA1) ?? string.Empty;
            CommandUpdateRom.Parameters["md5"].Value = VarFix.ToDBString(file.MD5) ?? string.Empty;

            CommandUpdateRom.ExecuteNonQuery();
        }

        private static void FileUpdateRomAlt(RvFile file)
        {
            CommandUpdateRomAlt.Parameters["FileId"].Value = file.FileId;
            CommandUpdateRomAlt.Parameters["Type"].Value = file.AltType;
            CommandUpdateRomAlt.Parameters["size"].Value = file.AltSize;
            CommandUpdateRomAlt.Parameters["crc"].Value = VarFix.ToDBString(file.AltCRC) ?? string.Empty;
            CommandUpdateRomAlt.Parameters["sha1"].Value = VarFix.ToDBString(file.AltSHA1) ?? string.Empty;
            CommandUpdateRomAlt.Parameters["md5"].Value = VarFix.ToDBString(file.AltMD5) ?? string.Empty;

            CommandUpdateRomAlt.ExecuteNonQuery();
        }

        private static void FileUpdateZeroRom(RvFile file)
        {
            CommandUpdateRomZero.Parameters["FileId"].Value = file.FileId;
            CommandUpdateRomZero.Parameters["crc"].Value = VarFix.ToDBString(file.CRC) ?? string.Empty;
            CommandUpdateRomZero.Parameters["sha1"].Value = VarFix.ToDBString(file.SHA1) ?? string.Empty;
            CommandUpdateRomZero.Parameters["md5"].Value = VarFix.ToDBString(file.MD5) ?? string.Empty;

            CommandUpdateRomZero.ExecuteNonQuery();
        }

        #endregion

        #region MatchRomToaFile

        public static SqliteCommand CommandSHA1
        {
            get
            {
                if (_commandSHA1 == null)
                {
                    _commandSHA1 = new SqliteCommand(@"
                        SELECT FileId FROM FILES
                        WHERE
	                        (                  @sha1 = sha1 ) AND
	                        ( @md5  IS null OR @md5  = md5  ) AND
	                        ( @crc  IS null OR @crc  = crc  ) AND
	                        ( @size IS null OR @size = size )
                        LIMIT 1",
                    Program.db.Connection);

                    _commandSHA1.Parameters.Add(new SqliteParameter("sha1", SqliteType.Text));
                    _commandSHA1.Parameters.Add(new SqliteParameter("md5", SqliteType.Text));
                    _commandSHA1.Parameters.Add(new SqliteParameter("crc", SqliteType.Text));
                    _commandSHA1.Parameters.Add(new SqliteParameter("size", SqliteType.Integer));
                }

                return _commandSHA1;
            }
        }
        private static SqliteCommand? _commandSHA1;

        public static SqliteCommand CommandMD5
        {
            get
            {
                if (_commandMD5 == null)
                {
                    _commandMD5 = new SqliteCommand(@"
                        SELECT FileId FROM FILES
                        WHERE
	                        (                  @md5  = md5  ) AND
	                        ( @crc  IS null OR @crc  = crc  ) AND
	                        ( @size IS null OR @size = size )
                        LIMIT 1",
                    Program.db.Connection);

                    _commandMD5.Parameters.Add(new SqliteParameter("md5", SqliteType.Text));
                    _commandMD5.Parameters.Add(new SqliteParameter("crc", SqliteType.Text));
                    _commandMD5.Parameters.Add(new SqliteParameter("size", SqliteType.Integer));
                }

                return _commandMD5;
            }
        }
        private static SqliteCommand? _commandMD5;

        public static SqliteCommand CommandCRC
        {
            get
            {
                if (_commandCRC == null)
                {
                    _commandCRC = new SqliteCommand(@"
                        SELECT FileId FROM FILES
                        WHERE
	                        (                  @crc  = crc  ) AND
	                        ( @size IS null OR @size = size )
                        LIMIT 1",
                    Program.db.Connection);

                    _commandCRC.Parameters.Add(new SqliteParameter("crc", SqliteType.Text));
                    _commandCRC.Parameters.Add(new SqliteParameter("size", SqliteType.Integer));
                }

                return _commandCRC;
            }
        }
        private static SqliteCommand? _commandCRC;

        public static SqliteCommand CommandSize
        {
            get
            {
                if (_commandSize == null)
                {
                    _commandSize = new SqliteCommand(@"
                        SELECT FileId FROM FILES
                        WHERE
	                        ( @size = size )
                        LIMIT 1",
                    Program.db.Connection);

                    _commandSize.Parameters.Add(new SqliteParameter("size", SqliteType.Integer));
                }

                return _commandSize;
            }
        }
        private static SqliteCommand? _commandSize;

        public static SqliteCommand CommandSHA1Alt
        {
            get
            {
                if (_commandSHA1Alt == null)
                {
                    _commandSHA1Alt = new SqliteCommand(@"
                        SELECT FileId FROM FILES
                        WHERE
                            (               @alttype = alttype ) AND
	                        (                  @sha1 = altsha1 ) AND
	                        ( @md5  IS null OR @md5  = altmd5  ) AND
	                        ( @crc  IS null OR @crc  = altcrc  ) AND
	                        ( @size IS null OR @size = altsize )
                        LIMIT 1",
                    Program.db.Connection);

                    _commandSHA1Alt.Parameters.Add(new SqliteParameter("alttype", SqliteType.Text));
                    _commandSHA1Alt.Parameters.Add(new SqliteParameter("sha1", SqliteType.Text));
                    _commandSHA1Alt.Parameters.Add(new SqliteParameter("md5", SqliteType.Text));
                    _commandSHA1Alt.Parameters.Add(new SqliteParameter("crc", SqliteType.Text));
                    _commandSHA1Alt.Parameters.Add(new SqliteParameter("size", SqliteType.Integer));
                }

                return _commandSHA1Alt;
            }
        }
        private static SqliteCommand? _commandSHA1Alt;

        public static SqliteCommand CommandMD5Alt
        {
            get
            {
                if (_commandMD5Alt == null)
                {
                    _commandMD5Alt = new SqliteCommand(@"
                        SELECT FileId FROM FILES
                        WHERE
                            (               @alttype = alttype ) AND
	                        (                  @md5  = altmd5  ) AND
	                        ( @crc  IS null OR @crc  = altcrc  ) AND
	                        ( @size IS null OR @size = altsize )
                        LIMIT 1",
                    Program.db.Connection);

                    _commandMD5Alt.Parameters.Add(new SqliteParameter("alttype", SqliteType.Text));
                    _commandMD5Alt.Parameters.Add(new SqliteParameter("md5", SqliteType.Text));
                    _commandMD5Alt.Parameters.Add(new SqliteParameter("crc", SqliteType.Text));
                    _commandMD5Alt.Parameters.Add(new SqliteParameter("size", SqliteType.Integer));
                }

                return _commandMD5Alt;
            }
        }
        private static SqliteCommand? _commandMD5Alt;

        public static SqliteCommand CommandCRCAlt
        {
            get
            {
                if (_commandCRCAlt == null)
                {
                    _commandCRCAlt = new SqliteCommand(@"
                        SELECT FileId FROM FILES
                        WHERE
                            (               @alttype = alttype ) AND
	                        (                  @crc  = altcrc  ) AND
	                        ( @size IS null OR @size = altsize )
                        limit 1",
                    Program.db.Connection);

                    _commandCRCAlt.Parameters.Add(new SqliteParameter("alttype", SqliteType.Text));
                    _commandCRCAlt.Parameters.Add(new SqliteParameter("crc", SqliteType.Text));
                    _commandCRCAlt.Parameters.Add(new SqliteParameter("size", SqliteType.Integer));
                }

                return _commandCRCAlt;
            }
        }
        private static SqliteCommand? _commandCRCAlt;

        public static uint? FindAFile(RvRom tFile)
        {
            if (tFile.SHA1 != null)
            {
                CommandSHA1.Parameters["sha1"].Value = VarFix.ToDBString(tFile.SHA1) ?? string.Empty;
                CommandSHA1.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5) ?? string.Empty;
                CommandSHA1.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC) ?? string.Empty;
                CommandSHA1.Parameters["size"].Value = tFile.Size;

                var res = CommandSHA1.ExecuteScalar();
                if (res != null && res != DBNull.Value)
                    return (uint?)Convert.ToInt32(res);

                if (!FileHeaderReader.FileHeaderReader.AltHeaderFile(tFile.AltType))
                    return null;

                CommandSHA1Alt.Parameters["alttype"].Value = (int)tFile.AltType;
                CommandSHA1Alt.Parameters["sha1"].Value = VarFix.ToDBString(tFile.SHA1) ?? string.Empty;
                CommandSHA1Alt.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5) ?? string.Empty;
                CommandSHA1Alt.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC) ?? string.Empty;
                CommandSHA1Alt.Parameters["size"].Value = tFile.Size;

                res = CommandSHA1Alt.ExecuteScalar();
                if (res != null && res != DBNull.Value)
                    return (uint?)Convert.ToInt32(res);

                return null;
            }

            if (tFile.MD5 != null)
            {
                CommandMD5.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5) ?? string.Empty;
                CommandMD5.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC) ?? string.Empty;
                CommandMD5.Parameters["size"].Value = tFile.Size;

                var res = CommandMD5.ExecuteScalar();
                if (res != null && res != DBNull.Value)
                    return (uint?)Convert.ToInt32(res);

                if (!FileHeaderReader.FileHeaderReader.AltHeaderFile(tFile.AltType))
                    return null;

                CommandMD5Alt.Parameters["alttype"].Value = (int)tFile.AltType;
                CommandMD5Alt.Parameters["md5"].Value = VarFix.ToDBString(tFile.MD5) ?? string.Empty;
                CommandMD5Alt.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC) ?? string.Empty;
                CommandMD5Alt.Parameters["size"].Value = tFile.Size;

                res = CommandMD5Alt.ExecuteScalar();
                if (res != null && res != DBNull.Value)
                    return (uint?)Convert.ToInt32(res);

                return null;
            }

            if (tFile.CRC != null)
            {
                CommandCRC.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC) ?? string.Empty;
                CommandCRC.Parameters["size"].Value = tFile.Size;

                var res = CommandCRC.ExecuteScalar();
                if (res != null && res != DBNull.Value)
                    return (uint?)Convert.ToInt32(res);

                if (!FileHeaderReader.FileHeaderReader.AltHeaderFile(tFile.AltType))
                    return null;

                CommandCRCAlt.Parameters["alttype"].Value = (int)tFile.AltType;
                CommandCRCAlt.Parameters["crc"].Value = VarFix.ToDBString(tFile.CRC) ?? string.Empty;
                CommandCRCAlt.Parameters["size"].Value = tFile.Size;

                res = CommandCRCAlt.ExecuteScalar();
                if (res != null && res != DBNull.Value)
                    return (uint?)Convert.ToInt32(res);

                return null;
            }

            if (tFile.Size != null && tFile.Size == 0)
            {
                CommandSize.Parameters["size"].Value = tFile.Size;

                var res = CommandSize.ExecuteScalar();
                if (res != null && res != DBNull.Value)
                    return (uint?)Convert.ToInt32(res);
            }

            return null;
        }

        #endregion
    }
}