
using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using CoreGraphics;
using Foundation;
using AppKit;
using CoreAnimation;
using CoreVideo;
using OpenGL;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;

namespace Octopus.Player.UI.macOS
{
    public partial class OpenGLLayer : CoreAnimation.CAOpenGLLayer
    {
        // Not defined in Xamarin.Mac :(
        const CGLPixelFormatAttribute kCGLPFAOpenGLProfile = (CGLPixelFormatAttribute)99u;
        const CGLPixelFormatAttribute kCGLOGLPVersion_3_2_Core = (CGLPixelFormatAttribute)0x3200;

        PlayerWindow PlayerWindow { get; set; }
        public GPU.OpenGL.Render.Context RenderContext { get; private set; }

        volatile bool forceRender;
        
        public OpenGLLayer(PlayerWindow playerWindow) : base()
        {
            PlayerWindow = playerWindow;
            Initialize();
        }

        // Called when created from unmanaged code
        public OpenGLLayer(IntPtr handle) : base(handle)
        {
            Initialize();
        }

        // Called when created directly from a XIB file
        [Export("initWithCoder:")]
        public OpenGLLayer(NSCoder coder) : base(coder)
        {
            Initialize();
        }

        // Shared initialization code
        void Initialize()
        {
            Asynchronous = true;
            forceRender = true;
        }

        public override bool CanDrawInCGLContext (CGLContext glContext, CGLPixelFormat pixelFormat, double timeInterval, ref CVTimeStamp timeStamp)
        {
            return forceRender;
        }

        public override void DrawInCGLContext(OpenGL.CGLContext glContext, CGLPixelFormat pixelFormat, double timeInterval, ref CVTimeStamp timeStamp)
        {
            if (RenderContext == null)
            {
                RenderContext = new GPU.OpenGL.Render.Context(PlayerWindow.NativeWindow, glContext);
                RenderContext.ForceRender += delegate { forceRender = true; };
                PlayerWindow.OnRenderInit(RenderContext);
            }

            PlayerWindow.OnRenderFrame(timeInterval);
            forceRender = false;
        }

        public override CGLPixelFormat CopyCGLPixelFormatForDisplayMask (uint mask)
        {
            // Create a 24-bit modern OpenGL context with no depth buffer
            var attribs = new CGLPixelFormatAttribute[] {
                kCGLPFAOpenGLProfile, kCGLOGLPVersion_3_2_Core,
                CGLPixelFormatAttribute.Accelerated,
                CGLPixelFormatAttribute.DoubleBuffer,
                CGLPixelFormatAttribute.ColorSize, (CGLPixelFormatAttribute)24,
                CGLPixelFormatAttribute.DepthSize, (CGLPixelFormatAttribute)0 };

            int pixelFormatIndex;
            CGLPixelFormat pixelFormat = new CGLPixelFormat(attribs, out pixelFormatIndex);
            return pixelFormat;
        }
    }
}

