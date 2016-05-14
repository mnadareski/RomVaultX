using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using DokanNet;
using RomVaultX.SupportedFiles.GZ;
using RomVaultX.Util;

namespace RomVaultX
{

    public class VFile
    {
        public string FileName;
        public long Length;
        public int FileId;
        public bool IsDirectory;
        public DateTime CreationTime;
        public DateTime LastAccessTime;
        public DateTime LastWriteTime;

        public List<VGzFile> Files;
    }

    public class VGzFile
    {
        public long LocalHeaderOffset;
        public long LocalHeaderLength;
        public byte[] LocalHeader;

        public byte[] GZipSHA1;
        public long CompressedDataOffset;
        public long CompressedDataLength;

        public GZip GZip;
    }


    internal class VDrive : IDokanOperations
    {
        private static long TotalBytes()
        {
            return 95906406250;
            using (DbCommand getTotalBytes = Program.db.Command(@"select sum(zipfilelength) from game"))
            {
                return Convert.ToInt64(getTotalBytes.ExecuteScalar());
            }
        }

        private static int? DirFind(string dirFullname)
        {
            string testName = dirFullname.Substring(1) + @"\";
            using (DbCommand getDirectoryId = Program.db.Command(@"select DirId From DIR where fullname=@FName"))
            {
                DbParameter pFName = Program.db.Parameter("FName", testName);
                getDirectoryId.Parameters.Add(pFName);

                object ret = getDirectoryId.ExecuteScalar();
                if (ret == null || ret == DBNull.Value)
                    return null;
                return Convert.ToInt32(ret);
            }
        }

        private static VFile DirInfo(string dirFullname)
        {
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
                return null;
            }
        }

        private static List<VFile> DirGetSubDirs(int dirId)
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

        private static List<VFile> DirGetFiles(int dirId)
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
                        files.Add(
                            new VFile
                            {
                                IsDirectory = false,
                                FileId = Convert.ToInt32(dr["GameId"]),
                                FileName = (string)dr["name"],
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

        private static VFile DirGetFile(int dirId, string filename)
        {
            using (DbCommand getFileInDirectory = Program.db.Command(@"select game.gameId, ZipFileLength,LastWriteTime,CreationTime,LastAccessTime from Dat, game where dat.DatId = game.datId and ZipFileLength > 0 and dirid = @dirId and game.name = @name"))
            {
                DbParameter pDirId = Program.db.Parameter("DirId", dirId); getFileInDirectory.Parameters.Add(pDirId);
                DbParameter pName = Program.db.Parameter("Name", filename); getFileInDirectory.Parameters.Add(pName);
                using (DbDataReader dr = getFileInDirectory.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        VFile vFile = new VFile
                        {
                            IsDirectory = false,
                            FileId = Convert.ToInt32(dr["GameId"]),
                            FileName = filename,
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

        private bool LoadVFile(VFile vfile)
        {

            vfile.Files = new List<VGzFile>();

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
                DbParameter pGameId = Program.db.Parameter("GameId", vfile.FileId);
                getRoms.Parameters.Add(pGameId);
                using (DbDataReader dr = getRoms.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        VGzFile gf = new VGzFile
                        {
                            LocalHeaderOffset = Convert.ToInt64(dr["LocalFileHeaderOffset"]),
                            LocalHeaderLength = Convert.ToInt64(dr["LocalFileHeaderLength"]),
                            LocalHeader = (byte[])dr["LocalFileHeader"],
                            GZipSHA1 = VarFix.CleanMD5SHA1(dr["sha1"].ToString(), 20),
                            CompressedDataOffset = Convert.ToInt64(dr["LocalFileHeaderOffset"]) + Convert.ToInt64(dr["LocalFileHeaderLength"]),
                            CompressedDataLength = Convert.ToInt64(dr["compressedsize"]),
                            GZip = null // opened as needed
                        };
                        vfile.Files.Add(gf);
                    }
                }
            }


            // the central directory is now added on to the end of the file list, like is another file with zero bytes of compressed data.
            using (DbCommand getCentralDir = Program.db.Command(
               @"select 
                    CentralDirectory, 
                    CentralDirectoryOffset, 
                    CentralDirectoryLength 
                 from game where GameId=@gameId"))
            {
                DbParameter pGameId = Program.db.Parameter("GameId", vfile.FileId);
                getCentralDir.Parameters.Add(pGameId);
                using (DbDataReader dr = getCentralDir.ExecuteReader())
                {
                    if (!dr.Read())
                        return false;

                    VGzFile gf = new VGzFile
                    {
                        LocalHeaderOffset = Convert.ToInt64(dr["CentralDirectoryOffset"]),
                        LocalHeaderLength = Convert.ToInt64(dr["CentralDirectoryLength"]),
                        LocalHeader = (byte[])dr["CentralDirectory"],
                        GZipSHA1 = null,
                        CompressedDataOffset = Convert.ToInt64(dr["CentralDirectoryOffset"]) + Convert.ToInt64(dr["CentralDirectoryLength"]),
                        CompressedDataLength = 0,
                        GZip = null  // not used
                    };
                    vfile.Files.Add(gf);
                }
            }


            return true;
        }

        public NtStatus CreateFile(string fileName, DokanNet.FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, DokanFileInfo info)
        {
            VFile vfile;
            
            if (fileName == "\\")
            {
                vfile = new VFile
                {
                    FileName = fileName,
                    IsDirectory = true,
                    CreationTime = DateTime.Today,
                    LastWriteTime = DateTime.Today,
                    LastAccessTime = DateTime.Today,
                    FileId = 0
                };
                info.Context = vfile;
                return NtStatus.Success;
            }

            vfile = DirInfo(fileName);
            if (vfile != null)
            {
                info.Context = vfile;
                return NtStatus.Success;
            }

            //not a directory so test for file
            string dirPart = Path.GetDirectoryName(fileName);
            string filePart = Path.GetFileNameWithoutExtension(fileName);

            int? dirId;
            if (string.IsNullOrEmpty(dirPart))
            {
                dirId = 0;
            }
            else
            {
                dirId = DirFind(dirPart);
                if (dirId == null)
                    return NtStatus.NoSuchFile;
            }

            vfile = DirGetFile((int)dirId, filePart);
            if (vfile == null)
                return NtStatus.NoSuchFile;

            vfile.FileName = fileName;

            if ((access & DokanNet.FileAccess.ReadData) != 0)
            {
                // opening file to read, so setup the rest of the data.
                if (!LoadVFile(vfile))
                    return NtStatus.NoSuchFile;
            }

            info.Context = vfile;

            return NtStatus.Success;

        }

        public void Cleanup(string fileName, DokanFileInfo info)
        {
            Debug.WriteLine("");
            Debug.WriteLine("-----------Cleanup---------------------------------");
            Debug.WriteLine("Filename : " + fileName);
        }

        public void CloseFile(string fileName, DokanFileInfo info)
        {
            Debug.WriteLine("");
            Debug.WriteLine("-----------CloseFile---------------------------------");
            Debug.WriteLine("Filename : " + fileName);

            VFile vfile = (VFile)info.Context;
            if (vfile == null)
                return;

            if (vfile.Files == null)
                return;

            foreach (VGzFile gf in vfile.Files)
            {
                if (gf.GZip == null) continue;
                gf.GZip.Close();
            }
        }


        private void copyData(byte[] source, byte[] destination, long sourceOffset, long destinationOffset, long sourceLength, long destinationLength)
        {
            // this is where to start reading in the source array
            long sourceStart;
            long destinationStart;

            if (sourceOffset < destinationOffset)
            {
                sourceStart = destinationOffset - sourceOffset;
                destinationStart = 0;
                sourceLength -= sourceStart;

                // check if source is all before destination
                if (sourceLength <= 0)
                    return;
            }
            else
            {
                sourceStart = 0;
                destinationStart = sourceOffset - destinationOffset;
                destinationLength -= destinationStart;

                // check if desination is all before source
                if (destinationLength <= 0)
                    return;
            }

            long actualLength = Math.Min(sourceLength, destinationLength);
            for (int i = 0; i < actualLength; i++)
                destination[destinationStart + i] = source[sourceStart + i];
        }


        private void copyStream(VGzFile source, byte[] destination, long sourceOffset, long destinationOffset, long sourceLength, long destinationLength)
        {
            // this is where to start reading in the source array
            long sourceStart;
            long destinationStart;

            if (sourceOffset < destinationOffset)
            {
                sourceStart = destinationOffset - sourceOffset;
                destinationStart = 0;
                sourceLength -= sourceStart;

                // check if source is all before destination
                if (sourceLength <= 0)
                    return;
            }
            else
            {
                sourceStart = 0;
                destinationStart = sourceOffset - destinationOffset;
                destinationLength -= destinationStart;

                // check if desination is all before source
                if (destinationLength <= 0)
                    return;
            }

            if (source.GZip == null)
            {
                source.GZip = new GZip();

                string strFilename = Getfilename(source.GZipSHA1);
                source.GZip.ReadGZip(strFilename, false);
            }

            Stream coms;
            source.GZip.GetRawStream(out coms);
            coms.Position += sourceStart;

            long actualLength = Math.Min(sourceLength, destinationLength);
            coms.Read(destination, (int)destinationStart, (int)actualLength);

            coms.Close();
        }
        private static string Getfilename(byte[] SHA1)
        {
            return @"RomRoot\" + VarFix.ToString(SHA1[0]) + @"\" +
                         VarFix.ToString(SHA1[1]) + @"\" +
                         VarFix.ToString(SHA1) + ".gz";

        }

        public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, DokanFileInfo info)
        {
            bytesRead = 0;
            VFile vfile = (VFile)info.Context;
            if (vfile == null)
                return NtStatus.NoSuchFile;

            // trying to fill all of the buffer
            bytesRead = buffer.Length;

            // if reading past the EOF then read 0 bytes
            if (offset > vfile.Length)
            {
                bytesRead = 0;
                return NtStatus.Success;
            }
            // if reading to the EOF then set the number of bytes left to read
            if (offset + bytesRead > vfile.Length)
                bytesRead = (int)(vfile.Length - offset);

            foreach (VGzFile gf in vfile.Files)
            {
                copyData(gf.LocalHeader, buffer, gf.LocalHeaderOffset, offset, gf.LocalHeaderLength, bytesRead);
                copyStream(gf, buffer, gf.CompressedDataOffset, offset, gf.CompressedDataLength, bytesRead);
            }

            return NtStatus.Success;
        }

        public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset, DokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus FlushFileBuffers(string fileName, DokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, DokanFileInfo info)
        {
            Debug.WriteLine("");
            Debug.WriteLine("-----------GetFileInformation---------------------------------");
            Debug.WriteLine("Filename : " + fileName);

            fileInfo = new FileInformation();

            VFile vfile = (VFile)info.Context;
            if (vfile == null)
                return NtStatus.NoSuchFile;

            fileInfo = new FileInformation
            {
                FileName = vfile.FileName,
                Length = vfile.Length,
                CreationTime = vfile.CreationTime,
                LastAccessTime = vfile.LastAccessTime,
                LastWriteTime = vfile.LastWriteTime,
                Attributes = vfile.IsDirectory ? FileAttributes.Directory : FileAttributes.Normal
            };
            return NtStatus.Success;

        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, DokanFileInfo info)
        {
            Debug.WriteLine("");
            Debug.WriteLine("-----------FindFiles---------------------------------");
            Debug.WriteLine("Filename : " + fileName);

            return FindFilesWithPattern(fileName, "*", out files, info);

        }

        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files, DokanFileInfo info)
        {
            Debug.WriteLine("");
            Debug.WriteLine("-----------FindFilesWithPattern---------------------------------");
            Debug.WriteLine("Filename : " + fileName);
            Debug.WriteLine("searchPattern : " + searchPattern);

            // if (searchPattern != "*" && searchPattern !="DatRoot")
            //     Debug.WriteLine("Unknown search pattern");

            files = new List<FileInformation>();

            VFile vfile = (VFile)info.Context;
            if (vfile == null)
                return NtStatus.NoSuchFile;

            GetEmptyDirectoryDefaultFiles(fileName, ref files);

            int dirId = vfile.FileId;

            List<VFile> dirs = DirGetSubDirs(dirId);
            foreach (VFile dir in dirs)
            {
                if (searchPattern != "*" && searchPattern != dir.FileName) continue;
                FileInformation fi = new FileInformation
                {
                    FileName = dir.FileName,
                    Length = 0,
                    Attributes = FileAttributes.Directory | FileAttributes.ReadOnly,
                    CreationTime = dir.CreationTime,
                    LastAccessTime = dir.LastAccessTime,
                    LastWriteTime = dir.LastWriteTime
                };
                files.Add(fi);
            }

            List<VFile> dFiles = DirGetFiles(dirId);
            foreach (VFile file in dFiles)
            {
                if (searchPattern != "*" && searchPattern != file.FileName + ".zip") continue;
                FileInformation fi = new FileInformation
                {
                    FileName = file.FileName + ".zip",
                    Length = file.Length,
                    Attributes = FileAttributes.Normal | FileAttributes.ReadOnly,
                    CreationTime = file.CreationTime,
                    LastAccessTime = file.LastAccessTime,
                    LastWriteTime = file.LastWriteTime
                };
                files.Add(fi);
            }

            return NtStatus.Success;
        }

        private void GetEmptyDirectoryDefaultFiles(string fileName, ref IList<FileInformation> files)
        {
            if (fileName == "\\")
                return;
            files.Add(new FileInformation() { FileName = ".", Attributes = FileAttributes.Directory, CreationTime = DateTime.Today, LastWriteTime = DateTime.Today, LastAccessTime = DateTime.Today });
            files.Add(new FileInformation() { FileName = "..", Attributes = FileAttributes.Directory, CreationTime = DateTime.Today, LastWriteTime = DateTime.Today, LastAccessTime = DateTime.Today });
        }


        public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, DokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, DokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus DeleteFile(string fileName, DokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus DeleteDirectory(string fileName, DokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus MoveFile(string oldName, string newName, bool replace, DokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus SetEndOfFile(string fileName, long length, DokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus SetAllocationSize(string fileName, long length, DokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus LockFile(string fileName, long offset, long length, DokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus UnlockFile(string fileName, long offset, long length, DokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes, out long totalNumberOfFreeBytes, DokanFileInfo info)
        {
            freeBytesAvailable = 0;
            totalNumberOfBytes = TotalBytes();
            totalNumberOfFreeBytes = 0;
            return DokanResult.Success;
        }

        public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features, out string fileSystemName, DokanFileInfo info)
        {
            volumeLabel = "RomVaultX";
            fileSystemName = "RomVaultX";
            features = FileSystemFeatures.CasePreservedNames | FileSystemFeatures.CaseSensitiveSearch |
                       FileSystemFeatures.PersistentAcls | FileSystemFeatures.SupportsRemoteStorage |
                       FileSystemFeatures.UnicodeOnDisk;
            return DokanResult.Success;
        }

        public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security, AccessControlSections sections, DokanFileInfo info)
        {
            Debug.WriteLine("");
            Debug.WriteLine("-----------GetFileSecurity---------------------------------");
            Debug.WriteLine("Filename : " + fileName);

            security = new DirectorySecurity();
            return NtStatus.Success;
        }

        public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections, DokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus Mounted(DokanFileInfo info)
        {
            return NtStatus.Success;
        }

        public NtStatus Unmounted(DokanFileInfo info)
        {
            return NtStatus.Success;
        }

        public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, DokanFileInfo info)
        {
            Debug.WriteLine("");
            Debug.WriteLine("-----------FindStreams---------------------------------");
            Debug.WriteLine("Filename : " + fileName);

            streams = new FileInformation[0];
            return NtStatus.Success;

        }
    }

}
