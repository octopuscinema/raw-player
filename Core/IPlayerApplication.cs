using System;
using System.IO;
using System.Linq;
using System.Reflection;
using static Octopus.Player.Core.Playback.IPlayback;

namespace Octopus.Player.Core
{
    public interface IPlayerApplication : IDisposable
    {
        string LatestVersionURL { get; }

        string LogPath { get; }

        string RecentFilesJsonPath { get; }
        
        string FavouritesJsonPath { get; }
        
        string ProductName { get; }
        string ProductVersion { get; }
        
        string ProductBuildVersion { get; }
      
        string ProductCopyright { get; }
        public string ProductLicense { get ; }
        public string ProductVersionMajor { get; }

        public string[] OpenOnStart { get; set; }

        void CheckForUpdates(IPlayerWindow window, bool interactive = false);
    }
}