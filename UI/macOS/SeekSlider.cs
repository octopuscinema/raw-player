using System;
using System.Collections.Generic;
using System.Linq;
using Foundation;
using AppKit;
using Octopus.Player.UI.macOS;

namespace Octopus.Player.UI
{
	public partial class SeekSlider : NSSlider
    {
        private NativePlayerWindow NativePlayerWindow { get { return (NativePlayerWindow)Window; } }

        // Called when created from unmanaged code
        public SeekSlider (IntPtr handle) : base (handle)
		{
			Initialize ();
		}

		// Called when created directly from a XIB file
		[Export ("initWithCoder:")]
		public SeekSlider (NSCoder coder) : base (coder)
		{
			Initialize ();
		}

		// Shared initialization code
		void Initialize ()
		{
		}

        public override void MouseUp(NSEvent theEvent)
        {
			// Fix for drag complete not caught in sliderdrag event
            if (NativePlayerWindow.ActiveSliders.Contains(Identifier))
            {
                NativePlayerWindow.ActiveSliders.Remove(Identifier);
                NativePlayerWindow.PlayerWindow.SliderDragComplete(Identifier, FloatValue);
            }
        }
    }
}
