using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace Osiris.Extensions.HowLongToBeat
{
    public sealed partial class HowLongToBeatGlobalPageControl : UserControl
    {
        private const double FixedHeaderHeight = 76;
        private const string NativeTopPanelTypeName =
            "Playnite.DesktopApp.Controls.Views.TopPanel";

        private readonly IPlayniteAPI api;
        private readonly CompletionTimeCache cache;
        private readonly CompletionTimeGameSettingsStore gameSettingsStore;
        private readonly HowLongToBeatSettings settings;
        private readonly string fallbackIconPath;
        private readonly ObservableCollection<HowLongToBeatGameRow> games =
            new ObservableCollection<HowLongToBeatGameRow>();
        private readonly FrameworkElement fixedHeader;
        private TextBox searchBox;
        private string searchText = string.Empty;
        private bool showOnlyPlayedGames;
        private bool subscriptionsAttached;

        public ICollectionView GamesView { get; }

        public HowLongToBeatGlobalPageControl()
            : this(null, null, null, null)
        {
        }

        internal HowLongToBeatGlobalPageControl(
            IPlayniteAPI api,
            CompletionTimeCache cache,
            CompletionTimeGameSettingsStore gameSettingsStore,
            HowLongToBeatSettings settings)
        {
            this.api = api;
            this.cache = cache;
            this.gameSettingsStore = gameSettingsStore;
            this.settings = settings;
            fallbackIconPath = ResolveOsirisFallbackIconPath(api);
            InitializeComponent();
            GamesView = CollectionViewSource.GetDefaultView(games);
            GamesView.Filter = FilterGame;
            SortByCombo.SelectedIndex = 0;
            SortByCombo.SelectionChanged += OnSortByChanged;
            ShowOnlyPlayedToggle.Checked += OnShowOnlyPlayedChanged;
            ShowOnlyPlayedToggle.Unchecked += OnShowOnlyPlayedChanged;
            GamesGrid.LayoutUpdated += OnGamesGridLayoutUpdated;
            RefreshUpdateDatabaseButton();
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

            if (settings != null)
            {
                settings.SettingsChanged += OnGlobalSettingsChanged;
                settings.PropertyChanged += OnSettingsPropertyChanged;
            }

            if (gameSettingsStore != null)
            {
                gameSettingsStore.SettingsChanged += OnGameSettingsChanged;
            }

            subscriptionsAttached = true;
        }

        private void DetachSubscriptions()
        {
            if (!subscriptionsAttached)
            {
                return;
            }

            if (settings != null)
            {
                settings.SettingsChanged -= OnGlobalSettingsChanged;
                settings.PropertyChanged -= OnSettingsPropertyChanged;
            }

            if (gameSettingsStore != null)
            {
                gameSettingsStore.SettingsChanged -= OnGameSettingsChanged;
            }

            subscriptionsAttached = false;
        }

        private void OnGlobalSettingsChanged()
        {
            ReloadGamesOnDispatcher();
        }

        private void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(HowLongToBeatSettings.CanUpdateDatabase) ||
                args.PropertyName == nameof(HowLongToBeatSettings.IsDatabaseUpdateRunning) ||
                args.PropertyName == nameof(HowLongToBeatSettings.DatabaseUpdateStatus))
            {
                if (Dispatcher.CheckAccess())
                {
                    RefreshUpdateDatabaseButton();
                }
                else
                {
                    Dispatcher.BeginInvoke(
                        new Action(RefreshUpdateDatabaseButton),
                        DispatcherPriority.DataBind);
                }
            }
        }

        private void OnGameSettingsChanged(Guid gameId)
        {
            ReloadGamesOnDispatcher();
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

            var profile = settings?.TimeProfile ?? CompletionTimeProfiles.Average;
            var rows = api.Database.Games
                .Where(game => game != null)
                .OrderBy(game => game.Name ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
                .Select(game => CreateRow(game, profile))
                .ToList();

            games.Clear();
            foreach (var row in rows)
            {
                games.Add(row);
            }

            GamesView.Refresh();
        }

        private HowLongToBeatGameRow CreateRow(Game game, string profile)
        {
            var storedSettings = gameSettingsStore?.Load(game.Id) ?? new CompletionTimeGameSettings();
            if (!storedSettings.Enabled)
            {
                return HowLongToBeatGameRow.WithoutTimes(
                    game,
                    ResolveIconPath(game.Icon),
                    fallbackIconPath,
                    profile,
                    "Disabled for this game");
            }

            var result = SelectStoredResult(game, storedSettings);
            if (result?.Found != true || !result.HasAnyTime)
            {
                return HowLongToBeatGameRow.WithoutTimes(
                    game,
                    ResolveIconPath(game.Icon),
                    fallbackIconPath,
                    profile,
                    "Not fetched");
            }

            var mainStory = result.GetMainStorySeconds(profile);
            var mainExtra = result.GetMainExtraSeconds(profile);
            var completionist = result.GetCompletionistSeconds(profile);
            if (mainStory <= 0 && mainExtra <= 0 && completionist <= 0)
            {
                return HowLongToBeatGameRow.WithoutTimes(
                    game,
                    ResolveIconPath(game.Icon),
                    fallbackIconPath,
                    profile,
                    profile + " estimates unavailable");
            }

            return new HowLongToBeatGameRow(
                game,
                ResolveIconPath(game.Icon),
                fallbackIconPath,
                profile,
                mainStory,
                mainExtra,
                completionist);
        }

        private CompletionTimeResult SelectStoredResult(
            Game game,
            CompletionTimeGameSettings storedSettings)
        {
            var isManual = storedSettings.ManualResult?.Found == true &&
                           storedSettings.ManualResult.RemoteGameId > 0;
            var selected = isManual
                ? storedSettings.ManualResult?.Clone()
                : storedSettings.AutomaticResult?.Clone();

            CompletionTimeResult cached = null;
            cache?.TryGet(game.Name, game.ReleaseDate?.Year, false, out cached);
            if (selected == null)
            {
                return cached;
            }

            if (cached?.Found == true &&
                cached.RemoteGameId == selected.RemoteGameId &&
                cached.HasDetailedProfiles &&
                !selected.HasDetailedProfiles)
            {
                return cached;
            }

            return selected;
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
            var row = item as HowLongToBeatGameRow;
            return row != null &&
                   (!showOnlyPlayedGames || row.PlayedSeconds > 0) &&
                   (string.IsNullOrWhiteSpace(searchText) ||
                    row.Name.IndexOf(searchText, StringComparison.CurrentCultureIgnoreCase) >= 0);
        }

        private void OnSortByChanged(object sender, SelectionChangedEventArgs args)
        {
            var selected = SortByCombo.SelectedItem as ComboBoxItem;
            ApplySort(selected?.Tag as string ?? "Title");
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
                switch (sortMode)
                {
                    case "TimePlayed":
                        GamesView.SortDescriptions.Add(new SortDescription(
                            nameof(HowLongToBeatGameRow.PlayedSeconds),
                            ListSortDirection.Descending));
                        break;
                    case "LongestCompletion":
                        GamesView.SortDescriptions.Add(new SortDescription(
                            nameof(HowLongToBeatGameRow.LongestTimeSeconds),
                            ListSortDirection.Descending));
                        break;
                    default:
                        break;
                }

                GamesView.SortDescriptions.Add(new SortDescription(
                    nameof(HowLongToBeatGameRow.Name),
                    ListSortDirection.Ascending));
            }
        }

        private void OnShowOnlyPlayedChanged(object sender, RoutedEventArgs args)
        {
            showOnlyPlayedGames = ShowOnlyPlayedToggle.IsChecked == true;
            GamesView?.Refresh();
        }

        private async void OnUpdateDatabaseClick(object sender, RoutedEventArgs args)
        {
            if (settings == null || !settings.CanUpdateDatabase)
            {
                return;
            }

            await settings.UpdateDatabaseAsync();
            ReloadGames();
            RefreshUpdateDatabaseButton();
        }

        private void RefreshUpdateDatabaseButton()
        {
            if (UpdateDatabaseButton == null)
            {
                return;
            }

            UpdateDatabaseButton.IsEnabled = settings?.CanUpdateDatabase == true;
            UpdateDatabaseButton.ToolTip = settings?.DatabaseUpdateStatus;
        }

        private void OnGameTitleClick(object sender, RoutedEventArgs args)
        {
            var row = (sender as FrameworkElement)?.DataContext as HowLongToBeatGameRow;
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

        private static bool TryNavigateThroughOsiris(
            object viewModel,
            HowLongToBeatGameRow row)
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

                navigateToGame.Invoke(
                    null,
                    new object[] { viewModel, row.SourceGame, row.GameId });
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

    internal sealed class HowLongToBeatGameRow
    {
        public Game SourceGame { get; }
        public Guid GameId { get; }
        public string Name { get; }
        public string IconPath { get; }
        public string FallbackIconPath { get; }
        public string TimeProfile { get; }
        public ulong PlayedSeconds { get; }
        public string PlayedTimeText { get; }
        public long MainStorySeconds { get; }
        public long MainExtraSeconds { get; }
        public long CompletionistSeconds { get; }
        public long LongestTimeSeconds { get; }
        public bool HasTimes => LongestTimeSeconds > 0;
        public string StatusText { get; }

        public HowLongToBeatGameRow(
            Game game,
            string iconPath,
            string fallbackIconPath,
            string timeProfile,
            long mainStorySeconds,
            long mainExtraSeconds,
            long completionistSeconds)
        {
            SourceGame = game;
            GameId = game?.Id ?? Guid.Empty;
            Name = string.IsNullOrWhiteSpace(game?.Name) ? "Unnamed Game" : game.Name;
            IconPath = iconPath;
            FallbackIconPath = fallbackIconPath;
            TimeProfile = timeProfile;
            PlayedSeconds = game?.Playtime ?? 0;
            PlayedTimeText = FormatPlayedTime(PlayedSeconds);
            MainStorySeconds = Math.Max(0, mainStorySeconds);
            MainExtraSeconds = Math.Max(0, mainExtraSeconds);
            CompletionistSeconds = Math.Max(0, completionistSeconds);
            LongestTimeSeconds = Math.Max(
                MainStorySeconds,
                Math.Max(MainExtraSeconds, CompletionistSeconds));
            StatusText = string.Empty;
        }

        private HowLongToBeatGameRow(
            Game game,
            string iconPath,
            string fallbackIconPath,
            string timeProfile,
            string statusText)
            : this(game, iconPath, fallbackIconPath, timeProfile, 0, 0, 0)
        {
            StatusText = statusText;
        }

        public static HowLongToBeatGameRow WithoutTimes(
            Game game,
            string iconPath,
            string fallbackIconPath,
            string timeProfile,
            string statusText)
        {
            return new HowLongToBeatGameRow(
                game,
                iconPath,
                fallbackIconPath,
                timeProfile,
                statusText);
        }

        private static string FormatPlayedTime(ulong seconds)
        {
            var hours = (ulong)Math.Round(seconds / 3600d, MidpointRounding.AwayFromZero);
            return hours == 1 ? "1 Hour" : hours + " Hours";
        }
    }
}
