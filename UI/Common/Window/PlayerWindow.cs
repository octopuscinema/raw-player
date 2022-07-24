using Octopus.Player.Core;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Octopus.Player.UI
{
    public class PlayerWindow : IDisposable, IPlayerWindow
    {
        public INativeWindow NativeWindow { get; private set; }
        public Core.Playback.IPlayback Playback { get; private set; }
        GPU.Render.IContext RenderContext { get; set; }
        public ITheme Theme { get; private set; }

        public PlayerWindow(INativeWindow nativeWindow, ITheme theme = null)
        {
            NativeWindow = nativeWindow;
            Theme = theme != null ? theme : new DefaultTheme();
            NativeWindow.EnableMenuItem("clip", false);
            NativeWindow.SetLabelContent("timeCodeLabel", "--:--:--:--");
            NativeWindow.SetLabelContent("durationLabel", "--:--:--");
        }

        public void LeftMouseDown(uint clickCount)
        {
            // Double click to go full screen
            if (clickCount == 2)
                NativeWindow.ToggleFullscreen();
        }

        public void RightMouseDown(uint clickCount)
        {

        }

        private void MenuWhiteBalanceClick(string whiteBalanceMenuId)
        {
            if (Playback != null && Playback.Clip != null && Playback.Clip.RawParameters.HasValue)
            {
                var whiteBalancePresets = new Dictionary<string, Tuple<float, float>>()
                {
                    { "whiteBalanceAsShot", null },
                    { "whiteBalanceShade", new Tuple<float, float>(     7500.0f, 10.0f) },
                    { "whiteBalanceCloud", new Tuple<float, float>(     6500.0f, 10.0f) },
                    { "whiteBalanceDaylight", new Tuple<float, float>(  5500.0f, 10.0f) },
                    { "whiteBalanceFluorescent", new Tuple<float,float>(3800.0f, 21.0f) },
                    { "whiteBalanceTungsten", new Tuple<float, float>(  3200.0f, 0.0f) }
                };
                Debug.Assert(whiteBalancePresets.ContainsKey(whiteBalanceMenuId));

                var rawParameters = Playback.Clip.RawParameters.Value;
                rawParameters.whiteBalance = whiteBalancePresets[whiteBalanceMenuId];
                Playback.Clip.RawParameters = rawParameters;
                NativeWindow.CheckMenuItem(whiteBalanceMenuId);
                RenderContext.RequestRender();
            }
        }

        private void MenuExposureClick(string exposureMenuId)
        {
            if (Playback != null && Playback.Clip != null && Playback.Clip.RawParameters.HasValue)
            {
                var exposurePresets = new Dictionary<string, float?>()
                {
                    {"exposureAsShot", null },
                    {"exposureMinusTwo", -2.0f },
                    {"exposureMinusOne", -1.0f },
                    {"exposureZero", 0.0f },
                    {"exposurePlusOne", 1.0f },
                    {"exposurePlusTwo", 2.0f }
                };
                Debug.Assert(exposurePresets.ContainsKey(exposureMenuId));

                var rawParameters = Playback.Clip.RawParameters.Value;
                rawParameters.exposure = exposurePresets[exposureMenuId];
                Playback.Clip.RawParameters = rawParameters;
                NativeWindow.CheckMenuItem(exposureMenuId);
                RenderContext.RequestRender();
            }
        }

        private void MenuAdvancedRawParameterClick(string id)
        {
            if (Playback == null || Playback.Clip == null || !Playback.Clip.RawParameters.HasValue)
                return;

            NativeWindow.ToggleMenuItemChecked(id);

            var rawParameters = Playback.Clip.RawParameters.Value;
            switch (id)
            {
                case "highlightRecovery":
                    rawParameters.highlightRecovery = NativeWindow.MenuItemIsChecked(id) ? Core.HighlightRecovery.On : Core.HighlightRecovery.Off;
                    if (rawParameters.highlightRecovery == Core.HighlightRecovery.On)
                    {
                        NativeWindow.CheckMenuItem("highlightRollOff", true, false);
                        rawParameters.highlightRollOff = Core.HighlightRollOff.Low;
                    }
                    NativeWindow.EnableMenuItem("highlightRollOff", rawParameters.highlightRecovery == Core.HighlightRecovery.On ? false : true);
                    break;
                case "highlightRollOff":
                    rawParameters.highlightRollOff = NativeWindow.MenuItemIsChecked(id) ? Core.HighlightRollOff.Low : Core.HighlightRollOff.Off;
                    break;
                case "toneMapping":
                    rawParameters.toneMappingOperator = NativeWindow.MenuItemIsChecked(id) ? Core.ToneMappingOperator.SDR : Core.ToneMappingOperator.None;
                    break;
                case "gamutCompression":
                    rawParameters.gamutCompression = NativeWindow.MenuItemIsChecked(id) ? Core.GamutCompression.Rec709 : Core.GamutCompression.Off;
                    break;
                default:
                    Debug.Assert(false, "Unhandled menu item: " + id);
                    return;
            }
            Playback.Clip.RawParameters = rawParameters;
            RenderContext.RequestRender();
        }

        public void MenuItemClick(string id)
        {
            switch (id)
            {
                // Advanced raw paramters
                case "highlightRecovery":
                case "highlightRollOff":
                case "toneMapping":
                case "gamutCompression":
                    MenuAdvancedRawParameterClick(id);
                    break;

                // White balance
                case "whiteBalanceAsShot":
                case "whiteBalanceShade":
                case "whiteBalanceCloud":
                case "whiteBalanceDaylight":
                case "whiteBalanceFluorescent":
                case "whiteBalanceTungsten":
                    if (!NativeWindow.MenuItemIsChecked(id))
                        MenuWhiteBalanceClick(id);
                    break;

                // Exposure
                case "exposureAsShot":
                case "exposureMinusTwo":
                case "exposureMinusOne":
                case "exposureZero":
                case "exposurePlusOne":
                case "exposurePlusTwo":
                    if (!NativeWindow.MenuItemIsChecked(id))
                        MenuExposureClick(id);
                    break;

                // Colour space
                case "colorSpaceRec709":
                    NativeWindow.CheckMenuItem(id);
                    break;

                // Gamma space
                case "gammaSpaceRec709":
                    NativeWindow.CheckMenuItem(id);
                    break;

                // Help
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
                    NativeWindow.OpenUrl("\"http://www.octopuscinema.com/wiki/index.php?title=HI!\"");
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

                // Open
                case "openCinemaDNG":
                    var dngPath = NativeWindow.OpenFolderDialogue("Select folder containing CinemaDNG sequence", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
                    if (dngPath != null)
                        OpenCinemaDNG(dngPath);
                    break;

                // Clip
                case "metadata":
                    Debug.Assert(Playback != null && Playback.Clip != null);
                    if (Playback != null && Playback.Clip != null)
                        NativeWindow.Alert(AlertType.Blank, Playback.Clip.Metadata.ToString() + "\n", "Metadata for '" + Playback.Clip.Metadata.Title + "'");
                    break;

                // Debayer
                case "debayerQualityDraft":
                    break;
                default:
                    Debug.Assert(false, "Unhandled menu item: " + id);
                    break;
            }
        }

        public void ButtonClick(string id)
        {
            switch (id)
            {
                case "playButton":
                    if (Playback != null)
                    {
                        if (Playback.State == Core.Playback.State.Stopped || Playback.State == Core.Playback.State.Paused || Playback.State == Core.Playback.State.PausedEnd)
                            Playback.Play();
                    }
                    break;
                case "pauseButton":
                    if ( Playback != null )
                    {
                        if (Playback.State == Core.Playback.State.Playing)
                            Playback.Pause();
                    }
                    break;
                default:
                    break;
            }
        }

        private Core.Error OpenCinemaDNG(string dngPath)
        {
            var dngSequenceClip = new Core.ClipCinemaDNG(dngPath);
            return OpenClip<Core.Playback.PlaybackCinemaDNG>(dngSequenceClip);
        }

        private Core.Error OpenClip<T>(Core.IClip clip) where T : Core.Playback.Playback
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
                Playback.StateChanged -= OnPlaybackStateChanged;
                Playback.FrameDisplayed -= OnFrameDisplayed;
                Playback.Dispose();
                Playback = null;
            }

            // Create the playback if necessary
            if (Playback == null)
            {
                Playback = Activator.CreateInstance(typeof(T), this, RenderContext) as T;
                Playback.ClipOpened += OnClipOpened;
                Playback.ClipClosed += OnClipClosed;
                Playback.StateChanged += OnPlaybackStateChanged;
                Playback.FrameDisplayed += OnFrameDisplayed;
            }
            else
                Playback.Close();

            // Open the clip
            return Playback.Open(clip);
        }

        private void OnPlaybackStateChanged(object sender, EventArgs e)
        {
            Debug.Assert(Playback != null);
            switch (Playback.State)
            {
                case Core.Playback.State.Stopped:
                    NativeWindow.SetSliderValue("seekBar", 0.0f);
                    NativeWindow.SetButtonVisibility("pauseButton", false);
                    NativeWindow.SetButtonVisibility("playButton", true);
                    break;
                case Core.Playback.State.Paused:
                case Core.Playback.State.PausedEnd:
                    NativeWindow.SetButtonVisibility("pauseButton", false);
                    NativeWindow.SetButtonVisibility("playButton", true);
                    break;
                case Core.Playback.State.Empty:
                    NativeWindow.SetButtonVisibility("pauseButton", false);
                    NativeWindow.SetButtonVisibility("playButton", false);
                    break;
                case Core.Playback.State.Playing:
                case Core.Playback.State.Buffering:
                case Core.Playback.State.PlayingFromBuffer:
                    NativeWindow.SetButtonVisibility("pauseButton", true);
                    NativeWindow.SetButtonVisibility("playButton", false);
                    break;
            }
        }

        public void OnFrameDisplayed(object sender, uint frame)
        {
            // Update seek bar
            var playhead = (Playback.LastFrame == Playback.FirstFrame) ? 1.0f : (float)(frame - Playback.FirstFrame) / (float)(Playback.LastFrame - Playback.FirstFrame);
            NativeWindow.SetSliderValue("seekBar", playhead);
        }

        public void OnClipClosed(object sender, EventArgs e)
        {
            NativeWindow.SetLabelContent("timeCodeLabel", "--:--:--:--");
            NativeWindow.SetLabelContent("durationLabel", "--:--:--");
            NativeWindow.EnableMenuItem("clip", false);
            NativeWindow.SetWindowTitle("OCTOPUS RAW Player");
            RenderContext.BackgroundColor = Theme.EmptyBackground;
            RenderContext.RedrawBackground = GPU.Render.RedrawBackground.Once;
            NativeWindow.UnlockAspect();
        }

        public void OnClipOpened(object sender, EventArgs e)
        {
            NativeWindow.SetLabelContent("timeCodeLabel", "00:00:00:00");
            NativeWindow.SetSliderValue("seekBar", 0.0f);
            NativeWindow.EnableMenuItem("clip", true);
            NativeWindow.CheckMenuItem("exposureAsShot");
            NativeWindow.CheckMenuItem("toneMapping", true, false);

            bool isColour = false;
            Debug.Assert(Playback != null && Playback.Clip != null);
            if (Playback != null && Playback.Clip != null && Playback.Clip.Metadata != null)
            {
                var duration = new Core.Maths.TimeCode(Playback.Clip.Metadata.DurationFrames, Playback.Clip.Metadata.Framerate);
                NativeWindow.SetLabelContent("durationLabel", duration.ToString());

                NativeWindow.SetWindowTitle(Playback.Clip.Metadata.Title);
                NativeWindow.SetMenuItemTitle("exposureAsShot", "As Shot (" + Playback.Clip.Metadata.ExposureValue.ToString("F") + ")");

                if (Playback.Clip.Metadata.ColorProfile.HasValue)
                {
                    isColour = true;
                    var asShotWhiteBalance = Playback.Clip.Metadata.ColorProfile.Value.AsShotWhiteBalance();
                    if (asShotWhiteBalance.Item2 == 0.0)
                        NativeWindow.SetMenuItemTitle("whiteBalanceAsShot", "As Shot (" + asShotWhiteBalance.Item1.ToString("0") + "K)");
                    else
                        NativeWindow.SetMenuItemTitle("whiteBalanceAsShot", "As Shot (" + asShotWhiteBalance.Item1.ToString("0") + "K, Tint: " +
                            asShotWhiteBalance.Item2.ToString("+#;-#;0") + ")");
                    NativeWindow.CheckMenuItem("whiteBalanceAsShot");
                }
                NativeWindow.LockAspect(Playback.Clip.Metadata.AspectRatio);
            }

            NativeWindow.EnableMenuItem("whiteBalance", isColour);
            NativeWindow.EnableMenuItem("colorSpace", isColour);
            NativeWindow.EnableMenuItem("debayerQuality", isColour);
            NativeWindow.EnableMenuItem("highlightRecovery", isColour);
            NativeWindow.EnableMenuItem("gamutCompression", isColour);
            NativeWindow.EnableMenuItem("highlightRollOff", false);
            NativeWindow.CheckMenuItem("highlightRecovery", isColour, false);
            NativeWindow.CheckMenuItem("highlightRollOff", isColour, false);
            NativeWindow.CheckMenuItem("gamutCompression", isColour, false);

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

        public void Dispose()
        {
            if (Playback != null)
            {
                Debug.Assert(Playback.IsOpen());
                if (Playback.IsOpen())
                    Playback.Close();
                Playback.Dispose();
                Playback = null;
            }
        }

        public void InvokeOnUIThread(Action action, bool async = true)
        {
            NativeWindow.InvokeOnUIThread(action, async);
        }
    }
}

