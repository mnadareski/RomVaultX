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

        private static readonly uint[] _crc32Lookup;
        private uint _crc;

        static ThreadCRC()
        {
            const uint polynomial = 0xEDB88320;
            const int CRC_NUM_TABLES = 8;

            unchecked
            {
                _crc32Lookup = new uint[256 * CRC_NUM_TABLES];
                int i;
                for (i = 0; i < 256; i++)
                {
                    uint r = (uint)i;
                    for (int j = 0; j < 8; j++)
                        r = (r >> 1) ^ (polynomial & ~((r & 1) - 1));
                    _crc32Lookup[i] = r;
                }
                for (; i < 256 * CRC_NUM_TABLES; i++)
                {
                    uint r = _crc32Lookup[i - 256];
                    _crc32Lookup[i] = _crc32Lookup[r & 0xFF] ^ (r >> 8);
                }
            }
        }

        public ThreadCRC()
        {
            _waitEvent = new AutoResetEvent(false);
            _outEvent = new AutoResetEvent(false);
            _finished = false;

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

                uint crc = _crc;
                int offset = 0;

                // offset hardcoded to zero so this loop is not needed
                //for (; (offset & 7) != 0 && _size != 0; _size--)
                //    crc = (crc >> 8) ^ _crc32Lookup[(byte)crc ^ _buffer[offset++]];

                if (_size >= 8)
                {
                    int end = (_size - 8) & ~7;
                    _size -= end;
                    end += offset;

                    while (offset != end)
                    {
                        crc ^= (uint)(_buffer[offset] + (_buffer[offset + 1] << 8) + (_buffer[offset + 2] << 16) + (_buffer[offset + 3] << 24));
                        uint high = (uint)(_buffer[offset + 4] + (_buffer[offset + 5] << 8) + (_buffer[offset + 6] << 16) + (_buffer[offset + 7] << 24));
                        offset += 8;

                        crc = _crc32Lookup[(byte)crc + 0x700]
                            ^ _crc32Lookup[(byte)(crc >>= 8) + 0x600]
                            ^ _crc32Lookup[(byte)(crc >>= 8) + 0x500]
                            ^ _crc32Lookup[/*(byte)*/(crc >> 8) + 0x400]
                            ^ _crc32Lookup[(byte)(high) + 0x300]
                            ^ _crc32Lookup[(byte)(high >>= 8) + 0x200]
                            ^ _crc32Lookup[(byte)(high >>= 8) + 0x100]
                            ^ _crc32Lookup[/*(byte)*/(high >> 8) + 0x000];
                    }
                }

                while (_size-- != 0)
                    crc = (crc >> 8) ^ _crc32Lookup[(byte)crc ^ _buffer[offset++]];

                _crc = crc;

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
