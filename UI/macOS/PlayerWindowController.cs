
using System;
using System.Collections.Generic;
using System.Linq;
using Foundation;
using AppKit;

namespace Octopus.Player.UI.macOS
{
	public partial class PlayerWindowController : AppKit.NSWindowController
	{
		public PlayerWindow PlayerWindow { get { return (PlayerWindow)Window; } }

		public PlayerWindowController (IntPtr handle) : base(handle)
		{
		}

		// Called when created directly from a XIB file
		[Export("initWithCoder:")]
		public PlayerWindowController (NSCoder coder) : base(coder)
		{
		}

		// Call to load from the XIB/NIB file
		public PlayerWindowController () : base("PlayerWindow")
		{
		}
	}
}

