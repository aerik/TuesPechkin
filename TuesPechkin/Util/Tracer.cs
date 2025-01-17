﻿using System;
using System.Diagnostics;

namespace TuesPechkin
{
    public enum TraceSeverity { Trace=0, Warn=1, Critical=2};

    public delegate void TraceCallback(String str, TraceSeverity severity);

    public static class Tracer
    {
        private readonly static TraceSource source = new TraceSource("pechkin:default");

        public static TraceSource Source
        {
            get
            {
                return source;
            }
        }

        public static void Trace(String message)
        {
            source.TraceInformation(message);
        }

        public static void Warn(String message)
        {
            source.TraceEvent(TraceEventType.Warning, 0, message);
        }

        public static void Critical(String message)
        {
            source.TraceEvent(TraceEventType.Warning, 0, message);
        }

        public static void Warn(String message, Exception e)
        {
            source.TraceEvent(TraceEventType.Warning, 0, String.Format(message + "{0}", e));
        }

        public static void Critical(String message, Exception e)
        {
            source.TraceEvent(TraceEventType.Critical, 0, String.Format(message + "{0}", e));
        }
    }
}