using FlowHttp;
using Playnite.SDK;
using Playnite.SDK.Controls;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using PluginsCommon;
using PluginsCommon.Converters;
using SteamCommon;
using SteamCommon.Models;
using SteamScreenshots.Application.Services;
using SteamScreenshots.Domain.Enums;
using SteamScreenshots.Domain.ValueObjects;
using SteamScreenshots.Screenshots;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace SteamScreenshots.ScreenshotsControl
{
    /// <summary>
    /// Interaction logic for SteamScreenshotsControl.xaml
    /// </summary>
    public partial class SteamScreenshotsControl : PluginUserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private static readonly ILogger _logger = LogManager.GetLogger();
        private readonly ScreenshotManagementService _screenshotManagementService;
        private readonly IPlayniteAPI _playniteApi;
        private readonly SteamScreenshotsSettingsViewModel _settingsViewModel;
        private readonly DesktopView _activeViewAtCreation;
        private readonly DispatcherTimer _updateControlDataDelayTimer;
        private FileSystemWatcher _localMediaWatcher;
        private CancellationTokenSource _loadCancellation;
        private CancellationTokenSource _selectionCancellation;
        private int _selectionLoadVersion;

        private static readonly Regex LocalScreenshotPattern = new Regex(
            @"^GalleryImage(\d*)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex LocalTrailerPattern = new Regex(
            @"^Trailer(\d*)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private bool _screenshotsFullBitmapImagesAccessed = false;
        private bool _isValuesDefaultState = true;
        private Game _currentGame;
        private Guid _activeContext = default;

        private ObservableCollection<Screenshot> _screenshots = new ObservableCollection<Screenshot>();
        private ObservableCollection<GalleryMediaItem> _mediaItems = new ObservableCollection<GalleryMediaItem>();
        private BitmapImage _currentImageBitmap;
        private Screenshot _selectedScreenshot;
        private GalleryMediaItem _selectedMediaItem;
        private string _currentTrailerUrl;
        private bool _isTrailerSelected;
        private string _galleryStatusText = "Loading gallery...";
        private bool _isGalleryEnabled = true;
        private string _gallerySource = "Steam";
        private string _gallerySteamAppId;


        public ObservableCollection<Screenshot> Screenshots
        {
            get => _screenshots;
            set
            {
                _screenshots = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<GalleryMediaItem> MediaItems
        {
            get => _mediaItems;
            set
            {
                _mediaItems = value;
                OnPropertyChanged();
            }
        }

        public BitmapImage CurrentImageBitmap
        {
            get => _currentImageBitmap;
            set
            {
                _currentImageBitmap = value;
                OnPropertyChanged();
            }
        }

        public Screenshot SelectedScreenshot
        {
            get => _selectedScreenshot;
            set
            {
                if (_selectedScreenshot != value)
                {
                    var previousScreenshot = _selectedScreenshot;
                    _selectedScreenshot = value;
                    BeginLoadSelectedScreenshot(_selectedScreenshot, previousScreenshot);
                    OnPropertyChanged();
                }
            }
        }

        public GalleryMediaItem SelectedMediaItem
        {
            get => _selectedMediaItem;
            set
            {
                if (_selectedMediaItem == value)
                {
                    return;
                }

                _selectedMediaItem = value;
                if (value is Trailer trailer)
                {
                    CancelSelectionLoad();
                    var previousScreenshot = _selectedScreenshot;
                    _selectedScreenshot = null;
                    CurrentImageBitmap = null;
                    previousScreenshot?.ReleaseStageImage();
                    ReleaseStageImagesExcept(null);
                    IsTrailerSelected = true;
                    CurrentTrailerUrl = trailer.VideoUrl;
                    OnPropertyChanged(nameof(SelectedScreenshot));
                }
                else
                {
                    IsTrailerSelected = false;
                    CurrentTrailerUrl = null;
                    SelectedScreenshot = value as Screenshot;
                }

                OnPropertyChanged();
            }
        }

        public string CurrentTrailerUrl
        {
            get => _currentTrailerUrl;
            private set
            {
                _currentTrailerUrl = value;
                OnPropertyChanged();
            }
        }

        public bool IsTrailerSelected
        {
            get => _isTrailerSelected;
            private set
            {
                _isTrailerSelected = value;
                OnPropertyChanged();
            }
        }

        public string GalleryStatusText
        {
            get => _galleryStatusText;
            private set
            {
                _galleryStatusText = value;
                OnPropertyChanged();
            }
        }

        public bool IsGalleryEnabled
        {
            get => _isGalleryEnabled;
            private set
            {
                _isGalleryEnabled = value;
                OnPropertyChanged();
            }
        }

        public string GallerySource
        {
            get => _gallerySource;
            private set
            {
                _gallerySource = value;
                OnPropertyChanged();
            }
        }

        public IEnumerable<BitmapImage> ScreenshotsBitmapImages => _screenshots.Select(x => x.ThumbnailImage);

        public IEnumerable<BitmapImage> ScreenshotsFullBitmapImages
        {
            get
            {
                _screenshotsFullBitmapImagesAccessed = true;
                return _screenshots.Select(x => x.FullImage);
            }
        }

        public List<Screenshot> ScreenshotsForExport
        {
            get
            {
                _screenshotsFullBitmapImagesAccessed = true;
                return _screenshots.ToList();
            }
        }

        public RelayCommand OpenScreenshotsViewCommand { get; }
        public RelayCommand SelectPreviousScreenshotCommand { get; }
        public RelayCommand SelectNextScreenshotCommand { get; }

        // Osiris supplies its standard window chrome without coupling the plugin to the theme assembly.
        public Action<Window> ConfigureExpandedWindow { get; set; }
        public bool UseCinematicExpandedExperience => _settingsViewModel.Settings.ExpandedExperience == "Cinematic";


        public SteamScreenshotsControl(SteamScreenshotsSettingsViewModel settingsViewModel, ScreenshotManagementService screenshotManagementService)
        {
            _screenshotManagementService = screenshotManagementService;
            _settingsViewModel = settingsViewModel;
            _playniteApi = API.Instance;


            SetControlTextBlockStyle();

            if (_playniteApi.ApplicationInfo.Mode == ApplicationMode.Desktop)
            {
                _activeViewAtCreation = _playniteApi.MainView.ActiveDesktopView;
            }

            _updateControlDataDelayTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(700)
            };
            _updateControlDataDelayTimer.Tick += new EventHandler(UpdateControlData);

            OpenScreenshotsViewCommand = new RelayCommand(() => OpenScreenshotsView(_selectedMediaItem as Screenshot));
            SelectPreviousScreenshotCommand = new RelayCommand(() => SelectPreviousImageScreenshot());
            SelectNextScreenshotCommand = new RelayCommand(() => SelectNextScreenshot());

            InitializeComponent();
            AddKeyBindings();
            Unloaded += OnControlUnloaded;

            DataContext = this;
        }


        private void AddKeyBindings()
        {
            var leftKeyBinding = new KeyBinding
            {
                Key = Key.Left,
                Command = SelectPreviousScreenshotCommand
            };
            InputBindings.Add(leftKeyBinding);

            var rightKeyBinding = new KeyBinding
            {
                Key = Key.Right,
                Command = SelectNextScreenshotCommand
            };
            InputBindings.Add(rightKeyBinding);

            ScreenshotsListBox.PreviewMouseWheel += WheelHandler;
        }

        private void WheelHandler(object s, MouseWheelEventArgs e)
        {
            if (e.Delta > 0) // Scroll up
            {
                if (SelectPreviousScreenshotCommand?.CanExecute(null) == true)
                {
                    SelectPreviousScreenshotCommand.Execute(null);
                }

                e.Handled = true;
            }
            else if (e.Delta < 0) // Scroll down
            {
                if (SelectNextScreenshotCommand?.CanExecute(null) == true)
                {
                    SelectNextScreenshotCommand.Execute(null);
                }

                e.Handled = true;
            }
        }

        private void SetControlTextBlockStyle()
        {
            // Desktop mode uses BaseTextBlockStyle and Fullscreen Mode uses TextBlockBaseStyle
            var baseStyleName = _playniteApi.ApplicationInfo.Mode == ApplicationMode.Desktop ? "BaseTextBlockStyle" : "TextBlockBaseStyle";
            if (ResourceProvider.GetResource(baseStyleName) is Style baseStyle && baseStyle.TargetType == typeof(TextBlock))
            {
                var implicitStyle = new Style(typeof(TextBlock), baseStyle);
                Resources.Add(typeof(TextBlock), implicitStyle);
            }
        }

        private async void UpdateControlData(object sender, EventArgs e)
        {
            _updateControlDataDelayTimer.Stop();
            var cancellationToken = _loadCancellation?.Token ?? CancellationToken.None;
            try
            {
                await UpdateControlAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // A game or page change superseded this gallery request.
            }
        }

        private void SetCollapsedVisibility()
        {
            Visibility = Visibility.Collapsed;
            _settingsViewModel.Settings.IsControlVisible = false;
        }

        private void SetVisibleVisibility()
        {
            OnPropertyChanged(nameof(ScreenshotsBitmapImages));
            OnPropertyChanged(nameof(ScreenshotsFullBitmapImages));
            OnPropertyChanged(nameof(ScreenshotsForExport));
            Visibility = Visibility.Visible;
            _settingsViewModel.Settings.IsControlVisible = true;
        }

        public override void GameContextChanged(Game oldContext, Game newContext)
        {
            //The GameContextChanged method is rised even when the control
            //is not in the active view. To prevent unecessary processing we
            //can stop processing if the active view is not the same one was
            //the one during creation
            CancelPendingLoads();
            if (_playniteApi.ApplicationInfo.Mode == ApplicationMode.Desktop && _activeViewAtCreation != _playniteApi.MainView.ActiveDesktopView)
            {
                return;
            }

            if (!_isValuesDefaultState)
            {
                ResetToDefaultValues();
            }

            if (newContext is null)
            {
                return;
            }

            _currentGame = newContext;
            var displaySettings = LoadGalleryDisplaySettings(newContext);
            IsGalleryEnabled = displaySettings.Enabled;
            GallerySource = displaySettings.Source;
            _gallerySteamAppId = displaySettings.SteamAppId;
            if (!IsGalleryEnabled)
            {
                GalleryStatusText = null;
                DisposeLocalMediaWatcher();
                SetCollapsedVisibility();
                return;
            }
            _loadCancellation = new CancellationTokenSource();
            GalleryStatusText = "Loading gallery...";
            if (string.Equals(GallerySource, "Local", StringComparison.OrdinalIgnoreCase))
                ConfigureLocalMediaWatcher(newContext);
            else
                DisposeLocalMediaWatcher();
            _updateControlDataDelayTimer.Start();
        }

        private GalleryGameSettings LoadGalleryDisplaySettings(Game game)
        {
            var settings = new GalleryGameSettings { Enabled = true, Source = "Steam" };
            try
            {
                var directory = _playniteApi.Database.GetFileStoragePath(game.Id);
                var path = Path.Combine(directory, "OsirisGallery.ini");
                if (!File.Exists(path)) return settings;
                foreach (var line in File.ReadAllLines(path))
                {
                    var separator = line.IndexOf('=');
                    if (separator <= 0) continue;
                    var key = line.Substring(0, separator).Trim();
                    var value = line.Substring(separator + 1).Trim();
                    if (string.Equals(key, "Enabled", StringComparison.OrdinalIgnoreCase))
                    {
                        bool enabled;
                        if (bool.TryParse(value, out enabled)) settings.Enabled = enabled;
                    }
                    else if (string.Equals(key, "Source", StringComparison.OrdinalIgnoreCase))
                    {
                        settings.Source = string.Equals(value, "Local", StringComparison.OrdinalIgnoreCase)
                            ? "Local"
                            : "Steam";
                    }
                    else if (string.Equals(key, "SteamAppId", StringComparison.OrdinalIgnoreCase))
                    {
                        long steamAppId;
                        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out steamAppId) &&
                            steamAppId > 0)
                        {
                            settings.SteamAppId = steamAppId.ToString(CultureInfo.InvariantCulture);
                        }
                    }
                }
            }
            catch
            {
            }
            return settings;
        }

        private void ConfigureLocalMediaWatcher(Game game)
        {
            DisposeLocalMediaWatcher();

            var directory = _playniteApi.Database.GetFileStoragePath(game.Id);
            if (string.IsNullOrEmpty(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);

            _localMediaWatcher = new FileSystemWatcher(directory)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };
            _localMediaWatcher.Created += OnLocalMediaChanged;
            _localMediaWatcher.Changed += OnLocalMediaChanged;
            _localMediaWatcher.Deleted += OnLocalMediaChanged;
            _localMediaWatcher.Renamed += OnLocalMediaRenamed;
            _localMediaWatcher.EnableRaisingEvents = true;
        }

        private void OnLocalMediaChanged(object sender, FileSystemEventArgs args)
        {
            if (IsLocalGalleryMedia(args.Name))
            {
                QueueLocalMediaRefresh();
            }
        }

        private void OnLocalMediaRenamed(object sender, RenamedEventArgs args)
        {
            if (IsLocalGalleryMedia(args.Name) || IsLocalGalleryMedia(args.OldName))
            {
                QueueLocalMediaRefresh();
            }
        }

        private void QueueLocalMediaRefresh()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                CancelPendingLoads();
                _loadCancellation = new CancellationTokenSource();
                _updateControlDataDelayTimer.Start();
            }));
        }

        private static bool IsLocalGalleryMedia(string fileName)
        {
            var name = Path.GetFileNameWithoutExtension(fileName ?? string.Empty);
            return LocalScreenshotPattern.IsMatch(name) || LocalTrailerPattern.IsMatch(name);
        }

        private void OnControlUnloaded(object sender, RoutedEventArgs args)
        {
            CancelPendingLoads();
            DisposeLocalMediaWatcher();
        }

        private void CancelPendingLoads()
        {
            _updateControlDataDelayTimer.Stop();
            CancelSelectionLoad();
            if (_loadCancellation != null)
            {
                _loadCancellation.Cancel();
                _loadCancellation.Dispose();
                _loadCancellation = null;
            }
        }

        private void CancelSelectionLoad()
        {
            _selectionLoadVersion++;
            if (_selectionCancellation != null)
            {
                _selectionCancellation.Cancel();
                _selectionCancellation.Dispose();
                _selectionCancellation = null;
            }
        }

        private async void BeginLoadSelectedScreenshot(
            Screenshot screenshot,
            Screenshot previousScreenshot = null)
        {
            CancelSelectionLoad();
            CurrentImageBitmap = null;
            if (previousScreenshot != null && !ReferenceEquals(previousScreenshot, screenshot))
            {
                previousScreenshot.ReleaseStageImage();
            }
            if (screenshot == null)
            {
                return;
            }

            var parentToken = _loadCancellation?.Token ?? CancellationToken.None;
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
            _selectionCancellation = cancellation;
            var version = _selectionLoadVersion;
            try
            {
                var bitmap = await Task.Run(
                    () => screenshot.GetStageImage(cancellation.Token),
                    cancellation.Token);
                if (!cancellation.IsCancellationRequested &&
                    version == _selectionLoadVersion &&
                    ReferenceEquals(_selectedScreenshot, screenshot))
                {
                    CurrentImageBitmap = bitmap;
                    ReleaseStageImagesExcept(screenshot);
                }
                else
                {
                    screenshot.ReleaseStageImage();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                _logger.Warn(exception, "Gallery image could not be loaded.");
            }
            finally
            {
                if (ReferenceEquals(_selectionCancellation, cancellation))
                {
                    _selectionCancellation.Dispose();
                    _selectionCancellation = null;
                }
            }
        }

        private void DisposeLocalMediaWatcher()
        {
            if (_localMediaWatcher == null)
            {
                return;
            }

            _localMediaWatcher.EnableRaisingEvents = false;
            _localMediaWatcher.Created -= OnLocalMediaChanged;
            _localMediaWatcher.Changed -= OnLocalMediaChanged;
            _localMediaWatcher.Deleted -= OnLocalMediaChanged;
            _localMediaWatcher.Renamed -= OnLocalMediaRenamed;
            _localMediaWatcher.Dispose();
            _localMediaWatcher = null;
        }

        private void ResetToDefaultValues()
        {
            SetCollapsedVisibility();
            _activeContext = default;
            ReleaseTransientImages();
            Screenshots.Clear();
            MediaItems.Clear();
            SelectedMediaItem = null;
            CurrentImageBitmap = null;
            CurrentTrailerUrl = null;
            IsTrailerSelected = false;
            GalleryStatusText = "Loading gallery...";
            OnPropertyChanged(nameof(ScreenshotsBitmapImages));
            OnPropertyChanged(nameof(ScreenshotsFullBitmapImages));
            OnPropertyChanged(nameof(ScreenshotsForExport));
            _isValuesDefaultState = true;
        }

        private void ReleaseTransientImages()
        {
            foreach (var screenshot in Screenshots)
            {
                screenshot.ReleaseTransientImages();
            }
            CurrentImageBitmap = null;
        }

        private void ReleaseStageImagesExcept(Screenshot retainedScreenshot)
        {
            foreach (var screenshot in Screenshots)
            {
                if (!ReferenceEquals(screenshot, retainedScreenshot))
                {
                    screenshot.ReleaseStageImage();
                }
            }
        }

        private async Task UpdateControlAsync(CancellationToken cancellationToken)
        {
            if (GameContext is null)
            {
                return;
            }

            await LoadControlData(GameContext, cancellationToken).ConfigureAwait(false);
        }

        private async Task LoadControlData(Game game, CancellationToken cancellationToken = default)
        {
            var scopeContext = Guid.NewGuid();
            _activeContext = scopeContext;
            _isValuesDefaultState = false;
            var screenshots = new List<Screenshot>();
            var trailers = new List<Trailer>();

            if (string.Equals(_gallerySource, "Local", StringComparison.OrdinalIgnoreCase))
            {
                var directory = _playniteApi.Database.GetFileStoragePath(game.Id);
                var files = Directory.Exists(directory)
                    ? Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly)
                    : new string[0];

                screenshots = files
                    .Select(path => new { Path = path, Match = LocalScreenshotPattern.Match(Path.GetFileNameWithoutExtension(path)) })
                    .Where(item => item.Match.Success)
                    .OrderBy(item => GetLocalGalleryIndex(item.Match))
                    .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                    .Select(item => _screenshotManagementService.CreateLocalScreenshot(item.Path))
                    .ToList();
                trailers = files
                    .Select(path => new { Path = path, Match = LocalTrailerPattern.Match(Path.GetFileNameWithoutExtension(path)) })
                    .Where(item => item.Match.Success)
                    .OrderBy(item => GetLocalGalleryIndex(item.Match))
                    .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                    .Select(item => _screenshotManagementService.CreateLocalTrailer(item.Path))
                    .ToList();

                if (screenshots.Count > 0)
                {
                    await Task.Run(
                        () => screenshots[0].InitializeStageImage(cancellationToken),
                        cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                try
                {
                    var steamId = _gallerySteamAppId;
                    if (string.IsNullOrEmpty(steamId))
                    {
                        steamId = Steam.GetGameSteamId(game, true, true);
                    }
                    if (string.IsNullOrEmpty(steamId))
                    {
                        steamId = await Task.Run(() =>
                            SteamWeb.GetSteamIdFromSearch(game.Name, null, cancellationToken)).ConfigureAwait(false);
                    }

                    if (!string.IsNullOrEmpty(steamId))
                    {
                        var lazyLoadFullImage = !_screenshotsFullBitmapImagesAccessed;
                        screenshots = await _screenshotManagementService.GetScreenshots(
                            ScreenshotServiceType.Steam,
                            steamId,
                            new ScreenshotInitializationOptions(false, lazyLoadFullImage),
                            cancellationToken).ConfigureAwait(false);
                        trailers = await _screenshotManagementService.GetTrailers(
                            ScreenshotServiceType.Steam,
                            steamId,
                            cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.Warn(exception, "Steam Store Gallery could not be loaded.");
                    screenshots.Clear();
                    trailers.Clear();
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (_activeContext == scopeContext && _currentGame?.Id == game.Id)
            {
                try
                {
                    _playniteApi.MainView.UIDispatcher.Invoke(() =>
                    {  
                        Screenshots.Clear();
                        foreach (var screenshot in screenshots)
                        {
                            Screenshots.Add(screenshot);
                        }

                        MediaItems.Clear();
                        foreach (var screenshot in screenshots)
                        {
                            MediaItems.Add(screenshot);
                        }
                        foreach (var trailer in trailers)
                        {
                            MediaItems.Add(trailer);
                        }

                        SelectedMediaItem = MediaItems.FirstOrDefault();
                        if (SelectedMediaItem == null)
                        {
                            GalleryStatusText = "No gallery content available.";
                            SetCollapsedVisibility();
                        }
                        else
                        {
                            GalleryStatusText = null;
                            SetVisibleVisibility();
                        }
                    });
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Error during LoadControlData");
                }
            }

        }

        private static int GetLocalGalleryIndex(Match match)
        {
            int index;
            return match != null && match.Groups.Count > 1 &&
                int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out index)
                ? index
                : 1;
        }

        private sealed class GalleryGameSettings
        {
            public bool Enabled { get; set; }
            public string Source { get; set; }
            public string SteamAppId { get; set; }
        }

        private void SelectPreviousImageScreenshot()
        {
            if (MediaItems.Count <= 1 || _selectedMediaItem is null)
            {
                return;
            }

            var currentSelectIndex = MediaItems.IndexOf(_selectedMediaItem);
            if (currentSelectIndex == -1)
            {
                return;
            }

            if (currentSelectIndex == 0)
            {
                SelectedMediaItem = MediaItems[MediaItems.Count - 1];
            }
            else
            {
                SelectedMediaItem = MediaItems[currentSelectIndex - 1];
            }
        }

        private void SelectNextScreenshot()
        {
            if (MediaItems.Count <= 1 || _selectedMediaItem is null)
            {
                return;
            }

            var currentSelectIndex = MediaItems.IndexOf(_selectedMediaItem);
            if (currentSelectIndex == -1)
            {
                return;
            }

            if (currentSelectIndex == MediaItems.Count - 1)
            {
                SelectedMediaItem = MediaItems[0];
            }
            else
            {
                SelectedMediaItem = MediaItems[currentSelectIndex + 1];
            }
        }

        private void OpenScreenshotsView(Screenshot selectedImage = null)
        {
            if (_currentGame is null || !_screenshots.HasItems() || IsTrailerSelected)
            {
                return;
            }

            var window = new Window
            {
                Width = 1330,
                Height = 845,
                WindowStyle = WindowStyle.None,
                WindowState = UseCinematicExpandedExperience ? WindowState.Normal : WindowState.Maximized,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Colors.Black),
                Title = _currentGame.Name,
                Owner = API.Instance.Dialogs.GetCurrentAppWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = new ScreenshotsView()
            };

            var screenshotsViewModel = new ScreenshotsViewModel(window, _screenshots.ToList());
            if (selectedImage != null)
            {
                screenshotsViewModel.SelectScreenshot(selectedImage);
            }

            window.DataContext = screenshotsViewModel;
            try
            {
                ConfigureExpandedWindow?.Invoke(window);
                window.ShowDialog();
            }
            finally
            {
                window.Close();
            }
            var index = _screenshots.IndexOf(screenshotsViewModel.LastDisplayedScreenshot);
            if (index != -1)
            {
                SelectedScreenshot = _screenshots[index];
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }


    }
}
