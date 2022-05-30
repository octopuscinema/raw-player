﻿using System;
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
    public partial class PlayerWindow : Window, INativeWindow
    {
        public bool IsFullscreen { get; private set; }
        private WindowState NonFullscreenWindowState { get; set; }
        private WindowLogic WindowLogic { get; set; }

        public PlayerWindow()
        {
            InitializeComponent();
            // Code to get App
            // INativeApp nativeApp = ((INativeApp)Application.Current);
            // AppLogic = new AppLogic(nativeApp);
            WindowLogic = new WindowLogic(this);

            // Save the startup window state
            NonFullscreenWindowState = WindowState;

            // Start the OpenGL control
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
            switch(e.ChangedButton)
            {
                case MouseButton.Left:
                    WindowLogic.LeftMouseDown((uint)e.ClickCount);
                    break;
                case MouseButton.Right:
                    WindowLogic.RightMouseDown((uint)e.ClickCount);
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

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(sender.GetType() == typeof(MenuItem));
            var menuItem = (MenuItem)sender;
            if (menuItem != null)
                WindowLogic.MenuItemClick(menuItem.Name);
        }
    }
}
