using System;
using CoreGraphics;
using Foundation;
using AppKit;
using ObjCRuntime;

namespace Octopus.Player.UI.macOS
{
	class App
	{
		static void Main (string[] args)
		{
			NSApplication.Init ();
			NSApplication.Main (args);
		}
	}
}

