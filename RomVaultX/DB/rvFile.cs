using System;
using System.Data.Common;
using RomVaultX.Util;

namespace RomVaultX.DB
{
    public class RvFile
    {
        public ulong Size;
        public ulong CompressedSize;
        public byte[] CRC;
        public byte[] SHA1;
        public byte[] MD5;

        public FileType AltType;
        public ulong? AltSize;
        public byte[] AltCRC;
        public byte[] AltSHA1;
        public byte[] AltMD5;

        private static readonly DbCommand SqlWrite;
        private static readonly DbCommand SqlUpdateRom;
        private static readonly DbCommand SqlUpdateRomAlt;
        private static readonly DbCommand SqlUpdateZeroRom;

        static RvFile()
        {
            SqlWrite = Program.db.Command(
                @"INSERT INTO FILES (size,compressedsize,crc,sha1,md5,alttype,altsize,altcrc,altsha1,altmd5)
                        VALUES (@Size,@compressedsize,@CRC,@SHA1,@MD5,@alttype,@altsize,@altcrc,@altsha1,@altmd5);

                SELECT last_insert_rowid();");

            SqlWrite.Parameters.Add(Program.db.Parameter("size"));
            SqlWrite.Parameters.Add(Program.db.Parameter("compressedsize"));
            SqlWrite.Parameters.Add(Program.db.Parameter("crc"));
            SqlWrite.Parameters.Add(Program.db.Parameter("sha1"));
            SqlWrite.Parameters.Add(Program.db.Parameter("md5"));
            SqlWrite.Parameters.Add(Program.db.Parameter("alttype"));
            SqlWrite.Parameters.Add(Program.db.Parameter("altsize"));
            SqlWrite.Parameters.Add(Program.db.Parameter("altcrc"));
            SqlWrite.Parameters.Add(Program.db.Parameter("altsha1"));
            SqlWrite.Parameters.Add(Program.db.Parameter("altmd5"));

            SqlUpdateRom = Program.db.Command(
                @"
                    UPDATE ROM SET 
	                    FileId = @FileId,
                        LocalFileHeader = null,
                        LocalFileHeaderOffset = null,
                        LocalFileHeaderLength=null
                    WHERE
	                    (                 sha1 = @sha1 ) AND
	                    ( md5  is NULL OR md5  = @md5  ) AND 
	                    ( crc  is NULL OR crc  = @crc  ) AND
	                    ( size is NULL OR size = @Size ) AND
	                    ( status != 'nodump' OR status is NULL) AND 
                        FileId IS NULL;
		
                    UPDATE ROM SET 
	                    FileId = @FileId,
                        LocalFileHeader = null,
                        LocalFileHeaderOffset = null,
                        LocalFileHeaderLength=null
                    WHERE
	                    (                 md5  = @md5  ) AND 
	                    ( sha1 is NULL OR sha1 = @sha1 ) AND
	                    ( crc  is NULL OR crc  = @crc  ) AND
	                    ( size is NULL OR size = @Size ) AND
	                    ( status != 'nodump' OR status is NULL) AND 
                        FileId IS NULL;
		
                    UPDATE ROM SET 
	                    FileId = @FileId,
                        LocalFileHeader = null,
                        LocalFileHeaderOffset = null,
                        LocalFileHeaderLength=null
                    WHERE
	                    (                 crc  = @crc  ) AND
	                    ( sha1 is NULL OR sha1 = @sha1 ) AND
	                    ( md5  is NULL OR md5  = @md5  ) AND 
	                    ( size is NULL OR size = @Size ) AND
	                    ( status != 'nodump' OR status is NULL) AND 
                        FileId IS NULL;
                ");
            SqlUpdateRom.Parameters.Add(Program.db.Parameter("FileId"));
            SqlUpdateRom.Parameters.Add(Program.db.Parameter("size"));
            SqlUpdateRom.Parameters.Add(Program.db.Parameter("crc"));
            SqlUpdateRom.Parameters.Add(Program.db.Parameter("sha1"));
            SqlUpdateRom.Parameters.Add(Program.db.Parameter("md5"));

            SqlUpdateRomAlt = Program.db.Command(
                @"
                    UPDATE ROM SET 
	                    FileId = @FileId,
                        LocalFileHeader = null,
                        LocalFileHeaderOffset = null,
                        LocalFileHeaderLength=null
                    WHERE
                        (                 type = @type ) AND
	                    (                 sha1 = @sha1 ) AND
	                    ( md5  is NULL OR md5  = @md5  ) AND 
	                    ( crc  is NULL OR crc  = @crc  ) AND
	                    ( size is NULL OR size = @Size ) AND
	                    ( status != 'nodump' OR status is NULL) AND 
                        FileId IS NULL;
		
                    UPDATE ROM SET 
	                    FileId = @FileId,
                        LocalFileHeader = null,
                        LocalFileHeaderOffset = null,
                        LocalFileHeaderLength=null
                    WHERE
                        (                 type = @type ) AND
	                    (                 md5  = @md5  ) AND 
	                    ( sha1 is NULL OR sha1 = @sha1 ) AND
	                    ( crc  is NULL OR crc  = @crc  ) AND
	                    ( size is NULL OR size = @Size ) AND
	                    ( status != 'nodump' OR status is NULL) AND 
                        FileId IS NULL;
		
                    UPDATE ROM SET 
	                    FileId = @FileId,
                        LocalFileHeader = null,
                        LocalFileHeaderOffset = null,
                        LocalFileHeaderLength=null
                    WHERE
                        (                 type = @type ) AND
	                    (                 crc  = @crc  ) AND
	                    ( sha1 is NULL OR sha1 = @sha1 ) AND
	                    ( md5  is NULL OR md5  = @md5  ) AND 
	                    ( size is NULL OR size = @Size ) AND
	                    ( status != 'nodump' OR status is NULL) AND 
                        FileId IS NULL;
                ");
            SqlUpdateRomAlt.Parameters.Add(Program.db.Parameter("FileId"));
            SqlUpdateRomAlt.Parameters.Add(Program.db.Parameter("type"));
            SqlUpdateRomAlt.Parameters.Add(Program.db.Parameter("size"));
            SqlUpdateRomAlt.Parameters.Add(Program.db.Parameter("crc"));
            SqlUpdateRomAlt.Parameters.Add(Program.db.Parameter("sha1"));
            SqlUpdateRomAlt.Parameters.Add(Program.db.Parameter("md5"));



            SqlUpdateZeroRom = Program.db.Command(
                @"
                    UPDATE ROM SET 
	                    FileId = @FileId,
                        LocalFileHeader = null,
                        LocalFileHeaderOffset = null,
                        LocalFileHeaderLength=null
                    WHERE
	                    ( Size=0 ) AND
	                    ( crc  is NULL OR crc  = @crc  ) AND
	                    ( sha1 is NULL OR sha1 = @sha1 ) AND
	                    ( md5  is NULL OR md5  = @md5  ) AND 
	                    ( status != 'nodump' OR status is NULL) AND 
                        FileId IS NULL;
                ");
            SqlUpdateZeroRom.Parameters.Add(Program.db.Parameter("FileId"));
            SqlUpdateZeroRom.Parameters.Add(Program.db.Parameter("crc"));
            SqlUpdateZeroRom.Parameters.Add(Program.db.Parameter("sha1"));
            SqlUpdateZeroRom.Parameters.Add(Program.db.Parameter("md5"));

        }

        public static void MakeDB()
        {
            DataAccessLayer.ExecuteNonQuery(@"
              
                CREATE TABLE IF NOT EXISTS [FILES] (
                    [FileId] INTEGER PRIMARY KEY NOT NULL,
                    [size] INTEGER NOT NULL,
                    [compressedsize] INTEGER NULL,
                    [crc] VARCHAR(8) NULL,
                    [sha1] VARCHAR(40) NULL,
                    [md5] VARCHAR(32) NULL,
                    [alttype] VARCHAR(8) NULL,
                    [altsize] INTEGER NULL,
                    [altcrc] VARCHAR(8) NULL,
                    [altsha1] VARCHAR(40) NULL,
                    [altmd5] VARCHAR(32) NULL
                );
            ");
        }
        public void DBWrite()
        {
            DataAccessLayer.Begin();

            SqlWrite.Parameters["size"].Value = Size;
            SqlWrite.Parameters["compressedsize"].Value = CompressedSize;
            SqlWrite.Parameters["crc"].Value = VarFix.ToDBString(CRC);
            SqlWrite.Parameters["sha1"].Value = VarFix.ToDBString(SHA1);
            SqlWrite.Parameters["md5"].Value = VarFix.ToDBString(MD5);
            SqlWrite.Parameters["alttype"].Value = (int)AltType;
            SqlWrite.Parameters["altsize"].Value = AltSize;
            SqlWrite.Parameters["altcrc"].Value = VarFix.ToDBString(AltCRC);
            SqlWrite.Parameters["altsha1"].Value = VarFix.ToDBString(AltSHA1);
            SqlWrite.Parameters["altmd5"].Value = VarFix.ToDBString(AltMD5);

            object res = SqlWrite.ExecuteScalar();
            UInt32 fileId= Convert.ToUInt32(res);

            if (Size != 0)
            {
                SqlUpdateRom.Parameters["FileId"].Value = fileId;
                SqlUpdateRom.Parameters["size"].Value = Size;
                SqlUpdateRom.Parameters["crc"].Value = VarFix.ToDBString(CRC);
                SqlUpdateRom.Parameters["sha1"].Value = VarFix.ToDBString(SHA1);
                SqlUpdateRom.Parameters["md5"].Value = VarFix.ToDBString(MD5);
                SqlUpdateRom.ExecuteNonQuery();

                if (FileHeaderReader.AltHeaderFile(AltType))
                {
                    SqlUpdateRomAlt.Parameters["FileId"].Value = fileId;
                    SqlUpdateRomAlt.Parameters["type"].Value = AltType;
                    SqlUpdateRomAlt.Parameters["size"].Value = AltSize;
                    SqlUpdateRomAlt.Parameters["crc"].Value = VarFix.ToDBString(AltCRC);
                    SqlUpdateRomAlt.Parameters["sha1"].Value = VarFix.ToDBString(AltSHA1);
                    SqlUpdateRomAlt.Parameters["md5"].Value = VarFix.ToDBString(AltMD5);
                    SqlUpdateRomAlt.ExecuteNonQuery();
                }
            }
            else
            {
                SqlUpdateZeroRom.Parameters["FileId"].Value = fileId;
                SqlUpdateZeroRom.Parameters["crc"].Value = VarFix.ToDBString(CRC);
                SqlUpdateZeroRom.Parameters["sha1"].Value = VarFix.ToDBString(SHA1);
                SqlUpdateZeroRom.Parameters["md5"].Value = VarFix.ToDBString(MD5);
                SqlUpdateZeroRom.ExecuteNonQuery();                
            }
            DataAccessLayer.Commit();

        }
    }
}
