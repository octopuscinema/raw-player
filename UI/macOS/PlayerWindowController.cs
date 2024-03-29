
using System;
using Foundation;

namespace Octopus.Player.UI.macOS
{
	public partial class PlayerWindowController : AppKit.NSWindowController
	{
		public NativePlayerWindow PlayerWindow { get { return (NativePlayerWindow)Window; } }

		public PlayerWindowController (IntPtr handle) : base(handle)
		{
		}

		// Called when created directly from a XIB file
		[Export("initWithCoder:")]
		public PlayerWindowController (NSCoder coder) : base(coder)
		{
		}

		// Call to load from the XIB/NIB file
		public PlayerWindowController () : base("NativePlayerWindow")
		{
		}
	}
}

