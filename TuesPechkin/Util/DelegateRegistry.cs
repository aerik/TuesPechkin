using System;
using System.Collections.Generic;
using System.Text;

namespace TuesPechkin
{
    internal class DelegateRegistry
    {
        private readonly Dictionary<IntPtr, List<Delegate>> registry = new Dictionary<IntPtr, List<Delegate>>();
        private readonly Dictionary<TpHandle, List<Delegate>> safeRegistry = new Dictionary<TpHandle, List<Delegate>>();

        public void Register(IntPtr converter, Delegate callback)
        {
            List<Delegate> delegates;

            if (!registry.TryGetValue(converter, out delegates))
            {
                delegates = new List<Delegate>();
                registry.Add(converter, delegates);
            }

            delegates.Add(callback);
        }
        public void Register(TpHandle converter, Delegate callback)
        {
            List<Delegate> delegates;

            if (!safeRegistry.TryGetValue(converter, out delegates))
            {
                delegates = new List<Delegate>();
                safeRegistry.Add(converter, delegates);
            }
            delegates.Add(callback);
        }

        public void Unregister(IntPtr converter)
        {
            registry.Remove(converter);
        }
        public void Unregister(TpHandle converter)
        {
            safeRegistry.Remove(converter);
        }
    }
}
