using Playnite.SDK;
using PluginsCommon.Converters;
using System;
using System.Collections.Generic;
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
using System.Windows.Threading;

namespace SteamScreenshots.Screenshots
{
    /// <summary>
    /// Interaction logic for ScreenshotsView.xaml
    /// </summary>
    public partial class ScreenshotsView : UserControl
    {
        private DateTime _lastClickTime = DateTime.MinValue;
        private const int DoubleClickTimeLimit = 500;
        private readonly DispatcherTimer _controlsIdleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.2) };
        private Point? _lastPointerPosition;
        private Window _viewerWindow;

        public ScreenshotsView()
        {
            SetControlTextBlockStyle();
            Loaded += ScreenshotsView_Loaded;
            Unloaded += ScreenshotsView_Unloaded;
            Focusable = true;
            InitializeComponent();
            _controlsIdleTimer.Tick += ControlsIdleTimer_Tick;
            PreviewMouseMove += ScreenshotsView_PreviewMouseMove;
            PreviewMouseDown += (sender, args) => ShowControls();
            PreviewMouseWheel += (sender, args) => ShowControls();
            PreviewKeyDown += (sender, args) => ShowControls();
        }

        private void ScreenshotsView_Unloaded(object sender, RoutedEventArgs e)
        {
            DetachViewerWindow();
        }

        private void DetachViewerWindow()
        {
            _controlsIdleTimer.Stop();
            _lastPointerPosition = null;
            if (_viewerWindow != null) _viewerWindow.Closed -= ViewerWindow_Closed;
            _viewerWindow = null;
        }

        private void ViewerWindow_Closed(object sender, EventArgs args) => DetachViewerWindow();

        private void ScreenshotsView_Loaded(object sender, RoutedEventArgs e)
        {
            DetachViewerWindow();
            _viewerWindow = Window.GetWindow(this);
            if (_viewerWindow != null) _viewerWindow.Closed += ViewerWindow_Closed;
            Focus();
            Keyboard.Focus(this);
            ShowControls();
        }

        private void ShowControls()
        {
            GalleryNavigationControls.Visibility = Visibility.Visible;
            GalleryCloseButton.Visibility = Visibility.Visible;
            _controlsIdleTimer.Stop();
            if (IsLoaded) _controlsIdleTimer.Start();
        }

        private void ControlsIdleTimer_Tick(object sender, EventArgs args)
        {
            if (IsMouseCaptureWithin) return;
            _controlsIdleTimer.Stop();
            GalleryNavigationControls.Visibility = Visibility.Hidden;
            GalleryCloseButton.Visibility = Visibility.Hidden;
        }

        private void ScreenshotsView_PreviewMouseMove(object sender, MouseEventArgs args)
        {
            // WPF can raise MouseMove on layout changes without actual pointer movement.
            var position = args.GetPosition(this);
            if (_lastPointerPosition.HasValue && (_lastPointerPosition.Value - position).Length < 1) return;
            _lastPointerPosition = position;
            ShowControls();
        }

        private void SetControlTextBlockStyle()
        {
            // Desktop mode uses BaseTextBlockStyle and Fullscreen Mode uses TextBlockBaseStyle
            var baseStyleName = API.Instance.ApplicationInfo.Mode == ApplicationMode.Desktop ? "BaseTextBlockStyle" : "TextBlockBaseStyle";
            if (ResourceProvider.GetResource(baseStyleName) is Style baseStyle && baseStyle.TargetType == typeof(TextBlock))
            {
                var implicitStyle = new Style(typeof(TextBlock), baseStyle);
                Resources.Add(typeof(TextBlock), implicitStyle);
            }
        }

        private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var currentTime = DateTime.Now;
            var elapsedMilliseconds = (currentTime - _lastClickTime).TotalMilliseconds;

            if (elapsedMilliseconds < DoubleClickTimeLimit)
            {
                if (DataContext is ScreenshotsViewModel viewModel && viewModel.CloseWindowCommand.CanExecute(null))
                {
                    viewModel.CloseWindowCommand.Execute(null);
                }
            }

            _lastClickTime = currentTime;
        }
    }
}
