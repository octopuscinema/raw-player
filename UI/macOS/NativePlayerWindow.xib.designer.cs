// ------------------------------------------------------------------------------
//  <autogenerated>
//      This code was generated by a tool.
//      Mono Runtime Version: 4.0.30319.1
// 
//      Changes to this file may cause incorrect behavior and will be lost if 
//      the code is regenerated.
//  </autogenerated>
// ------------------------------------------------------------------------------

namespace Octopus.Player.UI.macOS
{
	// Should subclass MonoMac.AppKit.NSWindow
	[Foundation.Register("NativePlayerWindow")]
	public partial class NativePlayerWindow {
	}
	
	// Should subclass MonoMac.AppKit.NSWindowController
	[Foundation.Register("PlayerWindowController")]
	public partial class PlayerWindowController {
	}

	// Should subclass MonoMac.AppKit.NSView
	[Foundation.Register("PlaybackControlsView")]
	public partial class PlaybackControlsView {
	}
	
	// Should subclass MonoMac.AppKit.NSView
	[Foundation.Register("PlayerView")]
	public partial class PlayerView {
		
		#pragma warning disable 0169
		[Foundation.Export("ButtonClick:")]
		partial void ButtonClick (AppKit.NSButton sender);

		#pragma warning disable 0169
        [Foundation.Export("SliderDrag:")]
        partial void SliderDrag(AppKit.NSSlider sender);

    }
}
