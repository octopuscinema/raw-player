using System;
using System.Diagnostics;

namespace Octopus.Player.UI
{
    public class WindowLogic
    {
        private INativeWindow NativeWindow { get; set; }

        public WindowLogic(INativeWindow nativeWindow)
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
                    break;
                default:
                    Debug.Assert(false,"Unhandled menu item: " + name);
                    break;
            }
        }
    }
}

