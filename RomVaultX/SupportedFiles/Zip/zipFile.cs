using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

// UInt16 = ushort
// UInt32 = uint
// ULong = ulong
using RomVaultX.SupportedFiles.Zip.ZLib;

namespace RomVaultX.SupportedFiles.Zip
{
  
    public class ZipFile
    {
        const int Buffersize = 4096 * 128;
        private static byte[] _buffer;

        private const uint LocalFileHeaderSignature = 0x04034b50;
        private const uint CentralDirectoryHeaderSigniature = 0x02014b50;
        private const uint EndOfCentralDirSignature = 0x06054b50;
        private const uint Zip64EndOfCentralDirSignatue = 0x06064b50;
        private const uint Zip64EndOfCentralDirectoryLocator = 0x07064b50;

        class LocalFile
        {
            private readonly Stream _zipFs;
            public string FileName { get; private set; }
            public ushort _generalPurposeBitFlag { get; private set; }
            private ushort _compressionMethod;
            private ushort _lastModFileTime;
            private ushort _lastModFileDate;
            public byte[] CRC { get; private set; }
            private ulong _compressedSize;
            public ulong UncompressedSize { get; private set; }
            public ulong RelativeOffsetOfLocalHeader; // only in centeral directory

            private ulong _dataLocation;

            public bool Zip64 { get; private set; }
            public bool TrrntZip { get; private set; }
            
            public byte[] sha1 { get; private set; }
            public byte[] md5 { get; private set; }
            
            public ZipReturn FileStatus = ZipReturn.ZipUntested;
            public LocalFile(Stream zipFs)
            {
                _zipFs = zipFs;
            }

            public ZipReturn CenteralDirectoryRead()
            {
                try
                {
                    BinaryReader br = new BinaryReader(_zipFs);

                    uint thisSignature = br.ReadUInt32();
                    if (thisSignature != CentralDirectoryHeaderSigniature)
                        return ZipReturn.ZipCenteralDirError;

                    br.ReadUInt16(); // Version Made By

                    br.ReadUInt16(); // Version Needed To Extract


                    _generalPurposeBitFlag = br.ReadUInt16();
                    _compressionMethod = br.ReadUInt16();
                    if (_compressionMethod != 8 && _compressionMethod != 0)
                        return ZipReturn.ZipUnsupportedCompression;

                    _lastModFileTime = br.ReadUInt16();
                    _lastModFileDate = br.ReadUInt16();
                    CRC = ReadCRC(br);

                    _compressedSize = br.ReadUInt32();
                    UncompressedSize = br.ReadUInt32();

                    ushort fileNameLength = br.ReadUInt16();
                    ushort extraFieldLength = br.ReadUInt16();
                    ushort fileCommentLength = br.ReadUInt16();

                    br.ReadUInt16(); // diskNumberStart
                    br.ReadUInt16(); // internalFileAttributes
                    br.ReadUInt32(); // externalFileAttributes

                    RelativeOffsetOfLocalHeader = br.ReadUInt32();

                    byte[] bFileName = br.ReadBytes(fileNameLength);
                    FileName = (_generalPurposeBitFlag & (1 << 11)) == 0 ?
                        GetString(bFileName) :
                        Encoding.UTF8.GetString(bFileName, 0, fileNameLength);

                    Byte[] extraField = br.ReadBytes(extraFieldLength);
                    br.ReadBytes(fileCommentLength); // File Comments

                    int pos = 0;
                    while (extraFieldLength > pos)
                    {
                        ushort type = BitConverter.ToUInt16(extraField, pos);
                        pos += 2;
                        ushort blockLength = BitConverter.ToUInt16(extraField, pos);
                        pos += 2;
                        switch (type)
                        {
                            case 0x0001:
                                Zip64 = true;
                                if (UncompressedSize == 0xffffffff)
                                {
                                    UncompressedSize = BitConverter.ToUInt64(extraField, pos);
                                    pos += 8;
                                }
                                if (_compressedSize == 0xffffffff)
                                {
                                    _compressedSize = BitConverter.ToUInt64(extraField, pos);
                                    pos += 8;
                                }
                                if (RelativeOffsetOfLocalHeader == 0xffffffff)
                                {
                                    RelativeOffsetOfLocalHeader = BitConverter.ToUInt64(extraField, pos);
                                    pos += 8;
                                }
                                break;
                            case 0x7075:
                                //byte version = extraField[pos];
                                pos += 1;
                                uint nameCRC32 = BitConverter.ToUInt32(extraField, pos);
                                pos += 4;

                                CRC32 crcTest = new CRC32();
                                crcTest.SlurpBlock(bFileName, 0, fileNameLength);
                                uint fCRC = crcTest.Crc32ResultU;

                                if (nameCRC32 != fCRC) return ZipReturn.ZipCenteralDirError;

                                int charLen = blockLength - 5;

                                FileName = Encoding.UTF8.GetString(extraField, pos, charLen);
                                pos += charLen;

                                break;
                            default:
                                pos += blockLength;
                                break;
                        }
                    }

                    return ZipReturn.ZipGood;
                }
                catch
                {
                    return ZipReturn.ZipCenteralDirError;
                }

            }

            public ZipReturn LocalFileHeaderRead()
            {
                try
                {
                    TrrntZip = true;

                    BinaryReader br = new BinaryReader(_zipFs);

                    _zipFs.Position = (long)RelativeOffsetOfLocalHeader;
                    uint thisSignature = br.ReadUInt32();
                    if (thisSignature != LocalFileHeaderSignature)
                        return ZipReturn.ZipLocalFileHeaderError;

                    br.ReadUInt16();  // version needed to extract
                    ushort generalPurposeBitFlagLocal = br.ReadUInt16();
                    if (generalPurposeBitFlagLocal != _generalPurposeBitFlag) TrrntZip = false;

                    ushort tshort = br.ReadUInt16();
                    if (tshort != _compressionMethod) return ZipReturn.ZipLocalFileHeaderError;

                    tshort = br.ReadUInt16();
                    if (tshort != _lastModFileTime) return ZipReturn.ZipLocalFileHeaderError;

                    tshort = br.ReadUInt16();
                    if (tshort != _lastModFileDate) return ZipReturn.ZipLocalFileHeaderError;

                    byte[] tCRC = ReadCRC(br);
                    if (((_generalPurposeBitFlag & 8) == 0) && !ByteArrCompare(tCRC, CRC)) return ZipReturn.ZipLocalFileHeaderError;

                    uint tCompressedSize = br.ReadUInt32();
                    if (Zip64 && tCompressedSize != 0xffffffff && tCompressedSize != _compressedSize)   // if Zip64 File then the compressedSize should be 0xffffffff
                        return ZipReturn.ZipLocalFileHeaderError;
                    if ((_generalPurposeBitFlag & 8) == 8 && tCompressedSize != 0)   // if bit 4 set then no compressedSize is set yet
                        return ZipReturn.ZipLocalFileHeaderError;
                    if (!Zip64 && (_generalPurposeBitFlag & 8) != 8 && tCompressedSize != _compressedSize) // check the compressedSize
                        return ZipReturn.ZipLocalFileHeaderError;



                    uint tUnCompressedSize = br.ReadUInt32();
                    if (Zip64 && tUnCompressedSize != 0xffffffff && tUnCompressedSize != UncompressedSize)   // if Zip64 File then the unCompressedSize should be 0xffffffff
                        return ZipReturn.ZipLocalFileHeaderError;
                    if ((_generalPurposeBitFlag & 8) == 8 && tUnCompressedSize != 0)   // if bit 4 set then no unCompressedSize is set yet
                        return ZipReturn.ZipLocalFileHeaderError;
                    if (!Zip64 && (_generalPurposeBitFlag & 8) != 8 && tUnCompressedSize != UncompressedSize) // check the unCompressedSize
                        return ZipReturn.ZipLocalFileHeaderError;

                    ushort fileNameLength = br.ReadUInt16();
                    ushort extraFieldLength = br.ReadUInt16();


                    byte[] bFileName = br.ReadBytes(fileNameLength);
                    string tFileName = (generalPurposeBitFlagLocal & (1 << 11)) == 0 ?
                        GetString(bFileName) :
                        Encoding.UTF8.GetString(bFileName, 0, fileNameLength);

                    byte[] extraField = br.ReadBytes(extraFieldLength);


                    Zip64 = false;
                    int pos = 0;
                    while (extraFieldLength > pos)
                    {
                        ushort type = BitConverter.ToUInt16(extraField, pos);
                        pos += 2;
                        ushort blockLength = BitConverter.ToUInt16(extraField, pos);
                        pos += 2;
                        switch (type)
                        {
                            case 0x0001:
                                Zip64 = true;
                                if (tUnCompressedSize == 0xffffffff)
                                {
                                    ulong tLong = BitConverter.ToUInt64(extraField, pos);
                                    if (tLong != UncompressedSize) return ZipReturn.ZipLocalFileHeaderError;
                                    pos += 8;
                                }
                                if (tCompressedSize == 0xffffffff)
                                {
                                    ulong tLong = BitConverter.ToUInt64(extraField, pos);
                                    if (tLong != _compressedSize) return ZipReturn.ZipLocalFileHeaderError;
                                    pos += 8;
                                }
                                break;
                            case 0x7075:
                                //byte version = extraField[pos];
                                pos += 1;
                                uint nameCRC32 = BitConverter.ToUInt32(extraField, pos);
                                pos += 4;

                                CRC32 crcTest = new CRC32();
                                crcTest.SlurpBlock(bFileName, 0, fileNameLength);
                                uint fCRC = crcTest.Crc32ResultU;

                                if (nameCRC32 != fCRC) return ZipReturn.ZipLocalFileHeaderError;

                                int charLen = blockLength - 5;

                                tFileName = Encoding.UTF8.GetString(extraField, pos, charLen);
                                pos += charLen;

                                break;
                            default:
                                pos += blockLength;
                                break;
                        }
                    }

                    if (!CompareString(FileName, tFileName)) return ZipReturn.ZipLocalFileHeaderError;

                    _dataLocation = (ulong)_zipFs.Position;

                    if ((_generalPurposeBitFlag & 8) == 0) return ZipReturn.ZipGood;

                    _zipFs.Position += (long)_compressedSize;

                    tCRC = ReadCRC(br);
                    if (!ByteArrCompare(tCRC, new byte[] { 0x50, 0x4b, 0x07, 0x08 }))
                        tCRC = ReadCRC(br);

                    if (!ByteArrCompare(tCRC, CRC)) return ZipReturn.ZipLocalFileHeaderError;

                    uint tint = br.ReadUInt32();
                    if (tint != _compressedSize) return ZipReturn.ZipLocalFileHeaderError;

                    tint = br.ReadUInt32();
                    if (tint != UncompressedSize) return ZipReturn.ZipLocalFileHeaderError;

                    return ZipReturn.ZipGood;
                }
                catch
                {
                    return ZipReturn.ZipLocalFileHeaderError;
                }


            }

            private Stream _readStream;
            public ZipReturn LocalFileOpenReadStream(bool raw, out Stream stream, out ulong streamSize, out ushort compressionMethod)
            {
                streamSize = 0;
                compressionMethod = _compressionMethod;

                _readStream = null;
                _zipFs.Seek((long)_dataLocation, SeekOrigin.Begin);

                switch (_compressionMethod)
                {
                    case 8:
                        if (raw)
                        {
                            _readStream = _zipFs;
                            streamSize = _compressedSize;
                        }
                        else
                        {
                            _readStream = new DeflateStream(_zipFs, CompressionMode.Decompress, true);
                            streamSize = UncompressedSize;

                        }
                        break;
                    case 0:
                        _readStream = _zipFs;
                        streamSize = _compressedSize;  // same as UncompressedSize
                        break;
                }
                stream = _readStream;
                return stream == null ? ZipReturn.ZipErrorGettingDataStream : ZipReturn.ZipGood;
            }
            public ZipReturn LocalFileCloseReadStream()
            {
                DeflateStream dfStream = _readStream as DeflateStream;
                if (dfStream != null)
                {
                    dfStream.Close();
                    dfStream.Dispose();
                }
                return ZipReturn.ZipGood;
            }



            public void LocalFileCheck()
            {
                if (FileStatus != ZipReturn.ZipUntested) return;

                try
                {
                    Stream sInput = null;
                    _zipFs.Seek((long)_dataLocation, SeekOrigin.Begin);

                    switch (_compressionMethod)
                    {
                        case 8:
                            sInput = new DeflateStream(_zipFs, CompressionMode.Decompress, true);
                            break;
                        case 0:
                            sInput = _zipFs;
                            break;
                    }

                    if (sInput == null)
                    {
                        FileStatus = ZipReturn.ZipErrorGettingDataStream;
                        return;
                    }

                    CRC32Hash crc32 = new CRC32Hash();
                    MD5 lmd5 = System.Security.Cryptography.MD5.Create();
                    SHA1 lsha1 = System.Security.Cryptography.SHA1.Create();

                    ulong sizetogo = UncompressedSize;
                    if (_buffer == null)
                        _buffer = new byte[Buffersize];

                    while (sizetogo > 0)
                    {
                        int sizenow = sizetogo > Buffersize ? Buffersize : (int)sizetogo;
                        sInput.Read(_buffer, 0, sizenow);

                        crc32.TransformBlock(_buffer, 0, sizenow, null, 0);
                        lmd5.TransformBlock(_buffer, 0, sizenow, null, 0);
                        lsha1.TransformBlock(_buffer, 0, sizenow, null, 0);

                        sizetogo = sizetogo - (ulong)sizenow;
                    }

                    crc32.TransformFinalBlock(_buffer, 0, 0);
                    lmd5.TransformFinalBlock(_buffer, 0, 0);
                    lsha1.TransformFinalBlock(_buffer, 0, 0);

                    byte[] testcrc = crc32.Hash;
                    md5 = lmd5.Hash;
                    sha1 = lsha1.Hash;

                    if (_compressionMethod == 8)
                    {
                        sInput.Close();
                        sInput.Dispose();
                    }

                    FileStatus = ByteArrCompare(CRC, testcrc) ? ZipReturn.ZipGood : ZipReturn.ZipCRCDecodeError;
                }
                catch
                {
                    FileStatus = ZipReturn.ZipDecodeError;
                }
            }

            private static byte[] ReadCRC(BinaryReader br)
            {
                byte[] tCRC = new byte[4];
                tCRC[3] = br.ReadByte();
                tCRC[2] = br.ReadByte();
                tCRC[1] = br.ReadByte();
                tCRC[0] = br.ReadByte();
                return tCRC;
            }


        }


        private IO.FileInfo _zipFileInfo;


        private ulong _centerDirStart;
        private ulong _centerDirSize;
        private ulong _endOfCenterDir64;

        byte[] _fileComment;
        private Stream _zipFs;

        private uint _localFilesCount;
        private readonly List<LocalFile> _localFiles = new List<LocalFile>();

        private ZipStatus _pZipStatus;
        private bool _zip64;
        public ZipOpenType ZipOpen;

        public ZipStatus ZipStatus { get { return _pZipStatus; } }

        public int LocalFilesCount() { return _localFiles.Count; }

        public string Filename(int i) { return _localFiles[i].FileName; }
        public ulong UncompressedSize(int i) { return _localFiles[i].UncompressedSize; }
        public ulong? LocalHeader(int i)
        {
            return ((_localFiles[i]._generalPurposeBitFlag & 8) == 0) ? (ulong?)_localFiles[i].RelativeOffsetOfLocalHeader : null;
        }
        public ZipReturn FileStatus(int i) { return _localFiles[i].FileStatus; }
        public byte[] CRC32(int i) { return _localFiles[i].CRC; }
        public byte[] MD5(int i) { return _localFiles[i].md5; }
        public byte[] SHA1(int i) { return _localFiles[i].sha1; }


        ~ZipFile()
        {
            if (_zipFs != null)
            {
                _zipFs.Close();
                _zipFs.Dispose();
            }
        }


        private ZipReturn FindEndOfCentralDirSignature()
        {
            long fileSize = _zipFs.Length;
            long maxBackSearch = 0xffff;

            if (_zipFs.Length < maxBackSearch)
                maxBackSearch = fileSize;

            const long buffSize = 0x400;

            byte[] buffer = new byte[buffSize + 4];

            long backPosition = 4;
            while (backPosition < maxBackSearch)
            {
                backPosition += buffSize;
                if (backPosition > maxBackSearch) backPosition = maxBackSearch;

                long readSize = backPosition > (buffSize + 4) ? (buffSize + 4) : backPosition;

                _zipFs.Position = fileSize - backPosition;

                _zipFs.Read(buffer, 0, (int)readSize);


                for (long i = readSize - 4; i >= 0; i--)
                {
                    if ((buffer[i] != 0x50) || (buffer[i + 1] != 0x4b) || (buffer[i + 2] != 0x05) || (buffer[i + 3] != 0x06)) continue;

                    _zipFs.Position = (fileSize - backPosition) + i;
                    return ZipReturn.ZipGood;
                }
            }
            return ZipReturn.ZipCenteralDirError;
        }


        private ZipReturn EndOfCentralDirRead()
        {
            BinaryReader zipBr = new BinaryReader(_zipFs);

            uint thisSignature = zipBr.ReadUInt32();
            if (thisSignature != EndOfCentralDirSignature)
                return ZipReturn.ZipEndOfCentralDirectoryError;

            ushort tushort = zipBr.ReadUInt16();     // NumberOfThisDisk
            if (tushort != 0) return ZipReturn.ZipEndOfCentralDirectoryError;

            tushort = zipBr.ReadUInt16();     // NumberOfThisDiskCenterDir
            if (tushort != 0) return ZipReturn.ZipEndOfCentralDirectoryError;

            _localFilesCount = zipBr.ReadUInt16();     // TotalNumberOfEnteriesDisk

            tushort = zipBr.ReadUInt16();     // TotalNumber of enteries in the central directory 
            if (tushort != _localFilesCount) return ZipReturn.ZipEndOfCentralDirectoryError;

            _centerDirSize = zipBr.ReadUInt32();     // SizeOfCenteralDir
            _centerDirStart = zipBr.ReadUInt32();     // Offset

            ushort zipFileCommentLength = zipBr.ReadUInt16();

            _fileComment = zipBr.ReadBytes(zipFileCommentLength);

            if (_zipFs.Position != _zipFs.Length) _pZipStatus |= ZipStatus.ExtraData;

            return ZipReturn.ZipGood;
        }

        private ZipReturn Zip64EndOfCentralDirRead()
        {
            _zip64 = true;
            BinaryReader zipBr = new BinaryReader(_zipFs);

            uint thisSignature = zipBr.ReadUInt32();
            if (thisSignature != Zip64EndOfCentralDirSignatue)
                return ZipReturn.ZipEndOfCentralDirectoryError;

            ulong tulong = zipBr.ReadUInt64(); // Size of zip64 end of central directory record
            if (tulong != 44) return ZipReturn.Zip64EndOfCentralDirError;

            zipBr.ReadUInt16(); // version made by

            ushort tushort = zipBr.ReadUInt16(); // version needed to extract
            if (tushort != 45) return ZipReturn.Zip64EndOfCentralDirError;

            uint tuint = zipBr.ReadUInt32(); // number of this disk
            if (tuint != 0) return ZipReturn.Zip64EndOfCentralDirError;

            tuint = zipBr.ReadUInt32(); // number of the disk with the start of the central directory
            if (tuint != 0) return ZipReturn.Zip64EndOfCentralDirError;

            _localFilesCount = (uint)zipBr.ReadUInt64(); // total number of entries in the central directory on this disk

            tulong = zipBr.ReadUInt64(); // total number of entries in the central directory
            if (tulong != _localFilesCount) return ZipReturn.Zip64EndOfCentralDirError;

            _centerDirSize = zipBr.ReadUInt64(); // size of central directory

            _centerDirStart = zipBr.ReadUInt64(); // offset of start of central directory with respect to the starting disk number

            return ZipReturn.ZipGood;
        }

        private ZipReturn Zip64EndOfCentralDirectoryLocatorRead()
        {
            _zip64 = true;
            BinaryReader zipBr = new BinaryReader(_zipFs);

            uint thisSignature = zipBr.ReadUInt32();
            if (thisSignature != Zip64EndOfCentralDirectoryLocator)
                return ZipReturn.ZipEndOfCentralDirectoryError;

            uint tuint = zipBr.ReadUInt32();  // number of the disk with the start of the zip64 end of centeral directory
            if (tuint != 0) return ZipReturn.Zip64EndOfCentralDirectoryLocatorError;

            _endOfCenterDir64 = zipBr.ReadUInt64(); // relative offset of the zip64 end of central directroy record

            tuint = zipBr.ReadUInt32();  // total number of disks
            if (tuint != 1) return ZipReturn.Zip64EndOfCentralDirectoryLocatorError;

            return ZipReturn.ZipGood;
        }

        public ZipReturn ZipFileOpen(string newFilename, long timestamp, bool readHeaders)
        {
            ZipFileClose();
            _pZipStatus = ZipStatus.None;
            _zip64 = false;
            _centerDirStart = 0;
            _centerDirSize = 0;
            _zipFileInfo = null;

            try
            {
                if (!IO.File.Exists(newFilename))
                {
                    ZipFileClose();
                    return ZipReturn.ZipErrorFileNotFound;
                }
                if (timestamp != 0)
                {
                    _zipFileInfo = new IO.FileInfo(newFilename);
                    if (_zipFileInfo.LastWriteTime != timestamp)
                    {
                        ZipFileClose();
                        return ZipReturn.ZipErrorTimeStamp;
                    }
                }
                int errorCode = IO.FileStream.OpenFileRead(newFilename, out _zipFs);
                if (errorCode != 0)
                {
                    ZipFileClose();
                    if (errorCode == 32)
                        return ZipReturn.ZipFileLocked;
                    return ZipReturn.ZipErrorOpeningFile;
                }
            }
            catch (PathTooLongException)
            {
                ZipFileClose();
                return ZipReturn.ZipFileNameToLong;
            }
            catch (IOException)
            {
                ZipFileClose();
                return ZipReturn.ZipErrorOpeningFile;
            }
            ZipOpen = ZipOpenType.OpenRead;

            if (!readHeaders)
                return ZipReturn.ZipGood;


            try
            {
                ZipReturn zRet = FindEndOfCentralDirSignature();
                if (zRet != ZipReturn.ZipGood)
                {
                    ZipFileClose();
                    return zRet;
                }

                long endOfCentralDir = _zipFs.Position;
                zRet = EndOfCentralDirRead();
                if (zRet != ZipReturn.ZipGood)
                {
                    ZipFileClose();
                    return zRet;
                }

                // check if this is a ZIP64 zip and if it is read the Zip64 End Of Central Dir Info
                if (_centerDirStart == 0xffffffff || _centerDirSize == 0xffffffff || _localFilesCount == 0xffff)
                {
                    _zip64 = true;
                    _zipFs.Position = endOfCentralDir - 20;
                    zRet = Zip64EndOfCentralDirectoryLocatorRead();
                    if (zRet != ZipReturn.ZipGood)
                    {
                        ZipFileClose();
                        return zRet;
                    }
                    _zipFs.Position = (long)_endOfCenterDir64;
                    zRet = Zip64EndOfCentralDirRead();
                    if (zRet != ZipReturn.ZipGood)
                    {
                        ZipFileClose();
                        return zRet;
                    }
                }

                bool trrntzip = false;

                // check if the ZIP has a valid TorrentZip file comment
                if (_fileComment.Length == 22)
                {
                    if (GetString(_fileComment).Substring(0, 14) == "TORRENTZIPPED-")
                    {
                        CrcCalculatorStream crcCs = new CrcCalculatorStream(_zipFs, true);
                        byte[] buffer = new byte[_centerDirSize];
                        _zipFs.Position = (long)_centerDirStart;
                        crcCs.Read(buffer, 0, (int)_centerDirSize);
                        crcCs.Flush();
                        crcCs.Close();

                        uint r = (uint)crcCs.Crc;
                        crcCs.Dispose();

                        string tcrc = GetString(_fileComment).Substring(14, 8);
                        string zcrc = r.ToString("X8");
                        if (String.Compare(tcrc, zcrc, StringComparison.Ordinal) == 0)
                            trrntzip = true;

                    }
                }


                // now read the central directory
                _zipFs.Position = (long)_centerDirStart;

                _localFiles.Clear();
                _localFiles.Capacity = (int)_localFilesCount;
                for (int i = 0; i < _localFilesCount; i++)
                {
                    LocalFile lc = new LocalFile(_zipFs);
                    zRet = lc.CenteralDirectoryRead();
                    if (zRet != ZipReturn.ZipGood)
                    {
                        ZipFileClose();
                        return zRet;
                    }
                    _zip64 |= lc.Zip64;
                    _localFiles.Add(lc);
                }

                for (int i = 0; i < _localFilesCount; i++)
                {
                    zRet = _localFiles[i].LocalFileHeaderRead();
                    if (zRet != ZipReturn.ZipGood)
                    {
                        ZipFileClose();
                        return zRet;
                    }
                    trrntzip &= _localFiles[i].TrrntZip;
                }

                // check trrntzip file order
                if (trrntzip)
                    for (int i = 0; i < _localFilesCount - 1; i++)
                    {
                        if (TrrntZipStringCompare(_localFiles[i].FileName, _localFiles[i + 1].FileName) < 0) continue;
                        trrntzip = false;
                        break;
                    }

                // check trrntzip directories
                if (trrntzip)
                    for (int i = 0; i < _localFilesCount - 1; i++)
                    {
                        // see if we found a directory
                        string filename0 = _localFiles[i].FileName;
                        if (filename0.Substring(filename0.Length - 1, 1) != "/") continue;

                        // see if the next file is in that directory
                        string filename1 = _localFiles[i + 1].FileName;
                        if (filename1.Length <= filename0.Length) continue;
                        if (TrrntZipStringCompare(filename0, filename1.Substring(0, filename0.Length)) != 0) continue;

                        // if we found a file in the directory then we do not need the directory entry
                        trrntzip = false;
                        break;
                    }

                if (trrntzip)
                    _pZipStatus |= ZipStatus.TrrntZip;

                return ZipReturn.ZipGood;
            }
            catch
            {
                ZipFileClose();
                return ZipReturn.ZipErrorReadingFile;
            }

        }

        public void ZipFileClose()
        {
            if (ZipOpen == ZipOpenType.Closed)
            {
                return;
            }

            if (ZipOpen == ZipOpenType.OpenRead)
            {
                if (_zipFs != null)
                {
                    _zipFs.Close();
                    _zipFs.Dispose();
                }
                ZipOpen = ZipOpenType.Closed;
                return;
            }
        }

        private int _readIndex;
        public ZipReturn ZipFileOpenReadStream(int index, bool raw, out Stream stream, out ulong streamSize, out ushort compressionMethod)
        {
            streamSize = 0;
            compressionMethod = 0;
            _readIndex = index;
            stream = null;
            if (ZipOpen != ZipOpenType.OpenRead)
                return ZipReturn.ZipReadingFromOutputFile;

            ZipReturn zRet = _localFiles[index].LocalFileHeaderRead();
            if (zRet != ZipReturn.ZipGood)
            {
                ZipFileClose();
                return zRet;
            }

            return _localFiles[index].LocalFileOpenReadStream(raw, out stream, out streamSize, out compressionMethod);
        }


        public ZipReturn ZipFileCloseReadStream()
        {
            return _localFiles[_readIndex].LocalFileCloseReadStream();
        }

        public void DeepScan()
        {
            foreach (LocalFile lfile in _localFiles)
                lfile.LocalFileCheck();
        }

        private static void CreateDirForFile(string sFilename)
        {
            string strTemp = IO.Path.GetDirectoryName(sFilename);

            if (String.IsNullOrEmpty(strTemp)) return;

            if (IO.Directory.Exists(strTemp)) return;


            while (strTemp.Length > 0 && !IO.Directory.Exists(strTemp))
            {
                int pos = strTemp.LastIndexOf(IO.Path.DirectorySeparatorChar);
                if (pos < 0) pos = 0;
                strTemp = strTemp.Substring(0, pos);
            }

            while (sFilename.IndexOf(IO.Path.DirectorySeparatorChar, strTemp.Length + 1) > 0)
            {
                strTemp = sFilename.Substring(0, sFilename.IndexOf(IO.Path.DirectorySeparatorChar, strTemp.Length + 1));
                IO.Directory.CreateDirectory(strTemp);
            }
        }

        private static string GetString(byte[] b)
        {
            string s = "";
            for (int i = 0; i < b.Length; i++)
                s += (char)b[i];
            return s;
        }
        private static bool CompareString(string s1, string s2)
        {
            char[] c1 = s1.ToCharArray();
            char[] c2 = s2.ToCharArray();

            if (c1.Length != c2.Length)
                return false;

            for (int i = 0; i < c1.Length; i++)
                if (c1[i] != c2[i]) return false;
            return true;
        }


        private static bool ByteArrCompare(byte[] b0, byte[] b1)
        {
            if (b0 == null || b1 == null)
                return false;
            if (b0.Length != b1.Length)
                return false;

            for (int i = 0; i < b0.Length; i++)
            {
                if (b0[i] != b1[i])
                    return false;
            }
            return true;
        }

        private static int TrrntZipStringCompare(string string1, string string2)
        {
            char[] bytes1 = string1.ToCharArray();
            char[] bytes2 = string2.ToCharArray();

            int pos1 = 0;
            int pos2 = 0;

            for (; ; )
            {
                if (pos1 == bytes1.Length)
                    return ((pos2 == bytes2.Length) ? 0 : -1);
                if (pos2 == bytes2.Length)
                    return 1;

                int byte1 = bytes1[pos1++];
                int byte2 = bytes2[pos2++];

                if (byte1 >= 65 && byte1 <= 90) byte1 += 0x20;
                if (byte2 >= 65 && byte2 <= 90) byte2 += 0x20;

                if (byte1 < byte2)
                    return -1;
                if (byte1 > byte2)
                    return 1;
            }
        }
    }

}
