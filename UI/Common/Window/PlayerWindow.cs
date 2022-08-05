using Octopus.Player.Core;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Octopus.Player.UI
{
    public class PlayerWindow : IPlayerWindow
    {
        public INativeWindow NativeWindow { get; private set; }
        public Core.Playback.IPlayback Playback { get; private set; }
        public ITheme Theme { get; private set; }
        private GPU.Render.IContext RenderContext { get; set; }
        private Timer AnimateOutControlsTimer { get; set; }

        private DateTime lastInteraction;

        // Application info (Maybe move to a separate class)
        public string ProductName { get { return Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute)).OfType<AssemblyProductAttribute>().FirstOrDefault().Product; } }
        public string Version { get { return Assembly.GetEntryAssembly().GetName().Version.ToString(); } }
        public string VersionMajor 
        { 
            get 
            {
                var versionParts = Version.Split('.', StringSplitOptions.RemoveEmptyEntries);
                return versionParts.Length > 0 ? versionParts[0] : "";
            }
        }
        public string Company { get { return Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyCompanyAttribute)).OfType<AssemblyCompanyAttribute>().FirstOrDefault().Company; } }
        public string License { get { return "MIT License"; } }
        public string Copyright { get { return Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute)).OfType<AssemblyCopyrightAttribute>().FirstOrDefault().Copyright; } }

        public PlayerWindow(INativeWindow nativeWindow, ITheme theme = null)
        {
            NativeWindow = nativeWindow;
            Theme = theme != null ? theme : new DefaultTheme();
            lastInteraction = DateTime.Now;
        }

        public void OnLoad()
        {
            NativeWindow.EnableMenuItem("clip", false);
            NativeWindow.SetLabelContent("timeCodeLabel", "--:--:--:--");
            NativeWindow.SetLabelContent("durationLabel", "--:--:--");
            NativeWindow.SetLabelContent("fastForwardLabel", "");
            NativeWindow.SetLabelContent("fastRewindLabel", "");
            NativeWindow.SetButtonEnabled("playButton", false);
            NativeWindow.SetButtonEnabled("pauseButton", false);
            NativeWindow.SetButtonEnabled("fastForwardButton", false);
            NativeWindow.SetButtonEnabled("fastRewindButton", false);
            NativeWindow.SetButtonEnabled("nextButton", false);
            NativeWindow.SetButtonEnabled("previousButton", false);
            NativeWindow.SetSliderEnabled("seekBar", false);

            // Create the animate controls timer
            AnimateOutControlsTimer = new Timer(new TimerCallback(AnimateOutControls), null, TimeSpan.Zero, TimeSpan.FromSeconds(1.0));
        }

        private void AnimateOutControls(object obj)
        {
            if (NativeWindow.ControlsAnimationState == ControlsAnimationState.In && (DateTime.Now - lastInteraction) > Theme.ControlsAnimationDelay && Playback != null &&
                Playback.State != Core.Playback.State.Empty)
            {
                NativeWindow.InvokeOnUIThread(() =>
                {
                    if (NativeWindow.ControlsAnimationState == ControlsAnimationState.In)
                        NativeWindow.AnimateOutControls();
                });
            }
        }

        public void LeftMouseDown(uint clickCount)
        {
            lastInteraction = DateTime.Now;
            if (NativeWindow.ControlsAnimationState == ControlsAnimationState.Out)
                NativeWindow.AnimateInControls();

            // Double click to go full screen
            if (clickCount == 2)
                NativeWindow.ToggleFullscreen();
        }

        public void RightMouseDown(uint clickCount)
        {
            lastInteraction = DateTime.Now;
            if (NativeWindow.ControlsAnimationState == ControlsAnimationState.Out)
                NativeWindow.AnimateInControls();
        }

        public void MouseMove(in Vector2 localPosition)
        {
            lastInteraction = DateTime.Now;
            if (NativeWindow.ControlsAnimationState == ControlsAnimationState.Out)
                NativeWindow.AnimateInControls();
        }

        public void MouseExited(in Vector2 localPosition)
        {
            if (NativeWindow.ControlsAnimationState == ControlsAnimationState.In && Playback != null && Playback.State != Core.Playback.State.Empty)
                NativeWindow.AnimateOutControls();
        }

        public void MouseEntered(in Vector2 localPosition)
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
            lastInteraction = DateTime.Now;
            if (NativeWindow.ControlsAnimationState == ControlsAnimationState.Out)
                NativeWindow.AnimateInControls();

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
                    string aboutText = "\n\t\t  " + ProductName + "\n\t\t  ------------------------\n\t\t     ";
                    string versionText = VersionMajor == "0" ?  "Pre-release  " + Version : "Release " + Version;
                    aboutText += versionText + "\n\t\t           ";
                    aboutText += License;
                    aboutText += "\n\n\n\t\t" + Copyright + "\t\t";
                    NativeWindow.Alert(AlertType.Blank, aboutText, "About " + ProductName);
                    break;
                case "visitInstagram":
                    NativeWindow.OpenUrl("https://www.instagram.com/octopuscinema/");
                    break;
                case "visitYoutube":
                    NativeWindow.OpenUrl("https://www.youtube.com/channel/UCq7Bk-mekVLJS63I6XUpyLw");
                    break;
                case "visitWebsite":
                    NativeWindow.OpenUrl("\"http://www.octopuscinema.com/wiki/index.php?title=OCTOPUS_RAW_Player\"");
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
            lastInteraction = DateTime.Now;
            if (NativeWindow.ControlsAnimationState == ControlsAnimationState.Out)
                NativeWindow.AnimateInControls();

            switch (id)
            {
                case "previousButton":
                    if ( Playback != null && Playback.State != Core.Playback.State.Empty)
                    {
                        if (Playback.State == Core.Playback.State.Stopped)
                        {

                        }
                        else
                        {
                            bool resume = Playback.IsPlaying;
                            Playback.Stop();
                            if (resume)
                                Playback.Play();
                        }
                    }
                    break;
                case "fastRewindButton":
                    if (Playback != null && Playback.State != Core.Playback.State.Empty && Playback.State != Core.Playback.State.Stopped)
                    {
                        if ((Playback.State == Core.Playback.State.Playing || Playback.State == Core.Playback.State.PlayingFromBuffer) && Playback.Velocity != Core.Playback.PlaybackVelocity.Backward10x)
                            Playback.Pause();
                        switch (Playback.Velocity)
                        {
                            case Core.Playback.PlaybackVelocity.Backward10x:
                            case Core.Playback.PlaybackVelocity.Backward5x:
                                Playback.Velocity = Core.Playback.PlaybackVelocity.Backward10x;
                                break;
                            case Core.Playback.PlaybackVelocity.Backward2x:
                                Playback.Velocity = Core.Playback.PlaybackVelocity.Backward5x;
                                break;
                            default:
                                Playback.Velocity = Core.Playback.PlaybackVelocity.Backward2x;
                                break;
                        }
                        if (Playback.State == Core.Playback.State.PausedEnd || Playback.State == Core.Playback.State.Paused)
                            Playback.Play();
                    }
                    break;
                case "fastForwardButton":
                    if (Playback != null && Playback.State != Core.Playback.State.Empty && Playback.State != Core.Playback.State.PausedEnd)
                    {
                        if ((Playback.State == Core.Playback.State.Playing || Playback.State == Core.Playback.State.PlayingFromBuffer ) && Playback.Velocity != Core.Playback.PlaybackVelocity.Forward10x)
                            Playback.Pause();
                        switch (Playback.Velocity)
                        {
                            case Core.Playback.PlaybackVelocity.Forward10x:
                            case Core.Playback.PlaybackVelocity.Forward5x:
                                Playback.Velocity = Core.Playback.PlaybackVelocity.Forward10x;
                                break;
                            case Core.Playback.PlaybackVelocity.Forward2x:
                                Playback.Velocity = Core.Playback.PlaybackVelocity.Forward5x;
                                break;
                            default:
                                Playback.Velocity = Core.Playback.PlaybackVelocity.Forward2x;
                                break;
                        }
                        if (Playback.State == Core.Playback.State.Stopped || Playback.State == Core.Playback.State.Paused)
                            Playback.Play();
                    }
                    break;
                case "playButton":
                    if (Playback != null)
                    {
                        if (Playback.State == Core.Playback.State.Stopped || Playback.State == Core.Playback.State.Paused || Playback.State == Core.Playback.State.PausedEnd)
                        {
                            Playback.Velocity = Core.Playback.PlaybackVelocity.Forward1x;
                            Playback.Play();
                        }
                    }
                    break;
                case "pauseButton":
                    if (Playback != null)
                    {
                        if (Playback.State == Core.Playback.State.Playing || Playback.State == Core.Playback.State.PlayingFromBuffer)
                        {
                            Playback.Pause();
                            Playback.Velocity = Core.Playback.PlaybackVelocity.Forward1x;
                        }
                    }
                    break;
                default:
                    break;
            }
        }

        private Error OpenCinemaDNG(string dngPath)
        {
            var dngSequenceClip = new ClipCinemaDNG(dngPath);
            return OpenClip<Core.Playback.PlaybackCinemaDNG>(dngSequenceClip);
        }

        private Error OpenClip<T>(IClip clip) where T : Core.Playback.Playback
        {
            var dngValidity = clip.Validate();
            if (dngValidity != Error.None)
                return dngValidity;

            // Current playback doesn't support this clip, shut it down
            if (Playback != null && !Playback.SupportsClip(clip))
            {
                Playback.Close();
                Playback.ClipOpened -= OnClipOpened;
                Playback.ClipClosed -= OnClipClosed;
                Playback.StateChanged -= OnPlaybackStateChanged;
                Playback.FrameDisplayed -= OnFrameDisplayed;
                Playback.FrameSkipped -= OnFrameSkipped;
                Playback.FrameMissing -= OnFrameMissing;
                Playback.VelocityChanged -= OnPlaybackVelocityChanged;
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
                Playback.FrameSkipped += OnFrameSkipped;
                Playback.FrameMissing += OnFrameMissing;
                Playback.VelocityChanged += OnPlaybackVelocityChanged;
            }
            else
                Playback.Close();

            // Open the clip, if that fails close the playback
            var error = Playback.Open(clip);
            if (error != Error.None)
            {
                if (Playback.IsOpen())
                    Playback.Close();
                Playback.Dispose();
                Playback = null;
                string openFailtureMessage = "Failed to open '" + clip.Metadata.Title + "'\nError: " + error.ToString();
                NativeWindow.Alert(AlertType.Error, openFailtureMessage, "Error opening clip");
            }
            return error;
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
                    Playback.Velocity = Core.Playback.PlaybackVelocity.Forward1x;
                    break;
                case Core.Playback.State.Paused:
                    NativeWindow.SetButtonVisibility("pauseButton", false);
                    NativeWindow.SetButtonVisibility("playButton", true);
                    break;
                case Core.Playback.State.PausedEnd:
                    NativeWindow.SetButtonVisibility("pauseButton", false);
                    NativeWindow.SetButtonVisibility("playButton", true);
                    Playback.Velocity = Core.Playback.PlaybackVelocity.Forward1x;
                    break;
                case Core.Playback.State.Empty:
                    NativeWindow.SetSliderValue("seekBar", 0.0f);
                    NativeWindow.SetButtonVisibility("pauseButton", false);
                    NativeWindow.SetButtonVisibility("playButton", true);
                    break;
                case Core.Playback.State.Playing:
                case Core.Playback.State.Buffering:
                case Core.Playback.State.PlayingFromBuffer:
                    NativeWindow.SetButtonVisibility("pauseButton", true);
                    NativeWindow.SetButtonVisibility("playButton", false);
                    break;
            }
        }

        public void OnFrameDisplayed(uint frame, in Core.Maths.TimeCode timeCode)
        {
            UpdateFrameUI(frame, timeCode, Theme.LabelColour);
        }

        public void OnFrameSkipped(uint frameRequested, uint frameDisplayed, in Core.Maths.TimeCode synthesisedTimeCode)
        {
            UpdateFrameUI(frameRequested, synthesisedTimeCode, Theme.SkippedFrameColour);
        }

        public void OnFrameMissing(uint frameRequested, in Core.Maths.TimeCode synthesisedTimeCode)
        {
            UpdateFrameUI(frameRequested, synthesisedTimeCode, Theme.MissingFrameColour);
        }

        private void UpdateFrameUI(uint frame, in Core.Maths.TimeCode timeCode, in Vector3 timeCodeLabelColour)
        {
            // Update seek bar
            var playhead = (Playback.LastFrame == Playback.FirstFrame) ? 1.0f : (float)(frame - Playback.FirstFrame) / (float)(Playback.LastFrame - Playback.FirstFrame);
            NativeWindow.SetSliderValue("seekBar", playhead);

            // Update timecode label
            NativeWindow.SetLabelContent("timeCodeLabel", timeCode.ToString(), timeCodeLabelColour);
        }

        public void OnClipClosed(object sender, EventArgs e)
        {
            NativeWindow.SetButtonEnabled("playButton", false);
            NativeWindow.SetButtonEnabled("pauseButton", false);
            NativeWindow.SetButtonEnabled("fastRewindButton", false);
            NativeWindow.SetButtonEnabled("fastForwardButton", false);
            NativeWindow.SetButtonEnabled("nextButton", false);
            NativeWindow.SetButtonEnabled("previousButton", false);
            NativeWindow.SetSliderEnabled("seekBar", false);
            NativeWindow.SetLabelContent("timeCodeLabel", "--:--:--:--", Theme.LabelColour);
            NativeWindow.SetLabelContent("durationLabel", "--:--:--");
            NativeWindow.EnableMenuItem("clip", false);
            NativeWindow.SetWindowTitle("OCTOPUS RAW Player");
            RenderContext.BackgroundColor = Theme.EmptyBackground;
            RenderContext.RedrawBackground = GPU.Render.RedrawBackground.Once;
            if (NativeWindow.AspectLocked)
                NativeWindow.UnlockAspect();
        }

        public void OnClipOpened(object sender, EventArgs e)
        {
            NativeWindow.SetButtonEnabled("playButton", true);
            NativeWindow.SetButtonEnabled("pauseButton", true);
            NativeWindow.SetButtonEnabled("fastRewindButton", true);
            NativeWindow.SetButtonEnabled("fastForwardButton", true);
            NativeWindow.SetButtonEnabled("previousButton", true);
            NativeWindow.SetSliderEnabled("seekBar", true);
            NativeWindow.SetSliderValue("seekBar", 0.0f);
            NativeWindow.EnableMenuItem("clip", true);
            NativeWindow.CheckMenuItem("exposureAsShot");
            NativeWindow.CheckMenuItem("toneMapping", true, false);

            bool isColour = false;
            Debug.Assert(Playback != null && Playback.Clip != null);
            if (Playback != null && Playback.Clip != null && Playback.Clip.Metadata != null)
            {
                // Show warning about missing framerate
                if (!Playback.Clip.Metadata.Framerate.HasValue)
                    NativeWindow.Alert(AlertType.Warning, "Clip is missing framerate metadata.\nPlayback framerate will default to: " + Playback.Framerate.ToString(true) + "fps.", "Missing framerate information");

                // Set start time code label
                var startTimeCode = Playback.Clip.Metadata.StartTimeCode.HasValue ? new Core.Maths.TimeCode(Playback.Clip.Metadata.StartTimeCode.Value)
                    : new Core.Maths.TimeCode(0, Playback.Framerate);
                NativeWindow.SetLabelContent("timeCodeLabel", startTimeCode.ToString(), Theme.LabelColour);

                // Set duration label
                bool? dropFrame = Playback.Clip.Metadata.StartTimeCode.HasValue ? Playback.Clip.Metadata.StartTimeCode.Value.DropFlag : (bool?)null;
                var duration = new Core.Maths.TimeCode(Playback.Clip.Metadata.DurationFrames, Playback.Framerate, dropFrame);
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

        private void OnPlaybackVelocityChanged(object sender, EventArgs e)
        {
            string velocityLabel = Math.Abs((int)Playback.Velocity).ToString() + "×";
            bool isForward = Core.Playback.Extensions.IsForward(Playback.Velocity);
            NativeWindow.SetLabelContent("fastForwardLabel", isForward && (Playback.Velocity != Core.Playback.PlaybackVelocity.Forward1x) ? velocityLabel : "");
            NativeWindow.SetLabelContent("fastRewindLabel", isForward ? "" : velocityLabel);
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
            if (AnimateOutControlsTimer != null)
            {
                using (var waitHandle = new ManualResetEvent(false))
                {
                    AnimateOutControlsTimer.Dispose(waitHandle);
                    waitHandle.WaitOne();
                }
                AnimateOutControlsTimer = null;
            }

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

