using System;
using System.IO;
using System.Threading;

namespace RomVaultX.SupportedFiles.Files
{
	class ThreadLoadBuffer : IDisposable
	{
		private readonly AutoResetEvent _waitEvent;
		private readonly AutoResetEvent _outEvent;
		private readonly Thread _tWorker;

		private byte[] _buffer;
		private int _size;
		private readonly Stream _ds;
		private bool _finished;

		public ThreadLoadBuffer(Stream ds)
		{
			_waitEvent = new AutoResetEvent(false);
			_outEvent = new AutoResetEvent(false);
			_finished = false;
			_ds = ds;

			_tWorker = new Thread(MainLoop);
			_tWorker.Start();
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
				if (_finished)
				{
					break;
				}
				_ds.Read(_buffer, 0, _size);
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
