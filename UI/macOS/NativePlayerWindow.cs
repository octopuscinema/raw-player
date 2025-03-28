
using System;
using Foundation;
using AppKit;
using System.Diagnostics;
using OpenTK.Mathematics;
using CoreAnimation;
using System.Collections.Generic;
using System.Linq;
using CoreText;
using System.Security.Policy;
using System.IO;
using AVFoundation;
using UniformTypeIdentifiers;
using Octopus.Player.GPU;

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

		public HashSet<string> ActiveSliders { get; set; } = new HashSet<string>();

        private Dictionary<NSTextField, NSFont> labelFonts = new Dictionary<NSTextField, NSFont>();

		private PlaybackControlsView PlaybackControls { get; set; }

		private bool IsDarkMode { get { return EffectiveAppearance.Name.Contains("dark", StringComparison.CurrentCultureIgnoreCase); } }

        public Player.UI.PlayerApplication PlayerApplication { get { return ((AppDelegate)NSApplication.SharedApplication.Delegate).PlayerApplication; } }

		public bool DropAreaVisible
		{
			get { return !FindView(ContentView, "dropArea").Hidden; }
			set { FindView(ContentView, "dropArea").Hidden = !value; }
		}

		public bool FeedVisible
		{
            get { return feedVisible; }
            set
			{
				feedVisible = value;
                FindView(ContentView, "feed").Hidden = !feedVisible;
			}
        }
		
		public bool RenderContinuouslyHint
		{
			set { }
		}

        public Audio.IContext AudioContext { get; private set; }

		private bool feedVisible;

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
            feedVisible = !FindView(ContentView, "feed").Hidden;
            ControlsAnimationState = ControlsAnimationState.In;

			// Create audio context
			AudioContext = new Audio.macOS.Context();

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

#nullable enable
        public string? OpenFileDialogue(string title, string defaultDirectory, IReadOnlyCollection<Tuple<string, string>> extensionsDescriptions)
#nullable disable
        {
            var dialog = NSOpenPanel.OpenPanel;
            dialog.CanChooseFiles = true;
            dialog.CanChooseDirectories = false;
            dialog.CanCreateDirectories = false;
            dialog.Message = title;
			dialog.AllowsMultipleSelection = false;

            var fileTypes = extensionsDescriptions.Select(fileType => UTType.CreateFromExtension(fileType.Item1.Trim('*').Trim('.').ToLower()))
                    .Where(utType => utType != null)
                    .ToArray()!;

            dialog.AllowedContentTypes = fileTypes;
            
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

#nullable enable
        public string? SaveFileDialogue(string title, string defaultDirectory, IReadOnlyCollection<Tuple<string, string>> extensionsDescriptions)
#nullable disable
        {
            var dialog = NSSavePanel.SavePanel;
			dialog.ExtensionHidden = false;
            dialog.CanCreateDirectories = false;
            dialog.Message = title;
			
            var fileTypes = extensionsDescriptions.Select(fileType => UTType.CreateFromExtension(fileType.Item1.Trim('*').Trim('.').ToLower()))
                    .Where(utType => utType != null)
                    .ToArray()!;

            dialog.AllowedContentTypes = fileTypes;

			if (dialog.RunModal() == 1 && dialog.Url != null)
				return dialog.Url.Path;

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

		public Core.Error SavePng(string path, byte[] data, in Vector2i dimensions, GPU.Format format, GPU.Orientation orientation, bool ignoreAlpha = true)
		{
			CoreGraphics.CGBitmapFlags bitmapFlags;
            switch (format)
			{
				case Format.RGBX8:
				case Format.RGBX16:
					bitmapFlags = CoreGraphics.CGBitmapFlags.NoneSkipLast;
                    break;
				case Format.BGRA8:
					bitmapFlags = CoreGraphics.CGBitmapFlags.ByteOrder32Little | (ignoreAlpha ? CoreGraphics.CGBitmapFlags.NoneSkipFirst : CoreGraphics.CGBitmapFlags.First);
                    break;
				case Format.RGBA16:
				case Format.RGBA8:
                    bitmapFlags = ignoreAlpha ? CoreGraphics.CGBitmapFlags.NoneSkipLast : CoreGraphics.CGBitmapFlags.Last;
					break;
                default:
					throw new ArgumentException("Unsupported pixel format for png");
			}

			try
			{
				using var nsUrl = NSUrl.FromFilename(path);
				using var colorSpace = CoreGraphics.CGColorSpace.CreateDeviceRGB();
				using var dataProvider = new CoreGraphics.CGDataProvider(data);
				
				using var imageDestination = ImageIO.CGImageDestination.Create(nsUrl, MobileCoreServices.UTType.PNG, imageCount: 1);
				using var image = new CoreGraphics.CGImage(dimensions.X, dimensions.Y, format.ComponentBitDepth(), format.SizeBits(), dimensions.X * format.SizeBytes(),
					colorSpace, bitmapFlags, dataProvider, null, false, CoreGraphics.CGColorRenderingIntent.Default);

                var options = new ImageIO.CGImageDestinationOptions();

				switch(orientation)
				{
					case Orientation.TopRight:
						options.Dictionary[ImageIO.CGImageProperties.Orientation] = new NSNumber((int)ImageIO.CGImagePropertyOrientation.UpMirrored);
						break;
                    case Orientation.BottomLeft:
                        options.Dictionary[ImageIO.CGImageProperties.Orientation] = new NSNumber((int)ImageIO.CGImagePropertyOrientation.Down);
                        break;
                    case Orientation.BottomRight:
                        options.Dictionary[ImageIO.CGImageProperties.Orientation] = new NSNumber((int)ImageIO.CGImagePropertyOrientation.DownMirrored);
                        break;
                    case Orientation.LeftTop:
                        options.Dictionary[ImageIO.CGImageProperties.Orientation] = new NSNumber((int)ImageIO.CGImagePropertyOrientation.LeftMirrored);
                        break;
                    case Orientation.LeftBottom:
                        options.Dictionary[ImageIO.CGImageProperties.Orientation] = new NSNumber((int)ImageIO.CGImagePropertyOrientation.Left);
                        break;
                    case Orientation.RightTop:
                        options.Dictionary[ImageIO.CGImageProperties.Orientation] = new NSNumber((int)ImageIO.CGImagePropertyOrientation.RightMirrored);
                        break;
                    case Orientation.RightBottom:
                        options.Dictionary[ImageIO.CGImageProperties.Orientation] = new NSNumber((int)ImageIO.CGImagePropertyOrientation.Right);
                        break;
                }

                imageDestination.AddImage(image, options);
				imageDestination.Close();

				return Core.Error.None;
			}
            catch (Exception e)
            {
                Trace.WriteLine("Failed to save png: " + path + "\n" + e.Message);
                return Core.Error.BadPath;
			}
		}

        public void OpenContextMenu(string id)
        {
            throw new NotSupportedException();
        }

#nullable enable
        public void OpenContextMenu(List<string> mainMenuItems, List<(string,string)?>? additionalItems = null)
#nullable disable
        {
            if (contextMenu != null)
                contextMenu.Dispose();

            contextMenu = new NSMenu();
            foreach (var mainMenuItem in mainMenuItems)
                contextMenu.AddItem((NSMenuItem)NSApplication.SharedApplication.MainMenu.ItemWithTitle(mainMenuItem).Copy());

			// Add additional items
			if (additionalItems != null)
			{
				contextMenu.AddItem(NSMenuItem.SeparatorItem);
				foreach (var additionalItem in additionalItems)
				{
					if (!additionalItem.HasValue)
					{
                        contextMenu.AddItem(NSMenuItem.SeparatorItem);
                        continue;
					}

					var item = new NSMenuItem(additionalItem.Value.Item1);
					item.Identifier = additionalItem.Value.Item2;
					item.Enabled = true;
					item.Activated += (sender, e) => { PlayerWindow.MenuItemClick(item.Identifier); };
                    contextMenu.AddItem(item);
                }
			}

			// Open context menu
            contextMenu.PopUpMenu(null, NSEvent.CurrentMouseLocation, null);

            // Done with context menu
			foreach (var item in contextMenu.Items)
				item.Dispose();
			contextMenu.Dispose();
			contextMenu = null;
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
			using (var nsUrl = new NSUrl(url.Replace("\"", "")))
			{
				NSWorkspace.SharedWorkspace.OpenURL(nsUrl, NSWorkspaceLaunchOptions.Default, new NSDictionary(), out urlError);
			}
		}

		public void ShowInNavigator(List<string> paths)
		{
			var urls = new List<NSUrl>();
			foreach (var path in paths)
			{
				if (Directory.Exists(path))
					urls.Add(new NSUrl(path, true));
				else if (File.Exists(path))
                    urls.Add(new NSUrl(path, false));
            }
            NSWorkspace.SharedWorkspace.ActivateFileViewer(urls.ToArray());
			foreach (var url in urls)
				url.Dispose();
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

#nullable enable
        public void AddMenuItem(string parentId, string name, uint? index, Action onClick, string? id = null)
#nullable disable
        {
            var parentMenu = FindMenuItem(NSApplication.SharedApplication.MainMenu, parentId).Submenu;
			var menuItem = new NSMenuItem(name);
			menuItem.Activated += (sender, e) => { onClick(); };
			if (id != null)
				menuItem.Identifier = id;

            if (index.HasValue)
                parentMenu.InsertItem(menuItem, (nint)index.Value);
			else
                parentMenu.AddItem(menuItem);
        }

		public void RemoveMenuItem(string parentId, string id)
		{
			var parent = FindMenuItem(NSApplication.SharedApplication.MainMenu, parentId).Submenu;
			var item = FindMenuItem(NSApplication.SharedApplication.MainMenu, id);

			if (parent != null && item != null)
			{
				parent.RemoveItem(item);
				item.Dispose();
			}
		}

		public bool MenuItemExists(string id)
		{
			return FindMenuItem(NSApplication.SharedApplication.MainMenu, id) != null;
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

        public void Notification(string title, string caption, IDictionary<string,string> arguments = null)
        {
            // Trigger a local notification after the time has elapsed
            using var notification = new NSUserNotification();

			// Add text and sound to the notification
			notification.Title = title;
			notification.InformativeText = caption;
            notification.SoundName = NSUserNotification.NSUserNotificationDefaultSoundName;
			notification.HasActionButton = false;

            NSUserNotificationCenter.DefaultUserNotificationCenter.DeliverNotification(notification);
        }
    }
}

