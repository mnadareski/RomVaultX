using System;
using System.Collections.Generic;
using System.IO;

namespace RomVaultX.SupportedFiles.SevenZip
{
    public class TesterStream : Stream
    {
        private int pos = 0;
        private List<byte> arrByte;
        private byte[] testarr;
        private int index = 0;

        public TesterStream(byte[] test)
        {
            arrByte = new List<byte>();
            testarr = test;
            index = 0;
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                buffer[offset + i] = arrByte[pos];
                pos += 1;
            }
            return count;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                byte test = buffer[i + offset];
                arrByte.Add(test);
                if (test != testarr[index])
                {
                    Console.WriteLine("Expected =" + testarr[index] + " : Found = " + test);
                }
                index += 1;
            }
        }

        public override bool CanRead
        {
            get { throw new NotImplementedException(); }
        }

        public override bool CanSeek
        {
            get { throw new NotImplementedException(); }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override long Length
        {
            get { return arrByte.Count; }
        }

        public override long Position { get; set; }
    }
}
