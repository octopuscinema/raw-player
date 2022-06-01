
using System;
using Foundation;
using AppKit;
using System.Diagnostics;

namespace Octopus.Player.UI.macOS
{
	public partial class NativePlayerWindow : AppKit.NSWindow, INativeWindow
	{
		public PlayerWindow WindowLogic { get; private set; }

		// Called when created from unmanaged code
		public NativePlayerWindow (IntPtr handle) : base(handle)
		{
			// Create platform independant window logic
			WindowLogic = new PlayerWindow(this);
		}

		// Called when created directly from a XIB file
		[Export("initWithCoder:")]
		public NativePlayerWindow (NSCoder coder) : base(coder)
		{
			// Create platform independant window logic
			WindowLogic = new PlayerWindow(this);
		}

		// Native window implementations
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

				/*
				var alert = new NSAlert()
				{
					AlertStyle = NSAlertStyle.Informational,
					InformativeText = "Selected: " + url.Path,
					MessageText = "Folder Selected"
				};
				alert.RunModal();
				*/

				var url = dialog.Urls[0];
				if (url != null)
					return url.Path;
			}

			return null;
		}

		public void InformationAlert(string message, string title)
        {
			var alert = new NSAlert()
			{
				AlertStyle = NSAlertStyle.Informational,
				InformativeText = message,
				MessageText = title
			};
			alert.RunModal();
		}

        public void Exit()
        {
			NSApplication.SharedApplication.Terminate(this);
        }
    }
}

