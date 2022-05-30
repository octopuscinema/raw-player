
using System;
using Foundation;
using AppKit;

namespace Octopus.Player.UI.macOS
{
	public partial class PlayerWindow : AppKit.NSWindow, INativeWindow
	{
		// Called when created from unmanaged code
		public PlayerWindow (IntPtr handle) : base(handle)
		{
			
		}

		// Called when created directly from a XIB file
		[Export("initWithCoder:")]
		public PlayerWindow (NSCoder coder) : base(coder)
		{
			
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

		public string? OpenFolderDialogue(string title, string defaultDirectory)
        {
			var dlg = NSOpenPanel.OpenPanel;
			dlg.CanChooseFiles = false;
			dlg.CanChooseDirectories = true;
			//dlg.AllowedFileTypes = new string[] { "txt", "html", "md", "css" };

			if (dlg.RunModal() == 1)
			{
				// Nab the first file
				var url = dlg.Urls[0];

				if (url != null)
					return url.Path;
			}
			
			return null;
		}
	}
}

