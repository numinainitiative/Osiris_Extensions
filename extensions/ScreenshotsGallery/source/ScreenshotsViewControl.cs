using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Playnite.SDK;
using Playnite.SDK.Controls;
using Playnite.SDK.Models;

namespace Osiris.Extensions.ScreenshotsGallery
{
    public sealed class ScreenshotsViewControl : PluginUserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty HasScreenshotsProperty = DependencyProperty.Register(
            nameof(HasScreenshots), typeof(bool), typeof(ScreenshotsViewControl), new PropertyMetadata(false));
        public bool HasScreenshots { get => (bool)GetValue(HasScreenshotsProperty); private set => SetValue(HasScreenshotsProperty, value); }
        public Action<Window> ConfigureExpandedWindow { get; set; }
        public Func<Button> CreateExpandedCloseButton { get; set; }
        public Func<object, FrameworkElement> CreateExpandedNavigationControls { get; set; }
        public ObservableCollection<ScreenshotItem> MediaItems { get; } = new ObservableCollection<ScreenshotItem>();
        public ScreenshotItem SelectedMediaItem
        {
            get => index >= 0 && index < MediaItems.Count ? MediaItems[index] : null;
            set { var selected = MediaItems.IndexOf(value); if (selected >= 0 && selected != index) Select(selected); }
        }
        public BitmapSource CurrentImageBitmap => currentImage.Source as BitmapSource;
        public string CaptureDateText => index >= 0 && index < files.Length ? files[index].LastWriteTime.ToString("g") : "";
        public ICommand OpenScreenshotsViewCommand { get; }
        public ICommand SelectPreviousScreenshotCommand { get; }
        public ICommand SelectNextScreenshotCommand { get; }
        public event PropertyChangedEventHandler PropertyChanged;
        private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        public bool UseCinematicExpandedExperience => mode() == "Cinematic";
        public ICommand OpenFolderCommand { get; }
        private readonly string profile;
        private readonly Func<string> mode;
        private readonly Image currentImage = new Image { Stretch = Stretch.Uniform };
        private readonly Image nextImage = new Image { Stretch = Stretch.Uniform };
        private readonly Grid stage = new Grid { ClipToBounds = true, Background = Brushes.Black };
        private readonly ColumnDefinition previewColumn = new ColumnDefinition { Width = new GridLength(0.35, GridUnitType.Star) };
        private readonly Button previous = Arrow(false);
        private readonly Button next = Arrow(true);
        private readonly TextBlock caption = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(119, 122, 130)), FontSize = 16, Margin = new Thickness(0, 8, 0, 0) };
        private readonly DispatcherTimer refresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        private FileInfo[] files = new FileInfo[0];
        private Guid gameId;
        private string signature;
        private int generation, imageRequest, index;
        private bool loading;

        public ScreenshotsViewControl(string profile, Func<string> mode)
        {
            this.profile = profile;
            this.mode = mode;
            OpenFolderCommand = new ActionCommand(OpenFolder);
            OpenScreenshotsViewCommand = new ActionCommand(() => OpenExpanded(index));
            SelectPreviousScreenshotCommand = new ActionCommand(() => Select(index - 1));
            SelectNextScreenshotCommand = new ActionCommand(() => Select(index + 1));
            stage.ColumnDefinitions.Add(new ColumnDefinition());
            stage.ColumnDefinitions.Add(previewColumn);
            var main = ImageButton(currentImage);
            main.Click += (_, __) => OpenExpanded(index);
            stage.Children.Add(main);
            var preview = ImageButton(nextImage);
            preview.Margin = new Thickness(16, 0, 0, 0);
            preview.Click += (_, __) => { if (index + 1 < files.Length) OpenExpanded(index + 1); };
            Grid.SetColumn(preview, 1);
            stage.Children.Add(preview);
            previous.HorizontalAlignment = HorizontalAlignment.Left;
            next.HorizontalAlignment = HorizontalAlignment.Right;
            previous.Click += (_, __) => Select(index - 1);
            next.Click += (_, __) => Select(index + 1);
            stage.Children.Add(previous);
            stage.Children.Add(next);
            var body = new StackPanel { Margin = new Thickness(16, 10, 16, 16) };
            body.Children.Add(stage);
            body.Children.Add(caption);
            Content = body;
            SizeChanged += (_, __) => ResizeStage();
            refresh.Tick += (_, __) => Refresh();
            Loaded += (_, __) => { refresh.Start(); Refresh(); };
            Unloaded += (_, __) => { refresh.Stop(); generation++; signature = null; };
        }

        public override void GameContextChanged(Game oldContext, Game newContext)
        {
            gameId = newContext?.Id ?? Guid.Empty;
            generation++;
            imageRequest++;
            signature = null;
            files = new FileInfo[0];
            HasScreenshots = false;
            currentImage.Source = nextImage.Source = null;
            MediaItems.Clear();
            Changed(nameof(CurrentImageBitmap));
            Changed(nameof(CaptureDateText));
            // A collapsed card still needs to discover its first screenshot.
            Refresh();
        }

        private void ResizeStage()
        {
            var width = Math.Max(0, ActualWidth - 32);
            stage.Height = Math.Max(150, width / (files.Length > 1 ? 1.35 : 1) * 9 / 16);
        }

        private async void Refresh()
        {
            if (loading || gameId == Guid.Empty) return;
            loading = true;
            var request = generation;
            var folder = ScreenshotCollector.GameFolder(profile, gameId);
            try
            {
                var discovered = await Task.Run(() => ScreenshotCollector.IsEnabled(profile, gameId) && Directory.Exists(folder)
                    ? new DirectoryInfo(folder).GetFiles("*.png").OrderByDescending(f => f.LastWriteTimeUtc).ThenBy(f => f.Name).ToArray()
                    : new FileInfo[0]);
                if (request != generation) return;
                var fingerprint = string.Join("|", discovered.Select(f => f.Name + ":" + f.Length + ":" + f.LastWriteTimeUtc.Ticks));
                if (signature == fingerprint) return;
                var thumbnails = await Task.Run(() => discovered.Select(f => new ScreenshotItem
                {
                    ThumbnailImage = LoadImage(f.FullName, 180)
                }).ToArray());
                if (request != generation) return;
                files = discovered;
                MediaItems.Clear();
                foreach (var thumbnail in thumbnails) MediaItems.Add(thumbnail);
                HasScreenshots = files.Length > 0;
                signature = fingerprint;
                index = 0;
                previewColumn.Width = files.Length > 1 ? new GridLength(0.35, GridUnitType.Star) : new GridLength(0);
                ResizeStage();
                Select(0);
            }
            catch (Exception error) { LogManager.GetLogger().Warn("Screenshots Gallery refresh failed: " + error.Message); }
            finally { loading = false; }
        }

        private async void Select(int selected)
        {
            if (selected < 0 || selected >= files.Length)
            {
                if (files.Length == 0) { currentImage.Source = nextImage.Source = null; Changed(nameof(CurrentImageBitmap)); Changed(nameof(CaptureDateText)); }
                return;
            }
            index = selected;
            Changed(nameof(SelectedMediaItem));
            previous.IsEnabled = index > 0;
            next.IsEnabled = index + 1 < files.Length;
            previous.Visibility = next.Visibility = files.Length > 1 ? Visibility.Visible : Visibility.Collapsed;
            caption.Text = files[index].LastWriteTime.ToString("g") + " · " + (index + 1) + " / " + files.Length;
            var path = files[index].FullName;
            var previewPath = index + 1 < files.Length ? files[index + 1].FullName : null;
            var request = ++imageRequest;
            var images = await Task.Run(() => new[] { LoadImage(path, 1600), previewPath == null ? null : LoadImage(previewPath, 640) });
            if (request != imageRequest) return;
            currentImage.Source = images[0];
            nextImage.Source = images[1];
            Changed(nameof(CurrentImageBitmap));
            Changed(nameof(CaptureDateText));
            if (images[0] == null) signature = null; // Retry a file still being written.
        }

        private async void OpenExpanded(int selected)
        {
            if (selected < 0 || selected >= files.Length) return;
            var cinematic = UseCinematicExpandedExperience;
            var snapshot = files.Select(f => f.FullName).ToArray();
            var dates = files.Select(f => f.LastWriteTime.ToString("g")).ToArray();
            var context = generation;
            var initial = await Task.Run(() => LoadImage(snapshot[selected], 0));
            if (initial == null || context != generation || !IsLoaded) return;
            var model = new ExpandedImage { BitmapImageA = initial };
            var image = new Image { Source = initial, Stretch = Stretch.Uniform };
            var content = new Grid { Background = Brushes.Black };
            content.Children.Add(image);
            var dateLabel = new TextBlock { Text = dates[selected], Foreground = Brushes.White, FontSize = 17, FontWeight = FontWeights.SemiBold, FontFamily = new FontFamily("Rajdhani") };
            var dateOverlay = new Border { Child = dateLabel, Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
                Padding = new Thickness(10, 4, 10, 4), CornerRadius = new CornerRadius(4), HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false };
            content.Children.Add(dateOverlay);
            Action positionDate = () =>
            {
                var bitmap = image.Source;
                if (bitmap == null || bitmap.Width <= 0 || bitmap.Height <= 0) return;
                var scale = Math.Min(content.ActualWidth / bitmap.Width, content.ActualHeight / bitmap.Height);
                dateOverlay.Margin = new Thickness(Math.Max(0, (content.ActualWidth - bitmap.Width * scale) / 2) + 16, 0, 0,
                    Math.Max(0, (content.ActualHeight - bitmap.Height * scale) / 2) + 16);
            };
            content.SizeChanged += (_, __) => positionDate();
            var left = Arrow(false);
            var right = Arrow(true);
            left.HorizontalAlignment = HorizontalAlignment.Left;
            right.HorizontalAlignment = HorizontalAlignment.Right;
            var close = CreateExpandedCloseButton?.Invoke() ?? Arrow(true);
            content.Children.Add(close);
            var window = new Window { Title = "Screenshots Gallery", Owner = Application.Current.MainWindow, Content = content,
                DataContext = model, Background = Brushes.Black, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Width = 1100, Height = 700,
                WindowState = cinematic ? WindowState.Normal : WindowState.Maximized };
            var alive = true;
            var request = 0;
            Action updateButtons = () => { model.ImagePositionLabel = (selected + 1) + " / " + snapshot.Length; };
            Action<int> navigate = async delta =>
            {
                var target = (selected + delta + snapshot.Length) % snapshot.Length;
                selected = target;
                updateButtons();
                var version = ++request;
                var bitmap = await Task.Run(() => LoadImage(snapshot[target], 0));
                if (!alive || version != request || bitmap == null) return;
                image.Source = bitmap;
                dateLabel.Text = dates[target];
                positionDate();
                model.BitmapImageA = bitmap;
            };
            left.Click += (_, __) => navigate(-1);
            right.Click += (_, __) => navigate(1);
            model.BackCommand = new ActionCommand(() => navigate(-1));
            model.NextCommand = new ActionCommand(() => navigate(1));
            model.HasMultipleImages = snapshot.Length > 1;
            var controls = CreateExpandedNavigationControls?.Invoke(model);
            if (controls != null) content.Children.Add(controls);
            else { content.Children.Add(left); content.Children.Add(right); }
            var idle = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.2) };
            Action showControls = () =>
            {
                close.Visibility = Visibility.Visible;
                if (controls != null) controls.Visibility = Visibility.Visible;
                idle.Stop(); idle.Start();
            };
            idle.Tick += (_, __) => { if (content.IsMouseCaptureWithin) return; idle.Stop(); close.Visibility = Visibility.Hidden; if (controls != null) controls.Visibility = Visibility.Hidden; };
            Point? pointer = null;
            content.PreviewMouseMove += (_, e) => { var point = e.GetPosition(content); if (pointer.HasValue && (pointer.Value - point).Length < 1) return; pointer = point; showControls(); };
            content.PreviewMouseDown += (_, __) => showControls();
            content.PreviewKeyDown += (_, __) => showControls();
            close.Click += (_, __) => window.Close();
            window.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) window.Close();
                else if (e.Key == Key.Left) navigate(-1);
                else if (e.Key == Key.Right) navigate(1);
            };
            window.Closed += (_, __) => alive = false;
            updateButtons();
            ConfigureExpandedWindow?.Invoke(window);
            window.Loaded += (_, __) => showControls();
            try { window.ShowDialog(); }
            finally { idle.Stop(); alive = false; window.Close(); image.Source = null; model.BitmapImageA = null; }
            if (context == generation) Select(selected);
        }

        private static BitmapSource LoadImage(string path, int width)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var image = new BitmapImage();
                    image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
                    if (width > 0) image.DecodePixelWidth = width;
                    image.StreamSource = stream; image.EndInit(); image.Freeze();
                    return image;
                }
            }
            catch { return null; }
        }

        private void OpenFolder()
        {
            if (gameId == Guid.Empty || !HasScreenshots) return;
            try { Process.Start(new ProcessStartInfo(ScreenshotCollector.GameFolder(profile, gameId)) { UseShellExecute = true }); }
            catch (Exception error) { LogManager.GetLogger().Warn("Screenshots Gallery could not open folder: " + error.Message); }
        }

        private static Button ImageButton(Image image)
        {
            var template = new ControlTemplate(typeof(Button));
            template.VisualTree = new FrameworkElementFactory(typeof(ContentPresenter));
            return new Button { Content = image, Template = template, Padding = new Thickness(0), Cursor = Cursors.Hand,
                Background = Brushes.Black, BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch };
        }

        private static Button Arrow(bool right)
        {
            var path = new System.Windows.Shapes.Path { Data = Geometry.Parse(right ? "M 9,5 L 16,12 L 9,19" : "M 15,5 L 8,12 L 15,19"),
                Stroke = Brushes.White, StrokeThickness = 2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round, Width = 24, Height = 24 };
            return new Button { Content = path, Width = 36, Height = 42, Margin = new Thickness(12), Background = Brushes.Black,
                BorderBrush = new SolidColorBrush(Color.FromRgb(119, 122, 130)), BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center };
        }

        private sealed class ActionCommand : ICommand
        {
            private readonly Action action;
            public ActionCommand(Action action) { this.action = action; }
            public bool CanExecute(object parameter) => true;
            public void Execute(object parameter) => action();
            public event EventHandler CanExecuteChanged { add { } remove { } }
        }
        public sealed class ScreenshotItem
        {
            public BitmapSource ThumbnailImage { get; set; }
            public bool IsVideo => false;
        }
        private sealed class ExpandedImage : INotifyPropertyChanged
        {
            public ICommand BackCommand { get; set; }
            public ICommand NextCommand { get; set; }
            public bool HasMultipleImages { get; set; }
            private string position;
            public string ImagePositionLabel { get => position; set { position = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImagePositionLabel))); } }
            private BitmapSource image;
            public BitmapSource BitmapImageA { get => image; set { image = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BitmapImageA))); } }
            public event PropertyChangedEventHandler PropertyChanged;
        }
    }
}
