
using System;
using Foundation;
using AppKit;
using System.Diagnostics;
using OpenTK.Mathematics;

namespace Octopus.Player.UI.macOS
{
	public partial class NativePlayerWindow : AppKit.NSWindow, INativeWindow
	{
		public PlayerWindow PlayerWindow { get; private set; }

        public Vector2i FramebufferSize { get { return new Vector2i((int)ContentView.Frame.Width, (int)ContentView.Frame.Height); } }

        // Called when created from unmanaged code
        public NativePlayerWindow (IntPtr handle) : base(handle)
		{
			// Create platform independant window logic
			PlayerWindow = new PlayerWindow(this);
		}

		// Called when created directly from a XIB file
		[Export("initWithCoder:")]
		public NativePlayerWindow (NSCoder coder) : base(coder)
		{
			// Create platform independant window logic
			PlayerWindow = new PlayerWindow(this);
		}

		public void LockAspect(Core.Maths.Rational ratio)
        {
			ContentAspectRatio = new CoreGraphics.CGSize(ratio.Numerator, ratio.Denominator);
		}

		public void UnlockAspect()
		{
			ResizeIncrements = new CoreGraphics.CGSize(1.0f, 1.0f);
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

        public void Alert(AlertType alertType, string message, string title)
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
				default:
					Debug.Assert(false);
					return;
			}
			alert?.RunModal();
		}

        public void OpenUrl(string url)
        {
			NSError urlError;
			NSWorkspace.SharedWorkspace.OpenURL(new NSUrl(url), NSWorkspaceLaunchOptions.Default, new NSDictionary(), out urlError);
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

		public void SetLabelContent(string id, string content, Vector3? colour = null)
		{
			InvokeOnMainThread(() =>
			{
				var label = FindControl<NSTextField>(ContentView, id);
				if (label != null)
				{
					label.StringValue = content;
					if (colour.HasValue)
						label.TextColor = NSColor.FromRgb(colour.Value.X, colour.Value.Y, colour.Value.Z);
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

        public void SetSliderValue(string id, float value)
        {
			InvokeOnMainThread(() =>
			{
				var slider = FindControl<NSSlider>(ContentView, id);
				if (slider != null)
					slider.FloatValue = value;
			});
        }

		public void InvokeOnUIThread(Action action, bool async = true)
		{
			if (async)
				BeginInvokeOnMainThread(action);
			else
				InvokeOnMainThread(action);
		}
	}
}

