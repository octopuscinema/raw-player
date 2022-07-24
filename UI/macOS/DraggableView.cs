
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
    public partial class DraggableView : AppKit.NSVisualEffectView
    {
        CGPoint? startPoint;
        CGPoint? frameOrigin;

        // Called when created from unmanaged code
        public DraggableView(IntPtr handle) : base(handle)
        {
            //TODO: Rename to PlaybackControlsView
        }

        [Export("initWithCoder:")]
        public DraggableView(NSCoder coder) : base(coder)
        {
        }

        public override void AwakeFromNib()
        {
            base.AwakeFromNib();

            Appearance = NSAppearance.GetAppearance(NSAppearance.NameVibrantDark);
            BlendingMode = NSVisualEffectBlendingMode.WithinWindow;
            Material = NSVisualEffectMaterial.Menu;
            State = NSVisualEffectState.FollowsWindowActiveState;
        }

        public override void MouseDown(NSEvent theEvent)
        {
            startPoint = theEvent.LocationInWindow;
            frameOrigin = Frame.Location;
        }

        public override void MouseDragged(NSEvent theEvent)
        {
            if (startPoint.HasValue && frameOrigin.HasValue)
            {
                var offset = new CGPoint(theEvent.LocationInWindow.X - startPoint.Value.X, theEvent.LocationInWindow.Y - startPoint.Value.Y);
                var frame = Frame;
                frame.Location = new CGPoint( frameOrigin.Value.X + offset.X, frameOrigin.Value.Y + offset.Y);

                var playerWindow = (NativePlayerWindow)Window;
                if (playerWindow != null)
                {
                    var margin = playerWindow.PlayerWindow.Theme.PlaybackControlsMargin;
                    frame.X = (nfloat)Math.Max(margin, frame.X);
                    frame.Y = (nfloat)Math.Max(margin, frame.Y);
                }
                Frame = frame;
            }
        }

        public override void MouseUp(NSEvent theEvent)
        {
            startPoint = null;
            frameOrigin = null;
        }
    }
}

