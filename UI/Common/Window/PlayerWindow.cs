using OpenTK.Mathematics;
using System;
using System.Diagnostics;

namespace Octopus.Player.UI
{
    public class PlayerWindow
    {
        public INativeWindow NativeWindow { get; private set; }
        public Core.Playback.IPlayback Playback { get; private set; }
        GPU.Render.IContext RenderContext { get; set; }
        public ITheme Theme { get; private set; }

        public PlayerWindow(INativeWindow nativeWindow, ITheme theme = null)
        {
            NativeWindow = nativeWindow;
            Theme = theme != null ? theme : new DefaultTheme();
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
                case "visitGithub":
                    NativeWindow.OpenUrl("https://github.com/octopuscinema");
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
                case "metadata":
                    Debug.Assert(Playback != null && Playback.Clip != null);
                    if (Playback != null && Playback.Clip != null)
                        NativeWindow.Alert(AlertType.Blank, Playback.Clip.Metadata.ToString() + "\n", "Metadata for '" + Playback.Clip.Metadata.Title + "'");
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
                Playback.ClipOpened -= OnClipOpened;
                Playback.ClipClosed -= OnClipClosed;
                Playback = null;
            }

            // Create the playback if necessary
            if (Playback == null)
            {
                Playback = Activator.CreateInstance(typeof(T), RenderContext) as T;
                Playback.ClipOpened += OnClipOpened;
                Playback.ClipClosed += OnClipClosed;
            } 
            else
                Playback.Close();

            // Open the clip
            return Playback.Open(clip);
        }
        public void OnClipClosed(object sender, EventArgs e)
        {
            NativeWindow.EnableMenuItem("clip", false);
            NativeWindow.SetWindowTitle("OCTOPUS RAW Player");
            RenderContext.BackgroundColor = Theme.EmptyBackground;
            RenderContext.RedrawBackground = GPU.Render.RedrawBackground.Once;
        }

        public void OnClipOpened(object sender, EventArgs e)
        {
            NativeWindow.EnableMenuItem("clip", true);
            Debug.Assert(Playback != null && Playback.Clip != null);
            if ( Playback != null && Playback.Clip != null )
                NativeWindow.SetWindowTitle(Playback.Clip.Metadata.Title);
            RenderContext.BackgroundColor = Theme.ClipBackground;
            RenderContext.RedrawBackground = GPU.Render.RedrawBackground.Once;
        }

        public void OnFramebufferResize(Vector2i framebufferSize)
        {
            RenderContext.RedrawBackground = GPU.Render.RedrawBackground.Once;
        }

        public void OnRenderInit(GPU.Render.IContext renderContext)
        {
            RenderContext = renderContext;
            RenderContext.BackgroundColor = Theme.EmptyBackground;
            RenderContext.RedrawBackground = GPU.Render.RedrawBackground.Once;
        }

        public void OnRenderFrame(double timeInterval)
        {
            RenderContext.OnRenderFrame(timeInterval);
            if (Playback != null)
                Playback.OnRenderFrame(timeInterval);
        }
    }
}

