
using System;
using System.Collections.Generic;
using System.Linq;
using CoreGraphics;
using Foundation;
using AppKit;
using CoreAnimation;
using OpenGL;
using OpenTK.Mathematics;

namespace Octopus.Player.UI.macOS
{
    public partial class PlayerView : NSView
    {
        private OpenGLLayer GLLayer { get; set; }

        private NativePlayerWindow NativePlayerWindow { get { return (NativePlayerWindow)Window; } }

        private NSTrackingArea trackingArea;

        private HashSet<string> activeSliders = new HashSet<string>();

        // Called when created from unmanaged code
        public PlayerView(IntPtr handle) : base(handle)
        {
        }

        // Called when created directly from a XIB file
        [Export("initWithCoder:")]
        public PlayerView(NSCoder coder) : base(coder)
        {
        }

        ~PlayerView()
        {
            if (trackingArea != null)
                trackingArea.Dispose();
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

        public override void ResizeSubviewsWithOldSize(CGSize oldSize)
        {
            base.ResizeSubviewsWithOldSize(oldSize);
            ResizeGLLayer();
            UpdateLayout();
        }

        public override void ResizeWithOldSuperviewSize(CGSize oldSize)
        {
            base.ResizeWithOldSuperviewSize(oldSize);
            ResizeGLLayer();
            UpdateLayout();
        }

        private void UpdateLayout()
        {
            if ( NativePlayerWindow != null )
            {
                var playbackControls = NativePlayerWindow.FindView(NativePlayerWindow.ContentView, "playbackControls");
                var frame = playbackControls.Frame;
                frame.Location = new CGPoint(Frame.Width/2 - frame.Width/2, NativePlayerWindow.PlayerWindow.Theme.PlaybackControlsMargin);
                playbackControls.Frame = frame;
            }
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
            base.MouseDown(theEvent);
            var modifiers = theEvent.ModifierFlags.ToModifierNameList();
            if (NativePlayerWindow != null)
                NativePlayerWindow.PlayerWindow.LeftMouseDown((uint)theEvent.ClickCount, modifiers);
        }

        public override void RightMouseDown(NSEvent theEvent)
        {
            base.RightMouseDown(theEvent);
            var modifiers = theEvent.ModifierFlags.ToModifierNameList();
            if (NativePlayerWindow != null)
                NativePlayerWindow.PlayerWindow.RightMouseDown((uint)theEvent.ClickCount);
        }

        public override void MouseMoved(NSEvent theEvent)
        {
            base.MouseMoved(theEvent);
            if (NativePlayerWindow != null)
                NativePlayerWindow.PlayerWindow.MouseMove(new Vector2((float)theEvent.LocationInWindow.X, (float)theEvent.LocationInWindow.Y));
        }
        
        public override void MouseDragged(NSEvent theEvent)
        {
            base.MouseDragged(theEvent);
            if (NativePlayerWindow != null)
                NativePlayerWindow.PlayerWindow.MouseMove(new Vector2((float)theEvent.LocationInWindow.X, (float)theEvent.LocationInWindow.Y));
        }

        public override void MouseExited(NSEvent theEvent)
        {
            base.MouseExited(theEvent);
            if (NativePlayerWindow != null)
                NativePlayerWindow.PlayerWindow.MouseExited(new Vector2((float)theEvent.LocationInWindow.X, (float)theEvent.LocationInWindow.Y));
        }

        public override void MouseEntered(NSEvent theEvent)
        {
            base.MouseEntered(theEvent);
            if (NativePlayerWindow != null)
                NativePlayerWindow.PlayerWindow.MouseEntered(new Vector2((float)theEvent.LocationInWindow.X, (float)theEvent.LocationInWindow.Y));
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

        partial void SliderDrag(NSSlider sender)
        {
            if (NativePlayerWindow == null)
                return;

            // Drag has ended if there are no mouse button events
            bool dragEnd = (NSEvent.CurrentPressedMouseButtons == 0);

            if ( dragEnd )
            {
                if (activeSliders.Contains(sender.Identifier))
                {
                    activeSliders.Remove(sender.Identifier);
                    NativePlayerWindow.PlayerWindow.SliderDragComplete(sender.Identifier, sender.FloatValue);
                }
                else
                    NativePlayerWindow.PlayerWindow.SliderSetValue(sender.Identifier, sender.FloatValue);
                
                return;
            }
                
            if ( activeSliders.Contains(sender.Identifier) )
                NativePlayerWindow.PlayerWindow.SliderDragDelta(sender.Identifier, sender.FloatValue);
            else
            {
                activeSliders.Add(sender.Identifier);
                NativePlayerWindow.PlayerWindow.SliderDragStart(sender.Identifier);
            }
        }
    }
}

