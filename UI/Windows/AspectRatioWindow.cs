using Octopus.Player.Core.Maths;
using OpenTK.Mathematics;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Octopus.Player.UI.Windows
{
    public class AspectRatioWindow : Window
    {
        private const int WM_SIZING = 0x0214;
        private const int WM_WINDOWPOSCHANGING = 0x0046;
        private const int WMSZ_BOTTOM = 6;
        private const int WMSZ_BOTTOMLEFT = 7;
        private const int WMSZ_BOTTOMRIGHT = 8;
        private const int WMSZ_LEFT = 1;
        private const int WMSZ_RIGHT = 2;
        private const int WMSZ_TOP = 3;
        private const int WMSZ_TOPLEFT = 4;
        private const int WMSZ_TOPRIGHT = 5;
        private const int SWP_NOSIZE = 0x0001;

        public bool IsFullscreen { get; protected set; }
        protected Rational? lockedAspectRatio;
        private int sizingEdge;
        protected virtual Vector2 AspectRatioArea { get; }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
                source.AddHook(new HwndSourceHook(WinProc));
        }

        protected Vector2? TransformPixelToLogical(in Vector2i sizePixels)
        {
            var presentationSource = PresentationSource.FromVisual(this);
            if (presentationSource == null)
                return null;
            Matrix transformFromDevice = presentationSource.CompositionTarget.TransformFromDevice;
            var size = transformFromDevice.Transform(new Vector(sizePixels.X, sizePixels.Y));
            return new Vector2((float)size.X, (float)size.Y);
        }

        protected Vector2i? TransformLogicalToPixel(in Vector2d sizeLogical)
        {
            var presentationSource = PresentationSource.FromVisual(this);
            if (presentationSource == null)
                return null;
            Matrix transformToDevice = presentationSource.CompositionTarget.TransformToDevice;
            var size = transformToDevice.Transform(new Vector(sizeLogical.X, sizeLogical.Y));
            return new Vector2i((int)size.X, (int)size.Y);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public int flags;
        }

        private IntPtr WinProc(IntPtr hwnd, Int32 msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_SIZING:
                    sizingEdge = wParam.ToInt32();
                    break;

                case WM_WINDOWPOSCHANGING:
                    if (!lockedAspectRatio.HasValue || IsFullscreen)
                        return IntPtr.Zero;
                    var windowPositionObject = Marshal.PtrToStructure(lParam, typeof(WINDOWPOS));
                    if (windowPositionObject == null)
                        return IntPtr.Zero;

                    WINDOWPOS windowPosition = (WINDOWPOS)windowPositionObject;
                    if ((windowPosition.flags & SWP_NOSIZE) == 0)
                    {
                        // Calculate client aspect ratio
                        var clientAspectRatio = lockedAspectRatio.Value;
                        

                        var logicalSize = TransformPixelToLogical(new Vector2i(windowPosition.cx, windowPosition.cy));
                        if (logicalSize.HasValue && (logicalSize.Value.X != Width || logicalSize.Value.Y != Height))
                        {
                            switch (sizingEdge)
                            {
                                case WMSZ_TOP:
                                case WMSZ_BOTTOM:
                                case WMSZ_TOPRIGHT:
                                    windowPosition.cx = (int)(windowPosition.cy * lockedAspectRatio.Value.ToDouble());
                                    break;

                                case WMSZ_LEFT:
                                case WMSZ_RIGHT:
                                case WMSZ_BOTTOMRIGHT:
                                case WMSZ_BOTTOMLEFT:
                                    windowPosition.cy = (int)(windowPosition.cx * (1.0 / lockedAspectRatio.Value.ToDouble()));
                                    break;

                                case WMSZ_TOPLEFT:
                                    var rightEdge = windowPosition.x + windowPosition.cx;
                                    windowPosition.cx = (int)(windowPosition.cy * lockedAspectRatio.Value.ToDouble());
                                    windowPosition.x = rightEdge - windowPosition.cx;
                                    break;
                            }
                        }
                    }
                    Marshal.StructureToPtr(windowPosition, lParam, true);
                    break;
            }

            return IntPtr.Zero;
        }
    }
}
