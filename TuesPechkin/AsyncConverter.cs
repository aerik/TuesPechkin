using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace TuesPechkin
{
    public sealed class AsyncConverter : StandardConverter, IConverter
    {
        public AsyncConverter(IToolset toolset)
            : base(toolset)
        {
            toolset.Unloaded += (sender, args) =>
            {
                object obj = sender;
                Abort();
            };

            if (toolset is NestingToolset)
            {
                //(toolset as NestingToolset).BeforeUnload += (sender, args) =>
                //{
                //    Invoke(sender as ActionShim);
                //};
            }
        }

        public override byte[] Convert(IDocument document)
        {
            return ConvertAsync(document,CancellationToken.None).Result;
        }

        public Task<byte[]> ConvertAsync(IDocument document, CancellationToken token)
        {
            StartThread();
            var tcs = new TaskCompletionSource<byte[]>();
            if (!_Stopped && document != null)
            {
                lock (_QueueLock)
                {
                    if (token != CancellationToken.None)
                    {
                        token.Register(() =>
                        {
                            if (tcs.TrySetCanceled())
                            {
                                base.Close();
                                Abort();
                            }
                        });
                    }
                    taskQueue.Enqueue(new TaskItem() { Document = document, Token = token, CompletionSource = tcs });
                }
            }
            else
            {
                tcs.SetResult(null);
            }
            return tcs.Task;
        }

        


        private Thread _InnerThread;

        private readonly object _QueueLock = new object();

        private readonly object _StartLock = new object();

        private bool _Stopped = true;

        private readonly Queue<TaskItem> taskQueue = new Queue<TaskItem>();

        private void StartThread()
        {
            if (!_Stopped) return;
            Thread thread = null;
            lock (_StartLock)
            {
                if (_InnerThread != null) StopThread();//shouldn't happen, but maybe on error?
                _InnerThread = new Thread(Run)
                {
                    IsBackground = true
                };
                thread = _InnerThread;
                _Stopped = false;
            }
            thread.Start();
        }

        private void StopThread()
        {
            lock (_StartLock)
            {
                if (_InnerThread != null)
                {
                    while (_InnerThread.ThreadState != ThreadState.Stopped) { }

                    _InnerThread = null;
                }
                _Stopped = true;
            }
            Toolset.Unload();
        }
        public void Abort()
        {
            lock (_StartLock)
            { 
                if( _InnerThread != null)
                {
                    Tracer.Warn(string.Format("T:{0} Aborting worker thread {1} [{2}]", Thread.CurrentThread.Name, _InnerThread.ManagedThreadId, Thread.CurrentThread.ManagedThreadId));
                    if (_InnerThread.IsAlive) _InnerThread.Abort();
                    _InnerThread = null;
                }
                _Stopped = true;
            }
            Toolset.Unload();
        }

        private void Run()
        {
            try
            {
                using (WindowsIdentity.Impersonate(IntPtr.Zero))
                {
                    Thread.CurrentPrincipal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
                }
                while (!_Stopped)
                {
                    TaskItem item = null;
                    lock (_QueueLock)
                    {
                        if (taskQueue.Count > 0)
                        {
                            item = taskQueue.Dequeue();
                        }
                    }
                    if (item == null)
                    {
                        Thread.Sleep(100);
                        continue;
                    }
                    if (item.Document == null || item.CompletionSource == null) continue;

                    byte[] resultBytes = base.Convert(item.Document);
                    if (!item.CompletionSource.Task.IsCompleted && !item.CompletionSource.Task.IsFaulted)
                    {
                        item.CompletionSource.SetResult(resultBytes);
                    }
                }
            }
            catch (Exception ex)
            {
                string err = ex.Message;
            }
        }

        private class TaskItem
        {
            public IDocument Document { get; set; }
            public CancellationToken Token { get; set; }
            public TaskCompletionSource<byte[]> CompletionSource { get; set; }
        }
    }
}