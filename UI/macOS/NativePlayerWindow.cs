
using System;
using Foundation;
using AppKit;
using System.Diagnostics;
using OpenTK.Mathematics;
using CoreAnimation;
using System.Collections.Generic;
using System.Linq;
using CoreText;

namespace Octopus.Player.UI.macOS
{
	public partial class NativePlayerWindow : NSWindow, INativeWindow
	{
		public PlayerWindow PlayerWindow { get; private set; }

        public Vector2i FramebufferSize { get { return new Vector2i((int)ContentView.Frame.Width, (int)ContentView.Frame.Height); } }

        public ControlsAnimationState ControlsAnimationState { get; private set; }

		public bool AspectLocked { get; private set; }

		private ITheme Theme { get { return PlayerWindow.Theme; } }

        public bool MouseInsidePlaybackControls { get; private set; }

		private Dictionary<NSTextField, NSFont> labelFonts = new Dictionary<NSTextField, NSFont>();

		private PlaybackControlsView PlaybackControls { get; set; }

		private bool IsDarkMode { get { return EffectiveAppearance.Name.Contains("dark", StringComparison.CurrentCultureIgnoreCase); } }

        public Player.UI.PlayerApplication PlayerApplication { get { return ((AppDelegate)NSApplication.SharedApplication.Delegate).PlayerApplication; } }

        private IDisposable appearanceObserver;

		private NSMenu contextMenu;

        // Called when created from unmanaged code
        public NativePlayerWindow (IntPtr handle) : base(handle)
		{
			OnCreate();
        }

        // Called when created directly from a XIB file
        [Export("initWithCoder:")]
		public NativePlayerWindow(NSCoder coder) : base(coder)
		{
			OnCreate();	
        }

		private void OnCreate()
        {
            ControlsAnimationState = ControlsAnimationState.In;

			// Create platform independant window logic
            PlayerWindow = new PlayerWindow(this, (IsDarkMode) ? (ITheme)new DefaultThemeDark() : null);
            PlayerWindow.OnLoad();
            WillClose += OnClose;

            // Subscribe to playback control view mouse enter/exit
            PlaybackControls = (PlaybackControlsView)FindView(ContentView, "playbackControls");
            PlaybackControls.MouseEnter += OnPlaybackControlsMouseEnter;
            PlaybackControls.MouseExit += OnPlaybackControlsMouseExit;

            // Catch dark mode
            appearanceObserver = AddObserver("effectiveAppearance", Foundation.NSKeyValueObservingOptions.New, OnAppearanceChanged);
        }

		private void OnAppearanceChanged(Foundation.NSObservedChange obj)
        {
			PlayerWindow.Theme = IsDarkMode ? (ITheme)new DefaultThemeDark() : (ITheme)new DefaultTheme();
        }

        private void OnPlaybackControlsMouseExit(object sender, EventArgs e)
        {
            MouseInsidePlaybackControls = false;
        }

        private void OnPlaybackControlsMouseEnter(object sender, EventArgs e)
        {
            MouseInsidePlaybackControls = true;
        }

        private void OnClose(object sender, EventArgs e)
        {
			// Delete additionally created label fonts
			foreach (var entry in labelFonts)
			{
                entry.Key.Font.Dispose();
				entry.Key.Font = entry.Value;
			}
			labelFonts.Clear();

            Debug.Assert(PlayerWindow != null);
			PlayerWindow.Dispose();
			PlayerWindow = null;

			appearanceObserver.Dispose();
			appearanceObserver = null;

			if (contextMenu != null)
            {
				contextMenu.Dispose();
				contextMenu = null;
            }
        }

		public void LockAspect(Core.Maths.Rational ratio)
        {
			AspectLocked = true;

			// Change size immediately and set content aspect ratio
			var playerView = FindView(ContentView, "playerView");
			var contentSize = playerView.Frame.Size;
			var aspectCorrectContentSize = new Vector2d(contentSize.Height * ratio.ToDouble(), contentSize.Height);
			SetContentSize(new CoreGraphics.CGSize(aspectCorrectContentSize.X, aspectCorrectContentSize.Y));
			ContentAspectRatio = new CoreGraphics.CGSize(ratio.Numerator, ratio.Denominator);

			// Apply content min size
			var playbackControls = FindView(ContentView, "playbackControls");
			var minContentSize = new Vector2d(playbackControls.Frame.Width, playbackControls.Frame.Height) + (new Vector2d(Theme.PlaybackControlsMargin, Theme.PlaybackControlsMargin) * 2);
			minContentSize.X = Math.Max(minContentSize.X, minContentSize.Y * ratio.ToDouble());
			minContentSize.Y = Math.Max(minContentSize.Y, minContentSize.X / ratio.ToDouble());
			ContentMinSize = new CoreGraphics.CGSize(minContentSize.X, minContentSize.Y);
		}

		public void UnlockAspect()
		{
			ResizeIncrements = new CoreGraphics.CGSize(1.0f, 1.0f);
			AspectLocked = false;
		}
		
		public void SetWindowTitle(string text)
        {
			Title = text;
        }

        public void ToggleFullscreen()
        {
			base.ToggleFullScreen(null);
        }

#nullable enable
		public string? OpenFolderDialogue(string title, string defaultDirectory)
#nullable disable
		{
			var dialog = NSOpenPanel.OpenPanel;
			dialog.CanChooseFiles = false;
			dialog.CanChooseDirectories = true;
			dialog.CanCreateDirectories = false;
			dialog.Message = title;

			if (dialog.RunModal() == 1)
			{
				Debug.Assert(dialog.Urls.Length > 0);
				if (dialog.Urls.Length == 0)
					return null;

				var url = dialog.Urls[0];
				if (url != null)
					return url.Path;
			}

			return null;
		}

        public void Exit()
        {
			NSApplication.SharedApplication.Terminate(this);
        }

        public AlertResponse Alert(AlertType alertType, string message, string title)
        {
			NSAlert alert = null;
			switch (alertType)
            {
				case AlertType.Blank:
				case AlertType.Information:
					alert = new NSAlert() { AlertStyle = NSAlertStyle.Informational, InformativeText = message, MessageText = title };
					break;
				case AlertType.Error:
					alert = new NSAlert() { AlertStyle = NSAlertStyle.Critical, InformativeText = message, MessageText = title };
					break;
				case AlertType.Warning:
					alert = new NSAlert() { AlertStyle = NSAlertStyle.Warning, InformativeText = message, MessageText = title };
					break;
				case AlertType.YesNo:
					alert = NSAlert.WithMessage(title, "Yes", "No", null, message);
                    break;
				default:
					throw new Exception();
			}
			
			switch(alert?.RunModal())
            {
				case 1:
					return AlertResponse.Yes;
				case 0:
					return AlertResponse.No;
				default:
					return AlertResponse.None;
            }
		}


        public void OpenContextMenu(string id)
        {
            throw new NotSupportedException();
        }

        public void OpenContextMenu(List<string> mainMenuItems)
        {
            if (contextMenu != null)
                contextMenu.Dispose();

            contextMenu = new NSMenu();
            foreach (var mainMenuItem in mainMenuItems)
                contextMenu.AddItem((NSMenuItem)NSApplication.SharedApplication.MainMenu.ItemWithTitle(mainMenuItem).Copy());
            contextMenu.PopUpMenu(null, NSEvent.CurrentMouseLocation, null);
        }

        public void OpenAboutPanel()
        {
			var systemFont = NSFont.SystemFontOfSize(NSFont.SmallSystemFontSize);
			var creditsAttributes = new CTStringAttributes()
			{
				ForegroundColorFromContext = true,
				Font = new CTFont(systemFont.FontName, systemFont.PointSize)
			};

            var options = new Dictionary<string, object>()
            {
				{ "ApplicationName", PlayerApplication.ProductName },
				{ "Version", PlayerApplication.ProductBuildVersion },
				{ "ApplicationVersion", PlayerApplication.ProductVersion },
				{ "Copyright", PlayerApplication.ProductCopyright },
				{ "Credits", new NSAttributedString(PlayerApplication.ProductLicense, creditsAttributes) }
            };

            NSApplication.SharedApplication.OrderFrontStandardAboutPanelWithOptions2(NSDictionary.FromObjectsAndKeys(options.Values.ToArray(), options.Keys.ToArray()));
        }

        public void OpenUrl(string url)
        {
			NSError urlError;
			NSWorkspace.SharedWorkspace.OpenURL(new NSUrl(url), NSWorkspaceLaunchOptions.Default, new NSDictionary(), out urlError);
		}

        public void OpenTextEditor(string textFilePath)
        {
			NSWorkspace.SharedWorkspace.OpenFile(textFilePath);
        }

        private NSMenuItem FindMenuItem(NSMenu root, string id)
        {
			if (root == null)
				return null;

			foreach(var item in root.Items)
            {
				if (item.Identifier == id)
					return item;
				var found = FindMenuItem(item.Submenu, id);
				if (found != null)
					return found;
            }

			return null;
        }

        public void EnableMenuItem(string id, bool enable)
        {
			var item = FindMenuItem(NSApplication.SharedApplication.MainMenu, id);
			if (item != null)
				item.Enabled = enable;
        }

        public void CheckMenuItem(string id, bool check = true, bool uncheckSiblings = true)
        {
			var item = FindMenuItem(NSApplication.SharedApplication.MainMenu, id);
			if (item != null)
			{
				item.State = check ? NSCellStateValue.On : NSCellStateValue.Off;
				if (uncheckSiblings && item.ParentItem != null && item.ParentItem.Submenu != null)
				{
					foreach(var sibling in item.ParentItem.Submenu.Items)
                    {
						if (sibling != item)
							sibling.State = NSCellStateValue.Off;
                    }
				}
			}
		}

		public bool MenuItemIsChecked(string id)
		{
			var item = FindMenuItem(NSApplication.SharedApplication.MainMenu, id);
			if (item != null)
				return item.State == NSCellStateValue.On;
			return false;
		}

		public void ToggleMenuItemChecked(string id)
        {
			bool check = !MenuItemIsChecked(id);
			CheckMenuItem(id, check, false);
		}

		public void SetMenuItemTitle(string id, string name)
        {
			var item = FindMenuItem(NSApplication.SharedApplication.MainMenu, id);
			if (item != null)
				item.Title = name;
		}

        public void AddMenuItem(string parentId, string name, uint? index, Action onClick)
        {
			var parentMenu = FindMenuItem(NSApplication.SharedApplication.MainMenu, parentId).Submenu;
			var menuItem = new NSMenuItem(name);
			menuItem.Activated += (sender, e) => { onClick(); };

			if (index.HasValue)
                parentMenu.InsertItem(menuItem, (nint)index.Value);
			else
                parentMenu.AddItem(menuItem);
        }

        public void AddMenuSeperator(string parentId, uint? index)
		{
            var parentMenu = FindMenuItem(NSApplication.SharedApplication.MainMenu, parentId).Submenu;
            if (index.HasValue)
                parentMenu.InsertItem(NSMenuItem.SeparatorItem, (nint)index.Value);
            else
                parentMenu.AddItem(NSMenuItem.SeparatorItem);
        }

        internal NSView FindView(NSView root, string id)
		{
			if (root == null)
				return null;

			foreach (var item in root.Subviews)
			{
				if (item.Identifier == id)
					return item;
				var found = FindView(item, id);
				if (found != null)
					return found;
			}

			return null;
		}

		private T FindControl<T>(NSView root, string id) where T : NSView
		{
			return (T)FindView(root, id);
		}

		public void SetLabelContent(string id, string content, Vector3? colour = null, bool? fixedWidthDigitHint = null)
		{
			InvokeOnMainThread(() =>
			{
				var label = FindControl<NSTextField>(ContentView, id);
				if (label != null)
				{
					label.StringValue = content;
					if (colour.HasValue)
						label.TextColor = NSColor.FromRgba(colour.Value.X, colour.Value.Y, colour.Value.Z, label.TextColor.AlphaComponent);
					
					// Switch to fixed width digit font
					if (fixedWidthDigitHint.HasValue)
					{
						bool useFixedWidthDigit = fixedWidthDigitHint.Value;

                        if ( !labelFonts.ContainsKey(label) && useFixedWidthDigit )
                        {
							labelFonts[label] = label.Font;
                            label.Font = NSFont.MonospacedDigitSystemFontOfSize(labelFonts[label].PointSize, NSFontWeight.Regular);
                        }
						else if ( labelFonts.ContainsKey(label) && !useFixedWidthDigit )
                        {
							label.Font.Dispose();
							label.Font = labelFonts[label];
                            labelFonts.Remove(label);
                        }
					}
                }
            });
		}

		public void SetButtonVisibility(string id, bool visible)
        {
			InvokeOnMainThread(() =>
			{
				var button = FindControl<NSButton>(ContentView, id);
				if (button != null)
					button.Hidden = !visible;
			});
		}

		public void SetButtonEnabled(string id, bool enabled)
		{
			InvokeOnMainThread(() =>
			{
				var button = FindControl<NSButton>(ContentView, id);
				if (button != null)
				{
					button.Enabled = enabled;
					button.AlphaValue = enabled ? Theme.DefaultOpacity : Theme.DisabledOpacity;
				}
			});
		}

		public void SetSliderValue(string id, float value)
        {
			InvokeOnMainThread(() =>
			{
				var slider = FindControl<NSSlider>(ContentView, id);
				if (slider != null)
					slider.FloatValue = value;
			});
        }

		public void SetSliderEnabled(string id, bool enabled)
		{
			InvokeOnMainThread(() =>
			{
				var slider = FindControl<NSSlider>(ContentView, id);
				if (slider != null)
				{
					slider.Enabled = enabled;
					slider.AlphaValue = enabled ? Theme.DefaultOpacity : Theme.DisabledOpacity;
				}
			});
		}

		public void InvokeOnUIThread(Action action, bool async = true)
		{
			if (async)
				BeginInvokeOnMainThread(action);
			else
				InvokeOnMainThread(action);
		}

        public void AnimateInControls()
        {
			Debug.Assert(ControlsAnimationState == ControlsAnimationState.Out);
			ControlsAnimationState = ControlsAnimationState.In;
			NSAnimationContext.RunAnimation(ctx =>
			{
				ctx.Duration = Theme.ControlsAnimation.TotalSeconds;
				ctx.TimingFunction = CAMediaTimingFunction.FromName(CAMediaTimingFunction.Linear);
				((NSView)PlaybackControls.Animator).AlphaValue = 1.0f;
			});
		}

        public void AnimateOutControls()
        {
			if ( WindowNumberAtPoint(NSEvent.CurrentMouseLocation, 0) == WindowNumber )
				NSCursor.SetHiddenUntilMouseMoves(true);
			Debug.Assert(ControlsAnimationState == ControlsAnimationState.In);
			ControlsAnimationState = ControlsAnimationState.Out;
			NSAnimationContext.RunAnimation(ctx =>
			{
				ctx.Duration = Theme.ControlsAnimation.TotalSeconds;
				ctx.TimingFunction = CAMediaTimingFunction.FromName(CAMediaTimingFunction.Linear);
				((NSView)PlaybackControls.Animator).AlphaValue = 0.0f;
			});
		}
    }
}

