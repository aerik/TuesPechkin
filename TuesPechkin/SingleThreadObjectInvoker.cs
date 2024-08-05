using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TuesPechkin
{
    /// <summary>
    /// Create an object and invoke it's methods on a dedicated thread
    /// (c) Aerik Sylvan and Vet Rocket 2024, MIT license
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SingleThreadObjectInvoker<T> : IDisposable where T : class
    {
        protected readonly T _Instance;
        private readonly DedicatedThreadTaskScheduler _Scheduler;
        private readonly TaskFactory _TaskFactory;
        private bool _Disposed = false;

        public int QueuedTaskCount
        {
            get
            {
                return _Scheduler.QueuedTaskCount;
            }
        }

        public SingleThreadObjectInvoker(Func<T> factory)
        {
            _Scheduler = new DedicatedThreadTaskScheduler();
            _TaskFactory = new TaskFactory(_Scheduler);
            _Instance = _TaskFactory.StartNew(factory).Result;
        }

        public Task<TResult> InvokeAsync<TResult>(Func<T, TResult> func)
        {
            if (_Disposed) throw new ObjectDisposedException(nameof(SingleThreadObjectInvoker<T>));
            return _TaskFactory.StartNew(() => func(_Instance));
        }

        public Task InvokeAsync(Action<T> action)
        {
            if (_Disposed) throw new ObjectDisposedException(nameof(SingleThreadObjectInvoker<T>));
            return _TaskFactory.StartNew(() => action(_Instance));
        }
        //arbitrary action not attached the the _Instance
        public Task InvokeAsync(Action action)
        {
            if (_Disposed) throw new ObjectDisposedException(nameof(SingleThreadObjectInvoker<T>));
            return _TaskFactory.StartNew(action);
        }

        private Task InvokeInternal(Action<T> action)
        {
            //no check for _Disposed so we can queue a task during disposal
            return _TaskFactory.StartNew(() => action(_Instance));
        }

        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;//disallows any more external calls to queue tasks
            if (_Instance is IDisposable)
            {
                //add task to dispose _Instance to end of task queue
                Task dispTask = InvokeInternal(di => {
                    IDisposable disposable = di as IDisposable;
                    disposable.Dispose();
                });
            }
            _Scheduler.Dispose();
        }

        public void Abort()
        {
            if (_Disposed) return;
            _Disposed = true;
            IDisposable disposable = null;
            if (_Instance is IDisposable)
            {
                disposable = (IDisposable)_Instance;
            }
            //this is the trick, pass the dispose function to be executed on the same thread
            _Scheduler.Abort(() => {
                if (disposable != null) disposable.Dispose();
            });
            _Scheduler.Dispose();
        }
        public void TestAbort(Action action)
        {
            _Scheduler.TestAbort(action);
        }

        private class DedicatedThreadTaskScheduler : TaskScheduler, IDisposable
        {
            private readonly Thread _Thread;
            private readonly BlockingCollection<Task> _Tasks = new BlockingCollection<Task>();//defaults to queue
            private volatile bool _Stopped = false;
            private volatile bool _Disposed = false;
            private volatile bool _IsBusy = false;
            private volatile bool _CurrentSucceeded = true;

            public int QueuedTaskCount
            {
                get
                {
                    return _Tasks.Count;
                }
            }

            public DedicatedThreadTaskScheduler()
            {
                _Thread = new Thread(new ThreadStart(Execute)) { IsBackground = true };
                _Thread.SetApartmentState(ApartmentState.STA);
                _Thread.Start();
            }

            private void Execute()
            {
                Task task;
                try
                {
                    while (!_Stopped)
                    {
                        _IsBusy = true;
                        bool sucess = false;
                        try
                        {
                            task = _Tasks.Take();//blocks but quits when the collection is disposed
                            if (task != null)
                            {
                                sucess = TryExecuteTask(task);
                            }
                        }
                        catch (ThreadAbortException tax)
                        {
                            if (tax.ExceptionState != null && tax.ExceptionState is Action)
                            {
                                Action abortTask = (Action)tax.ExceptionState;
                                abortTask();
                            }
                            //ThreadAbortException gets rethrown, but I'm not exactly sure where it goes...
                        }
                        catch (InvalidOperationException)
                        {
                            //can happend when _Tasks is disposed / closed / whatever
                        }
                        finally
                        {
                            _CurrentSucceeded = sucess;
                            _IsBusy = false;
                        }
                        System.Threading.Thread.Yield();
                    }
                }
                finally
                {
                    try
                    {
                        _Tasks.Dispose();
                    }
                    catch { }
                }
            }

            protected override IEnumerable<Task> GetScheduledTasks()
            {
                return _Tasks.ToArray();
            }

            protected override void QueueTask(Task task)
            {
                if (_Disposed) throw new ObjectDisposedException(nameof(DedicatedThreadTaskScheduler));
                _Tasks.Add(task);
            }

            protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
            {
                return Thread.CurrentThread == _Thread && TryExecuteTask(task);
            }


            public void Abort(Action action)
            {
                if (_Disposed) return;
                _Disposed = true;
                try
                {
                    _Tasks.CompleteAdding();//do this before setting _Stopped                                               
                    // DON'T wait for pending tasks
                }
                catch (ObjectDisposedException) { }
                _Stopped = true;
                _Thread.Abort(action);
                _Thread.Join();
                _Tasks.Dispose();//redundant
            }

            public void TestAbort(Action action)
            {
                _Thread.Abort(action);
            }

            public void Dispose()
            {
                if (!_Disposed)
                {
                    _Disposed = true;
                    try
                    {
                        _Tasks.CompleteAdding();//do this before setting _Stopped                                               
                        Task.WaitAll(_Tasks.ToArray()); //wait for pending tasks
                    }
                    catch (ObjectDisposedException) { }
                    _Stopped = true;
                }
                _Thread.Join();
                _Tasks.Dispose();//redundant
            }
        }
    }
}
