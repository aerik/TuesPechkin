using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TuesPechkin
{

    public class AsyncConverter<AToolset> : MarshalByRefObject, IConverter, IDisposable where AToolset : MarshalByRefObject, IToolset
    {
        private readonly SingleThreadObjectInvoker<ToolsetConverter<AToolset>> _Invoker;
        private bool _IsDisposed;

        public AsyncConverter(Func<AToolset> toolsetFactory)
        {
            if (toolsetFactory == null)
            {
                throw new ArgumentNullException("toolsetFactory");
            }
            _Invoker = new SingleThreadObjectInvoker<ToolsetConverter<AToolset>>(()=> new ToolsetConverter<AToolset>(toolsetFactory));
            _Invoker.InvokeAsync(ts => { ts.ProgressChange += new EventHandler<ProgressChangeEventArgs>(Ts_ProgressChange); });

            Tracer.Trace(string.Format("T:{0} Created AsyncConverter", Thread.CurrentThread.Name));
        }

        private void Ts_ProgressChange(object sender, ProgressChangeEventArgs e)
        {
            Tracer.Trace("Progress Changed called on thread " + Thread.CurrentThread.ManagedThreadId + " " + AppDomain.CurrentDomain.FriendlyName);
        }

        public event EventHandler<BeginEventArgs> Begin;
        public event EventHandler<WarningEventArgs> Warning;
        public event EventHandler<ErrorEventArgs> Error;
        public event EventHandler<PhaseChangeEventArgs> PhaseChange;
        public event EventHandler<ProgressChangeEventArgs> ProgressChange;
        public event EventHandler<FinishEventArgs> Finish;

        public byte[] Convert(IDocument document)
        {
            return this.ConvertAsync(document).Result;
        }

        public Task<byte[]> ConvertAsync(IDocument document)
        {
            return _Invoker.InvokeAsync(conv => conv.Convert(document));
        }

        public void Abort()
        {
            if(_Invoker != null)
            {
                _Invoker.Abort();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_IsDisposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                if(_Invoker != null)
                {
                    _Invoker.Dispose();
                }
                _IsDisposed = true;
            }
        }

        // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        ~AsyncConverter()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
