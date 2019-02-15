using System;
using System.IO;
using RomVaultX.SupportedFiles.SevenZip.Common;
using RomVaultX.SupportedFiles.SevenZip.Compress.LZMA;

namespace RomVaultX.SupportedFiles.SevenZip
{
    public static class LZMACompressFile
    {
        public static void CompressFile(Stream inStream, Stream outStream, out byte[] codeMSbytes, ICodeProgress p)
        {
            Int32 dictionary = 1 << 24;
            Int32 posStateBits = 2;
            Int32 litContextBits = 3; // for normal files
            Int32 litPosBits = 0;
            Int32 algorithm = 2;
            Int32 numFastBytes = 128; //64;

            string mf = "bt4";
            bool eos = true;


            CoderPropID[] propIDs =
            {
                CoderPropID.DictionarySize,
                CoderPropID.PosStateBits,
                CoderPropID.LitContextBits,
                CoderPropID.LitPosBits,
                CoderPropID.Algorithm,
                CoderPropID.NumFastBytes,
                CoderPropID.MatchFinder,
                CoderPropID.EndMarker
            };

            object[] properties =
            {
                dictionary,
                posStateBits,
                litContextBits,
                litPosBits,
                algorithm,
                numFastBytes,
                mf,
                eos
            };



            Encoder encoder = new Encoder();
            encoder.SetCoderProperties(propIDs, properties);
            using (MemoryStream codeMS = new MemoryStream())
            {
                encoder.WriteCoderProperties(codeMS);
                codeMSbytes = new byte[codeMS.Length];
                codeMS.Position = 0;
                codeMS.Read(codeMSbytes, 0, codeMSbytes.Length);
            }
            encoder.Code(inStream, outStream, -1, -1, p);
        }
    }
}
