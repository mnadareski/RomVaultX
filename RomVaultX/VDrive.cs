using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using DokanNet;
using RomVaultX.DB;
using RomVaultX.SupportedFiles.GZ;
using RomVaultX.Util;

namespace RomVaultX
{
    public class VFile
    {
        public string FileName;
        public long Length;
        public bool IsDirectory;

        public int DirId;

        public List<vGZFile> files;
    }

    public class vGZFile
    {
        public long localHeaderOffset;
        public long localHeaderLength;
        public byte[] localHeader;

        public byte[] gZipSHA1;
        public long compressedDataOffset;
        public long compressedDataLength;

        public GZip gZip;
    }


    internal class VDrive : IDokanOperations
    {
        private readonly DateTime _dt = DateTime.Now;

        private static long TotalBytes()
        {
            using (SQLiteCommand getTotalBytes = new SQLiteCommand(@"select sum(zipfilelength) from game", DataAccessLayer.DBConnection))
            {
                return Convert.ToInt64(getTotalBytes.ExecuteScalar());
            }
        }

        private static int? GetDirectoryId(string directoryName)
        {
            using (SQLiteCommand getDirectoryId = new SQLiteCommand(@"select DirId From DIR where fullname=@FName", DataAccessLayer.DBConnection))
            {
                SQLiteParameter pFName = new SQLiteParameter("FName") { Value = directoryName };
                getDirectoryId.Parameters.Add(pFName);

                object ret = getDirectoryId.ExecuteScalar();
                return (ret == null) ? null : (int?)Convert.ToInt32(ret);
            }
        }

        private static List<string> GetDirectoryNames(int parentDirId)
        {
            List<string> directoryNames = new List<string>();
            using (SQLiteCommand getDirectory = new SQLiteCommand(@"select name From DIR where ParentDirId=@ParentId", DataAccessLayer.DBConnection))
            {
                SQLiteParameter pParentDirId = new SQLiteParameter("ParentId") { Value = parentDirId };
                getDirectory.Parameters.Add(pParentDirId);
                using (SQLiteDataReader dr = getDirectory.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        directoryNames.Add((string)dr["name"]);
                    }
                }
            }
            return directoryNames;
        }

        private static List<VFile> GetFilesInDirectory(int dirId)
        {
            List<VFile> files = new List<VFile>();
            using (SQLiteCommand getFilesInDirectory = new SQLiteCommand(@"select game.name,ZipFileLength from Dat,game where dat.DatId=game.datId and ZipFileLength>0 and dirId=@dirId", DataAccessLayer.DBConnection))
            {
                SQLiteParameter pDirId = new SQLiteParameter("DirId") { Value = dirId };
                getFilesInDirectory.Parameters.Add(pDirId);
                using (SQLiteDataReader dr = getFilesInDirectory.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        files.Add(new VFile { FileName = (string)dr["name"], Length = Convert.ToInt64(dr["ZipFileLength"]) });
                    }
                }
            }
            return files;
        }

        private bool GetFileInDirectory(int dirId, string filename, out int gameId, out long zipfileLength)
        {
            using (SQLiteCommand getFileInDirectory = new SQLiteCommand(@"select GameId, ZipFileLength from Dat, game where dat.DatId = game.datId and ZipFileLength > 0 and dirid = @dirId and game.name = @name", DataAccessLayer.DBConnection))
            {
                SQLiteParameter pDirId = new SQLiteParameter("DirId") { Value = dirId }; getFileInDirectory.Parameters.Add(pDirId);
                SQLiteParameter pName = new SQLiteParameter("Name") { Value = filename }; getFileInDirectory.Parameters.Add(pName);
                using (SQLiteDataReader dr = getFileInDirectory.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        gameId = Convert.ToInt32(dr["GameId"]);
                        zipfileLength = Convert.ToInt64(dr["ZipFileLength"]);
                        return true;
                    }
                }
            }
            gameId = -1;
            zipfileLength = 0;
            return false;
        }

        private bool LoadVFile(int gameId, VFile vfile)
        {

            vfile.files = new List<vGZFile>();

            using (SQLiteCommand getRoms = new SQLiteCommand(
                @"SELECT
                    FILES.sha1,
                    FILES.compressedsize,
                    LocalFileHeader,
                    LocalFileHeaderOffset,
                    LocalFileHeaderLength
                 FROM ROM,FILES WHERE ROM.FileId=FILES.FileId AND ROM.GameId=@GameId 
                 ORDER BY name", DataAccessLayer.DBConnection))
            {
                SQLiteParameter pGameId = new SQLiteParameter("GameId") { Value = gameId };
                getRoms.Parameters.Add(pGameId);
                using (SQLiteDataReader dr = getRoms.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        vGZFile gf = new vGZFile
                        {
                            localHeaderOffset = Convert.ToInt64(dr["LocalFileHeaderOffset"]),
                            localHeaderLength = Convert.ToInt64(dr["LocalFileHeaderLength"]),
                            localHeader = (byte[])dr["LocalFileHeader"],
                            gZipSHA1 = VarFix.CleanMD5SHA1(dr["sha1"].ToString(), 20),
                            compressedDataOffset = Convert.ToInt64(dr["LocalFileHeaderOffset"]) + Convert.ToInt64(dr["LocalFileHeaderLength"]),
                            compressedDataLength = Convert.ToInt64(dr["compressedsize"]),
                            gZip = null // opened as needed
                        };
                        vfile.files.Add(gf);
                    }
                }
            }


            // the central directory is now added on to the end of the file list, like is another file with zero bytes of compressed data.
            using (SQLiteCommand getCentralDir = new SQLiteCommand(
               @"select 
                    CentralDirectory, 
                    CentralDirectoryOffset, 
                    CentralDirectoryLength 
                 from game where GameId=@gameId", DataAccessLayer.DBConnection))
            {
                SQLiteParameter pGameId = new SQLiteParameter("GameId") { Value = gameId };
                getCentralDir.Parameters.Add(pGameId);
                using (SQLiteDataReader dr = getCentralDir.ExecuteReader())
                {
                    if (!dr.Read())
                        return false;

                    vGZFile gf = new vGZFile
                    {
                        localHeaderOffset = Convert.ToInt64(dr["CentralDirectoryOffset"]),
                        localHeaderLength = Convert.ToInt64(dr["CentralDirectoryLength"]),
                        localHeader = (byte[])dr["CentralDirectory"],
                        gZipSHA1 = null,
                        compressedDataOffset = Convert.ToInt64(dr["CentralDirectoryOffset"]) + Convert.ToInt64(dr["CentralDirectoryLength"]),
                        compressedDataLength = 0,
                        gZip = null  // not used
                    };
                    vfile.files.Add(gf);
                }
            }


            return true;
        }

        public NtStatus CreateFile(string fileName, DokanNet.FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, DokanFileInfo info)
        {
            Debug.WriteLine("");
            Debug.WriteLine("-----------CreateFile---------------------------------");
            Debug.WriteLine("Filename : " + fileName);
            Debug.WriteLine("FileAccess : " + access + "  FileShare : " + share);
            Debug.WriteLine("FileMode : " + mode + "  FileOptions : " + options);
            Debug.WriteLine("FileAttributes :" + attributes);

            Debug.WriteLine("DokanInfo IsDirectory : " + info.IsDirectory);
            VFile vfile;

            if (fileName == "\\")
            {
                vfile = new VFile
                {
                    FileName = fileName,
                    IsDirectory = true,
                    DirId = 0
                };
                info.Context = vfile;
                return NtStatus.Success;
            }

            int? dirId = GetDirectoryId(fileName.Substring(1) + @"\");
            if (dirId != null)
            {
                vfile = new VFile
                {
                    FileName = fileName,
                    IsDirectory = true,
                    DirId = (int)dirId
                };
                info.Context = vfile;
                return NtStatus.Success;
            }

            //not a directory so test for file
            string dirPart = Path.GetDirectoryName(fileName);
            string filePart = Path.GetFileNameWithoutExtension(fileName);

            if (string.IsNullOrEmpty(dirPart))
            {
                dirId = 0;
            }
            else
            {
                dirId = GetDirectoryId(dirPart.Substring(1) + @"\");
                if (dirId == null)
                    return NtStatus.NoSuchFile;
            }

            int gameId;
            long zipfilelength;
            if (!GetFileInDirectory((int)dirId, filePart, out gameId, out zipfilelength))
                return NtStatus.NoSuchFile;

            vfile = new VFile
            {
                FileName = fileName,
                Length = zipfilelength,
                IsDirectory = false, // finding a file.
                DirId = (int)dirId,
            };


            if ((access & DokanNet.FileAccess.ReadData) != 0)
            {
                // opening file to read, so setup the rest of the data.
                if (!LoadVFile(gameId, vfile))
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

            if (vfile.files == null)
                return;

            foreach (vGZFile gf in vfile.files)
            {
                if (gf.gZip == null) continue;
                gf.gZip.Close();
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


        private void copyStream(vGZFile source, byte[] destination, long sourceOffset, long destinationOffset, long sourceLength, long destinationLength)
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

            if (source.gZip == null)
            {
                source.gZip = new GZip();

                string strFilename = Getfilename(source.gZipSHA1);
                source.gZip.ReadGZip(strFilename, false);
            }

            Stream coms;
            source.gZip.GetRawStream(out coms);
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

            foreach (vGZFile gf in vfile.files)
            {
                copyData(gf.localHeader, buffer, gf.localHeaderOffset, offset, gf.localHeaderLength, bytesRead);
                copyStream(gf, buffer, gf.compressedDataOffset, offset, gf.compressedDataLength, bytesRead);
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

            fileInfo = new FileInformation();
            fileInfo.FileName = vfile.FileName;
            fileInfo.Length = vfile.Length;
            fileInfo.CreationTime = _dt;
            fileInfo.LastAccessTime = _dt;
            fileInfo.LastWriteTime = _dt;
            fileInfo.Attributes = vfile.IsDirectory ? FileAttributes.Directory : FileAttributes.Normal;
            return NtStatus.Success;

        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, DokanFileInfo info)
        {
            Debug.WriteLine("");
            Debug.WriteLine("-----------FindFiles---------------------------------");
            Debug.WriteLine("Filename : " + fileName);

            // if (searchPattern != "*" && searchPattern !="DatRoot")
            //     Debug.WriteLine("Unknown search pattern");

            files = new List<FileInformation>();

            VFile vfile = (VFile)info.Context;
            if (vfile == null)
                return NtStatus.NoSuchFile;
            int dirId = vfile.DirId;

            List<string> dirNames = GetDirectoryNames(dirId);
            foreach (string dirName in dirNames)
            {
                FileInformation fi = new FileInformation
                {
                    FileName = dirName,
                    Length = 0,
                    Attributes = FileAttributes.Directory | FileAttributes.ReadOnly,
                    CreationTime = _dt,
                    LastAccessTime = _dt,
                    LastWriteTime = _dt
                };
                files.Add(fi);
            }

            List<VFile> dFiles = GetFilesInDirectory(dirId);
            foreach (VFile file in dFiles)
            {
                FileInformation fi = new FileInformation
                {
                    FileName = file.FileName + ".zip",
                    Length = file.Length,
                    Attributes = FileAttributes.Normal | FileAttributes.ReadOnly,
                    CreationTime = _dt,
                    LastAccessTime = _dt,
                    LastWriteTime = _dt
                };
                files.Add(fi);
            }

            return NtStatus.Success;
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
            int dirId = vfile.DirId;

            List<string> dirNames = GetDirectoryNames(dirId);
            foreach (string dirName in dirNames)
            {
                if (searchPattern != "*" && searchPattern != dirName) continue;
                FileInformation fi = new FileInformation
                {
                    FileName = dirName,
                    Length = 0,
                    Attributes = FileAttributes.Directory | FileAttributes.ReadOnly,
                    CreationTime = _dt,
                    LastAccessTime = _dt,
                    LastWriteTime = _dt
                };
                files.Add(fi);
            }

            List<VFile> dFiles = GetFilesInDirectory(dirId);
            foreach (VFile file in dFiles)
            {
                if (searchPattern != "*" && searchPattern != file.FileName + ".zip") continue;
                FileInformation fi = new FileInformation
                {
                    FileName = file.FileName + ".zip",
                    Length = file.Length,
                    Attributes = FileAttributes.Normal | FileAttributes.ReadOnly,
                    CreationTime = _dt,
                    LastAccessTime = _dt,
                    LastWriteTime = _dt
                };
                files.Add(fi);
            }

            return NtStatus.Success;
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
                       FileSystemFeatures.UnicodeOnDisk | FileSystemFeatures.ReadOnlyVolume;
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
            //   throw new NotImplementedException();
            return NtStatus.Success;
        }

        public NtStatus Unmounted(DokanFileInfo info)
        {
            //   throw new NotImplementedException();
            return NtStatus.Success;
        }

        public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, DokanFileInfo info)
        {
            Debug.WriteLine("");
            Debug.WriteLine("-----------FindStreams---------------------------------");
            Debug.WriteLine("Filename : " + fileName);

            streams = new List<FileInformation>();
            FileInformation fileInfo = new FileInformation();

            VFile vfile = (VFile)info.Context;
            if (vfile == null)
                return NtStatus.NoSuchFile;

            fileInfo = new FileInformation();
            fileInfo.FileName = vfile.FileName;
            fileInfo.Length = vfile.Length;
            fileInfo.CreationTime = _dt;
            fileInfo.LastAccessTime = _dt;
            fileInfo.LastWriteTime = _dt;
            fileInfo.Attributes = vfile.IsDirectory ? FileAttributes.Directory : FileAttributes.Normal;

            streams.Add(fileInfo);
            return NtStatus.Success;

        }
    }
}
