using System;
using CoreGraphics;
using Foundation;
using AppKit;
using ObjCRuntime;

namespace Octopus.Player.UI.macOS
{
	public partial class AppDelegate : NSApplicationDelegate
	{
		PlayerWindowController PlayerWindowController { get; set; }

		public AppDelegate ()
		{
		}

		public override void DidFinishLaunching (NSNotification notification)
		{
			PlayerWindowController = new PlayerWindowController ();
			PlayerWindowController.Window.MakeKeyAndOrderFront (this);
		}
		
		public override bool ApplicationShouldTerminateAfterLastWindowClosed (NSApplication sender)
		{
			return true;
		}

		[Export("doClick:")]
		void MenuItemClick(NSObject sender)
		{
			var menuItem = (NSMenuItem)sender;
			if (menuItem != null)
				PlayerWindowController.PlayerWindow.WindowLogic.MenuItemClick(menuItem.Identifier);
			/*
			var dlg = NSOpenPanel.OpenPanel;
			dlg.CanChooseFiles = false;
			dlg.CanChooseDirectories = true;

			if (dlg.RunModal() == 1)
			{
				var alert = new NSAlert()
				{
					AlertStyle = NSAlertStyle.Informational,
					InformativeText = "At this point we should do something with the folder that the user just selected in the Open File Dialog box...",
					MessageText = "Folder Selected"
				};
				alert.RunModal();
			}
			*/
		}

		static void Main(string[] args)
		{
			NSApplication.Init();
			NSApplication.Main(args);
		}
	}
}

