using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Printing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace TuesPechkin
{
    public sealed class PdfToolset : MarshalByRefObject, IToolset
    {
        public event EventHandler Unloaded;

        public IDeployment Deployment { get; private set; }

        public bool Loaded { get; private set; }

        public TraceCallback TraceHandler { get; private set; }

        private IntPtr _wkhtmltoxPtr = IntPtr.Zero;

        public PdfToolset()
        {
        }

        public PdfToolset(IDeployment deployment)
        {
            if (deployment == null)
            {
                throw new ArgumentNullException("deployment");
            }

            Deployment = deployment;
        }

        public void Load(IDeployment deployment = null)
        {
            if (Loaded)
            {
                return;
            }

            if (deployment != null)
            {
                Deployment = deployment;
            }

            bool dirSet = WinApiHelper.SetDllDirectory(Deployment.Path);
            _wkhtmltoxPtr = WinApiHelper.LoadLibrary(WkhtmltoxBindings.DLLNAME);
            if (_wkhtmltoxPtr != IntPtr.Zero)
            {
                int initRes = WkhtmltoxBindings.wkhtmltopdf_init(0);
                Loaded = true;
                HandleTraceMessage("T:" + Thread.CurrentThread.Name + " Loaded library: " + WkhtmltoxBindings.DLLNAME);
            }
            else
            {
                int errCode = WinApiHelper.GetLastError();
                HandleTraceMessage("T:" + Thread.CurrentThread.Name + " Failed to load libary, error code " + errCode, TraceSeverity.Warn);
            }
        }

        public void Unload()
        {
            if (Loaded)
            {
                int dres = WkhtmltoxBindings.wkhtmltopdf_deinit();
                if (_wkhtmltoxPtr != IntPtr.Zero)
                {
                    bool unloaded = WinApiHelper.FreeLibrary(_wkhtmltoxPtr);
                    if (!unloaded)
                    {
                        int errCode = WinApiHelper.GetLastError();
                        if (errCode == 0)
                        {
                            HandleTraceMessage("T:" + Thread.CurrentThread.Name + " Failed to unload libary...", TraceSeverity.Warn);
                        }
                        else
                        {
                            HandleTraceMessage("T:" + Thread.CurrentThread.Name + " Error " + errCode + " unloading library", TraceSeverity.Warn);
                        }
                    }
                    else
                    {
                        HandleTraceMessage("T:" + Thread.CurrentThread.Name + " Unloaded library " + WkhtmltoxBindings.DLLNAME);
                    }
                }
                //try again... 
                try
                {
                    UnloadModule();
                }
                catch(Exception x)
                {
                    string err = x.Message;
                    HandleTraceMessage("T:" + Thread.CurrentThread.Name + " Error " + x.Message, TraceSeverity.Warn);
                }
                if (Unloaded != null)
                {
                    Unloaded(this, EventArgs.Empty);
                }
            }
            else
            {
                HandleTraceMessage("T:" + Thread.CurrentThread.Name + " Nothing to unload...", TraceSeverity.Warn);
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

        //could make thi spart if Itoolset, but don't really need to...
        private void HandleTraceMessage(string message, TraceSeverity severity = TraceSeverity.Trace)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            string msg = message + "[" + Thread.CurrentThread.ManagedThreadId + "]";
            if (TraceHandler != null)
            {
                TraceHandler.Invoke(msg, severity);
            }
            else
            {
                switch (severity)
                {
                    case TraceSeverity.Trace:
                        HandleTraceMessage(message);
                        break;
                    case TraceSeverity.Warn:
                        Tracer.Warn(message);
                        break;
                    case TraceSeverity.Critical:
                        Tracer.Critical(message);
                        break;
                }
            }
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }

        #region Rest of IToolset stuff
        public void SetToolsetMessageCallback(TraceCallback callback)
        {
            TraceHandler = callback;
        }
        public IntPtr CreateGlobalSettings()
        {
            HandleTraceMessage("T:" + Thread.CurrentThread.Name + " Creating global settings (wkhtmltopdf_create_global_settings)");

            return WkhtmltoxBindings.wkhtmltopdf_create_global_settings();
        }

        public IntPtr CreateObjectSettings()
        {
            HandleTraceMessage("T:" + Thread.CurrentThread.Name + " Creating object settings (wkhtmltopdf_create_object_settings)");

            return WkhtmltoxBindings.wkhtmltopdf_create_object_settings();
        }

        public int SetGlobalSetting(IntPtr setting, string name, string value)
        {
            HandleTraceMessage(
                String.Format(
                    "T:{0} Setting global setting '{1}' to '{2}' for config {3}",
                    Thread.CurrentThread.Name,
                    name,
                    value,
                    setting));

            var success = WkhtmltoxBindings.wkhtmltopdf_set_global_setting(setting, name, value);

            HandleTraceMessage(String.Format("...setting was {0}", success == 1 ? "successful" : "not successful"));

            return success;
        }

        public unsafe string GetGlobalSetting(IntPtr setting, string name)
        {
            HandleTraceMessage("T:" + Thread.CurrentThread.Name + " Getting global setting (wkhtmltopdf_get_global_setting)");

            byte[] buf = new byte[2048];

            fixed (byte* p = buf)
            {
                WkhtmltoxBindings.wkhtmltopdf_get_global_setting(setting, name, p, buf.Length);
            }

            int walk = 0;

            while (walk < buf.Length && buf[walk] != 0)
            {
                walk++;
            }

            return Encoding.UTF8.GetString(buf, 0, walk);
        }

        public int SetObjectSetting(IntPtr setting, string name, string value)
        {
            HandleTraceMessage(
                String.Format(
                    "T:{0} Setting object setting '{1}' to '{2}' for config {3}",
                    Thread.CurrentThread.Name,
                    name,
                    value,
                    setting));

            var success = WkhtmltoxBindings.wkhtmltopdf_set_object_setting(setting, name, value);

            HandleTraceMessage(String.Format("...setting was {0}", success == 1 ? "successful" : "not successful"));

            return success;
        }

        public unsafe string GetObjectSetting(IntPtr setting, string name)
        {
            HandleTraceMessage(string.Format(
                "T:{0} Getting object setting '{1}' for config {2}",
                Thread.CurrentThread.Name,
                name,
                setting));

            byte[] buf = new byte[2048];

            fixed (byte* p = buf)
            {
                WkhtmltoxBindings.wkhtmltopdf_get_object_setting(setting, name, p, buf.Length);
            }

            int walk = 0;

            while (walk < buf.Length && buf[walk] != 0)
            {
                walk++;
            }

            return Encoding.UTF8.GetString(buf, 0, walk);
        }

        public IntPtr CreateConverter(IntPtr globalSettings)
        {
            HandleTraceMessage("T:" + Thread.CurrentThread.Name + " Creating converter (wkhtmltopdf_create_converter)");

            return WkhtmltoxBindings.wkhtmltopdf_create_converter(globalSettings);
        }

        public void DestroyConverter(IntPtr converter)
        {
            HandleTraceMessage("T:" + Thread.CurrentThread.Name + " Destroying converter (wkhtmltopdf_destroy_converter)");

            WkhtmltoxBindings.wkhtmltopdf_destroy_converter(converter);

            pinnedCallbacks.Unregister(converter);
        }

        public void SetWarningCallback(IntPtr converter, StringCallback callback)
        {
            HandleTraceMessage("T:" + Thread.CurrentThread.Name + " Setting warning callback (wkhtmltopdf_set_warning_callback)");
            
            WkhtmltoxBindings.wkhtmltopdf_set_warning_callback(converter, callback);

            pinnedCallbacks.Register(converter, callback);
        }

        public void SetErrorCallback(IntPtr converter, StringCallback callback)
        {
            HandleTraceMessage("T:" + Thread.CurrentThread.Name + " Setting error callback (wkhtmltopdf_set_error_callback)");
            
            WkhtmltoxBindings.wkhtmltopdf_set_error_callback(converter, callback);

            pinnedCallbacks.Register(converter, callback);
        }

        public void SetFinishedCallback(IntPtr converter, IntCallback callback)
        {
            HandleTraceMessage("T:" + Thread.CurrentThread.Name + " Setting finished callback (wkhtmltopdf_set_finished_callback)");

            WkhtmltoxBindings.wkhtmltopdf_set_finished_callback(converter, callback);

            pinnedCallbacks.Register(converter, callback);
        }

        public void SetPhaseChangedCallback(IntPtr converter, VoidCallback callback)
        {
            HandleTraceMessage("T:" + Thread.CurrentThread.Name + " Setting phase change callback (wkhtmltopdf_set_phase_changed_callback)");

            WkhtmltoxBindings.wkhtmltopdf_set_phase_changed_callback(converter, callback);

            pinnedCallbacks.Register(converter, callback);
        }

        public void SetProgressChangedCallback(IntPtr converter, IntCallback callback)
        {
            HandleTraceMessage("T:" + Thread.CurrentThread.Name + " Setting progress change callback (wkhtmltopdf_set_progress_changed_callback)");

            WkhtmltoxBindings.wkhtmltopdf_set_progress_changed_callback(converter, callback);

            pinnedCallbacks.Register(converter, callback);
        }

        public bool PerformConversion(IntPtr converter)
        {
            HandleTraceMessage("T:" + Thread.CurrentThread.Name + " Starting conversion (wkhtmltopdf_convert)");

            return WkhtmltoxBindings.wkhtmltopdf_convert(converter) != 0;
        }

        public void AddObject(IntPtr converter, IntPtr objectConfig, string html)
        {
            HandleTraceMessage("T:" + Thread.CurrentThread.Name + " Adding string object (wkhtmltopdf_add_object)");

            WkhtmltoxBindings.wkhtmltopdf_add_object(converter, objectConfig, html);
        }

        public void AddObject(IntPtr converter, IntPtr objectConfig, byte[] html)
        {
            HandleTraceMessage("T:" + Thread.CurrentThread.Name + " Adding byte[] object (wkhtmltopdf_add_object)");

            WkhtmltoxBindings.wkhtmltopdf_add_object(converter, objectConfig, html);
        }

        public int GetPhaseNumber(IntPtr converter)
        {
            HandleTraceMessage("T:" + Thread.CurrentThread.Name + " Requesting current phase (wkhtmltopdf_current_phase)");

            return WkhtmltoxBindings.wkhtmltopdf_current_phase(converter);
        }

        public int GetPhaseCount(IntPtr converter)
        {
            HandleTraceMessage("T:" + Thread.CurrentThread.Name + " Requesting phase count (wkhtmltopdf_phase_count)");

            return WkhtmltoxBindings.wkhtmltopdf_phase_count(converter);
        }

        public string GetPhaseDescription(IntPtr converter, int phase)
        {
            HandleTraceMessage("T:" + Thread.CurrentThread.Name + " Requesting phase description (wkhtmltopdf_phase_description)");

            return Marshal.PtrToStringAnsi(WkhtmltoxBindings.wkhtmltopdf_phase_description(converter, phase));
        }

        public string GetProgressDescription(IntPtr converter)
        {
            HandleTraceMessage("T:" + Thread.CurrentThread.Name + " Requesting progress string (wkhtmltopdf_progress_string)");

            return Marshal.PtrToStringAnsi(WkhtmltoxBindings.wkhtmltopdf_progress_string(converter));
        }

        public int GetHttpErrorCode(IntPtr converter)
        {
            HandleTraceMessage("T:" + Thread.CurrentThread.Name + " Requesting http error code (wkhtmltopdf_http_error_code)");

            return WkhtmltoxBindings.wkhtmltopdf_http_error_code(converter);
        }

        public byte[] GetConverterResult(IntPtr converter)
        {
            HandleTraceMessage("T:" + Thread.CurrentThread.Name + " Requesting converter result (wkhtmltopdf_get_output)");

            IntPtr tmp;
            var len = WkhtmltoxBindings.wkhtmltopdf_get_output(converter, out tmp);
            var output = new byte[len];
            Marshal.Copy(tmp, output, 0, output.Length);
            return output;
        }

        #endregion

        private DelegateRegistry pinnedCallbacks = new DelegateRegistry();
    }
}
