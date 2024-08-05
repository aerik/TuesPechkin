using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TuesPechkin
{
    public class TpHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public IntPtr Ptr
        {
            get
            {
                if(this.IsInvalid || this.IsClosed) return IntPtr.Zero;
                return this.DangerousGetHandle();
            }
        }

        public TpHandle(bool ownsHandle) : base(ownsHandle)
        {
        }

        protected override bool ReleaseHandle()
        {
            throw new NotImplementedException();
        }
    }

    public class ConverterHandle : TpHandle
    {
        public ConverterHandle(bool ownsHandle) : base(ownsHandle)
        {
        }
        public ConverterHandle(IntPtr source): base(true)
        {
            this.SetHandle(source);
        }
        protected override bool ReleaseHandle()
        {
            if (!this.IsInvalid && !this.IsClosed)
            {
                IntPtr ptr = this.Ptr;
                WkhtmltoxBindings.wkhtmltoimage_destroy_converter(this.Ptr);
                this.SetHandleAsInvalid();
            }
            return true;
        }
    }
}
