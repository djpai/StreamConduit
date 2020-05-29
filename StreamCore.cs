using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CoreUtils
{
    public class StreamConduit : Stream, IDisposable
    {
        //Queue<byte> mQ;
        byte[] mWBuff = null;
        Func<Stream, int, Task> mConduitAction;
        ManualResetEventSlim mReadWait;
        AutoResetEvent mWriteWait;
        ManualResetEventSlim mCompleteWait;
        QueuedLock mQueuedLock;
        Task mTask = null;
        CancellationTokenSource mCancelWrite;
        int mWOffset;
        int mWCount;
        public delegate Task ConduitFuncAsync(Stream stream, int FirstBufferSize);

        public StreamConduit(Func<Stream, Task> pConduitWriteActionAsync) : this(new ConduitFuncAsync((stream, bufferSize) => pConduitWriteActionAsync(stream)))
        {
        }

        public StreamConduit(Func<Stream, int, Task> pConduitWriteActionAsync) : this(new ConduitFuncAsync(pConduitWriteActionAsync))
        {
        }

        public StreamConduit(ConduitFuncAsync pConduitWriteActionAsync)
        {

            mQueuedLock = new QueuedLock();
            mCancelWrite = new CancellationTokenSource();
            mReadWait = new ManualResetEventSlim(false);
            mWriteWait = new AutoResetEvent(false);
            mCompleteWait = new ManualResetEventSlim(true);

            mConduitAction = async (stream, bufferSize) =>
            {
                try
                {
                    mCompleteWait.Reset();
                    await pConduitWriteActionAsync(stream, bufferSize);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    throw e;
                }
                finally
                {
                    mReadWait.Set();
                    mCompleteWait.Set();
                }
            };
        }


        public void StartConduitAction(int buffersize)
        {
            mTask = Task.Run(async () => await mConduitAction(this, buffersize < 4096 ? 4096 : buffersize), mCancelWrite.Token);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (mTask == null) StartConduitAction(count);
            int lRCount = count;

            while (count > 0)
            {
                mReadWait.Wait();

                if (mWBuff == null) return lRCount - count;

                if (count >= mWCount)
                {
                    Buffer.BlockCopy(mWBuff, mWOffset, buffer, offset, mWCount);
                    offset += mWCount;
                    count -= mWCount;
                    mWCount = 0;
                    mWBuff = null;
                    mReadWait.Reset();
                    mWriteWait.Set();
                }
                else
                {
                    Buffer.BlockCopy(mWBuff, mWOffset, buffer, offset, count);
                    mWOffset += count;
                    mWCount -= count;
                    count = 0;
                }
            }

            return lRCount;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count < 1) return;

            mQueuedLock.Enter();

            if (mTask == null) StartConduitAction(count);

            try
            {
                mWBuff = buffer;
                mWOffset = offset;
                mWCount = count;
                mReadWait.Set();
                mWriteWait.WaitOne();
            }
            finally
            {
                mQueuedLock.Exit();
            }
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return Read(buffer, offset, count);
            //return await Task.Run<int>(() => { return Read(buffer, offset, count); });
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Write(buffer, offset, count);
            //await Task.Run(() => { Write(buffer, offset, count); });
        }

        public override void Flush()
        {
            mReadWait.Set();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            mReadWait.Set();
            return base.FlushAsync(cancellationToken);
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override long Length
        {
            get { return -1; }
        }

        public override long Position
        {
            get { return mWOffset; }
            set { }
        }

        public override void SetLength(long value)
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return -1;
        }

        public long WriteBufferLength
        {
            get { return mWBuff == null ? 0 : mWBuff.Length; }
        }

        bool isDisposed = false;
        protected override void Dispose(bool disposing)
        {
            if (disposing && !isDisposed)
            {
                isDisposed = true;
                mReadWait.Set();
                mCompleteWait.Wait();
                mWBuff = null;
                mCancelWrite.Cancel();
                mWriteWait.Dispose();
                mReadWait.Dispose();
            }

            base.Dispose(disposing);
        }

    }

    public sealed class QueuedLock
    {
        object mSync;
        volatile int mLockCount = 0;
        volatile int mLockVal = 1;

        public QueuedLock()
        {
            mSync = new Object();
        }

        public void Enter()
        {
            var thislockVal = Interlocked.Increment(ref mLockCount);
            Monitor.Enter(mSync);
            while (true)
            {
                if (thislockVal == mLockVal)
                    return;
                else
                    Monitor.Wait(mSync);
            }
        }

        public void Exit()
        {
            Interlocked.Increment(ref mLockVal);
            Monitor.PulseAll(mSync);
            Monitor.Exit(mSync);
        }
    }


}
