using System;

namespace Octopus.Player.UI
{
    public class WindowLogic
    {
        private INativeWindow NativeWindow { get; set; }

        public WindowLogic(INativeWindow nativeWindow)
        {
            NativeWindow = nativeWindow;
        }

        public void LeftMouseDown(uint clickCount)
        {
            // Double click to go full screen
            if ( clickCount == 2 )
                NativeWindow.ToggleFullscreen();
        }

        public void RightMouseDown(uint clickCount)
        {
            
        }
    }
}

