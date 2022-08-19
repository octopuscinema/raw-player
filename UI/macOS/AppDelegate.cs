using System;
using CoreGraphics;
using Foundation;
using AppKit;
using ObjCRuntime;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;

namespace Octopus.Player.UI.macOS
{
    static partial class Extensions
    {
        public static List<string> ToModifierNameList(this NSEventModifierMask modifierFlags)
        {
            Dictionary<NSEventModifierMask, string> modifierNames = new Dictionary<NSEventModifierMask, string>()
            {
                { NSEventModifierMask.ShiftKeyMask, "Shift" },
                { NSEventModifierMask.ControlKeyMask, "Control" },
                { NSEventModifierMask.AlternateKeyMask, "Alt" },
                { NSEventModifierMask.CommandKeyMask, "Command" }
            };

            List<string> modifiers = new List<string>();

            var modifierKeys = Enum.GetValues(typeof(NSEventModifierMask)).Cast<NSEventModifierMask>();
            foreach (var key in modifierKeys)
            {
                if ((modifierFlags & key) != 0 && modifierNames.ContainsKey(key))
                    modifiers.Add(modifierNames[key]);
            }

            return modifiers;
        }
    }

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

            Dictionary<ushort, string> keyNames = new Dictionary<ushort, string>()
            {
                { 49, "Space" },
                { 31, "O" },
                { 3, "F" }
            };

            NSEvent.AddLocalMonitorForEventsMatchingMask(NSEventMask.KeyDown, (NSEvent theEvent) =>
            {
                var modifiers = theEvent.ModifierFlags.ToModifierNameList();
                if (keyNames.ContainsKey(theEvent.KeyCode))
                    return PlayerWindowController.PlayerWindow.PlayerWindow.PreviewKeyDown(keyNames[theEvent.KeyCode], modifiers) ? null : theEvent;
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
