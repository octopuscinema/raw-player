using Octopus.Player.Core;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;

namespace Octopus.Player.UI
{
    public class PlayerWindow : IPlayerWindow
    {
        public INativeWindow NativeWindow { get; private set; }
        public Core.Playback.IPlayback Playback { get; private set; }
        private GPU.Render.IContext RenderContext { get; set; }
        private Timer AnimateOutControlsTimer { get; set; }
        private ITheme theme;
        public ITheme Theme
        {
            get { return theme; }
            set
            {
                if (theme != value)
                {
                    theme = value;
                    OnThemeChanged();
                }
            }
        }
        public RecentFiles RecentFiles { get; private set; }

        private DateTime lastInteraction;
        private float playhead;
        public event IPlayerWindow.ClipOpenedEventHandler ClipOpened;

        public PlayerWindow(INativeWindow nativeWindow, ITheme theme = null)
        {
            NativeWindow = nativeWindow;
            Theme = theme != null ? theme : new DefaultTheme();
            lastInteraction = DateTime.Now;
            RecentFiles = new RecentFiles(this);
        }

        public void OnLoad()
        {
            NativeWindow.EnableMenuItem("clip", false);
            NativeWindow.SetLabelContent("timeCodeLabel", "", null, true);
            NativeWindow.SetLabelContent("durationLabel", "", null, true);
            NativeWindow.SetLabelContent("fastForwardLabel", "");
            NativeWindow.SetLabelContent("fastRewindLabel", "");
            NativeWindow.SetButtonEnabled("playButton", false);
            NativeWindow.SetButtonEnabled("pauseButton", false);
            NativeWindow.SetButtonEnabled("fastForwardButton", false);
            NativeWindow.SetButtonEnabled("fastRewindButton", false);
            NativeWindow.SetButtonEnabled("nextButton", false);
            NativeWindow.SetButtonEnabled("previousButton", false);
            NativeWindow.SetSliderEnabled("seekBar", false);

            // Setup recent files menu items
            if (RecentFiles.Entries.Count > 0)
            {
                uint recentFileIndex = 0;
                foreach (var recentFile in RecentFiles.Entries)
                {
                    Action openClip = () =>
                    {
                        if (recentFile.Type == typeof(ClipCinemaDNG).ToString())
                            OpenCinemaDNG(recentFile.Path);
                    };
                    NativeWindow.AddMenuItem("openRecent", PlayerApplication.ShortenPath(recentFile.Path), recentFileIndex++, openClip);
                }
                NativeWindow.AddMenuSeperator("openRecent", recentFileIndex);
                NativeWindow.EnableMenuItem("openRecent", true);
            }
            else
                NativeWindow.EnableMenuItem("openRecent", false);

            // Create the animate controls timer
            AnimateOutControlsTimer = new Timer(new TimerCallback(AnimateOutControls), null, TimeSpan.Zero, TimeSpan.FromSeconds(1.0));

            // Check for updates on startup
            if (NetworkInterface.GetIsNetworkAvailable())
                NativeWindow.PlayerApplication.CheckForUpdates(this);
        }

        private void OnThemeChanged()
        {
            if (RenderContext != null)
            {
                RenderContext.BackgroundColor = (Playback != null && Playback.IsOpen()) ? Theme.ClipBackground : Theme.EmptyBackground;
                RenderContext.RequestRender();
            }
        }

        private void AnimateOutControls(object obj)
        {
            if (!NativeWindow.MouseInsidePlaybackControls && NativeWindow.ControlsAnimationState == ControlsAnimationState.In && (DateTime.Now - lastInteraction) > Theme.ControlsAnimationDelay && Playback != null &&
                Playback.State != Core.Playback.State.Empty)
            {
                NativeWindow.InvokeOnUIThread(() =>
                {
                    if (NativeWindow.ControlsAnimationState == ControlsAnimationState.In)
                        NativeWindow.AnimateOutControls();
                });
            }
        }

        public void LeftMouseDown(uint clickCount, List<string> modifiers)
        {
            lastInteraction = DateTime.Now;
            if (NativeWindow.ControlsAnimationState == ControlsAnimationState.Out)
                NativeWindow.AnimateInControls();

            // Double click to go full screen
            if (clickCount == 2)
                NativeWindow.ToggleFullscreen();

            // Catch control-click on OSX
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && Playback != null && Playback.State != Core.Playback.State.Empty &&
                modifiers.Contains("Control") && modifiers.Count == 1 )
            {
                NativeWindow.OpenContextMenu(new List<string>() { "Clip", "Help" });
            }
        }

        public void RightMouseDown(uint clickCount)
        {
            lastInteraction = DateTime.Now;
            if (NativeWindow.ControlsAnimationState == ControlsAnimationState.Out)
                NativeWindow.AnimateInControls();

            // Open clip context menu if we have a clip loaded
            if (Playback != null && Playback.State != Core.Playback.State.Empty)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    NativeWindow.OpenContextMenu("PlayerContextMenu");
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    NativeWindow.OpenContextMenu(new List<string>() { "Clip", "Help" });
            }
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

        public bool PreviewKeyDown(string id, List<string> modifiers)
        {
            bool handled = false;
            bool showControls = false;

            switch (id)
            {
                case "F":
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && modifiers.Count == 1 && modifiers.Contains("Command"))
                    {
                        NativeWindow.ToggleFullscreen();
                        handled = true;
                    }
                    break;
                case "O":
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && modifiers.Count == 1 && modifiers.Contains("Control") )
                        MenuItemClick("openCinemaDNG");
                    break;
                case "F11":
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        NativeWindow.ToggleFullscreen();
                        handled = true;
                    }
                    break;
                case "Tab":
                    showControls = true;
                    break;
                case "Space":
                    if ( Playback != null && Playback.State != Core.Playback.State.Empty )
                    {
                        if (Playback.IsPlaying)
                        {
                            Playback.Pause();
                            Playback.Velocity = Core.Playback.PlaybackVelocity.Forward1x;
                        }
                        else
                        {
                            Playback.Velocity = Core.Playback.PlaybackVelocity.Forward1x;
                            Playback.Play();
                        }
                        handled = true;
                        showControls = true;
                    }
                    break;
            }

            if ( showControls )
            {
                lastInteraction = DateTime.Now;
                if (NativeWindow.ControlsAnimationState == ControlsAnimationState.Out)
                    NativeWindow.AnimateInControls();
            }

            return handled;
        }

        public void KeyDown(string id)
        {
            
        }

        public bool CanDropFile(string file)
        {
            if (Directory.Exists(file))
                return true;
            else if (System.IO.File.Exists(file))
                return string.Compare(Path.GetExtension(file), ".dng", true) == 0;
            return false;
        }

        public bool CanDropFiles(string[] files)
        {
            if (files.Length == 1)
                return CanDropFile(files[0]);

            foreach (var file in files)
            {
                if (System.IO.File.Exists(file) && string.Compare(Path.GetExtension(file), ".dng", true) == 0)
                    return true;
            }

            return false;
        }

        public void DropFile(string file)
        {
            if (Directory.Exists(file))
                OpenCinemaDNG(file);
            else if (System.IO.File.Exists(file))
            {
                var parentFolder = Directory.GetParent(file);
                if (parentFolder != null)
                    OpenCinemaDNG(parentFolder.FullName);
            }
        }

        public void DropFiles(string[] files)
        {
            if (files.Length == 1)
            {
                DropFile(files[0]);
                return;
            }

            foreach (var file in files)
            {
                if (System.IO.File.Exists(file) && string.Compare(Path.GetExtension(file), ".dng", true) == 0)
                {
                    var parentFolder = Directory.GetParent(file);
                    if (parentFolder == null)
                        continue;
                    OpenCinemaDNG(parentFolder.FullName);
                    return;
                }
            }
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
                var clipExposure = (float)Math.Round(Playback.Clip.Metadata.ExposureValue);
                var exposurePresets = new Dictionary<string, float?>()
                {
                    {"exposureAsShot", null },
                    {"exposureMinusTwo", clipExposure - 2.0f },
                    {"exposureMinusOne", clipExposure - 1.0f },
                    {"exposureZero", clipExposure },
                    {"exposurePlusOne", clipExposure + 1.0f },
                    {"exposurePlusTwo", clipExposure + 2.0f }
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

        private void MenuHighlightRollOffClick(string id)
        {
            if (Playback == null || Playback.Clip == null || !Playback.Clip.RawParameters.HasValue)
                return;

            var rawParameters = Playback.Clip.RawParameters.Value;
            switch (id)
            {
                case "highlightRollOffNone":
                    rawParameters.highlightRollOff = HighlightRollOff.Off;
                    break;
                case "highlightRollOffLow":
                    rawParameters.highlightRollOff = HighlightRollOff.Low;
                    break;
                case "highlightRollOffHigh":
                    rawParameters.highlightRollOff = HighlightRollOff.Medium;
                    break;
                default:
                    Debug.Assert(false, "Unhandled menu item: " + id);
                    return;
            }
            NativeWindow.CheckMenuItem(id, true);
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
                case "toneMapping":
                case "gamutCompression":
                    MenuAdvancedRawParameterClick(id);
                    break;

                // Highlight Roll Off
                case "highlightRollOffNone":
                case "highlightRollOffLow":
                case "highlightRollOffHigh":
                    if (!NativeWindow.MenuItemIsChecked(id))
                        MenuHighlightRollOffClick(id);
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
                case "license":
                    var licenseText = Resource.LoadAsciiResource("License.txt");
                    NativeWindow.Alert(AlertType.Blank, licenseText, "License");
                    break;
                case "openglInfo":
                    string renderApiInfo = "Version: " + RenderContext.ApiVersion;
                    renderApiInfo += "\nRenderer: " + RenderContext.ApiRenderer;
                    renderApiInfo += "\nVendor: " + RenderContext.ApiVendor;
                    switch (RenderContext.Api)
                    {
                        case GPU.Render.Api.OpenGL:
                            renderApiInfo += "\nGLSL version: " + RenderContext.ApiShadingLanguageVersion;
                            NativeWindow.Alert(AlertType.Information, renderApiInfo, "OpenGL information");
                            break;
                    }
                    break;
                case "viewLog":
                    if (NativeWindow.PlayerApplication.LogPath != null)
                        NativeWindow.OpenTextEditor(NativeWindow.PlayerApplication.LogPath);
                    break;
                case "reportProblem":
                    NativeWindow.OpenUrl("https://github.com/octopuscinema/raw-player/issues");
                    break;
                case "releaseNotes":
                    NativeWindow.OpenUrl("https://github.com/octopuscinema/raw-player/releases/tag/v" + NativeWindow.PlayerApplication.ProductVersion);
                    break;
                case "about":
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                        NativeWindow.OpenAboutPanel();
                    else
                    {
                        var version = new Version(NativeWindow.PlayerApplication.ProductVersion);
                        string versionText = version.Major == 0 ? "Pre-release " + version : "Release " + version;
                        versionText += " (" + NativeWindow.PlayerApplication.ProductBuildVersion + ")";
                        NativeWindow.Alert(AlertType.Blank, versionText + "\n" + NativeWindow.PlayerApplication.ProductLicense + "\n" + NativeWindow.PlayerApplication.ProductCopyright, 
                            "About " + NativeWindow.PlayerApplication.ProductName);
                    }
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

                // View
                case "fullscreen":
                    NativeWindow.ToggleFullscreen();
                    break;

                // Open
                case "openCinemaDNG":
                    var dngPath = NativeWindow.OpenFolderDialogue("Select folder containing CinemaDNG sequence", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
                    if (dngPath != null)
                        OpenCinemaDNG(dngPath);
                    break;

                // Clear recent files
                case "clearRecent":
                    RecentFiles.Clear();
                    NativeWindow.EnableMenuItem("openRecent", false);
                    break;

                // Check for updates
                case "checkForUpdates":
                    NativeWindow.PlayerApplication.CheckForUpdates(this, true);
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
                        if (Playback.State == Core.Playback.State.Stopped || ( Playback.IsPaused && (Playback.FirstFrame==Playback.LastFrame || playhead == 0.0f) ) )
                        {
                            var previousClip = Playback.Clip.PreviousClip;
                            if (previousClip != null)
                            {
                                if (previousClip.GetType() == typeof(ClipCinemaDNG))
                                    OpenClip<Core.Playback.PlaybackCinemaDNG>(previousClip);
                            }
                        }
                        else
                        {
                            bool wasPlaying = Playback.IsPlaying;
                            Playback.Stop();
                            if (wasPlaying)
                                Playback.Play();
                            else
                                SliderSetValue("seekBar", 0.0f);
                        }
                    }
                    break;
                case "nextButton":
                    if (Playback != null && Playback.State != Core.Playback.State.Empty)
                    {
                        var nextClip = Playback.Clip.NextClip;
                        if (nextClip != null)
                        {
                            if (nextClip.GetType() == typeof(ClipCinemaDNG))
                                OpenClip<Core.Playback.PlaybackCinemaDNG>(nextClip);
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

        public void SliderDragStart(string id)
        {
            if (Playback == null || id != "seekBar")
                return;
            Playback.SeekStart();
        }

        public void SliderDragComplete(string id, double value)
        {
            if (Playback == null || id != "seekBar")
                return;

            // Perform final forced seek request
            if (Playback.LastFrame != Playback.FirstFrame)
            {
                var frame = (uint)Math.Round(Playback.FirstFrame + (Playback.LastFrame - Playback.FirstFrame) * value);
                Playback.RequestSeek(frame, true);
            }

            Playback.SeekEnd();
        }

        public void SliderDragDelta(string id, double value)
        {
            if (Playback == null || id != "seekBar")
                return;

            // Dont do anything if clip is only 1 frame long
            if (Playback.LastFrame == Playback.FirstFrame)
                return;

            var frame = (uint)Math.Round(Playback.FirstFrame + (Playback.LastFrame - Playback.FirstFrame) * value);
            Playback.RequestSeek(frame);
        }

        public void SliderSetValue(string id, double value)
        {
            if (Playback == null || id != "seekBar" )
                return;

            // Don't hide controls while seeking
            lastInteraction = DateTime.Now;
            if (NativeWindow.ControlsAnimationState == ControlsAnimationState.Out)
                NativeWindow.AnimateInControls();

            if (Playback.LastFrame == Playback.FirstFrame)
                return;

            Playback.SeekStart();
            var frame = (uint)Math.Round(Playback.FirstFrame + (Playback.LastFrame - Playback.FirstFrame) * value);
            Playback.RequestSeek(frame, true);
            Playback.SeekEnd();
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
                Playback.SeekFrameDisplayed -= OnSeekFrameDisplayed;
                Playback.SeekFrameMissing -= OnSeekFrameMissing;
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
                Playback.SeekFrameDisplayed += OnSeekFrameDisplayed;
                Playback.SeekFrameMissing += OnSeekFrameMissing;
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

        public void OnSeekFrameDisplayed(uint frame, in Core.Maths.TimeCode timeCode)
        {
            UpdateFrameUI(frame, timeCode, Theme.LabelColour, false);
        }

        public void OnSeekFrameMissing(uint frameRequested, in Core.Maths.TimeCode synthesisedTimeCode)
        {
            UpdateFrameUI(frameRequested, synthesisedTimeCode, Theme.MissingFrameColour, false);
        }

        private void UpdateFrameUI(uint frame, in Core.Maths.TimeCode timeCode, in Vector3 timeCodeLabelColour, bool updateSeekBar = true)
        {
            // Update seek bar
            playhead = (Playback.LastFrame == Playback.FirstFrame) ? 1.0f : (float)(frame - Playback.FirstFrame) / (float)(Playback.LastFrame - Playback.FirstFrame);
            if (updateSeekBar)
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
            NativeWindow.SetLabelContent("timeCodeLabel", "", Theme.LabelColour);
            NativeWindow.SetLabelContent("durationLabel", "");
            NativeWindow.EnableMenuItem("clip", false);
            NativeWindow.SetWindowTitle("OCTOPUS RAW Player");
            RenderContext.BackgroundColor = Theme.EmptyBackground;
            RenderContext.RedrawBackground = GPU.Render.RedrawBackground.Once;
            NativeWindow.DropAreaVisible = true;
            if (NativeWindow.AspectLocked)
                NativeWindow.UnlockAspect();
        }

        public void OnClipOpened(object sender, EventArgs e)
        {
            playhead = 0.0f;
            NativeWindow.SetButtonEnabled("playButton", true);
            NativeWindow.SetButtonEnabled("pauseButton", true);
            NativeWindow.SetButtonEnabled("fastRewindButton", true);
            NativeWindow.SetButtonEnabled("fastForwardButton", true);
            NativeWindow.SetButtonEnabled("previousButton", true);
            NativeWindow.SetSliderEnabled("seekBar", true);
            NativeWindow.SetSliderValue("seekBar", playhead);
            NativeWindow.EnableMenuItem("clip", true);
            NativeWindow.CheckMenuItem("exposureAsShot");
            NativeWindow.CheckMenuItem("toneMapping", true, false);
            NativeWindow.DropAreaVisible = false;

            bool isColour = false;
            Debug.Assert(Playback != null && Playback.Clip != null);
            if (Playback != null && Playback.Clip != null && Playback.Clip.Metadata != null)
            {
                // There is a next clip
                if ( Playback.Clip.NextClip != null )
                    NativeWindow.SetButtonEnabled("nextButton", true);

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

                // Show as shot exposure text for menu
                NativeWindow.SetWindowTitle(Playback.Clip.Metadata.Title);
                NativeWindow.SetMenuItemTitle("exposureAsShot", "As Shot (" + Playback.Clip.Metadata.ExposureValue.ToString("F") + ")");

                // Set exposure text for manual adjustment
                NativeWindow.SetMenuItemTitle("exposureMinusTwo", ((int)Math.Round(Playback.Clip.Metadata.ExposureValue) -2 ).ToString("+#;-#;0"));
                NativeWindow.SetMenuItemTitle("exposureMinusOne", ((int)Math.Round(Playback.Clip.Metadata.ExposureValue) - 1).ToString("+#;-#;0"));
                NativeWindow.SetMenuItemTitle("exposureZero", ((int)Math.Round(Playback.Clip.Metadata.ExposureValue)).ToString("+#;-#;0"));
                NativeWindow.SetMenuItemTitle("exposurePlusOne", ((int)Math.Round(Playback.Clip.Metadata.ExposureValue) + 1).ToString("+#;-#;0"));
                NativeWindow.SetMenuItemTitle("exposurePlusTwo", ((int)Math.Round(Playback.Clip.Metadata.ExposureValue) + 2).ToString("+#;-#;0"));

                if (Playback.Clip.Metadata.ColorProfile.HasValue)
                {
                    isColour = true;
                    if (Playback.Clip.Metadata.ColorProfile.Value.asShotWhiteXY.HasValue)
                    {
                        var asShotWhiteBalance = Playback.Clip.Metadata.ColorProfile.Value.AsShotWhiteBalance();
                        if (asShotWhiteBalance.Item2 == 0.0)
                            NativeWindow.SetMenuItemTitle("whiteBalanceAsShot", "As Shot (" + asShotWhiteBalance.Item1.ToString("0") + "K)");
                        else
                            NativeWindow.SetMenuItemTitle("whiteBalanceAsShot", "As Shot (" + asShotWhiteBalance.Item1.ToString("0") + "K, Tint: " +
                                asShotWhiteBalance.Item2.ToString("+#;-#;0") + ")");
                        NativeWindow.CheckMenuItem("whiteBalanceAsShot");
                        NativeWindow.EnableMenuItem("whiteBalanceAsShot", true);
                    }
                    else
                    {
                        NativeWindow.SetMenuItemTitle("whiteBalanceAsShot", "As Shot (Unknown)");
                        NativeWindow.EnableMenuItem("whiteBalanceAsShot", false);
                        MenuWhiteBalanceClick("whiteBalanceDaylight");
                    }
                }
                NativeWindow.LockAspect(Playback.Clip.Metadata.AspectRatio);
            }

            NativeWindow.EnableMenuItem("whiteBalance", isColour);
            NativeWindow.EnableMenuItem("colorSpace", isColour);
            NativeWindow.EnableMenuItem("debayerQuality", isColour);
            NativeWindow.EnableMenuItem("highlightRecovery", isColour);
            NativeWindow.EnableMenuItem("gamutCompression", isColour);
            NativeWindow.EnableMenuItem("highlightRollOff", isColour);
            NativeWindow.CheckMenuItem("highlightRecovery", isColour, false);
            NativeWindow.CheckMenuItem("highlightRollOffLow", isColour);
            NativeWindow.CheckMenuItem("gamutCompression", isColour, false);

            RenderContext.BackgroundColor = Theme.ClipBackground;
            RenderContext.RedrawBackground = GPU.Render.RedrawBackground.Once;

            ClipOpened?.Invoke(Playback.Clip);
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
            RecentFiles.Dispose();
            RecentFiles = null;

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

