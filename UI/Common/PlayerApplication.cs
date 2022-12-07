#define TRACE_TO_FILE

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Xml;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace Octopus.Player.UI
{
    public class PlayerApplication : IDisposable
    {
        private class WebClientWithTimeout : System.Net.WebClient
        {
            public int Timeout { get; set; }

            public WebClientWithTimeout(int timeoutMS = 2000)
            {
                Timeout = timeoutMS;
            }

            protected override WebRequest GetWebRequest(Uri uri)
            {
                WebRequest lWebRequest = base.GetWebRequest(uri);
                lWebRequest.Timeout = Timeout;
                ((HttpWebRequest)lWebRequest).ReadWriteTimeout = Timeout;
                return lWebRequest;
            }
        }

        private TextWriterTraceListener textTraceListener;
        private WebClientWithTimeout asyncWebClient;

        public string LatestVersionURL { get { return "http://www.octopuscinema.com/downloads/OCTOPUS-RAW-Player.Version.xml"; } }

        public virtual string LogPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OCTOPUS RAW Player.log"); }
        }

        public virtual string RecentFilesJsonPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OCTOPUS RAW Player recent files.json"); }
        }

        public virtual string ProductName { get { return Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute)).OfType<AssemblyProductAttribute>().FirstOrDefault().Product; } }
        public virtual string ProductVersion { 
            get
            {
                var version = Assembly.GetEntryAssembly().GetName().Version;
                return version.Major + "." + version.Minor + "." + version.Build;
            }
        }
        public virtual string ProductBuildVersion
        {
            get
            {
                var playerCoreAssembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(assembly => assembly.GetName().Name == "Player.Core");
                var informationalVersion = playerCoreAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
                var informationalVersionDataStart = informationalVersion.IndexOf("-");
                return (informationalVersionDataStart < 0) ? ProductVersion : ProductVersion + informationalVersion.Substring(informationalVersionDataStart);
            }
        }
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

        public string[] OpenOnStart { get; set; }

        public PlayerApplication()
        {
#if TRACE_TO_FILE
            textTraceListener = new TextWriterTraceListener(LogPath);
            Trace.Listeners.Add(textTraceListener);
#endif
            Trace.AutoFlush = true;
            Trace.WriteLine(ProductName + " start [" + DateTime.Now + "]");

            // Log all unhandled exceptions in release without debugger
#if !DEBUG
            if ( !System.Diagnostics.Debugger.IsAttached )
                AppDomain.CurrentDomain.UnhandledException += LogUnhandledException;
#endif
        }

        private void LogUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = (Exception)e.ExceptionObject;
            if (exception != null)
            {
                Trace.WriteLine("Unhandled exception: " + exception.Message);
                Trace.Flush();
            }
        }

        public void Dispose()
        {
            Trace.Flush();
#if TRACE_TO_FILE
            Trace.Listeners.Remove(textTraceListener);
            textTraceListener.Dispose();
            textTraceListener = null;
#endif
            if (asyncWebClient != null)
            {
                asyncWebClient.Dispose();
                asyncWebClient = null;
            }
        }

        public static string ShortenPath(string path, int maxLength = 48)
        {
            string ellipsisChars = "...";
            char dirSeperatorChar = Path.DirectorySeparatorChar;
            string directorySeperator = dirSeperatorChar.ToString();

            if (path.Length <= maxLength)
                return path;
            int ellipsisLength = ellipsisChars.Length;
            if (maxLength <= ellipsisLength)
                return ellipsisChars;


            //alternate between taking a section from the start (firstPart) or the path and the end (lastPart)  
            bool isFirstPartsTurn = true; //drive letter has first priority, so start with that and see what else there is room for  

            //vars for accumulating the first and last parts of the final shortened path  
            string firstPart = "";
            string lastPart = "";
            //keeping track of how many first/last parts have already been added to the shortened path  
            int firstPartsUsed = 0;
            int lastPartsUsed = 0;

            string[] pathParts = path.Split(dirSeperatorChar);
            for (int i = 0; i < pathParts.Length; i++)
            {
                if (isFirstPartsTurn)
                {
                    string partToAdd = pathParts[firstPartsUsed] + directorySeperator;
                    if ((firstPart.Length + lastPart.Length + partToAdd.Length + ellipsisLength) > maxLength)
                    {
                        break;
                    }
                    firstPart = firstPart + partToAdd;
                    if (partToAdd == directorySeperator)
                    {
                        //this is most likely the first part of and UNC or relative path   
                        //do not switch to lastpart, as these are not "true" directory seperators  
                        //otherwise "\\myserver\theshare\outproject\www_project\file.txt" becomes "\\...\www_project\file.txt" instead of the intended "\\myserver\...\file.txt")  
                    }
                    else
                    {
                        isFirstPartsTurn = false;
                    }
                    firstPartsUsed++;
                }
                else
                {
                    int index = pathParts.Length - lastPartsUsed - 1; //-1 because of length vs. zero-based indexing  
                    string partToAdd = directorySeperator + pathParts[index];
                    if ((firstPart.Length + lastPart.Length + partToAdd.Length + ellipsisLength) > maxLength)
                    {
                        break;
                    }
                    lastPart = partToAdd + lastPart;
                    if (partToAdd == directorySeperator)
                    {
                        //this is most likely the last part of a relative path (e.g. "\websites\myproject\www_myproj\App_Data\")  
                        //do not proceed to processing firstPart yet  
                    }
                    else
                    {
                        isFirstPartsTurn = true;
                    }
                    lastPartsUsed++;
                }
            }

            if (lastPart == "")
            {
                //the filename (and root path) in itself was longer than maxLength, shorten it  
                lastPart = pathParts[pathParts.Length - 1];//"pathParts[pathParts.Length -1]" is the equivalent of "Path.GetFileName(pathToShorten)"  
                lastPart = lastPart.Substring(lastPart.Length + ellipsisLength + firstPart.Length - maxLength, maxLength - ellipsisLength - firstPart.Length);
            }

            return firstPart + ellipsisChars + lastPart;
        }

        public void CheckForUpdatesAsync(PlayerWindow window)
        {
            Action<string> processVersionInfo = (versionInfo) =>
            {
                var latestVersionXML = new XmlDocument();
                string downloadPageUrl;
                Version latestVersion;

                // Process version info
                try
                {
                    latestVersionXML.LoadXml(versionInfo);
                    latestVersion = new Version(latestVersionXML.SelectNodes("//Latest/Version")[0].InnerText);
                    downloadPageUrl = latestVersionXML.SelectNodes("//Latest/DownloadPageUrl")[0].InnerText;
                }
                catch (Exception e)
                {
                    Trace.WriteLine("Failed to process update data: " + e.Message);
                    return;
                }

                // Process optional release notes
                string releaseNotes = string.Empty;
                try
                {
                    releaseNotes = latestVersionXML.SelectNodes("//Latest/ReleaseNotes")[0].InnerText;
                }
                catch (Exception)
                {
                    Trace.WriteLine("Release notes not present in update data.");
                }

                // Check version against current version
                var currentVersion = new Version(ProductVersion);
                if (latestVersion > currentVersion)
                {
                    var alertText = "A new version of OCTOPUS RAW Player is available: " + latestVersion + "\nProceed to download page?";
                    if (!string.IsNullOrEmpty(releaseNotes))
                        alertText += "\n\n" + releaseNotes;
                    if (window.NativeWindow.Alert(AlertType.YesNo, alertText, "Update Available") == AlertResponse.Yes)
                        window.NativeWindow.OpenUrl("\"" + downloadPageUrl + "\"");
                }
            };

            // Clear prior async web client
            if (asyncWebClient != null)
            {
                asyncWebClient.Dispose();
                asyncWebClient = null;
            }

            // Setup new async web client
            asyncWebClient = new WebClientWithTimeout();
            asyncWebClient.DownloadStringCompleted += (sender, e) =>
            {
                try
                {
                    if (e.Error != null)
                        throw e.Error;
                    processVersionInfo(e.Result);
                }
                catch (Exception asyncException)
                {
                    Trace.WriteLine("Failed to download update data: " + asyncException.Message);
                }
            };

            // Begin download of version info
            try
            {
                asyncWebClient.DownloadStringAsync(new Uri(LatestVersionURL));
            }
            catch (Exception e)
            {
                Trace.WriteLine("Failed to download update data: " + e.Message);
                return;
            }
        }

        public void CheckForUpdates(PlayerWindow window, bool interactive = false)
        {
            Version latestVersion;
            string downloadPageUrl;
            var latestVersionXML = new XmlDocument();
            
            try
            {
                using (var wc = new WebClientWithTimeout())
                    latestVersionXML.LoadXml(wc.DownloadString(LatestVersionURL));
                latestVersion = new Version(latestVersionXML.SelectNodes("//Latest/Version")[0].InnerText);
                downloadPageUrl = latestVersionXML.SelectNodes("//Latest/DownloadPageUrl")[0].InnerText;
            }
            catch (Exception e)
            {
                if (interactive)
                    window.NativeWindow.Alert(AlertType.Warning, "Failed to retrieve update data.\n" + e.Message, "Check for Updates");
                return;
            }

            string releaseNotes = string.Empty;
            try
            {
                releaseNotes = latestVersionXML.SelectNodes("//Latest/ReleaseNotes")[0].InnerText;
            }
            catch(Exception)
            {
                Trace.WriteLine("Release notes not present in update data.");
            }

            var currentVersion = new Version(ProductVersion);
            if ( latestVersion > currentVersion )
            {
                var alertText = "A new version of OCTOPUS RAW Player is available: " + latestVersion + "\nProceed to download page?";
                if (!string.IsNullOrEmpty(releaseNotes))
                    alertText += "\n\n" + releaseNotes;
                if (window.NativeWindow.Alert(AlertType.YesNo, alertText, "Update Available") == AlertResponse.Yes)
                    window.NativeWindow.OpenUrl("\"" + downloadPageUrl + "\"");
            }
            else if (interactive)
                window.NativeWindow.Alert(AlertType.Information, "You have the latest version: " + currentVersion, "Check for Updates");
        }
    }
}
