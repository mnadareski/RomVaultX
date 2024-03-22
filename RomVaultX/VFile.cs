using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DokanNet;
using RomVaultX.SupportedFiles.GZ;
using RomVaultX.Util;

namespace RomVaultX
{
    public class VFile
    {
        public string? FileName;
        public long Length;
        private int _fileId;
        public bool IsDirectory;
        private int _fileSplitIndex = -1;
        private DateTime _creationTime;
        private DateTime _lastAccessTime;
        private DateTime _lastWriteTime;

        public List<VZipFile>? Files;

        public static explicit operator FileInformation(VFile v)
        {
            return new FileInformation
            {
                FileName = v.FileName,
                Length = v.Length,
                Attributes = v.IsDirectory ? FileAttributes.Directory | FileAttributes.ReadOnly : FileAttributes.Normal | FileAttributes.ReadOnly,
                CreationTime = v._creationTime,
                LastAccessTime = v._lastAccessTime,
                LastWriteTime = v._lastWriteTime
            };
        }

        /*
        private static string path = @"D:\tmp";
        private static string GetPath(string searchFilename)
        {
            return path + searchFilename;
        }
        */

        /*
        Dokan Filename format:
        \DatRoot
        \DatRoot\Rom
        \DatRoot\Rom\file.zip

        DB DIR -> fullname
        DatRoot\
        DatRoot\Rom\

        */

        // using the supplied filename, try and find and return the information (vFile) about this testFilename
        // this may be a file or a directory, so we need to also figure that out.
        public static VFile? FindFilename(string filename)
        {
            Debug.WriteLine("Trying to find information about  " + filename);

            // 1) test for the root direction
            VFile? retVal = FindRoot(filename);
            if (retVal != null)
                return retVal;

            // 2) test for a regular DB Directory
            retVal = FindInDBDir(filename);
            if (retVal != null)
                return retVal;

            // 3) test for a Dat Entry
            retVal = FindInDBDat(filename);
            if (retVal != null)
                return retVal;

            // Failed to file this filename
            return null;
        }

        private static VFile? FindRoot(string filename)
        {
            if (filename != @"\")
                return null;

            return new VFile
            {
                FileName = filename,
                IsDirectory = true,
                _fileId = 0,
                _creationTime = DateTime.Today,
                _lastWriteTime = DateTime.Today,
                _lastAccessTime = DateTime.Today
            };
        }

        private static VFile? FindInDBDir(string filename)
        {
            // try and find this directory in the DIR table
            string testName = filename.Substring(1) + @"\"; // takes the slash of the front of the string and add one on the end
            Debug.WriteLine("Looking in DIR from  " + testName);
            using (DbCommand getDirectoryId = Program.db.Command(@"
                                    SELECT 
                                        DirId,
                                        CreationTime,
                                        LastAccessTime,
                                        LastWriteTime
                                    FROM
                                        DIR 
                                    WHERE 
                                        fullname = @fullname"))
            {
                DbParameter pFName = Program.db.Parameter("fullname", testName);
                getDirectoryId.Parameters.Add(pFName);

                using (DbDataReader reader = getDirectoryId.ExecuteReader())
                {
                    if (!reader.Read())
                        return null;

                    return new VFile
                    {
                        FileName = filename,
                        IsDirectory = true,
                        _fileId = Convert.ToInt32(reader["DirId"]),
                        _creationTime = new DateTime(Convert.ToInt64(reader["CreationTime"])),
                        _lastAccessTime = new DateTime(Convert.ToInt64(reader["LastAccessTime"])),
                        _lastWriteTime = new DateTime(Convert.ToInt64(reader["LastWriteTime"]))
                    };
                }
            }
        }

        private static VFile? FindInDBDat(string filename)
        {
            int filenameLength = filename.Length;
            // we only search in the DB for .zip files so test for that extension
            bool isFile = (filenameLength > 4) && (filename.Substring(filenameLength - 4).ToLowerInvariant() == ".zip");

            string testFilename = filename;
            if (isFile)
            {
                // if is File remove the .zip file extension
                testFilename = testFilename.Substring(0, filenameLength - 4);
            }

            string dirName = testFilename;
            while (true)
            {
                int slashPos = dirName.LastIndexOf(@"\", StringComparison.Ordinal);
                if (slashPos <= 0)
                    return null;

                dirName = testFilename.Substring(0, slashPos);
                int? dirId = DirFind(dirName);
                if (dirId == null)
                    continue; // loop to next slash

                string filePart = testFilename.Substring(slashPos + 1);
                if (isFile)
                {
                    VFile vFile = DBGameFindFile((int)dirId, filePart, filename);
                    if (vFile != null)
                    {
                        vFile._fileSplitIndex = slashPos;
                        return vFile;
                    }
                }
                else
                {
                    VFile vFile = DBGameFindDir((int)dirId, filePart, filename);
                    if (vFile != null)
                    {
                        vFile._fileSplitIndex = slashPos;
                        return vFile;
                    }
                }

                return null;
            }
        }

        private static int? DirFind(string dirName)
        {
            if (string.IsNullOrEmpty(dirName))
                return null;

            string testName = dirName.Substring(1) + @"\";
            using DbCommand getDirectoryId = Program.db.Command(@"SELECT DirId FROM DIR WHERE fullname = @fullname");
            DbParameter pFName = Program.db.Parameter("fullname", testName);

            getDirectoryId.Parameters.Add(pFName);

            var ret = getDirectoryId.ExecuteScalar();
            if (ret == null || ret == DBNull.Value)
                return null;

            return Convert.ToInt32(ret);
        }

        private static VFile DBGameFindFile(int dirId, string searchFilename, string realFilename)
        {
            using (DbCommand getFileInDirectory = Program.db.Command(@"
                            SELECT 
                                GameId, 
                                ZipFileLength,
                                LastWriteTime,
                                CreationTime,
                                LastAccessTime 
                            FROM
                                GAME 
                            WHERE 
                                DirId = @DirId AND
                                ZipFileLength > 0 AND
                                name = @name"))
            {
                DbParameter pDirId = Program.db.Parameter("DirId", dirId);
                getFileInDirectory.Parameters.Add(pDirId);
                DbParameter pName = Program.db.Parameter("name", searchFilename.Replace(@"\", @"/"));
                getFileInDirectory.Parameters.Add(pName);
                using (DbDataReader dr = getFileInDirectory.ExecuteReader())
                {
                    if (!dr.Read())
                    {
                        return null;
                    }
                    VFile vFile = new VFile
                    {
                        IsDirectory = false,
                        _fileId = Convert.ToInt32(dr["GameId"]),
                        FileName = realFilename,
                        Length = Convert.ToInt64(dr["ZipFileLength"]),
                        _lastWriteTime = new DateTime(Convert.ToInt64(dr["LastWriteTime"])),
                        _creationTime = new DateTime(Convert.ToInt64(dr["CreationTime"])),
                        _lastAccessTime = new DateTime(Convert.ToInt64(dr["LastAccessTime"]))
                    };
                    return vFile;
                }
            }
        }

        private static VFile DBGameFindDir(int dirId, string searchDirectoryName, string realFilename)
        {
            using (DbCommand getFileInDirectory = Program.db.Command(@"
                            SELECT 
                                GameId, 
                                ZipFileLength,
                                LastWriteTime,
                                CreationTime,
                                LastAccessTime 
                            FROM
                                GAME 
                            WHERE 
                                DirId = @DirId AND
                                ZipFileLength > 0 AND 
                                name Like @name
                            LIMIT 1"))
            {
                DbParameter pDirId = Program.db.Parameter("DirId", dirId);
                getFileInDirectory.Parameters.Add(pDirId);
                DbParameter pName = Program.db.Parameter("name", searchDirectoryName.Replace(@"\", @"/") + @"/%");
                getFileInDirectory.Parameters.Add(pName);
                using (DbDataReader dr = getFileInDirectory.ExecuteReader())
                {
                    if (!dr.Read())
                    {
                        return null;
                    }
                    VFile vFile = new VFile
                    {
                        IsDirectory = true,
                        _fileId = dirId, // we are storing the id of the DIR not the GameId (So we can use the dirId later)
                        FileName = realFilename,
                        Length = Convert.ToInt64(dr["ZipFileLength"]),
                        _lastWriteTime = new DateTime(Convert.ToInt64(dr["LastWriteTime"])),
                        _creationTime = new DateTime(Convert.ToInt64(dr["CreationTime"])),
                        _lastAccessTime = new DateTime(Convert.ToInt64(dr["LastAccessTime"]))
                    };
                    return vFile;
                }
            }
        }

        public static List<VFile> DirGetSubItems(VFile vDir)
        {
            int dirId = vDir._fileId;
            List<VFile> dirs = [];

            if (!vDir.IsDirectory)
                return dirs;

            if (vDir._fileSplitIndex == -1)
            {
                // we are not inside a DAT directory structure

                // find any child DIR's from this DIR level
                using (DbCommand getDirectory = Program.db.Command(@"
                    SELECT 
                        DirId,
                        name,
                        CreationTime,
                        LastAccessTime,
                        LastWriteTime 
                    FROM
                        DIR
                    WHERE 
                        ParentDirId = @DirId"))
                {
                    DbParameter pParentDirId = Program.db.Parameter("DirId", dirId);
                    getDirectory.Parameters.Add(pParentDirId);
                    using (DbDataReader dr = getDirectory.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            string filename = (string)dr["name"];
                            bool found = dirs.Any(t => t.FileName == filename);
                            if (!found)
                            {
                                dirs.Add(
                                    new VFile
                                    {
                                        IsDirectory = true,
                                        _fileId = Convert.ToInt32(dr["DirId"]),
                                        FileName = filename,
                                        _creationTime = new DateTime(Convert.ToInt64(dr["CreationTime"])),
                                        _lastAccessTime = new DateTime(Convert.ToInt64(dr["LastAccessTime"])),
                                        _lastWriteTime = new DateTime(Convert.ToInt64(dr["LastWriteTime"]))
                                    }
                                );
                            }
                        }
                    }
                }

                // find any DB items from top filename level
                using (DbCommand getFilesInDirectory = Program.db.Command(@"
                        SELECT 
                            GameId, 
                            name,
                            ZipFileLength,
                            LastWriteTime,
                            CreationTime,
                            LastAccessTime
                        FROM 
                            GAME 
                        WHERE 
                            DirId = @DirId AND
                            ZipFileLength > 0"))
                {
                    DbParameter pDirId = Program.db.Parameter("DirId", vDir._fileId);
                    getFilesInDirectory.Parameters.Add(pDirId);
                    using (DbDataReader dr = getFilesInDirectory.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            string filename = (string)dr["name"];
                            // test if filename is a directory
                            int filenameSplit = filename.IndexOf(@"/", StringComparison.Ordinal);
                            if (filenameSplit >= 0)
                            {
                                string dirFilename = filename.Substring(0, filenameSplit);
                                bool found = dirs.Any(t => t.FileName == dirFilename);
                                if (!found)
                                {
                                    dirs.Add(new VFile
                                    {
                                        IsDirectory = true,
                                        _fileId = Convert.ToInt32(dr["GameId"]),
                                        FileName = dirFilename,
                                        _fileSplitIndex = filenameSplit,
                                        Length = Convert.ToInt64(dr["ZipFileLength"]),
                                        _creationTime = new DateTime(Convert.ToInt64(dr["CreationTime"])),
                                        _lastAccessTime = new DateTime(Convert.ToInt64(dr["LastAccessTime"])),
                                        _lastWriteTime = new DateTime(Convert.ToInt64(dr["LastWriteTime"]))
                                    });
                                }
                            }
                            else
                            {
                                string zipFilename = filename + ".zip";
                                bool found = dirs.Any(t => t.FileName == zipFilename);
                                if (!found)
                                    dirs.Add(new VFile
                                    {
                                        IsDirectory = false,
                                        _fileId = Convert.ToInt32(dr["GameId"]),
                                        FileName = zipFilename,
                                        Length = Convert.ToInt64(dr["ZipFileLength"]),
                                        _creationTime = new DateTime(Convert.ToInt64(dr["CreationTime"])),
                                        _lastAccessTime = new DateTime(Convert.ToInt64(dr["LastAccessTime"])),
                                        _lastWriteTime = new DateTime(Convert.ToInt64(dr["LastWriteTime"]))
                                    });
                            }
                        }
                    }
                }
            }
            else
            {
                // we are in a DAT with sub directories

                string datfilePart = vDir.FileName.Substring(1 + vDir._fileSplitIndex).Replace(@"\", @"/") + @"/";
                int datfilePartLength = datfilePart.Length;
                // find any DB items from top filename level
                using (DbCommand getFilesInDirectory = Program.db.Command(@"
                        SELECT 
                            GameId, 
                            name,
                            ZipFileLength,
                            LastWriteTime,
                            CreationTime,
                            LastAccessTime
                        FROM 
                            GAME 
                        WHERE 
                            DirId = @DirId AND
                            ZipFileLength > 0 AND 
                            name LIKE @name"))
                {
                    DbParameter pDirName = Program.db.Parameter("name", datfilePart + "%");
                    getFilesInDirectory.Parameters.Add(pDirName);

                    DbParameter pDirId = Program.db.Parameter("DirId", vDir._fileId);
                    getFilesInDirectory.Parameters.Add(pDirId);
                    using (DbDataReader dr = getFilesInDirectory.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            string filename = (string)dr["name"];
                            filename = filename.Substring(datfilePartLength);
                            int filenameSplit = filename.IndexOf(@"/", StringComparison.Ordinal);
                            if (filenameSplit >= 0)
                            {
                                string dirFilename = filename.Substring(0, filenameSplit);
                                bool found = dirs.Any(t => t.FileName == dirFilename);
                                if (!found)
                                {
                                    dirs.Add(new VFile
                                    {
                                        IsDirectory = true,
                                        _fileId = Convert.ToInt32(dr["GameId"]),
                                        FileName = dirFilename,
                                        _fileSplitIndex = vDir._fileSplitIndex + filenameSplit, // check this is correct
                                        Length = Convert.ToInt64(dr["ZipFileLength"]),
                                        _creationTime = new DateTime(Convert.ToInt64(dr["CreationTime"])),
                                        _lastAccessTime = new DateTime(Convert.ToInt64(dr["LastAccessTime"])),
                                        _lastWriteTime = new DateTime(Convert.ToInt64(dr["LastWriteTime"]))
                                    });
                                }
                            }
                            else
                            {
                                string zipFilename = filename + ".zip";
                                bool found = dirs.Any(t => t.FileName == zipFilename);
                                if (!found)
                                {
                                    dirs.Add(new VFile
                                    {
                                        IsDirectory = false,
                                        _fileId = Convert.ToInt32(dr["GameId"]),
                                        FileName = zipFilename,
                                        Length = Convert.ToInt64(dr["ZipFileLength"]),
                                        _creationTime = new DateTime(Convert.ToInt64(dr["CreationTime"])),
                                        _lastAccessTime = new DateTime(Convert.ToInt64(dr["LastAccessTime"])),
                                        _lastWriteTime = new DateTime(Convert.ToInt64(dr["LastWriteTime"]))
                                    });
                                }
                            }
                        }
                    }
                }
            }

            return dirs;
        }

        /*
        private static VFile FindInRealRoot(string filename)
        {
            string fullPath = "RealRoot" + filename;
            DirectoryInfo di = new DirectoryInfo(fullPath);
            if (di.Exists)
            {
                VFile vfile = new VFile
                {
                    FileName = filename,
                    IsDirectory = true,
                    _creationTime = di.CreationTime,
                    _lastWriteTime = di.LastWriteTime,
                    _lastAccessTime = di.LastAccessTime,
                    _IsRealFile = true
                };
                return vfile;
            }

            FileInfo fi = new FileInfo(fullPath);
            if (fi.Exists)
            {
                VFile vfile = new VFile
                {
                    FileName = filename,
                    Length = fi.Length,
                    _creationTime = fi.CreationTime,
                    _lastWriteTime = fi.LastWriteTime,
                    _lastAccessTime = fi.LastAccessTime,
                    _IsRealFile = true
                };
                return vfile;
            }

            return null;
        }
        */

        public bool LoadVFileZipData() // used to get ready to load an actual ZIP file
        {
            Files = new List<VZipFile>();

            using (DbCommand getRoms = Program.db.Command(
                @"SELECT
                    LocalFileSha1,
                    LocalFileCompressedSize,
                    LocalFileHeader,
                    LocalFileHeaderOffset,
                    LocalFileHeaderLength
                 FROM 
                    ROM
                 WHERE 
                    ROM.GameId = @GameId
                    AND LocalFileHeaderLength > 0
                 ORDER BY 
                    ROM.RomId"))
            {
                DbParameter pGameId = Program.db.Parameter("GameId", _fileId);
                getRoms.Parameters.Add(pGameId);
                using (DbDataReader dr = getRoms.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        var gf = new VZipFile
                        {
                            LocalHeaderOffset = Convert.ToInt64(dr["LocalFileHeaderOffset"]),
                            LocalHeaderLength = Convert.ToInt64(dr["LocalFileHeaderLength"]),
                            LocalHeader = (byte[])dr["LocalFileHeader"],
                            GZipSha1 = VarFix.CleanMD5SHA1(dr["LocalFileSha1"].ToString(), 20),
                            CompressedDataOffset = Convert.ToInt64(dr["LocalFileHeaderOffset"]) + Convert.ToInt64(dr["LocalFileHeaderLength"]),
                            CompressedDataLength = Convert.ToInt64(dr["LocalFileCompressedsize"]),
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
                FROM GAME
                WHERE
                    GameId = @GameId"))
            {
                DbParameter pGameId = Program.db.Parameter("GameId", _fileId);
                getCentralDir.Parameters.Add(pGameId);
                using (DbDataReader dr = getCentralDir.ExecuteReader())
                {
                    if (!dr.Read())
                        return false;

                    var gf = new VZipFile
                    {
                        LocalHeaderOffset = Convert.ToInt64(dr["CentralDirectoryOffset"]),
                        LocalHeaderLength = Convert.ToInt64(dr["CentralDirectoryLength"]),
                        LocalHeader = (byte[])dr["CentralDirectory"],
                        GZipSha1 = null,
                        CompressedDataOffset = Convert.ToInt64(dr["CentralDirectoryOffset"]) + Convert.ToInt64(dr["CentralDirectoryLength"]),
                        CompressedDataLength = 0,
                        GZip = null // not used
                    };
                    Files.Add(gf);
                }
            }

            return true;
        }

        public class VZipFile
        {
            public long LocalHeaderOffset;
            public long LocalHeaderLength;
            public byte[]? LocalHeader;

            public byte[]? GZipSha1;
            public long CompressedDataOffset;
            public long CompressedDataLength;

            public GZip? GZip;
        }
    }
}