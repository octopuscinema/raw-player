using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Wpf;

namespace Octopus.Player.UI.Windows
{
    public struct DefaultWindowsTheme : ITheme
    {
        public Vector3 ClipBackground { get { return new Vector3(0, 0, 0); } }
        public Vector3 EmptyBackground { get { return new Vector3(SystemColors.MenuBarColor.R, SystemColors.MenuBarColor.G, SystemColors.MenuBarColor.B) / 255.0f; } }
    }

    /// <summary>
    /// Interaction logic for PlayerWindow.xaml
    /// </summary>
    public partial class NativePlayerWindow : Window, INativeWindow
    {
        public bool IsFullscreen { get; private set; }
        private WindowState NonFullscreenWindowState { get; set; }
        private PlayerWindow PlayerWindow { get; set; }
        public GPU.OpenGL.Render.Context RenderContext { get; private set; } = default!;

        public Vector2i FramebufferSize { get; private set; }

        public NativePlayerWindow()
        {
            InitializeComponent();
            
            // Create cross platform Window
            PlayerWindow = new PlayerWindow(this);

            // Save the startup window state
            NonFullscreenWindowState = WindowState;

            // Start the OpenGL control
            var mainSettings = new GLWpfControlSettings 
            { 
                MajorVersion = 3, 
                MinorVersion = 3,
                RenderContinuously = false 
            };
            GLControl.Start(mainSettings);
        }

        private void GLControl_OnRender(TimeSpan delta)
        {
            if (RenderContext == null)
            {
                RenderContext = new GPU.OpenGL.Render.Context(this, GLControl);
                RenderContext.ForceRender += delegate { GLControl.InvalidateVisual(); };
                PlayerWindow.OnRenderInit(RenderContext);
            }

            PlayerWindow.OnRenderFrame(delta.TotalSeconds);
        }

        private void GLControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            switch(e.ChangedButton)
            {
                case MouseButton.Left:
                    PlayerWindow.LeftMouseDown((uint)e.ClickCount);
                    break;
                case MouseButton.Right:
                    PlayerWindow.RightMouseDown((uint)e.ClickCount);
                    break;
                default:
                    break;
            }
        }

        public void SetWindowTitle(string text)
        {
            Title = text;
        }

        public void ToggleFullscreen()
        {
            if ( !IsFullscreen )
            {
                NonFullscreenWindowState = WindowState;
                WindowStyle = WindowStyle.None;
                if (WindowState == WindowState.Maximized)
                    WindowState = WindowState.Normal;
                WindowState = WindowState.Maximized;
                PlayerMenu.Visibility = Visibility.Collapsed;
                IsFullscreen = true;
            }
            else
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                WindowState = NonFullscreenWindowState;
                PlayerMenu.Visibility = Visibility.Visible;
                IsFullscreen = false;
            }
        }

        public string? OpenFolderDialogue(string title, string defaultDirectory)
        {
            using var dialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            dialog.EnsurePathExists = true;
            dialog.Multiselect = false;
            dialog.DefaultDirectory = defaultDirectory;

            return dialog.ShowDialog() == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok ? dialog.FileName : null;
        }
        public void Exit()
        {
            Application.Current.Shutdown();
        }

        private MenuItem? FindMenuItem(ItemCollection items, string id)
        {
            foreach (var item in items)
            {
                var menuItem = item as MenuItem;
                if (menuItem == null)
                    continue;
                if (menuItem.Name == id)
                    return menuItem;
                var found = FindMenuItem(menuItem.Items, id);
                if ( found != null)
                    return found;
            }

            return null;
        }

        public void EnableMenuItem(string id, bool enable)
        {
            var item = FindMenuItem(PlayerMenu.Items, id);
            if (item != null)
                item.IsEnabled = enable;
        }

        public void CheckMenuItem(string id, bool check = true, bool uncheckSiblings = true)
        {
            var item = FindMenuItem(PlayerMenu.Items, id);
            if (item != null)
            {
                item.IsChecked = check;
                if (uncheckSiblings)
                {
                    var parent = item.Parent as MenuItem;
                    if (parent != null)
                    {
                        foreach (var sibling in parent.Items)
                        {
                            var siblingItem = sibling as MenuItem;
                            if (siblingItem != null && siblingItem != item)
                                siblingItem.IsChecked = false;
                        }
                    }
                }
            }
        }

        public bool MenuItemIsChecked(string id)
        {
            var item = FindMenuItem(PlayerMenu.Items, id);
            return (item != null) ? item.IsChecked : false;
        }

        public void SetMenuItemTitle(string id, string name)
        {
            var item = FindMenuItem(PlayerMenu.Items, id);
            if (item != null)
                item.Header = "_" + name;
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(sender.GetType() == typeof(MenuItem));
            var menuItem = (MenuItem)sender;
            if (menuItem != null)
                PlayerWindow.MenuItemClick(menuItem.Name);
        }

        public void Alert(AlertType alertType, string message, string title)
        {
            switch (alertType)
            {
                case AlertType.Blank:
                    MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.None);
                    break;
                case AlertType.Information:
                    MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case AlertType.Error:
                    MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
                    break;
                case AlertType.Warning:
                    MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;
                default:
                    break;
            }
        }

        public void OpenUrl(string url)
        {
            try
            {
                Process.Start("explorer.exe", url);
            }
            catch(Exception e)
            {
                Trace.WriteLine(e.Message);
            }
        }

        private void GLControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            FramebufferSize = new Vector2i(GLControl.FrameBufferWidth, GLControl.FrameBufferHeight);
            PlayerWindow.OnFramebufferResize(FramebufferSize);
        }

#if WINDOW_ASPECT_RATIO_LOCK
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            HwndSource source = HwndSource.FromVisual(this) as HwndSource;
            if (source != null)
            {
                source.AddHook(new HwndSourceHook(WinProc));
            }
        }

        double xRatio = 1;
        double yRatio = 1;
        int sizingEdge = 0;

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
            const int WM_SIZE = 0x0005;
            const int WM_SIZING = 0x0214;
            const int WM_WINDOWPOSCHANGING = 0x0046;

            // https://docs.microsoft.com/en-us/windows/win32/winmsg/wm-sizing
            const int WMSZ_BOTTOM = 6;
            const int WMSZ_BOTTOMLEFT = 7;
            const int WMSZ_BOTTOMRIGHT = 8;
            const int WMSZ_LEFT = 1;
            const int WMSZ_RIGHT = 2;
            const int WMSZ_TOP = 3;
            const int WMSZ_TOPLEFT = 4;
            const int WMSZ_TOPRIGHT = 5;

            switch (msg)
            {
                case WM_SIZING:
                    sizingEdge = wParam.ToInt32();
                    break;

                case WM_WINDOWPOSCHANGING:
                    var position = (WINDOWPOS)Marshal.PtrToStructure(lParam, typeof(WINDOWPOS));

                    if (position.cx == this.Width && position.cy == this.Height)
                        return IntPtr.Zero;

                    switch (sizingEdge)
                    {
                        case WMSZ_TOP: // Top edge
                        case WMSZ_BOTTOM: // Bottom edge
                        case WMSZ_TOPRIGHT: // Top-right corner
                            position.cx = (int)(position.cy * xRatio);
                            break;

                        case WMSZ_LEFT: // Left edge
                        case WMSZ_RIGHT: // Right edge
                        case WMSZ_BOTTOMRIGHT: // Bottom-right corner
                        case WMSZ_BOTTOMLEFT: // Bottom-left corner
                            position.cy = (int)(position.cx * yRatio);
                            break;


                        case WMSZ_TOPLEFT: // Top-left corner
                            position.cx = (int)(position.cy * xRatio);
                            //position.x = (int)Left - (position.cx - (int)Width);
                            position.x = (int)position.x - (position.cx - (int)Width);
                            break;
                    }

                    Marshal.StructureToPtr(position, lParam, true);
                    break;
            }

            return IntPtr.Zero;
        }
#endif
    }
}
