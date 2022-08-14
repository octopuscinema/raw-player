﻿#define TRACE_TO_FILE

using System;
using System.Diagnostics;
using System.IO;

namespace Octopus.Player.UI
{
    public class PlayerApplication : IDisposable
    {
#if TRACE_TO_FILE
        private TextWriterTraceListener textTraceListener;
#endif
        public string LogPath
        {
            get
            {
#if TRACE_TO_FILE
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OCTOPUS RAW Player.log");
#else
                return null;
#endif
            }
        }

        public PlayerApplication()
        {
#if TRACE_TO_FILE
            textTraceListener = new TextWriterTraceListener(LogPath);
            Trace.Listeners.Add(textTraceListener);
#endif
            Trace.AutoFlush = true;
        }

        public void Dispose()
        {
            Trace.Flush();
#if TRACE_TO_FILE
            Trace.Listeners.Remove(textTraceListener);
            textTraceListener.Dispose();
            textTraceListener = null;
#endif
        }
    }
}
