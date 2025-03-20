
using System;
using System.Collections.Generic;
using System.Linq;
using CoreGraphics;
using Foundation;
using AppKit;
using CoreAnimation;
using OpenTK.Mathematics;
using Octopus.Player.Core.Playback;

namespace Octopus.Player.UI.macOS
{
    public partial class PlayerView : NSView
    {
        private OpenGLLayer GLLayer { get; set; }

        private NativePlayerWindow NativePlayerWindow { get { return (NativePlayerWindow)Window; } }

        private NSTrackingArea trackingArea;

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

            // Register for drag/drop
            RegisterForDraggedTypes(new string[] { NSPasteboard.NSFilenamesType });

            // Apply feed view style
            var feed = NativePlayerWindow.FindView(NativePlayerWindow.ContentView, "feed") as AppKit.NSVisualEffectView;
            feed.Appearance = NSAppearance.GetAppearance(NSAppearance.NameVibrantDark);
            feed.BlendingMode = NSVisualEffectBlendingMode.WithinWindow;
            feed.Material = NSVisualEffectMaterial.Menu;
            feed.State = NSVisualEffectState.FollowsWindowActiveState;
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
                var theme = NativePlayerWindow.PlayerWindow.Theme;

                var playbackControls = NativePlayerWindow.FindView(NativePlayerWindow.ContentView, "playbackControls");
                var playbackControlsFrame = playbackControls.Frame;
                playbackControlsFrame.Location = new CGPoint(Frame.Width/2 - playbackControlsFrame.Width/2, NativePlayerWindow.PlayerWindow.Theme.PlaybackControlsMargin);
                playbackControls.Frame = playbackControlsFrame;

                // Hide drop area of overlapping with playback controls
                var dropArea = NativePlayerWindow.FindView(NativePlayerWindow.ContentView, "dropArea");
                var dropAreaFrame = dropArea.Frame;
                var dropAreaPlaybackControlsDist = dropArea.Frame.DistanceTo(playbackControlsFrame);
                dropArea.AlphaValue = (float)Math.Clamp(dropAreaPlaybackControlsDist / theme.DropAreaOpacityMargin, 0.0, 1.0);

                // Also hide the feed
                var feed = NativePlayerWindow.FindView(NativePlayerWindow.ContentView, "feed");
                var feedDropAreaDist = feed.Frame.DistanceTo(dropAreaFrame);
                var feedPlaybackControlsDist = feed.Frame.DistanceTo(playbackControlsFrame);
                feed.AlphaValue = (float)Math.Clamp(Math.Min(feedPlaybackControlsDist, feedDropAreaDist) / theme.FeedOpacityMargin, 0.0, 1.0);
                feed.Hidden = feed.AlphaValue == 0 ? true : !NativePlayerWindow.FeedVisible;
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

        IEnumerable<string> DraggedFilenames(NSPasteboard pasteboard)
        {
            if (Array.IndexOf(pasteboard.Types, NSPasteboard.NSFilenamesType) < 0) yield break;
            foreach (var i in pasteboard.PasteboardItems) yield return new NSUrl(i.GetStringForType("public.file-url")).Path;
        }

        public override NSDragOperation DraggingEntered(NSDraggingInfo sender)
        {
            var files = DraggedFilenames(sender.DraggingPasteboard).ToArray();
            return NativePlayerWindow.PlayerWindow.CanDropFiles(files) ? NSDragOperation.Link : NSDragOperation.None;
        }

        public override bool PerformDragOperation(NSDraggingInfo sender)
        {
            var files = DraggedFilenames(sender.DraggingPasteboard).ToArray();
            NativePlayerWindow.PlayerWindow.DropFiles(files);
            return true;
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
                if (NativePlayerWindow.ActiveSliders.Contains(sender.Identifier))
                {
                    NativePlayerWindow.ActiveSliders.Remove(sender.Identifier);
                    NativePlayerWindow.PlayerWindow.SliderDragComplete(sender.Identifier, sender.FloatValue);
                }
                else
                    NativePlayerWindow.PlayerWindow.SliderSetValue(sender.Identifier, sender.FloatValue);
                
                return;
            }

            if ( NativePlayerWindow.ActiveSliders.Contains(sender.Identifier) )
                NativePlayerWindow.PlayerWindow.SliderDragDelta(sender.Identifier, sender.FloatValue);
            else
            {
                NativePlayerWindow.ActiveSliders.Add(sender.Identifier);
                NativePlayerWindow.PlayerWindow.SliderDragStart(sender.Identifier);
            }
        }
    }
}

