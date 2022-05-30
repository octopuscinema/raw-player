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
    public partial class PlayerWindow : Window
    {
        public bool IsFullscreen { get; private set; }
        private WindowState NonFullscreenWindowState { get; set; }

        public PlayerWindow()
        {
            InitializeComponent();

            // Save the startup window state
            NonFullscreenWindowState = WindowState;

            var mainSettings = new GLWpfControlSettings 
            { 
                MajorVersion = 3, 
                MinorVersion = 2,
                RenderContinuously = false 
            };
            GLControl.Start(mainSettings);
        }

        private void GLControl_OnRender(TimeSpan delta)
        {
            GL.ClearColor(Color4.Blue);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        }

        private void GLControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
                GLControl_MouseLeftDoubleClick();
        }

        private void GLControl_MouseLeftDoubleClick()
        {
            if (IsFullscreen)
                GoWindowed();
            else
                GoFullscreen();
        }

        private void GoFullscreen()
        {
            Debug.Assert(!IsFullscreen);
            NonFullscreenWindowState = WindowState;
            WindowStyle = WindowStyle.None;
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            WindowState = WindowState.Maximized;
            IsFullscreen = true;
        }

        private void GoWindowed()
        {
            Debug.Assert(IsFullscreen);
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = NonFullscreenWindowState;
            IsFullscreen = false;
        }
    }
}
