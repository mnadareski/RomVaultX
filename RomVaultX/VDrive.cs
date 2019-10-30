using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;

using RomVaultX.SupportedFiles.GZ;
using RomVaultX.Util;

using DokanNet;

using FileAccess = DokanNet.FileAccess;

namespace RomVaultX
{
    public class VDrive : IDokanOperations
    {
        private const FileAccess DataAccess = FileAccess.ReadData | FileAccess.WriteData | FileAccess.AppendData |
                FileAccess.Execute | FileAccess.GenericExecute | FileAccess.GenericWrite | FileAccess.GenericRead;

        private const FileAccess DataWriteAccess = FileAccess.WriteData | FileAccess.AppendData |
                FileAccess.Delete | FileAccess.GenericWrite;

        // TODO: Can this be cached? Or even populated when the dats are being loaded?
        private static long TotalBytes()
        {
            return 8L * 1024 * 1024 * 1024;
            using (DbCommand getTotalBytes = Program.db.Command(@"select sum(zipfilelength) from game"))
            {
                try
                {
                    return Convert.ToInt64(getTotalBytes.ExecuteScalar());
                }
                catch
                {
                    return 81920000;
                }
            }
        }

        // TODO: Explorer calls through to `LoadVFile` properly but 7z does not... why?
        public NtStatus CreateFile(string fileName, FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, IDokanFileInfo info)
        {
            Debug.WriteLine("");
            Debug.WriteLine("-----------CreateFile---------------------------------");
            Debug.WriteLine("Filename : " + fileName + " IsDirectory : " + info.IsDirectory);

            VFile vDir;
            bool sevenzip = false;

            // If we have a 7zip access
            if (fileName.EndsWith("\\" + frmMain.vDriveLetter))
                fileName = fileName.Remove(fileName.Length - 2);
            if (fileName.EndsWith(".zip") && info.IsDirectory)
            {
                //sevenzip = true;
                info.IsDirectory = false;
            }

            //First check a DIR. (If we know it is a directory.)
            if (info.IsDirectory)
            {
                switch (mode)
                {
                    case FileMode.Open:
                        vDir = VFile.DirInfo(fileName);
                        info.Context = vDir;
                        return vDir != null ? DokanResult.Success : DokanResult.PathNotFound;
                    case FileMode.CreateNew:
                        vDir = VFile.DirInfo(fileName);
                        if (vDir != null)
                            return DokanResult.FileExists;

                        vDir = VFile.CreateDir(fileName);
                        return vDir != null ? DokanResult.Success : DokanResult.PathNotFound;
                    default:
                        throw new NotImplementedException("Missing Directory Mode " + mode);
                }
            }

            //Check again for a DIR. (As we may not have know we have a directory.)
            vDir = VFile.DirInfo(fileName);
            if (vDir != null && vDir.IsDirectory)
            {
                switch (mode)
                {
                    case FileMode.Open:
                        info.IsDirectory = true;
                        info.Context = vDir;
                        return DokanResult.Success;
                    default:
                        throw new NotImplementedException("Missing Directory Mode " + mode);
                }
            }

            bool readWriteAttributes = (access & DataAccess) == 0;
            bool readAccess = (access & DataWriteAccess) == 0;

            switch (mode)
            {
                case FileMode.Open:
                    VFile vfile = VFile.GetFile(fileName);
                    if (vfile == null)
                        return DokanResult.FileNotFound;

                    if (readWriteAttributes)
                    {
                        if (sevenzip)
                            info.IsDirectory = true;

                        info.Context = vfile;
                        return DokanResult.Success;
                    }

                    if (readAccess)
                    {
                        if (!vfile.LoadVFile())
                            return DokanResult.Error;

                        if (sevenzip)
                            info.IsDirectory = true;

                        info.Context = vfile;
                        return DokanResult.Success;
                    }

                    // looks like we are trying to write to the file.
                    return DokanResult.AccessDenied;
                default:
                    throw new NotImplementedException("Missing Directory Mode " + mode);
            }
        }

        public void Cleanup(string fileName, IDokanFileInfo info)
        {
            Debug.WriteLine("");
            Debug.WriteLine("-----------Cleanup---------------------------------");
            Debug.WriteLine("Filename : " + fileName);
        }

        public void CloseFile(string fileName, IDokanFileInfo info)
        {
            Debug.WriteLine("");
            Debug.WriteLine("-----------CloseFile---------------------------------");
            Debug.WriteLine("Filename : " + fileName);

            VFile vfile = (VFile)info.Context;
            if (vfile?.Files == null)
                return;

            foreach (VFile.VZipFile gf in vfile.Files)
                gf.GZip?.Close();
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

        private void copyStream(VFile.VZipFile source, byte[] destination, long sourceOffset, long destinationOffset, long sourceLength, long destinationLength)
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

                string strFilename = GetFilename(source.GZipSHA1);
                source.GZip.ReadGZip(strFilename, false);
            }

            source.GZip.GetRawStream(out Stream coms);
            coms.Position += sourceStart;

            long actualLength = Math.Min(sourceLength, destinationLength);
            coms.Read(destination, (int)destinationStart, (int)actualLength);

            coms.Close();
        }

        public static string GetFilename(byte[] SHA1)
        {
            string path = "";

            bool exists = false;
            int i = 0;
            while (!exists)
            {
                string romRoot = AppSettings.ReadSetting("Depot" + i);
                if (romRoot == null)
                    break;

                path = romRoot + @"\" + VarFix.ToString(SHA1[0]) + @"\" +
                        VarFix.ToString(SHA1[1]) + @"\" +
                        VarFix.ToString(SHA1[2]) + @"\" +
                        VarFix.ToString(SHA1[3]) + @"\" +
                        VarFix.ToString(SHA1) + ".gz";
                exists = Alphaleonis.Win32.Filesystem.File.Exists(path);
                i++;
            }

            if (!exists)
            {
                path = @"RomRoot\" + VarFix.ToString(SHA1[0]) + @"\" +
                        VarFix.ToString(SHA1[1]) + @"\" +
                        VarFix.ToString(SHA1[2]) + @"\" +
                        VarFix.ToString(SHA1[3]) + @"\" +
                        VarFix.ToString(SHA1) + ".gz";
            }

            return path;
        }

        // TODO: Explorer calls this properly but 7z does not... why?
        public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, IDokanFileInfo info)
        {
            bytesRead = 0;
            VFile vfile = (VFile)info.Context;
            if (vfile == null)
                return NtStatus.NoSuchFile;

            // trying to fill all of the buffer
            bytesRead = buffer.Length;

            // if reading past the EOF then read 0 bytes
            if (Math.Abs(offset) > vfile.Length)
            {
                bytesRead = 0;
                return NtStatus.Success;
            }

            // if reading the entire file then set the number of bytes left to read
            if (Math.Abs(offset) + bytesRead > vfile.Length)
                bytesRead = (int)(vfile.Length - Math.Abs(offset));

            // If we have a positive offset (read from begin)
            if (offset >= 0)
            {
                foreach (VFile.VZipFile gf in vfile.Files)
                {
                    copyData(gf.LocalHeader, buffer, gf.LocalHeaderOffset, offset, gf.LocalHeaderLength, bytesRead);
                    copyStream(gf, buffer, gf.CompressedDataOffset, offset, gf.CompressedDataLength, bytesRead);
                }
            }
            // If we have a negative offset (read from end, usually only for archive initial reads)
            else
            {
                foreach (VFile.VZipFile gf in vfile.Files.Reverse())
                {
                    //copyStream(gf, buffer, gf.CompressedDataOffset, offset, gf.CompressedDataLength, bytesRead); // This should not even be used
                    copyData(gf.LocalHeader.Reverse().ToArray(), buffer, gf.LocalHeaderOffset, (vfile.Length + offset), gf.LocalHeaderLength, bytesRead);
                }
            }

            return NtStatus.Success;
        }

        public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus FlushFileBuffers(string fileName, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, IDokanFileInfo info)
        {
            Debug.WriteLine("");
            Debug.WriteLine("-----------GetFileInformation---------------------------------");
            Debug.WriteLine("Filename : " + fileName);

            VFile vfile = (VFile)info.Context;
            if (vfile == null)
            {
                fileInfo = new FileInformation();
                return NtStatus.NoSuchFile;
            }

            vfile.LoadVFile();
            info.Context = vfile;
            fileInfo = (FileInformation)vfile;

            return NtStatus.Success;
        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, IDokanFileInfo info)
        {
            Debug.WriteLine("");
            Debug.WriteLine("-----------FindFiles---------------------------------");
            Debug.WriteLine("Filename : " + fileName);

            return FindFilesWithPattern(fileName, "*", out files, info);
        }

        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files, IDokanFileInfo info)
        {
            Debug.WriteLine("");
            Debug.WriteLine("-----------FindFilesWithPattern---------------------------------");
            Debug.WriteLine("Filename : " + fileName);
            Debug.WriteLine("searchPattern : " + searchPattern);

            // if (searchPattern != "*" && searchPattern !="DatRoot")
            //	 Debug.WriteLine("Unknown search pattern");

            files = new List<FileInformation>();

            VFile vfile = (VFile)info.Context;
            if (vfile == null)
            {
                return NtStatus.NoSuchFile;
            }

            GetEmptyDirectoryDefaultFiles(fileName, ref files);

            List<VFile> dirs = VFile.DirGetSubDirs(vfile.FileId);
            foreach (VFile dir in dirs)
            {
                if (searchPattern != "*" && searchPattern != dir.FileName)
                {
                    continue;
                }
                FileInformation fi = (FileInformation)dir;
                files.Add(fi);
            }

            List<VFile> dFiles = VFile.DirGetFiles(vfile.FileId);
            foreach (VFile file in dFiles)
            {
                if (searchPattern != "*" && searchPattern != file.FileName + ".zip")
                {
                    continue;
                }
                FileInformation fi = (FileInformation)file;
                files.Add(fi);
            }

            return NtStatus.Success;
        }

        private void GetEmptyDirectoryDefaultFiles(string fileName, ref IList<FileInformation> files)
        {
            if (fileName == "\\")
            {
                return;
            }
            files.Add(new FileInformation() { FileName = ".", Attributes = FileAttributes.Directory, CreationTime = DateTime.Today, LastWriteTime = DateTime.Today, LastAccessTime = DateTime.Today });
            files.Add(new FileInformation() { FileName = "..", Attributes = FileAttributes.Directory, CreationTime = DateTime.Today, LastWriteTime = DateTime.Today, LastAccessTime = DateTime.Today });
        }

        public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus DeleteFile(string fileName, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus DeleteDirectory(string fileName, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus SetEndOfFile(string fileName, long length, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus SetAllocationSize(string fileName, long length, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus LockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus UnlockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes, out long totalNumberOfFreeBytes, IDokanFileInfo info)
        {
            freeBytesAvailable = 0;
            totalNumberOfBytes = TotalBytes();
            totalNumberOfFreeBytes = 0;
            return DokanResult.Success;
        }

        public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features, out string fileSystemName, out uint maximumComponentLength, IDokanFileInfo info)
        {
            volumeLabel = "RomVaultX";
            fileSystemName = "NTFS";
            features = FileSystemFeatures.CasePreservedNames | FileSystemFeatures.CaseSensitiveSearch |
                        FileSystemFeatures.PersistentAcls | FileSystemFeatures.SupportsRemoteStorage |
                        FileSystemFeatures.UnicodeOnDisk;
            maximumComponentLength = 256;
            return DokanResult.Success;
        }

        public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security, AccessControlSections sections, IDokanFileInfo info)
        {
            Debug.WriteLine("");
            Debug.WriteLine("-----------GetFileSecurity---------------------------------");
            Debug.WriteLine("Filename : " + fileName);

            security = new DirectorySecurity();
            return NtStatus.Success;
        }

        public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus Mounted(IDokanFileInfo info)
        {
            return NtStatus.Success;
        }

        public NtStatus Unmounted(IDokanFileInfo info)
        {
            return NtStatus.Success;
        }

        public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, IDokanFileInfo info)
        {
            Debug.WriteLine("");
            Debug.WriteLine("-----------FindStreams---------------------------------");
            Debug.WriteLine("Filename : " + fileName);

            streams = new FileInformation[0];
            return NtStatus.Success;
        }
    }
}
