/*
using System;
using System.Threading;
namespace RomVaultX.SupportedFiles.Files
{
    public class ThreadCRC : IDisposable
    {
        private readonly AutoResetEvent _waitEvent;
        private readonly AutoResetEvent _outEvent;
        private readonly Thread _tWorker;

        private byte[] _buffer;
        private int _size;
        private bool _finished;

        readonly uint[] _crc32Lookup;
        private uint _crc;

        public ThreadCRC()
        {
            _waitEvent = new AutoResetEvent(false);
            _outEvent = new AutoResetEvent(false);
            _finished = false;

            const uint polynomial = 0xedb88320u;

            _crc32Lookup = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint entry = i;
                for (uint j = 0; j < 8; j++)
                    if ((entry & 1) == 1)
                        entry = (entry >> 1) ^ polynomial;
                    else
                        entry = entry >> 1;
                _crc32Lookup[i] = entry;
            }

            _crc = 0xffffffffu;

            _tWorker = new Thread(MainLoop);
            _tWorker.Start();
        }

        public byte[] Hash
        {
            get
            {
                byte[] result = BitConverter.GetBytes(~_crc);
                Array.Reverse(result);
                return result;
            }
        }

        public void Dispose()
        {
            _waitEvent.Dispose();
            _outEvent.Dispose();
        }

        private void MainLoop()
        {
            while (true)
            {
                _waitEvent.WaitOne();
                if (_finished) break;
                for (int i = 0; i < _size; ++i)
                    _crc = (_crc >> 8) ^ _crc32Lookup[(_crc & 0xff) ^ _buffer[i]];

                _outEvent.Set();
            }
        }

        public void Trigger(byte[] buffer, int size)
        {
            _buffer = buffer;
            _size = size;
            _waitEvent.Set();
        }

        public void Wait()
        {
            _outEvent.WaitOne();
        }

        public void Finish()
        {
            _finished = true;
            _waitEvent.Set();
            _tWorker.Join();
        }
    }
}
*/