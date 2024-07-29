using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Remoting;
using System.Threading;

namespace TuesPechkin
{
    /// <summary>
    /// Deployments loaded with this class must be marked Serializable.
    /// </summary>
    /// <typeparam name="TToolset">The type of toolset to manage remotely.</typeparam>
    public sealed class RemotingToolset<TToolset> : NestingToolset
        where TToolset : MarshalByRefObject, IToolset, new()
    {
        public RemotingToolset(IDeployment deployment)
        {
            if (deployment == null)
            {
                throw new ArgumentNullException("deployment");
            }

            Deployment = deployment;
            Tracer.Trace("T:" + Thread.CurrentThread.Name + " Iniitializing RemotingToolset [" + Thread.CurrentThread.ManagedThreadId + "]");

            if (!WinApiHelper.SetDllDirectory(Deployment.Path))
            {
                int errCode = WinApiHelper.GetLastError();
                if (errCode != 0)
                {
                    Tracer.Warn("T:" + Thread.CurrentThread.Name + " Found unknown error " + errCode + " while initializing RemotingToolset [" + Thread.CurrentThread.ManagedThreadId + "]");
                }
            }
        }

        public override void Load(IDeployment deployment = null)
        {
            if (Loaded)
            {
                return;
            }

            if (deployment != null)
            {
                Deployment = deployment;
            }

            SetupAppDomain();

            var handle = Activator.CreateInstanceFrom(
                remoteDomain,
                typeof(TToolset).Assembly.Location,
                typeof(TToolset).FullName,
                false,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                null,
                null,
                null);

            TToolset toolset = handle.Unwrap() as TToolset;
            toolset.SetToolsetMessageCallback(new TraceCallback(HandleToolsetMessage));
            toolset.Load(Deployment);
            Deployment = toolset.Deployment;
            NestedToolset = toolset;
            Loaded = true;
        }

        public override event EventHandler Unloaded;

        public override void Unload()
        {
            if (Loaded)
            {
                TearDownAppDomain(null, EventArgs.Empty);
            }
            else
            {
                Tracer.Trace("T:" + Thread.CurrentThread.Name + " Nothing to unload...");
            }
        }

        private AppDomain remoteDomain;

        private void SetupAppDomain()
        {
            var setup = AppDomain.CurrentDomain.SetupInformation;

            setup.LoaderOptimization = LoaderOptimization.SingleDomain;

            remoteDomain = AppDomain.CreateDomain(
                string.Format("tuespechkin_{0}", Guid.NewGuid()),
                null,
                setup);

            if (AppDomain.CurrentDomain.IsDefaultAppDomain() == false)
            {
                AppDomain.CurrentDomain.DomainUnload += TearDownAppDomain;
            }
        }

        private void TearDownAppDomain(object sender, EventArgs e)
        {
            if (remoteDomain == null || remoteDomain == AppDomain.CurrentDomain)
            {
                return;
            }

            OnBeforeUnload(null);//don't know what this does
            NestedToolset.Unload();
            Tracer.Trace("T:" + Thread.CurrentThread.Name + " unloading remote App Domain [" + Thread.CurrentThread.ManagedThreadId + "]");
            try
            {
                AppDomain.Unload(remoteDomain);
            }
            catch (Exception ex)
            {
                string appErr = ex.Message;
            }

            UnloadModule();

            remoteDomain = null;
            Loaded = false;

            if (Unloaded != null)
            {
                Unloaded(this, EventArgs.Empty);
            }
        }

        private void HandleToolsetMessage(string message, TraceSeverity severity)
        {
            if (String.IsNullOrWhiteSpace(message)) return;
            switch (severity)
            {
                case TraceSeverity.Trace:
                    Tracer.Trace(message);
                    break;
                case TraceSeverity.Warn:
                    Tracer.Warn(message);
                    break;
                case TraceSeverity.Critical:
                    Tracer.Critical(message);
                    break;
            }
        }

        private void UnloadModule()
        {
            var expected = Path.Combine(
                Deployment.Path,
                WkhtmltoxBindings.DLLNAME);
            if (expected.StartsWith(".") && File.Exists(expected))
            {
                //get the full path
                FileInfo fi = new FileInfo(expected);
                expected = fi.FullName;
            }
            Tracer.Trace("T:" + Thread.CurrentThread.Name + " checking for module " + WkhtmltoxBindings.DLLNAME);
            foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
            {
                if (module.FileName == expected)
                {
                    Tracer.Warn("T:" + Thread.CurrentThread.Name + " Module still loaded: " + module.FileName);
                    int loops = 0;
                    while (WinApiHelper.FreeLibrary(module.BaseAddress) && loops < 10)
                    {
                        loops++;
                        Tracer.Trace("T:" + Thread.CurrentThread.Name + " Calling FreeLibrary for module: " + module.FileName);
                    }

                    break;
                }
            }
        }
    }
}