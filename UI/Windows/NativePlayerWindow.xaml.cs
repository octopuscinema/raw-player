using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Xml.Linq;
using Octopus.Player.Core.Maths;
using OpenTK.Mathematics;
using OpenTK.Wpf;

namespace Octopus.Player.UI.Windows
{
    static partial class Extensions
    {
        public static List<string> ToModifierList(this ModifierKeys modifiers)
        {
            List<string> modifierList = new List<string>();

            var modifierKeys = Enum.GetValues(typeof(ModifierKeys)).Cast<ModifierKeys>();
            foreach (var key in modifierKeys)
            {
                if ((modifiers & key) != 0)
                    modifierList.Add(key.ToString());
            }

            return modifierList;
        }

        public static Rect BoundsRelativeTo(this FrameworkElement child, Visual parent)
        {
            GeneralTransform gt = child.TransformToAncestor(parent);
            return gt.TransformBounds(new Rect(0, 0, child.ActualWidth, child.ActualHeight));
        }

        public static double DistanceTo(this FrameworkElement child, FrameworkElement to, Visual parent)
        {
            var rectA = BoundsRelativeTo(child, parent);
            var rectB = BoundsRelativeTo(to, parent);

            if (rectA.IntersectsWith(rectB))
                return 0.0f;

            var horizontalDistance = (rectA.Left > rectB.Right) ? rectA.Left - rectB.Right : rectB.Left - rectA.Right;
            var verticalDistance = (rectA.Top > rectB.Bottom) ? rectA.Top - rectB.Bottom: rectB.Top - rectA.Bottom;
            return Math.Max(horizontalDistance, verticalDistance);
        }
    }
     
    public partial class NativePlayerWindow : AspectRatioWindow, INativeWindow
    {
        [DllImport("UXTheme.dll", SetLastError = true, EntryPoint = "#138")]
        public static extern bool ShouldSystemUseDarkMode();

        public ControlsAnimationState ControlsAnimationState { get; private set; }
        public bool AspectLocked { get { return lockedContentAspectRatio.HasValue; } }

        private WindowState NonFullscreenWindowState { get; set; }
        private PlayerWindow PlayerWindow { get; set; }
        public GPU.OpenGL.Render.Context RenderContext { get; private set; } = default!;

        public Vector2i FramebufferSize { get; private set; }
        public bool MouseInsidePlaybackControls { get; private set; }
        private ITheme Theme { get { return PlayerWindow.Theme; } }

        public PlayerApplication PlayerApplication { get { return ((App)Application.Current).PlayerApplication; } }

        public bool DropAreaVisible
        {
            get { return dropArea.Visibility == Visibility.Visible; }
            set { dropArea.Visibility = value ? Visibility.Visible : Visibility.Hidden; }
        }

        private IntPtr? hwnd;

        private HashSet<Slider> activeSliders = new HashSet<Slider>();

        Point? playbackControlsDragStart;
        Point? playbackControlsPosition;

        public NativePlayerWindow()
        {
            ControlsAnimationState = ControlsAnimationState.In;
            InitializeComponent();

            // Create cross platform Window
            PlayerWindow = new PlayerWindow(this, ShouldSystemUseDarkMode() ? new DefaultWindowsThemeDark() : new DefaultWindowsTheme());
            Closed += OnClose;

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

            // Centre the playback controls at the bottom
            playbackControls.Margin = new Thickness(GLControl.ActualWidth / 2 - playbackControls.ActualWidth/2, 0, 0, Theme.PlaybackControlsMargin);
        }

        private void OnClose(object? sender, EventArgs e)
        {
            Debug.Assert(PlayerWindow != null);
            if (PlayerWindow != null) 
                PlayerWindow.Dispose();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var modifiers = Keyboard.Modifiers.ToModifierList();

            e.Handled = PlayerWindow.PreviewKeyDown(e.Key.ToString(), modifiers);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            PlayerWindow.KeyDown(e.Key.ToString());
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
                    PlayerWindow.LeftMouseDown((uint)e.ClickCount, Keyboard.Modifiers.ToModifierList());
                    break;
                case MouseButton.Right:
                    PlayerWindow.RightMouseDown((uint)e.ClickCount);
                    break;
                default:
                    break;
            }
        }

        private void GLControl_MouseMove(object sender, MouseEventArgs e)
        {
            var mousePosition = e.GetPosition(this);
            PlayerWindow.MouseMove(new Vector2((float)mousePosition.X, (float)mousePosition.Y));
        }

        private void GLControl_MouseLeave(object sender, MouseEventArgs e)
        {
            var mousePosition = e.GetPosition(this);
            PlayerWindow.MouseExited(new Vector2((float)mousePosition.X, (float)mousePosition.Y));
        }

        private void GLControl_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if ( files.Length > 0 )
                PlayerWindow.DropFiles(files);
            }
        }

        private void PlaybackControls_MouseDown(object sender, MouseButtonEventArgs e)
        {
            playbackControls.CaptureMouse();
            if ( e.ChangedButton == MouseButton.Left )
            {
                playbackControlsDragStart = e.GetPosition(this);
                playbackControlsPosition = new Point(playbackControls.Margin.Left, playbackControls.Margin.Bottom);
            }
        }

        private void PlaybackControls_MouseMove(object sender, MouseEventArgs e)
        {
            if (playbackControlsDragStart.HasValue && playbackControlsPosition.HasValue)
            {
                var delta = e.GetPosition(this) - playbackControlsDragStart.Value;

                var margin = playbackControls.Margin;
                margin.Left = Math.Max(Theme.PlaybackControlsMargin, playbackControlsPosition.Value.X + delta.X);
                margin.Bottom = Math.Max(Theme.PlaybackControlsMargin, playbackControlsPosition.Value.Y - delta.Y);
                
                margin.Left = Math.Min(margin.Left, GLControl.ActualWidth - (playbackControls.ActualWidth + Theme.PlaybackControlsMargin));
                margin.Bottom = Math.Min(margin.Bottom, GLControl.ActualHeight - (playbackControls.ActualHeight + Theme.PlaybackControlsMargin + PlayerMenu.ActualHeight));

                playbackControls.Margin = margin;
            }

            var mousePosition = e.GetPosition(this);
            PlayerWindow.MouseMove(new Vector2((float)mousePosition.X, (float)mousePosition.Y));
        }

        private void PlaybackControls_MouseUp(object sender, MouseButtonEventArgs e)
        {
            playbackControls.ReleaseMouseCapture();
            if (e.ChangedButton == MouseButton.Left)
            {
                playbackControlsDragStart = null;
                playbackControlsPosition = null;
            }
        }

        private void PlaybackControls_MouseEnter(object sender, MouseEventArgs e)
        {
            MouseInsidePlaybackControls = true;
        }

        private void PlaybackControls_MouseLeave(object sender, MouseEventArgs e)
        {
            MouseInsidePlaybackControls = false;
        }

        public void SetWindowTitle(string text)
        {
            Title = text;
        }

        public void ToggleFullscreen()
        {
            if ( !IsFullscreen )
            {
                IsFullscreen = true;
                NonFullscreenWindowState = WindowState;
                WindowStyle = WindowStyle.None;
                if (WindowState == WindowState.Maximized)
                    WindowState = WindowState.Normal;
                WindowState = WindowState.Maximized;
                PlayerMenu.Visibility = Visibility.Collapsed;
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
            dialog.Title = title;

            return dialog.ShowDialog() == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok ? dialog.FileName : null;
        }
        public void Exit()
        {
            Application.Current.Shutdown();
        }
        
        private T? FindControl<T>(string name) where T : Control
        {
            foreach (var field in GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Where(field => field.FieldType == typeof(T)))
            {
                var control = (T?)field.GetValue(this);
                if (control != null && control.Name == name)
                    return control;
            }

            return null;
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

        private ContextMenu? FindContextMenu(string? id = null)
        {
            if ( id != null)
                return (ContextMenu)FindResource(id);

            foreach (System.Collections.DictionaryEntry resourceEntry in Resources)
            {
                if (resourceEntry.Value is ContextMenu)
                    return (ContextMenu)resourceEntry.Value;
            }

            return null;
        }

        MenuItem? FindContextMenuItem(string id, string? contextMenuId = null)
        {
            var contextMenu = FindContextMenu(contextMenuId);
            return contextMenu == null ? null : FindMenuItem(contextMenu.Items, id);
        }

        public void EnableMenuItem(string id, bool enable)
        {
            var item = FindMenuItem(PlayerMenu.Items, id);
            if (item != null)
                item.IsEnabled = enable;

            var contextMenuItem = FindContextMenuItem(id);
            if (contextMenuItem != null)
                contextMenuItem.IsEnabled = enable;
        }

        public void CheckMenuItem(string id, bool check = true, bool uncheckSiblings = true)
        {
            List<MenuItem> items = new List<MenuItem>();
            var menuItem = FindMenuItem(PlayerMenu.Items, id);
            if (menuItem != null)
                items.Add(menuItem);
            var contextMenuItem = FindContextMenuItem(id);
            if (contextMenuItem != null)
                items.Add(contextMenuItem);

            foreach(var item in items)
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

        public void ToggleMenuItemChecked(string id)
        {
            bool check = !MenuItemIsChecked(id);
            CheckMenuItem(id, check, false);
        }

        public void SetMenuItemTitle(string id, string name)
        {
            var item = FindMenuItem(PlayerMenu.Items, id);
            if (item != null)
                item.Header = "_" + name;

            item = FindContextMenuItem(id);
            if (item != null)
                item.Header = "_" + name;
        }

        public void AddMenuItem(string parentId, string name, uint? index, Action onClick)
        {
            var parentItem = FindMenuItem(PlayerMenu.Items, parentId);
            if (parentItem != null)
            {
                MenuItem item = new MenuItem();
                item.Header = name;
                item.Click += (object sender, RoutedEventArgs e) => { onClick(); };
                if ( index.HasValue )
                    parentItem.Items.Insert((int)index.Value, item);
                else
                    parentItem.Items.Add(item);
            }
        }

        public void AddMenuSeperator(string parentId, uint? index)
        {
            var parentItem = FindMenuItem(PlayerMenu.Items, parentId);
            if (parentItem != null)
            {
                if (index.HasValue)
                    parentItem.Items.Insert((int)index.Value, new Separator());
                else
                    parentItem.Items.Add(new Separator());
            }
        }

        public void SetLabelContent(string id, string content, Vector3? colour = null, bool? fixedWidthDigitHint = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var label = FindControl<Label>(id);
                if (label != null)
                {
                    label.Content = content;
                    if ( colour.HasValue )
                        label.Foreground = new SolidColorBrush(Color.FromRgb((byte)(colour.Value.X * 255.0f), (byte)(colour.Value.Y * 255.0f), (byte)(colour.Value.Z * 255.0f)));
                    label.IsEnabled = content.Length > 0;
                }
            });
        }

        public void SetButtonVisibility(string id, bool visible)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var button = FindControl<Button>(id);
                if (button != null)
                    button.Visibility = visible ? Visibility.Visible : Visibility.Hidden;
            });
        }

        public void SetButtonEnabled(string id, bool enabled)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var button = FindControl<Button>(id);
                if (button != null)
                {
                    button.IsEnabled = enabled;
                    button.Opacity = enabled ? Theme.DefaultOpacity : Theme.DisabledOpacity;
                }
            });
        }

        public void SetSliderValue(string id, float value)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var slider = FindControl<Slider>(id);
                if (slider != null)
                {
                    activeSliders.Add(slider);
                    slider.Value = value * slider.Maximum;
                    activeSliders.Remove(slider);
                }
            });
        }

        public void SetSliderEnabled(string id, bool enabled)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var slider = FindControl<Slider>(id);
                if (slider != null)
                {
                    slider.IsEnabled = enabled;
                    slider.Opacity = enabled ? Theme.DefaultOpacity : Theme.DisabledOpacity;
                }
            });
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(sender.GetType() == typeof(MenuItem));
            var menuItem = (MenuItem)sender;
            if (menuItem != null)
                PlayerWindow.MenuItemClick(menuItem.Name);
        }

        private AlertResponse MessageBoxReturn(MessageBoxResult result)
        {
            switch (result)
            {
                case MessageBoxResult.None:
                case MessageBoxResult.OK:
                    return AlertResponse.None;
                case MessageBoxResult.Yes:
                    return AlertResponse.Yes;
                case MessageBoxResult.Cancel:
                case MessageBoxResult.No:
                    return AlertResponse.No;
                default:
                    throw new Exception();
            }
        }

        public AlertResponse Alert(AlertType alertType, string message, string title)
        {
            switch (alertType)
            {
                case AlertType.Blank:
                    return MessageBoxReturn(MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.None));
                case AlertType.Information:
                    return MessageBoxReturn(MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Information));
                case AlertType.Error:
                    return MessageBoxReturn(MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Error));
                case AlertType.Warning:
                    return MessageBoxReturn(MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Warning));
                case AlertType.YesNo:
                    return MessageBoxReturn(MessageBox.Show(this, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question));
                default:
                    throw new Exception();
            }
        }

        public void OpenContextMenu(string id)
        {
            var contextMenu = (ContextMenu)FindResource(id);
            contextMenu.IsOpen = true;
        }

        public void OpenContextMenu(List<string> mainMenuItems)
        {
            throw new NotSupportedException();
        }

        public void OpenAboutPanel()
        {
            throw new NotSupportedException();
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

        public void OpenTextEditor(string textFilePath)
        {
            try
            {
                Process.Start("notepad.exe", textFilePath);
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
            }
        }

        public void OpenAboutPanel(string license)
        {
            throw new NotSupportedException();
        }

        private void GLControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            FramebufferSize = new Vector2i(GLControl.FrameBufferWidth, GLControl.FrameBufferHeight);
            PlayerWindow.OnFramebufferResize(FramebufferSize);

            // Force playback controls to bottom centre when resizing
            playbackControls.Margin = new Thickness(GLControl.ActualWidth / 2 - playbackControls.ActualWidth / 2, 0, 0, Theme.PlaybackControlsMargin);

            // Hide drop area if it overlaps playback controls
            var dropAreaPlaybackControlsDist = dropArea.DistanceTo(playbackControls, this);
            dropArea.Opacity = Math.Clamp(dropAreaPlaybackControlsDist / Theme.DropAreaOpacityMargin, 0.0, 1.0);
        }

        public void LockAspect(Rational ratio)
        {
            Debug.Assert(!lockedContentAspectRatio.HasValue);
            lockedContentAspectRatio = ratio;
            var minContentSize = new Vector2d(playbackControls.ActualWidth + Theme.PlaybackControlsMargin * 2, playbackControls.ActualHeight + Theme.PlaybackControlsMargin * 2);
            minContentSize.X = Math.Max(minContentSize.X, minContentSize.Y * ratio.ToDouble());
            minContentSize.Y = Math.Max(minContentSize.Y, minContentSize.X / ratio.ToDouble());
            var windowDecorationSize = new Vector2d(ActualWidth, ActualHeight) - new Vector2d(GLControl.ActualWidth, GLControl.ActualHeight); 
            var minSize = windowDecorationSize + minContentSize;
            SetValue(MinWidthProperty, minSize.X);
            SetValue(MinHeightProperty, minSize.Y);

            // Change window size to fit aspect immediately
            var contentSize = new Vector2d(GLControl.ActualWidth, GLControl.ActualHeight);
            var aspectCorrectContentSize = new Vector2d(contentSize.Y * ratio.ToDouble(), contentSize.Y);
            var aspectCorrectWindowSize = aspectCorrectContentSize + windowDecorationSize;
            Width = aspectCorrectWindowSize.X;
            Height = aspectCorrectWindowSize.Y;
        }

        public void UnlockAspect()
        {
            Debug.Assert(lockedContentAspectRatio.HasValue);
            lockedContentAspectRatio = null;
            var minContentSize = new Vector2d(playbackControls.ActualWidth + Theme.PlaybackControlsMargin * 2, PlayerMenu.ActualHeight + playbackControls.ActualHeight + Theme.PlaybackControlsMargin * 2);
            var windowDecorationSize = new Vector2d(ActualWidth, ActualHeight) - new Vector2d(GLControl.ActualWidth, GLControl.ActualHeight);
            var minSize = windowDecorationSize + minContentSize;
            SetValue(MinWidthProperty, minSize.X);
            SetValue(MinHeightProperty, minSize.Y);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(sender.GetType() == typeof(Button));
            PlayerWindow.ButtonClick(((Button)sender).Name);
        }

        public void AnimateOutControls()
        {
            List<Control> controls = new List<Control>(){ playButton, pauseButton, fastForwardButton, fastRewindButton, nextButton, previousButton, seekBar };
            controls.ForEach(b => b.Focusable = false);
            Cursor = Cursors.None;
            Debug.Assert(ControlsAnimationState == ControlsAnimationState.In);
            ControlsAnimationState = ControlsAnimationState.Out;
            DoubleAnimation animation = new DoubleAnimation();
            animation.From = playbackControls.Opacity;
            animation.To = 0;
            animation.Duration = new Duration(Theme.ControlsAnimation);
            animation.AutoReverse = false;
            animation.RepeatBehavior = new RepeatBehavior(1);
            playbackControls.BeginAnimation(OpacityProperty, animation);
            PlayerMenu.BeginAnimation(OpacityProperty, animation);
        }

        public void AnimateInControls()
        {
            List<Control> controls = new List<Control>() { playButton, pauseButton, fastForwardButton, fastRewindButton, nextButton, previousButton, seekBar };
            controls.ForEach(b => b.Focusable = true);
            Cursor = Cursors.Arrow;
            Debug.Assert(ControlsAnimationState == ControlsAnimationState.Out);
            ControlsAnimationState = ControlsAnimationState.In;
            DoubleAnimation animation = new DoubleAnimation();
            animation.From = playbackControls.Opacity;
            animation.To = 1;
            animation.Duration = new Duration(Theme.ControlsAnimation);
            animation.AutoReverse = false;
            animation.RepeatBehavior = new RepeatBehavior(1);
            playbackControls.BeginAnimation(OpacityProperty, animation);
            PlayerMenu.BeginAnimation(OpacityProperty, animation);
        }

        public void InvokeOnUIThread(Action action, bool async = true)
        {
            if (async)
                Application.Current.Dispatcher.BeginInvoke(action);
            else
                Application.Current.Dispatcher.Invoke(action);
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("user32.dll")]
        internal static extern bool GetClientRect(IntPtr hwnd, ref RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            internal int left;
            internal int top;
            internal int right;
            internal int bottom;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Manually fit the grid to the window client area
            // This fixes a pixel gap bug
            RECT rect = new RECT();
            if ( !hwnd.HasValue )
                hwnd = new WindowInteropHelper(this).Handle;
            GetClientRect(hwnd.Value, ref rect);
            var clientSize = TransformPixelToLogical(new Vector2i(rect.right - rect.left, rect.bottom - rect.top));
            if (clientSize.HasValue)
            {
                PlayerGrid.Width = clientSize.Value.X + 0.5;
                PlayerGrid.Height = clientSize.Value.Y + 0.5;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var minContentSize = new Vector2d(playbackControls.ActualWidth + Theme.PlaybackControlsMargin * 2, PlayerMenu.ActualHeight + playbackControls.ActualHeight + Theme.PlaybackControlsMargin * 2);
            var windowDecorationSize = new Vector2d(ActualWidth, ActualHeight) - new Vector2d(GLControl.ActualWidth, GLControl.ActualHeight);
            var minSize = windowDecorationSize + minContentSize;
            SetValue(MinWidthProperty, minSize.X);
            SetValue(MinHeightProperty, minSize.Y);
            PlayerWindow.OnLoad();
        }

        private void Slider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            Trace.WriteLine("Slider_DragStarted");
            Debug.Assert(sender.GetType() == typeof(Slider));
            PlayerWindow.SliderDragStart(((Slider)sender).Name);

            Debug.Assert(!activeSliders.Contains((Slider)sender));
            activeSliders.Add((Slider)sender);
        }

        private void Slider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            Trace.WriteLine("Slider_DragCompleted");
            Debug.Assert(sender.GetType() == typeof(Slider));
            var slider = (Slider)sender;
            PlayerWindow.SliderDragComplete(slider.Name, slider.Value);

            activeSliders.Remove((Slider)sender);
        }

        private void Slider_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            Debug.Assert(sender.GetType() == typeof(Slider));
            var slider = (Slider)sender;
            PlayerWindow.SliderDragDelta(slider.Name, slider.Value);
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Debug.Assert(sender.GetType() == typeof(Slider));
            var slider = (Slider)sender;

            // Only fire callback if slider isnt actively from another source
            if ( !activeSliders.Contains(slider))
                PlayerWindow.SliderSetValue(slider.Name, e.NewValue);
        }
    }
}
