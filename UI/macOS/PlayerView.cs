
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

        private NativePlayerWindow NativePlayerWindow { get { return (NativePlayerWindow)Window; } }

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
            GLLayer = new OpenGLLayer(NativePlayerWindow.PlayerWindow);
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
                GLLayer.RenderContext.RequestRender();
            }
            if (NativePlayerWindow != null)
                NativePlayerWindow.PlayerWindow.OnFramebufferResize(NativePlayerWindow.FramebufferSize);
        }

        public override void MouseDown(NSEvent theEvent)
        {
            if (NativePlayerWindow != null)
                NativePlayerWindow.PlayerWindow.LeftMouseDown((uint)theEvent.ClickCount);
        }

        public override void RightMouseDown(NSEvent theEvent)
        {
            if (NativePlayerWindow != null)
                NativePlayerWindow.PlayerWindow.RightMouseDown((uint)theEvent.ClickCount);
        }

        public override void DidChangeBackingProperties()
        {
            base.DidChangeBackingProperties();

            if (GLLayer != null)
                GLLayer.ContentsScale = Window.BackingScaleFactor;
        }

        partial void ButtonClick(NSButton sender)
        {
            if (NativePlayerWindow != null)
                NativePlayerWindow.PlayerWindow.ButtonClick(sender.Identifier);
        }
    }
}

