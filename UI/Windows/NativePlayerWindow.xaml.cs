using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Wpf;

namespace Octopus.Player.UI.Windows
{
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
            dialog.InitialDirectory = defaultDirectory;
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

        public void EnableMenuItem(string name, bool enabled)
        {
            var menuItems = PlayerMenu.Items;
            foreach (var item in menuItems)
            {
                var menuItem = item as MenuItem;
                if (menuItem != null && menuItem.Name == name)
                {
                    menuItem.IsEnabled = enabled;
                    return;
                }
            }
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
    }
}
