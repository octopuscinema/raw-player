
using System;
using System.Collections.Generic;
using System.Linq;
using CoreGraphics;
using Foundation;
using AppKit;
using CoreAnimation;
using OpenGL;
using System.Diagnostics;

namespace Octopus.Player.UI.macOS
{
    public partial class PlaybackControlsView : AppKit.NSVisualEffectView
    {
        public event EventHandler MouseEnter;
        public event EventHandler MouseExit;

        private CGPoint? startPoint;
        private CGPoint? frameOrigin;

        private NSTrackingArea trackingArea;

        public PlaybackControlsView(IntPtr handle) : base(handle)
        {

        }

        [Export("initWithCoder:")]
        public PlaybackControlsView(NSCoder coder) : base(coder)
        {
        }

        ~PlaybackControlsView()
        {
            if (trackingArea != null)
                trackingArea.Dispose();
        }

        public override void AwakeFromNib()
        {
            base.AwakeFromNib();

            // Apply style
            Appearance = NSAppearance.GetAppearance(NSAppearance.NameVibrantDark);
            BlendingMode = NSVisualEffectBlendingMode.WithinWindow;
            Material = NSVisualEffectMaterial.Menu;
            State = NSVisualEffectState.FollowsWindowActiveState;
        }

        public override void UpdateTrackingAreas()
        {
            if (trackingArea != null)
            {
                RemoveTrackingArea(trackingArea);
                trackingArea.Dispose();
            }

            trackingArea = new NSTrackingArea(Frame, NSTrackingAreaOptions.ActiveInKeyWindow | NSTrackingAreaOptions.MouseEnteredAndExited | NSTrackingAreaOptions.MouseMoved, this, null);
            AddTrackingArea(trackingArea);
        }

        public override void MouseDown(NSEvent theEvent)
        {
            startPoint = theEvent.LocationInWindow;
            frameOrigin = Frame.Location;
        }

        public override void MouseDragged(NSEvent theEvent)
        {
            // Allow for dragging the view around
            if (startPoint.HasValue && frameOrigin.HasValue)
            {
                var offset = new CGPoint(theEvent.LocationInWindow.X - startPoint.Value.X, theEvent.LocationInWindow.Y - startPoint.Value.Y);
                var frame = Frame;
                frame.Location = new CGPoint(frameOrigin.Value.X + offset.X, frameOrigin.Value.Y + offset.Y);

                // Constrain location to within bounds
                var playerWindow = (NativePlayerWindow)Window;
                if (playerWindow != null)
                {
                    var margin = playerWindow.PlayerWindow.Theme.PlaybackControlsMargin;
                    frame.X = (nfloat)Math.Max(margin, frame.X);
                    frame.Y = (nfloat)Math.Max(margin, frame.Y);

                    var playerView = playerWindow.FindView(playerWindow.ContentView, "playerView");
                    frame.X = (nfloat)Math.Min(playerView.Frame.Width - (frame.Width + margin), frame.X);
                    frame.Y = (nfloat)Math.Min(playerView.Frame.Height - (frame.Height + margin), frame.Y);
                }
                Frame = frame;
            }
        }

        public override void MouseUp(NSEvent theEvent)
        {
            startPoint = null;
            frameOrigin = null;
        }

        public override void MouseExited(NSEvent theEvent)
        {
            base.MouseExited(theEvent);
            MouseExit?.Invoke(this, new EventArgs());
        }

        public override void MouseEntered(NSEvent theEvent)
        {
            base.MouseEntered(theEvent);
            MouseEnter?.Invoke(this, new EventArgs());
        }
    }
}

