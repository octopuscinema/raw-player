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
#if TRACE_TO_FILE
        private TextWriterTraceListener textTraceListener;
#endif
        public string LogPath
        {
            get
            {
#if TRACE_TO_FILE
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/Logs/OCTOPUS RAW Player.log");
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OCTOPUS RAW Player.log");
#else
                return null;
#endif
            }
        }

        public virtual string ProductName { get { return Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute)).OfType<AssemblyProductAttribute>().FirstOrDefault().Product; } }
        public virtual string ProductVersion { get { return Assembly.GetEntryAssembly().GetName().Version.ToString(); } }
        public string ProductVersionMajor
        {
            get
            {
                var versionParts = ProductVersion.Split('.', StringSplitOptions.RemoveEmptyEntries);
                return versionParts.Length > 0 ? versionParts[0] : "";
            }
        }
        public virtual string ProductBuildVersion { get { return ProductVersion; } }
        public virtual string ProductCopyright { get { return Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute)).OfType<AssemblyCopyrightAttribute>().FirstOrDefault().Copyright; } }
        public string ProductLicense { get { return "MIT License"; } }

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
