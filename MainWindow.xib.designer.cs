// ------------------------------------------------------------------------------
//  <autogenerated>
//      This code was generated by a tool.
//      Mono Runtime Version: 4.0.30319.1
// 
//      Changes to this file may cause incorrect behavior and will be lost if 
//      the code is regenerated.
//  </autogenerated>
// ------------------------------------------------------------------------------

namespace Octopus {
	
	
	// Should subclass MonoMac.AppKit.NSWindow
	[Foundation.Register("MainWindow")]
	public partial class MainWindow {
	}
	
	// Should subclass MonoMac.AppKit.NSWindowController
	[Foundation.Register("MainWindowController")]
	public partial class MainWindowController {
	}
	
	// Should subclass MonoMac.AppKit.NSView
	[Foundation.Register("MyView")]
	public partial class MyView {
		
		#pragma warning disable 0169
		[Foundation.Export("toggle:")]
		partial void toggle (AppKit.NSButton sender);
}
}
