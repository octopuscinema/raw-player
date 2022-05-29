
using System;
using Foundation;
using AppKit;

namespace Octopus.Player.UI.macOS
{
	public partial class PlayerWindow : AppKit.NSWindow
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
	}
}

