using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TuesPechkin
{
    public class SingleThreadObjectInvoker<T> : IDisposable where T : class
    {
        protected readonly T _Instance;
        private readonly DedicatedThreadTaskScheduler _Scheduler;
        private readonly TaskFactory _TaskFactory;
        private bool _Disposed = false;

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

        private Task InvokeInternal(Action<T> action)
        {
            return _TaskFactory.StartNew(() => action(_Instance));
        }

        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            if (_Instance is IDisposable)
            {
                Task dispTask = InvokeInternal(di => { 
                    IDisposable disposable = di as IDisposable;
                    disposable.Dispose();
                });
                dispTask.Wait();
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
            _Scheduler.Abort(()=> {
                if(disposable != null) disposable.Dispose();
            });
            _Scheduler.Dispose();
        }

        private class DedicatedThreadTaskScheduler : TaskScheduler, IDisposable
        {
            private readonly Thread _Thread;
            private readonly BlockingCollection<Task> _Tasks = new BlockingCollection<Task>();//defaults to queue
            private volatile bool _Stopped = false;
            private volatile bool _Disposed = false;
            private volatile bool _IsBusy = false;
            private volatile bool _CurrentSucceeded = true;

            private delegate void Interruption(object state);

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
                _Stopped = true;
                _Tasks.CompleteAdding();
                _Thread.Abort(action);
                _Thread.Join();
            }

            public void Dispose()
            {
                if (!_Disposed)
                {
                    _Disposed = true;
                    _Stopped = true;
                    _Tasks.CompleteAdding();
                }
                _Thread.Join();
            }
        }
    }
}
