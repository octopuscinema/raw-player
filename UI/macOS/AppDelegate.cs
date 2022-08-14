using System;
using CoreGraphics;
using Foundation;
using AppKit;
using ObjCRuntime;
using System.Collections.Generic;

namespace Octopus.Player.UI.macOS
{
    public partial class AppDelegate : NSApplicationDelegate
    {
        PlayerWindowController PlayerWindowController { get; set; }
        public PlayerApplication PlayerApplication { get; private set; }

        public AppDelegate()
        {
            PlayerApplication = new PlayerApplication();
        }

        public override void DidFinishLaunching(NSNotification notification)
        {
            PlayerWindowController = new PlayerWindowController();
            PlayerWindowController.Window.MakeKeyAndOrderFront(this);

            Dictionary<ushort, string> keyNames = new Dictionary<ushort, string>() {
                { 49, "Space" }
            };

            NSEvent.AddLocalMonitorForEventsMatchingMask(NSEventMask.KeyDown, (NSEvent theEvent) =>
            {
                if (keyNames.ContainsKey(theEvent.KeyCode))
                    return PlayerWindowController.PlayerWindow.PlayerWindow.PreviewKeyDown(keyNames[theEvent.KeyCode]) ? null : theEvent;
                return theEvent;
            });
        }

        public override bool ApplicationShouldTerminateAfterLastWindowClosed(NSApplication sender)
        {
            return true;
        }

        public override void WillTerminate(NSNotification notification)
        {
            PlayerApplication.Dispose();
            PlayerApplication = null;
        }

        [Export("doClick:")]
        void MenuItemClick(NSObject sender)
        {
            var menuItem = (NSMenuItem)sender;
            if (menuItem != null)
                PlayerWindowController.PlayerWindow.PlayerWindow.MenuItemClick(menuItem.Identifier);
        }

        static void Main(string[] args)
        {
            NSApplication.Init();
            NSApplication.Main(args);
        }
    }
}
