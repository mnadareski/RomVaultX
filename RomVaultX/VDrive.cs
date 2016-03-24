using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using DokanNet;
using RomVaultX.DB;

namespace RomVaultX
{
    public class vFile
    {
        public string FileName;
        public long Length;
        public bool isDirectory;

        public long DirId;
        public long GameId;
    }



    internal class VDrive : IDokanOperations
    {
        private SQLiteCommand getTotalBytes;
        private SQLiteCommand getDirectoryId;
        private SQLiteCommand getDirectory;
        private SQLiteCommand getFilesInDirectory;

        private SQLiteCommand findDir;
        private SQLiteCommand findGame;

        private DateTime dt = DateTime.Now;

        public VDrive()
        {
            getTotalBytes = new SQLiteCommand(@"select sum(zipfilelength) from game", DataAccessLayer.DBConnection);
            getDirectoryId = new SQLiteCommand(@"select DirId From DIR where fullname=@FName", DataAccessLayer.DBConnection);
            getDirectoryId.Parameters.Add(new SQLiteParameter("FName"));

            getDirectory = new SQLiteCommand(@"select name From DIR where ParentDirId=@ParentId", DataAccessLayer.DBConnection);
            getDirectory.Parameters.Add(new SQLiteParameter("ParentId"));

            getFilesInDirectory = new SQLiteCommand(@"select game.name,ZipFileLength from Dat,game where dat.DatId=game.datId and ZipFileLength>0 and dirid=@dId", DataAccessLayer.DBConnection);
            getFilesInDirectory.Parameters.Add(new SQLiteParameter("dId"));

            findDir = new SQLiteCommand(@"SELECT DirId FROM DIR WHERE fullname=@fullname", DataAccessLayer.DBConnection);
            findDir.Parameters.Add(new SQLiteParameter("fullname"));

            findGame = new SQLiteCommand(@"select GameId,ZipFileLength from Dat,game where dat.DatId=game.datId and ZipFileLength>0 and dirid=@dId and game.name=@name", DataAccessLayer.DBConnection);
            findGame.Parameters.Add(new SQLiteParameter("dId"));
            findGame.Parameters.Add(new SQLiteParameter("name"));
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
            vFile vfile;

            if (fileName == "\\")
            {
                vfile = new vFile
                {
                    FileName = fileName,
                    isDirectory = true,
                    DirId = 0
                };
                info.Context = vfile;
                return NtStatus.Success;
            }


            getDirectoryId.Parameters["FName"].Value = fileName.Substring(1) + @"\";
            object res = getDirectoryId.ExecuteScalar();
            if (res != null)
            {
                vfile = new vFile
                {
                    FileName = fileName,
                    isDirectory = true,
                    DirId = Convert.ToInt32(res)
                };
                info.Context = vfile;
                return NtStatus.Success;
            }

            //not a directory so test for file
            string dirPart = Path.GetDirectoryName(fileName);
            string filePart = Path.GetFileNameWithoutExtension(fileName);

            findDir.Parameters["fullname"].Value = dirPart.Substring(1) + @"\";
            object resD = findDir.ExecuteScalar();
            if (resD == null)
                return NtStatus.NoSuchFile;

            vfile = new vFile
            {
                FileName = fileName,
                isDirectory = false,  // finding a file.
                DirId = Convert.ToInt32(resD)
            };

            findGame.Parameters["did"].Value = vfile.DirId;
            findGame.Parameters["name"].Value = filePart;

            using (SQLiteDataReader resG = findGame.ExecuteReader())
            {
                if (!resG.Read())
                    return NtStatus.NoSuchFile;
                vfile.GameId = Convert.ToInt32(resG["GameId"]);
                vfile.Length = Convert.ToInt64(resG["ZipFileLength"]);
            }

            info.Context = vfile;

            if ((access & DokanNet.FileAccess.ReadData) != 0)
            {
                // opening file to read, so setup the rest of the data.
            }


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
        }

        public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, DokanFileInfo info)
        {
            throw new NotImplementedException();
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

            vFile vfile = (vFile) info.Context;
            if (vfile == null)
                return NtStatus.NoSuchFile;

            fileInfo = new FileInformation();
            fileInfo.FileName = vfile.FileName;
            fileInfo.Length = vfile.Length;
            fileInfo.CreationTime = dt;
            fileInfo.LastAccessTime = dt;
            fileInfo.LastWriteTime = dt;
            fileInfo.Attributes = vfile.isDirectory ? FileAttributes.Directory : FileAttributes.Normal;
            return NtStatus.Success;

        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, DokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files, DokanFileInfo info)
        {
            Debug.WriteLine("");
            Debug.WriteLine("-----------FindFilesWithPattern---------------------------------");
            Debug.WriteLine("Filename : " + fileName);
            Debug.WriteLine("searchPattern : " + searchPattern);

            if(searchPattern!="*")
                throw new NotImplementedException();

            files = new List<FileInformation>();

            vFile vfile = (vFile)info.Context;
            if (vfile == null)
                return NtStatus.NoSuchFile;
            long dirId = vfile.DirId;

            getDirectory.Parameters["ParentId"].Value = dirId;
            using (SQLiteDataReader dr = getDirectory.ExecuteReader())
            {
                while (dr.Read())
                {
                    FileInformation fi = new FileInformation();
                    fi.FileName = dr["name"].ToString();
                    fi.Attributes = FileAttributes.Directory | FileAttributes.ReadOnly;
                    fi.CreationTime = dt;
                    fi.LastAccessTime = dt;
                    fi.LastWriteTime = dt;
                    files.Add(fi);
                }
            }

            getFilesInDirectory.Parameters["dId"].Value = dirId;
            using (SQLiteDataReader dr = getFilesInDirectory.ExecuteReader())
            {
                while (dr.Read())
                {
                    FileInformation fi = new FileInformation();
                    fi.FileName = dr["name"] + ".zip";
                    fi.Length = Convert.ToInt64(dr["ZipFileLength"]);
                    fi.Attributes = FileAttributes.Normal | FileAttributes.ReadOnly;
                    fi.CreationTime = dt;
                    fi.LastAccessTime = dt;
                    fi.LastWriteTime = dt;
                    files.Add(fi);
                }
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
            totalNumberOfBytes = Convert.ToInt64(getTotalBytes.ExecuteScalar());
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
            throw new NotImplementedException();
        }

        public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, DokanFileInfo info)
        {
            throw new NotImplementedException();
        }
    }
}
