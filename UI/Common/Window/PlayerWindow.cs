using System;
using System.Diagnostics;
using Octopus.Player;

namespace Octopus.Player.UI
{
    public class PlayerWindow
    {
        private INativeWindow NativeWindow { get; set; }
        public Core.Playback.IPlayback Playback { get; private set; }
        public bool ForceRender { get; private set; }
        GPU.Render.IContext RenderContext { get; set; }

        public PlayerWindow(INativeWindow nativeWindow)
        {
            // Remove this commented out line if teh trace appears on MSVC (Windows) without it
            //Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
            NativeWindow = nativeWindow;
            ForceRender = true;
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
                case "about":
                    NativeWindow.Alert(AlertType.Blank, "\n\t\t  OCTOPUS RAW Player\n\t\t ------------------------\n\t\t  Pre-release version X.X\n\t\t           MIT License\n\n\n\t\t© 2022 OCTOPUSCINEMA\t\t", "About OCTOPUS RAW Player");
                    break;
                case "visitInstagram":
                    NativeWindow.OpenUrl("https://www.instagram.com/octopuscinema/");
                    break;
                case "visitYoutube":
                    NativeWindow.OpenUrl("https://www.youtube.com/channel/UCq7Bk-mekVLJS63I6XUpyLw");
                    break;
                case "visitWebsite":
                    NativeWindow.OpenUrl("http://www.octopuscinema.com/wiki/index.php?title=HI!");
                    break;
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
            var dngSequenceClip = new Core.Playback.ClipCinemaDNG(dngPath);
            return OpenClip<Core.Playback.PlaybackCinemaDNG>(dngSequenceClip);
        }

        private Core.Error OpenClip<T>(Core.Playback.IClip clip) where T : Core.Playback.Playback
        {
            var dngValidity = clip.Validate();
            if (dngValidity != Core.Error.None)
                return dngValidity;

            // Current playback doesn't support this clip, shut it down
            if (Playback != null && !Playback.SupportsClip(clip))
            {
                Playback.Close();
                Playback = null;
            }

            // Create the playback if necessary
            if (Playback == null)
                Playback = Activator.CreateInstance(typeof(T), RenderContext) as T;
            else
                Playback.Close();

            // Open the clip
            return Playback.Open(clip);
        }

        public void OnRenderInit(GPU.Render.IContext renderContext)
        {
            RenderContext = renderContext;
        }

        public void OnRenderFrame(double timeInterval)
        {
            RenderContext.OnRenderFrame(timeInterval);
            ForceRender = false;
        }
    }
}

