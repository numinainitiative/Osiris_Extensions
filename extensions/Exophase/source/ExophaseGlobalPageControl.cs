using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace Osiris.Extensions.Exophase
{
    public sealed partial class ExophaseGlobalPageControl : UserControl
    {
        private const double FixedHeaderHeight = 76;
        private const string NativeTopPanelTypeName =
            "Playnite.DesktopApp.Controls.Views.TopPanel";

        private readonly IPlayniteAPI api;
        private readonly ExophasePlugin plugin;
        private readonly ExophaseActivityStore activityStore;
        private readonly ExophaseGameSettingsStore gameSettingsStore;
        private readonly ExophaseActivityResolver activityResolver;
        private readonly ExophaseSettings settings;
        private readonly string fallbackIconPath;
        private readonly ObservableCollection<ExophaseGameRow> games =
            new ObservableCollection<ExophaseGameRow>();
        private readonly ObservableCollection<ExophasePlatformSummaryItem> platformStatistics =
            new ObservableCollection<ExophasePlatformSummaryItem>();
        private readonly FrameworkElement fixedHeader;
        private TextBox searchBox;
        private string searchText = string.Empty;
        private bool subscriptionsAttached;

        public ICollectionView GamesView { get; }

        public IEnumerable<ExophasePlatformSummaryItem> PlatformStatistics => platformStatistics;

        public ExophaseGlobalPageControl()
            : this(null, null, null, null, null, null)
        {
        }

        internal ExophaseGlobalPageControl(
            IPlayniteAPI api,
            ExophasePlugin plugin,
            ExophaseActivityStore activityStore,
            ExophaseGameSettingsStore gameSettingsStore,
            ExophaseActivityResolver activityResolver,
            ExophaseSettings settings)
        {
            this.api = api;
            this.plugin = plugin;
            this.activityStore = activityStore;
            this.gameSettingsStore = gameSettingsStore;
            this.activityResolver = activityResolver;
            this.settings = settings;
            fallbackIconPath = ResolveOsirisFallbackIconPath(api);

            InitializeComponent();
            GamesView = CollectionViewSource.GetDefaultView(games);
            GamesView.Filter = FilterGame;
            SortByCombo.SelectedIndex = 0;
            SortByCombo.SelectionChanged += OnSortByChanged;
            GlobalPlatformWheel.HoveredPlatformChanged += OnWheelHoveredPlatformChanged;
            GamesGrid.LayoutUpdated += OnGamesGridLayoutUpdated;
            PageScrollViewer.SizeChanged += OnPageScrollViewerSizeChanged;
            RefreshDatabaseUpdateControls();
            ApplySort("Title");
            DataContext = this;

            fixedHeader = CreateNativeTopPanel() ?? CreateFallbackHeader();
            fixedHeader.MinHeight = FixedHeaderHeight;
            fixedHeader.MaxHeight = FixedHeaderHeight;
            fixedHeader.HorizontalAlignment = HorizontalAlignment.Stretch;
            fixedHeader.VerticalAlignment = VerticalAlignment.Top;
            fixedHeader.Visibility = Visibility.Visible;
            FixedHeaderHost.Content = fixedHeader;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            IsVisibleChanged += OnIsVisibleChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs args)
        {
            fixedHeader.Visibility = Visibility.Visible;
            AttachSubscriptions();
            UpdateGamesTableHeight();
            ReloadGames();

            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow != null)
            {
                BindingOperations.SetBinding(
                    fixedHeader,
                    DataContextProperty,
                    new Binding("DataContext")
                    {
                        Source = mainWindow,
                        Mode = BindingMode.OneWay
                    });
            }

            Dispatcher.BeginInvoke(new Action(AttachSearchBox), DispatcherPriority.Loaded);
        }

        private void OnUnloaded(object sender, RoutedEventArgs args)
        {
            DetachSearchBox();
            DetachSubscriptions();
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            if (IsVisible && IsLoaded)
            {
                ReloadGames();
            }
        }

        private void AttachSubscriptions()
        {
            if (subscriptionsAttached)
            {
                return;
            }

            if (activityStore != null)
            {
                activityStore.SnapshotChanged += OnActivityChanged;
            }

            if (gameSettingsStore != null)
            {
                gameSettingsStore.SettingsChanged += OnGameSettingsChanged;
            }

            if (settings != null)
            {
                settings.PropertyChanged += OnSettingsPropertyChanged;
            }

            subscriptionsAttached = true;
        }

        private void DetachSubscriptions()
        {
            if (!subscriptionsAttached)
            {
                return;
            }

            if (activityStore != null)
            {
                activityStore.SnapshotChanged -= OnActivityChanged;
            }

            if (gameSettingsStore != null)
            {
                gameSettingsStore.SettingsChanged -= OnGameSettingsChanged;
            }

            if (settings != null)
            {
                settings.PropertyChanged -= OnSettingsPropertyChanged;
            }

            subscriptionsAttached = false;
        }

        private void OnActivityChanged()
        {
            ReloadGamesOnDispatcher();
        }

        private void OnGameSettingsChanged(Guid gameId)
        {
            ReloadGamesOnDispatcher();
        }

        private void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(ExophaseSettings.OverrideDisplayedPlaytime))
            {
                ReloadGamesOnDispatcher();
            }

            if (args.PropertyName == nameof(ExophaseSettings.CanSynchronize) ||
                args.PropertyName == nameof(ExophaseSettings.IsSynchronizationRunning) ||
                args.PropertyName == nameof(ExophaseSettings.SynchronizationStatus))
            {
                if (Dispatcher.CheckAccess())
                {
                    RefreshDatabaseUpdateControls();
                }
                else
                {
                    Dispatcher.BeginInvoke(
                        new Action(RefreshDatabaseUpdateControls),
                        DispatcherPriority.DataBind);
                }
            }
        }

        private void ReloadGamesOnDispatcher()
        {
            if (Dispatcher.CheckAccess())
            {
                ReloadGames();
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(ReloadGames), DispatcherPriority.DataBind);
            }
        }

        private void ReloadGames()
        {
            if (api?.Database?.Games == null)
            {
                return;
            }

            var rows = api.Database.Games
                .Where(game => game != null)
                .OrderBy(game => game.Name ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
                .Select(CreateRow)
                .ToList();

            games.Clear();
            foreach (var row in rows)
            {
                games.Add(row);
            }

            UpdateGameDataSummary(rows);
            UpdatePlatformStatistics(rows);
            GamesView.Refresh();
            RefreshDatabaseUpdateControls();
        }

        private void UpdateGameDataSummary(IEnumerable<ExophaseGameRow> rows)
        {
            var summary = CalculateGameDataSummary(rows);
            FetchedGamesCountText.Text = summary.Fetched.ToString(CultureInfo.CurrentCulture) +
                                         " Games with Exophase data";
            NoExophaseDataCountText.Text = summary.NoData.ToString(CultureInfo.CurrentCulture) +
                                           " With no Exophase data";
            DisabledGamesCountText.Text = summary.Disabled.ToString(CultureInfo.CurrentCulture) +
                                          " Games with Exophase manually disabled";
        }

        internal static ExophaseGameDataSummary CalculateGameDataSummary(
            IEnumerable<ExophaseGameRow> rows)
        {
            var values = (rows ?? Enumerable.Empty<ExophaseGameRow>())
                .Where(row => row != null)
                .ToList();
            return new ExophaseGameDataSummary(
                values.Count(row => row.DataState == ExophaseGameDataState.Fetched),
                values.Count(row => row.DataState == ExophaseGameDataState.NoData),
                values.Count(row => row.DataState == ExophaseGameDataState.Disabled));
        }

        private void UpdatePlatformStatistics(IEnumerable<ExophaseGameRow> rows)
        {
            SetHighlightedPlatform(null);
            var rowList = (rows ?? Enumerable.Empty<ExophaseGameRow>()).ToList();
            var statistics = AggregatePlatformStatistics(
                rowList.SelectMany(row => row.PlatformSegments));

            platformStatistics.Clear();
            foreach (var statistic in statistics)
            {
                platformStatistics.Add(statistic);
            }

            GlobalPlatformWheel.Segments = statistics;
            var totalPlaytime = statistics.Aggregate(
                0UL,
                (sum, item) => SaturatingAdd(sum, item.PlaytimeSeconds));
            GlobalTotalPlaytimeValue.Text =
                ExophaseActivityControl.FormatCompactPlaytime(totalPlaytime);
            GlobalTotalTrophiesValue.Text = FormatTotalTrophies(rowList);
            GlobalPlatformsUsedValue.Text = statistics.Count.ToString(CultureInfo.CurrentCulture);
            GlobalStatisticsContent.Visibility = statistics.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            GlobalStatisticsEmptyState.Visibility = statistics.Count > 0
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        internal static IReadOnlyList<ExophasePlatformSummaryItem> AggregatePlatformStatistics(
            IEnumerable<ExophaseTimelineSegment> segments)
        {
            var grouped = (segments ?? Enumerable.Empty<ExophaseTimelineSegment>())
                .Where(segment => segment != null && segment.PlaytimeSeconds > 0)
                .GroupBy(
                    segment => string.IsNullOrWhiteSpace(segment.Platform)
                        ? "Unknown"
                        : segment.Platform.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var visual = ExophasePlatformVisualCatalog.Resolve(group.Key);
                    return new
                    {
                        Visual = visual,
                        PlaytimeSeconds = group.Aggregate(
                            0UL,
                            (sum, segment) => SaturatingAdd(sum, segment.PlaytimeSeconds))
                    };
                })
                .OrderByDescending(item => item.PlaytimeSeconds)
                .ThenBy(item => item.Visual.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var total = grouped.Aggregate(
                0UL,
                (sum, item) => SaturatingAdd(sum, item.PlaytimeSeconds));

            return grouped.Select(item => new ExophasePlatformSummaryItem
            {
                Platform = item.Visual.DisplayName,
                PlaytimeSeconds = item.PlaytimeSeconds,
                PlaytimeText = ExophaseActivityControl.FormatCompactPlaytime(item.PlaytimeSeconds),
                Percentage = total == 0 ? 0d : item.PlaytimeSeconds / (double)total,
                PercentageText = ExophasePlatformTimeline.FormatPercentage(
                    total == 0 ? 0d : item.PlaytimeSeconds / (double)total),
                Brush = item.Visual.Brush,
                TagBrush = item.Visual.UseOsirisLogo
                    ? item.Visual.Brush
                    : ExophasePlatformTimeline.CreateTagBrush(item.Visual.Brush),
                IconGeometry = item.Visual.IconGeometry,
                UseOsirisLogo = item.Visual.UseOsirisLogo
            }).ToList();
        }

        private static ulong SaturatingAdd(ulong left, ulong right)
        {
            return ulong.MaxValue - left < right ? ulong.MaxValue : left + right;
        }

        internal static string FormatTotalTrophies(IEnumerable<ExophaseGameRow> rows)
        {
            var earned = (rows ?? Enumerable.Empty<ExophaseGameRow>())
                .Aggregate(0L, (sum, row) => sum + Math.Max(0, row?.EarnedTrophies ?? 0));
            var total = (rows ?? Enumerable.Empty<ExophaseGameRow>())
                .Aggregate(0L, (sum, row) => sum + Math.Max(0, row?.TotalTrophies ?? 0));
            return earned.ToString(CultureInfo.CurrentCulture) + "/" +
                   total.ToString(CultureInfo.CurrentCulture);
        }

        private ExophaseGameRow CreateRow(Game game)
        {
            var timeline = ResolvePlatformTimeline(game);
            var displayedPlaytime = timeline.Segments.Count > 0
                ? timeline.TotalPlaytimeSeconds
                : ResolveEffectivePlaytime(game);
            return new ExophaseGameRow(
                game,
                ResolveIconPath(game.Icon),
                fallbackIconPath,
                displayedPlaytime,
                timeline.TrophyCountText,
                timeline.EarnedTrophies,
                timeline.TotalTrophies,
                timeline.TrophyProgressRatio,
                timeline.Segments,
                timeline.StatusText,
                timeline.DataState);
        }

        private PlatformTimelineResult ResolvePlatformTimeline(Game game)
        {
            if (game == null || activityResolver == null)
            {
                return PlatformTimelineResult.WithoutData("No Exophase data");
            }

            try
            {
                var editableActivity = activityResolver.Resolve(game, false);
                var resolvedActivity = activityResolver.Resolve(game, true);
                if (!resolvedActivity.Enabled)
                {
                    return PlatformTimelineResult.WithoutData(
                        "Disabled for this game",
                        ExophaseGameDataState.Disabled);
                }

                if (!editableActivity.UsesManualMatch &&
                    editableActivity.Platforms.Any(platform => !platform.IsManual))
                {
                    var matchKey = ExophaseActivityParser.NormalizeTitle(game.Name);
                    var matchingLibraryGames = api.Database.Games.Count(candidate =>
                        string.Equals(
                            ExophaseActivityParser.NormalizeTitle(candidate.Name),
                            matchKey,
                            StringComparison.Ordinal));
                    if (matchingLibraryGames != 1)
                    {
                        return PlatformTimelineResult.WithoutData("Multiple title matches");
                    }
                }

                if (!ExophaseActivityControl.HasDisplayableActivity(editableActivity))
                {
                    return PlatformTimelineResult.WithoutData(
                        editableActivity.SnapshotIsInvalid
                            ? "Exophase data unavailable"
                            : editableActivity.HasSnapshot
                                ? "No Exophase data"
                                : "Synchronize Exophase");
                }

                var trophyPlatform = SelectTrophyPlatform(editableActivity.Platforms);
                var hasTrophyData = trophyPlatform != null;
                var earnedTrophies = Math.Max(0, trophyPlatform?.EarnedAwards ?? 0);
                var totalTrophies = Math.Max(0, trophyPlatform?.TotalAwards ?? 0);
                var trophyCountText = hasTrophyData
                    ? FormatTrophyCount(earnedTrophies, totalTrophies)
                    : "—";
                var trophyProgressRatio = hasTrophyData
                    ? ExophasePlatformTimeline.CalculateTrophyProgressRatio(
                        earnedTrophies,
                        totalTrophies)
                    : 0d;
                var dataState = editableActivity.Platforms.Any(platform =>
                    platform != null && !platform.IsManual && !platform.IsBaseline)
                    ? ExophaseGameDataState.Fetched
                    : ExophaseGameDataState.NoData;

                var segments = resolvedActivity.Platforms
                    .Where(platform => platform.PlaytimeSeconds > 0)
                    .OrderByDescending(platform => platform.PlaytimeSeconds)
                    .ThenBy(platform => platform.Platform, StringComparer.OrdinalIgnoreCase)
                    .Select(platform =>
                    {
                        var visual = ExophasePlatformVisualCatalog.Resolve(platform.Platform);
                        return new ExophaseTimelineSegment
                        {
                            Platform = visual.DisplayName,
                            PlaytimeSeconds = platform.PlaytimeSeconds,
                            Brush = visual.Brush,
                            TagBrush = visual.UseOsirisLogo
                                ? visual.Brush
                                : ExophasePlatformTimeline.CreateTagBrush(visual.Brush),
                            IconGeometry = visual.IconGeometry,
                            UseOsirisLogo = visual.UseOsirisLogo
                        };
                    })
                    .ToList();
                return segments.Count == 0
                    ? PlatformTimelineResult.WithoutData("No platform play time", dataState)
                    : new PlatformTimelineResult(
                        segments,
                        string.Empty,
                        resolvedActivity.TotalPlaytimeSeconds,
                        trophyCountText,
                        earnedTrophies,
                        totalTrophies,
                        trophyProgressRatio,
                        dataState);
            }
            catch
            {
                return PlatformTimelineResult.WithoutData("Exophase data unavailable");
            }
        }

        private ulong ResolveEffectivePlaytime(Game game)
        {
            if (game == null)
            {
                return 0;
            }

            try
            {
                return plugin?.GetEffectivePlaytimeForOsiris(game.Id.ToString()) ?? game.Playtime;
            }
            catch
            {
                return game.Playtime;
            }
        }

        internal static string FormatTrophyCount(int earnedTrophies, int totalTrophies)
        {
            return Math.Max(0, earnedTrophies) + "/" + Math.Max(0, totalTrophies);
        }

        internal static string FormatLastUpdated(DateTime fetchedLocal, DateTime localNow)
        {
            if (fetchedLocal == DateTime.MinValue)
            {
                return "Never updated";
            }

            if (fetchedLocal.Date == localNow.Date)
            {
                return "Last updated today at " + fetchedLocal.ToString("HH:mm", CultureInfo.CurrentCulture);
            }

            if (fetchedLocal.Date == localNow.Date.AddDays(-1))
            {
                return "Last updated yesterday at " + fetchedLocal.ToString("HH:mm", CultureInfo.CurrentCulture);
            }

            return "Last updated " + fetchedLocal.ToString(
                "dd/MM/yyyy 'at' HH:mm",
                CultureInfo.CurrentCulture);
        }

        internal static ExophaseResolvedPlatform SelectTrophyPlatform(
            IEnumerable<ExophaseResolvedPlatform> platforms)
        {
            return (platforms ?? Enumerable.Empty<ExophaseResolvedPlatform>())
                .Where(platform => platform != null &&
                                   !platform.IsManual &&
                                   !platform.IsBaseline &&
                                   (platform.EarnedAwards > 0 || platform.TotalAwards > 0))
                .OrderByDescending(platform => Math.Max(0, platform.EarnedAwards))
                .ThenBy(platform => platform.TotalAwards > 0
                    ? platform.TotalAwards
                    : int.MaxValue)
                .ThenBy(platform => platform.Platform, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private string ResolveIconPath(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                return null;
            }

            try
            {
                var path = Path.IsPathRooted(databasePath)
                    ? databasePath
                    : api.Database.GetFullFilePath(databasePath);
                return File.Exists(path) ? path : null;
            }
            catch
            {
                return null;
            }
        }

        private static string ResolveOsirisFallbackIconPath(IPlayniteAPI api)
        {
            try
            {
                var imageRoot = Path.Combine(
                    api?.Paths?.ApplicationPath ?? string.Empty,
                    "Themes",
                    "Desktop",
                    "Default",
                    "Images");
                var candidates = new[]
                {
                    Path.Combine(imageRoot, "applogo.png"),
                    Path.Combine(imageRoot, "applogo_dark.png")
                };
                return candidates.FirstOrDefault(File.Exists);
            }
            catch
            {
                return null;
            }
        }

        private bool FilterGame(object item)
        {
            var row = item as ExophaseGameRow;
            return row != null &&
                   (string.IsNullOrWhiteSpace(searchText) ||
                    row.Name.IndexOf(searchText, StringComparison.CurrentCultureIgnoreCase) >= 0);
        }

        private void OnSortByChanged(object sender, SelectionChangedEventArgs args)
        {
            var selected = SortByCombo.SelectedItem as ComboBoxItem;
            ApplySort(selected?.Tag as string ?? "Title");
        }

        private void OnWheelHoveredPlatformChanged(
            object sender,
            ExophasePlatformHoverEventArgs args)
        {
            SetHighlightedPlatform(args?.Platform);
        }

        private void OnPlatformLegendMouseEnter(object sender, MouseEventArgs args)
        {
            var platform = (sender as FrameworkElement)?.DataContext as
                ExophasePlatformSummaryItem;
            SetHighlightedPlatform(platform?.Platform);
        }

        private void OnPlatformLegendMouseLeave(object sender, MouseEventArgs args)
        {
            SetHighlightedPlatform(null);
        }

        internal void SetHighlightedPlatform(string platform)
        {
            var highlightedPlatform = string.IsNullOrWhiteSpace(platform)
                ? null
                : platform.Trim();
            GlobalPlatformWheel.HighlightedPlatform = highlightedPlatform;
            foreach (var statistic in platformStatistics)
            {
                statistic.SetMuted(
                    highlightedPlatform != null &&
                    !string.Equals(
                        statistic.Platform,
                        highlightedPlatform,
                        StringComparison.OrdinalIgnoreCase));
            }
        }

        private async void OnUpdateDatabaseClick(object sender, RoutedEventArgs args)
        {
            args.Handled = true;
            if (settings == null || !settings.CanSynchronize)
            {
                return;
            }

            await settings.SynchronizeAsync();
            ReloadGames();
            RefreshDatabaseUpdateControls();
        }

        private void RefreshDatabaseUpdateControls()
        {
            if (UpdateDatabaseButton != null)
            {
                UpdateDatabaseButton.IsEnabled = settings?.CanSynchronize == true;
                UpdateDatabaseButton.ToolTip = settings?.SynchronizationStatus;
            }

            if (LastDatabaseUpdateText == null)
            {
                return;
            }

            var snapshot = activityStore?.GetSnapshotInfo();
            var fetchedLocal = snapshot?.HasSnapshot == true &&
                               snapshot.SnapshotIsInvalid == false &&
                               snapshot.FetchedUtc != DateTime.MinValue
                ? snapshot.FetchedUtc.ToLocalTime()
                : DateTime.MinValue;
            LastDatabaseUpdateText.Text = FormatLastUpdated(fetchedLocal, DateTime.Now);
            LastDatabaseUpdateText.ToolTip = fetchedLocal == DateTime.MinValue
                ? null
                : fetchedLocal.ToString("F", CultureInfo.CurrentCulture);
        }

        private void ApplySort(string sortMode)
        {
            if (GamesView == null)
            {
                return;
            }

            using (GamesView.DeferRefresh())
            {
                GamesView.SortDescriptions.Clear();
                if (string.Equals(sortMode, "TimePlayed", StringComparison.Ordinal))
                {
                    GamesView.SortDescriptions.Add(new SortDescription(
                        nameof(ExophaseGameRow.PlayedSeconds),
                        ListSortDirection.Descending));
                }

                GamesView.SortDescriptions.Add(new SortDescription(
                    nameof(ExophaseGameRow.Name),
                    ListSortDirection.Ascending));
            }
        }

        private void OnGameTitleClick(object sender, RoutedEventArgs args)
        {
            var row = (sender as FrameworkElement)?.DataContext as ExophaseGameRow;
            if (row?.SourceGame == null || row.GameId == Guid.Empty)
            {
                return;
            }

            args.Handled = true;
            var viewModel = Application.Current?.MainWindow?.DataContext;
            if (TryNavigateThroughOsiris(viewModel, row))
            {
                return;
            }

            if (api?.MainView == null)
            {
                return;
            }

            api.MainView.SwitchToLibraryView();
            api.MainView.ActiveDesktopView = DesktopView.Details;
            api.MainView.SelectGame(row.GameId);
        }

        private static bool TryNavigateThroughOsiris(object viewModel, ExophaseGameRow row)
        {
            if (viewModel == null || row == null)
            {
                return false;
            }

            try
            {
                var navigationType = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Select(assembly => assembly.GetType("OsirisTheme.HeaderNavigation", false))
                    .FirstOrDefault(type => type != null);
                var navigateToGame = navigationType?.GetMethod(
                    "NavigateToGame",
                    BindingFlags.Public | BindingFlags.Static);
                var switchToDetails = navigationType?.GetMethod(
                    "ExecuteSwitchDetailsCommand",
                    BindingFlags.Public | BindingFlags.Static);
                if (navigateToGame == null || switchToDetails == null)
                {
                    return false;
                }

                navigateToGame.Invoke(null, new object[] { viewModel, row.SourceGame, row.GameId });
                switchToDetails.Invoke(null, new[] { viewModel });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void OnGamesGridLayoutUpdated(object sender, EventArgs args)
        {
            var scrollBar = FindVisualChildren<System.Windows.Controls.Primitives.ScrollBar>(GamesGrid)
                .FirstOrDefault(candidate =>
                    candidate.Orientation == Orientation.Vertical &&
                    candidate.Visibility == Visibility.Visible &&
                    candidate.ActualWidth > 0);
            var gutterWidth = scrollBar?.ActualWidth ?? 0;
            HeaderGutterFill.Width = gutterWidth;
            HeaderGutterFill.Visibility = gutterWidth > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void OnPageScrollViewerSizeChanged(object sender, SizeChangedEventArgs args)
        {
            UpdateGamesTableHeight();
        }

        private void UpdateGamesTableHeight()
        {
            if (GamesTableContainer == null || PageScrollViewer == null)
            {
                return;
            }

            var toolbarHeight = ScrollToolbar?.ActualHeight ?? 0d;
            GamesTableContainer.Height = CalculateGamesTableHeight(
                Math.Max(0d, PageScrollViewer.ActualHeight - toolbarHeight));
        }

        internal static double CalculateGamesTableHeight(double viewportHeight)
        {
            if (double.IsNaN(viewportHeight) ||
                double.IsInfinity(viewportHeight) ||
                viewportHeight <= 0)
            {
                return 640d;
            }

            return Math.Max(520d, Math.Round(viewportHeight * 0.8d));
        }

        private void AttachSearchBox()
        {
            DetachSearchBox();
            searchBox = FindVisualChild<TextBox>(fixedHeader);
            if (searchBox == null)
            {
                return;
            }

            searchBox.TextChanged += OnSearchTextChanged;
            searchText = searchBox.Text?.Trim() ?? string.Empty;
            GamesView.Refresh();
        }

        private void DetachSearchBox()
        {
            if (searchBox != null)
            {
                searchBox.TextChanged -= OnSearchTextChanged;
                searchBox = null;
            }
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs args)
        {
            searchText = searchBox?.Text?.Trim() ?? string.Empty;
            GamesView.Refresh();
        }

        private static T FindVisualChild<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null)
            {
                return null;
            }

            if (root is T match)
            {
                return match;
            }

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                var childMatch = FindVisualChild<T>(VisualTreeHelper.GetChild(root, index));
                if (childMatch != null)
                {
                    return childMatch;
                }
            }

            return null;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
            where T : DependencyObject
        {
            if (root == null)
            {
                yield break;
            }

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);
                if (child is T match)
                {
                    yield return match;
                }

                foreach (var descendant in FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }

        private static FrameworkElement CreateNativeTopPanel()
        {
            try
            {
                var topPanelType = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Select(assembly => assembly.GetType(NativeTopPanelTypeName, false))
                    .FirstOrDefault(type => type != null);

                return topPanelType == null
                    ? null
                    : Activator.CreateInstance(topPanelType) as FrameworkElement;
            }
            catch
            {
                return null;
            }
        }

        private static FrameworkElement CreateFallbackHeader()
        {
            return new Border
            {
                Height = FixedHeaderHeight,
                Background = new SolidColorBrush(Color.FromRgb(11, 11, 11)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(28, 28, 28)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
        }
    }

    public sealed class ExophasePlatformSummaryItem : INotifyPropertyChanged
    {
        private static readonly Brush MutedLegendBrush = CreateMutedLegendBrush();
        private static readonly Brush LightLegendForegroundBrush = CreateLegendForegroundBrush(240, 241, 247);
        private static readonly Brush DarkLegendForegroundBrush = CreateLegendForegroundBrush(9, 9, 9);
        private bool isMuted;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Platform { get; set; }

        public ulong PlaytimeSeconds { get; set; }

        public string PlaytimeText { get; set; }

        public double Percentage { get; set; }

        public string PercentageText { get; set; }

        public Brush Brush { get; set; }

        public Brush TagBrush { get; set; }

        public Brush LegendTagBrush => isMuted ? MutedLegendBrush : TagBrush;

        public Brush LegendForegroundBrush => UseOsirisLogo
            ? DarkLegendForegroundBrush
            : LightLegendForegroundBrush;

        public double LegendOpacity => isMuted ? 0.58d : 1d;

        public Geometry IconGeometry { get; set; }

        public bool UseOsirisLogo { get; set; }

        internal void SetMuted(bool muted)
        {
            if (isMuted == muted)
            {
                return;
            }

            isMuted = muted;
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(nameof(LegendTagBrush)));
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(nameof(LegendOpacity)));
        }

        private static Brush CreateMutedLegendBrush()
        {
            var brush = new SolidColorBrush(Color.FromRgb(77, 79, 85));
            brush.Freeze();
            return brush;
        }

        private static Brush CreateLegendForegroundBrush(byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }
    }

    internal enum ExophaseGameDataState
    {
        NoData,
        Fetched,
        Disabled
    }

    internal sealed class ExophaseGameDataSummary
    {
        public int Fetched { get; }
        public int NoData { get; }
        public int Disabled { get; }

        public ExophaseGameDataSummary(int fetched, int noData, int disabled)
        {
            Fetched = Math.Max(0, fetched);
            NoData = Math.Max(0, noData);
            Disabled = Math.Max(0, disabled);
        }
    }

    internal sealed class ExophaseGameRow
    {
        public Game SourceGame { get; }
        public Guid GameId { get; }
        public string Name { get; }
        public string IconPath { get; }
        public string FallbackIconPath { get; }
        public ulong PlayedSeconds { get; }
        public string PlayedTimeText { get; }
        public string TrophyCountText { get; }
        public int EarnedTrophies { get; }
        public int TotalTrophies { get; }
        public double TrophyProgressRatio { get; }
        public bool IsTrophyComplete => TotalTrophies > 0 && EarnedTrophies >= TotalTrophies;
        public IReadOnlyList<ExophaseTimelineSegment> PlatformSegments { get; }
        public bool HasPlatformTime => PlatformSegments.Count > 0;
        public string PlatformStatusText { get; }
        public ExophaseGameDataState DataState { get; }

        public ExophaseGameRow(
            Game game,
            string iconPath,
            string fallbackIconPath,
            ulong playedSeconds,
            string trophyCountText,
            int earnedTrophies,
            int totalTrophies,
            double trophyProgressRatio,
            IReadOnlyList<ExophaseTimelineSegment> platformSegments,
            string platformStatusText,
            ExophaseGameDataState dataState = ExophaseGameDataState.NoData)
        {
            SourceGame = game;
            GameId = game?.Id ?? Guid.Empty;
            Name = string.IsNullOrWhiteSpace(game?.Name) ? "Unnamed Game" : game.Name;
            IconPath = iconPath;
            FallbackIconPath = fallbackIconPath;
            PlayedSeconds = playedSeconds;
            PlayedTimeText = ExophaseActivityControl.FormatCompactPlaytime(playedSeconds);
            TrophyCountText = string.IsNullOrWhiteSpace(trophyCountText)
                ? "—"
                : trophyCountText;
            EarnedTrophies = Math.Max(0, earnedTrophies);
            TotalTrophies = Math.Max(0, totalTrophies);
            TrophyProgressRatio = Math.Max(0d, Math.Min(1d, trophyProgressRatio));
            PlatformSegments = platformSegments ?? new List<ExophaseTimelineSegment>();
            PlatformStatusText = platformStatusText ?? string.Empty;
            DataState = dataState;
        }

    }

    internal sealed class PlatformTimelineResult
    {
        public IReadOnlyList<ExophaseTimelineSegment> Segments { get; }

        public string StatusText { get; }

        public ulong TotalPlaytimeSeconds { get; }

        public string TrophyCountText { get; }

        public int EarnedTrophies { get; }

        public int TotalTrophies { get; }

        public double TrophyProgressRatio { get; }

        public ExophaseGameDataState DataState { get; }

        public PlatformTimelineResult(
            IReadOnlyList<ExophaseTimelineSegment> segments,
            string statusText,
            ulong totalPlaytimeSeconds,
            string trophyCountText,
            int earnedTrophies,
            int totalTrophies,
            double trophyProgressRatio,
            ExophaseGameDataState dataState)
        {
            Segments = segments ?? new List<ExophaseTimelineSegment>();
            StatusText = statusText ?? string.Empty;
            TotalPlaytimeSeconds = totalPlaytimeSeconds;
            TrophyCountText = string.IsNullOrWhiteSpace(trophyCountText) ? "—" : trophyCountText;
            EarnedTrophies = Math.Max(0, earnedTrophies);
            TotalTrophies = Math.Max(0, totalTrophies);
            TrophyProgressRatio = Math.Max(0d, Math.Min(1d, trophyProgressRatio));
            DataState = dataState;
        }

        public static PlatformTimelineResult WithoutData(
            string statusText,
            ExophaseGameDataState dataState = ExophaseGameDataState.NoData)
        {
            return new PlatformTimelineResult(
                new List<ExophaseTimelineSegment>(),
                statusText,
                0,
                "—",
                0,
                0,
                0d,
                dataState);
        }
    }
}
