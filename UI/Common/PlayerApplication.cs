#define TRACE_TO_FILE

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Octopus.Player.UI
{
    public class PlayerApplication : IDisposable
    {
        private TextWriterTraceListener textTraceListener;

        public virtual string LogPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OCTOPUS RAW Player.log"); }
        }

        public virtual string ProductName { get { return Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute)).OfType<AssemblyProductAttribute>().FirstOrDefault().Product; } }
        public virtual string ProductVersion { get { return Assembly.GetEntryAssembly().GetName().Version.ToString(); } }
        public virtual string ProductBuildVersion { get { return ProductVersion; } }
        public virtual string ProductCopyright { get { return Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute)).OfType<AssemblyCopyrightAttribute>().FirstOrDefault().Copyright; } }
        public string ProductLicense { get { return "MIT License"; } }
        public string ProductVersionMajor
        {
            get
            {
                var versionParts = ProductVersion.Split('.', StringSplitOptions.RemoveEmptyEntries);
                return versionParts.Length > 0 ? versionParts[0] : "";
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
