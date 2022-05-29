
using System;
using System.Collections.Generic;
using System.Linq;
using CoreGraphics;
using Foundation;
using AppKit;
using CoreAnimation;
using OpenGL;

namespace Octopus
{
    public partial class MyView : AppKit.NSView
    {

        static OpenGLLayer movingLayer;

        // Called when created from unmanaged code
        public MyView(IntPtr handle) : base(handle)
        {
        }

        // Called when created directly from a XIB file
        [Export("initWithCoder:")]
        public MyView(NSCoder coder) : base(coder)
        {
        }

        public override void AwakeFromNib()
        {
            Layer = new CALayer();
            Layer.AddSublayer(MovingLayer);
            WantsLayer = true;

            Window.Title = "OCTOPUS RAW Player";
        }

        private OpenGLLayer MovingLayer
        {
            get
            {
                if (movingLayer == null)
                {
                    movingLayer = new OpenGLLayer();
                    //movingLayer.RasterizationScale = 1.0f;
                    movingLayer.ContentsScale = Window.BackingScaleFactor;
                    movingLayer.Frame = Window.Frame; // new CGRect(0, 0, 150, 150);

                    movingLayer.Position = new CGPoint(0, 0);
                    movingLayer.AnchorPoint = new CGPoint(0, 0);
                }
                return movingLayer;
            }
        }

        public override void MouseDown(NSEvent theEvent)
        {
            CGPoint location = ConvertPointFromView(theEvent.LocationInWindow, null);
            //movingLayer.Position = new CGPoint(location.X, location.Y);
        }

        public override void DidChangeBackingProperties()
        {
            base.DidChangeBackingProperties();

            if (movingLayer != null)
                movingLayer.ContentsScale = Window.BackingScaleFactor;
        }

        partial void toggle(NSButton sender)
        {
            movingLayer.Animate = !movingLayer.Animate;
        }
    }
}

