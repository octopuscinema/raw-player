using System;
using System.Diagnostics;
using Octopus.Player;

namespace Octopus.Player.UI
{
    public class PlayerWindow
    {
        private INativeWindow NativeWindow { get; set; }
        public Core.Playback.IPlayback Playback { get; private set; }

        public PlayerWindow(INativeWindow nativeWindow)
        {
            NativeWindow = nativeWindow;
        }

        public void LeftMouseDown(uint clickCount)
        {
            // Double click to go full screen
            if ( clickCount == 2 )
                NativeWindow.ToggleFullscreen();
        }

        public void RightMouseDown(uint clickCount)
        {
            
        }

        public void MenuItemClick(string name)
        {
            switch(name)
            {
                case "exit":
                    NativeWindow.Exit();
                    break;
                case "fullscreen":
                    NativeWindow.ToggleFullscreen();
                    break;
                case "openCinemaDNG":
                    var dngPath = NativeWindow.OpenFolderDialogue("Select folder containing CinemaDNG sequence", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
                    if (dngPath != null)
                        OpenCinemaDNG(dngPath);
                    break;
                default:
                    Debug.Assert(false,"Unhandled menu item: " + name);
                    break;
            }
        }

        private Core.Error OpenCinemaDNG(string dngPath)
        {
            var dngSequenceClip = new Core.Playback.CinemaDNGClip(dngPath);
            var dngValidity = dngSequenceClip.Validate();
            if (dngValidity != Core.Error.None)
                return dngValidity;
            return Core.Error.None;
        }
    }
}

