using System;
using CoreGraphics;
using Foundation;
using AppKit;
using ObjCRuntime;

namespace Octopus.Player.UI.macOS
{
	public partial class AppDelegate : NSApplicationDelegate
	{
		PlayerWindowController windowController;

		public AppDelegate ()
		{
		}

		public override void DidFinishLaunching (NSNotification notification)
		{
			windowController = new PlayerWindowController ();
			windowController.Window.MakeKeyAndOrderFront (this);
		}
		
		public override bool ApplicationShouldTerminateAfterLastWindowClosed (NSApplication sender)
		{
			return true;
		}
	}
}

