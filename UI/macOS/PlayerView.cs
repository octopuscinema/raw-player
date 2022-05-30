
using System;
using System.Collections.Generic;
using System.Linq;
using CoreGraphics;
using Foundation;
using AppKit;
using CoreAnimation;
using OpenGL;

namespace Octopus.Player.UI.macOS
{
    public partial class PlayerView : AppKit.NSView
    {

        private OpenGLLayer GLLayer { get; set; }

        // Called when created from unmanaged code
        public PlayerView(IntPtr handle) : base(handle)
        {
        }

        // Called when created directly from a XIB file
        [Export("initWithCoder:")]
        public PlayerView(NSCoder coder) : base(coder)
        {
        }

        public override void AwakeFromNib()
        {
            // Create OpenGL layer
            GLLayer = new OpenGLLayer();
            GLLayer.ContentsScale = Window.BackingScaleFactor;
            GLLayer.Frame = Frame;
            GLLayer.Position = new CGPoint(0, 0);
            GLLayer.AnchorPoint = new CGPoint(0, 0);

            // Add OpenGL layer
            Layer = new CALayer();
            Layer.AddSublayer(GLLayer);
            WantsLayer = true;
        }

        public override void ResizeSubviewsWithOldSize(CGSize oldSize)
        {
            base.ResizeSubviewsWithOldSize(oldSize);
            ResizeGLLayer();
        }

        public override void ResizeWithOldSuperviewSize(CGSize oldSize)
        {
            base.ResizeWithOldSuperviewSize(oldSize);
            ResizeGLLayer();
        }

        private void ResizeGLLayer()
        {
            if (GLLayer != null)
            {
                GLLayer.Frame = Frame;
                GLLayer.RemoveAllAnimations();
            }
        }

        public override void MouseDown(NSEvent theEvent)
        {
            if (theEvent.ClickCount == 2)
                ToggleFullscreen();

//            CGPoint location = ConvertPointFromView(theEvent.LocationInWindow, null);
            //movingLayer.Position = new CGPoint(location.X, location.Y);
        }

        public void ToggleFullscreen()
        {
            Window.ToggleFullScreen(null);
        }
        
        public override void DidChangeBackingProperties()
        {
            base.DidChangeBackingProperties();

            if (GLLayer != null)
                GLLayer.ContentsScale = Window.BackingScaleFactor;
        }

        partial void toggle(NSButton sender)
        {
            GLLayer.Animate = !GLLayer.Animate;
        }
    }
}

