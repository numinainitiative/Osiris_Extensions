using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Playnite.SDK;
using Playnite.SDK.Controls;
using Playnite.SDK.Models;

namespace Osiris.Extensions.HowLongToBeat
{
    public partial class CompletionTimesControl : PluginUserControl, INotifyPropertyChanged
    {
        private static readonly ILogger Logger = LogManager.GetLogger();
        private readonly HowLongToBeatClient client;
        private readonly CompletionTimeCache cache;
        private readonly CompletionTimeGameSettingsStore gameSettingsStore;
        private readonly HowLongToBeatSettings settings;
        private CancellationTokenSource loadCancellation;
        private Game activeGame;
        private bool subscribedToSettings;
        private bool isCardVisible;
        private bool hasResult;
        private string statusMessage = "Select a game to view completion estimates.";
        private string mainStoryText = "—";
        private string mainExtraText = "—";
        private string completionistText = "—";
        private string timeProfileText = CompletionTimeProfiles.Average;
        private double mainStoryProgress;
        private double mainExtraProgress;
        private double completionistProgress;

        public event PropertyChangedEventHandler PropertyChanged;

        public bool IsCardVisible
        {
            get => isCardVisible;
            private set => SetField(ref isCardVisible, value);
        }

        public bool HasResult
        {
            get => hasResult;
            private set => SetField(ref hasResult, value);
        }

        public string StatusMessage
        {
            get => statusMessage;
            private set => SetField(ref statusMessage, value);
        }

        public string MainStoryText
        {
            get => mainStoryText;
            private set => SetField(ref mainStoryText, value);
        }

        public string MainExtraText
        {
            get => mainExtraText;
            private set => SetField(ref mainExtraText, value);
        }

        public string CompletionistText
        {
            get => completionistText;
            private set => SetField(ref completionistText, value);
        }

        public string TimeProfileText
        {
            get => timeProfileText;
            private set => SetField(ref timeProfileText, value);
        }

        public double MainStoryProgress
        {
            get => mainStoryProgress;
            private set => SetField(ref mainStoryProgress, value);
        }

        public double MainExtraProgress
        {
            get => mainExtraProgress;
            private set => SetField(ref mainExtraProgress, value);
        }

        public double CompletionistProgress
        {
            get => completionistProgress;
            private set => SetField(ref completionistProgress, value);
        }

        internal CompletionTimesControl(
            HowLongToBeatClient client,
            CompletionTimeCache cache,
            CompletionTimeGameSettingsStore gameSettingsStore,
            HowLongToBeatSettings settings)
        {
            this.client = client;
            this.cache = cache;
            this.gameSettingsStore = gameSettingsStore;
            this.settings = settings;
            InitializeComponent();
            DataContext = this;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public override void GameContextChanged(Game oldContext, Game newContext)
        {
            CancelPendingLoad();
            activeGame = newContext;
            var gameSettings = newContext == null
                ? null
                : gameSettingsStore.Load(newContext.Id);
            IsCardVisible = newContext != null && gameSettings.Enabled && settings.HasVisibleTimes;
            HasResult = false;
            StatusMessage = newContext == null
                ? "Select a game to view completion estimates."
                : "Loading completion estimates...";

            if (newContext == null || !gameSettings.Enabled || !settings.HasVisibleTimes)
            {
                return;
            }

            loadCancellation = new CancellationTokenSource();
            _ = LoadAsync(newContext, gameSettings, loadCancellation.Token);
        }

        private async Task LoadAsync(
            Game game,
            CompletionTimeGameSettings gameSettings,
            CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(350, cancellationToken).ConfigureAwait(false);
                var releaseYear = game.ReleaseDate?.Year;
                var isManual = gameSettings.ManualResult?.Found == true &&
                               gameSettings.ManualResult.RemoteGameId > 0;
                var storedPerGameResult = isManual
                    ? gameSettings.ManualResult
                    : gameSettings.AutomaticResult;
                var result = storedPerGameResult?.Clone();
                CompletionTimeResult cachedManualFallback = null;
                if (isManual && result?.RemoteGameId > 0 && !result.HasDetailedProfiles)
                {
                    CompletionTimeResult cachedManualResult;
                    if (cache.TryGet(game.Name, releaseYear, true, out cachedManualResult) &&
                        cachedManualResult?.RemoteGameId == result.RemoteGameId &&
                        cachedManualResult.HasDetailedProfiles)
                    {
                        if (cachedManualResult.FetchedUtc >= result.FetchedUtc)
                        {
                            result = cachedManualResult;
                        }
                        else
                        {
                            cachedManualFallback = cachedManualResult;
                        }
                    }
                }

                if (result == null && !cache.TryGet(game.Name, releaseYear, true, out result))
                {
                    CompletionTimeResult staleResult;
                    cache.TryGet(game.Name, releaseYear, false, out staleResult);
                    try
                    {
                        result = await client.SearchAsync(game.Name, releaseYear, cancellationToken).ConfigureAwait(false);
                        try
                        {
                            cache.Store(game.Name, releaseYear, result);
                        }
                        catch (Exception cacheException)
                        {
                            Logger.Warn(cacheException, "HowLongToBeat could not save its completion-time cache.");
                        }
                    }
                    catch when (staleResult?.Found == true && !cancellationToken.IsCancellationRequested)
                    {
                        result = staleResult;
                    }
                }

                if (result?.Found == true && result.RemoteGameId > 0 && !result.HasDetailedProfiles)
                {
                    try
                    {
                        var detailedResult = await client.GetDetailsAsync(
                            result.RemoteGameId,
                            cancellationToken).ConfigureAwait(false);
                        if (detailedResult?.Found == true)
                        {
                            result = detailedResult;
                            try
                            {
                                cache.Store(game.Name, releaseYear, result);
                            }
                            catch (Exception cacheException)
                            {
                                Logger.Warn(cacheException, "HowLongToBeat could not save its refreshed completion-time cache.");
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception detailsException)
                    {
                        Logger.Warn(detailsException, "HowLongToBeat could not load detailed completion-time profiles.");
                        if (cachedManualFallback?.Found == true)
                        {
                            result = cachedManualFallback;
                        }
                    }
                }

                if (result?.Found == true &&
                    result.RemoteGameId > 0 &&
                    (storedPerGameResult == null ||
                     storedPerGameResult.RemoteGameId != result.RemoteGameId ||
                     !storedPerGameResult.HasSameTimesAs(result)))
                {
                    try
                    {
                        gameSettingsStore.StoreFetchedResult(
                            game.Id,
                            isManual,
                            game.Name,
                            releaseYear,
                            result,
                            false);
                    }
                    catch (Exception settingsException)
                    {
                        Logger.Warn(settingsException, "HowLongToBeat could not embed completion times in the game's extension record.");
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                await Dispatcher.InvokeAsync(() => ApplyResult(game, result), System.Windows.Threading.DispatcherPriority.DataBind, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Logger.Warn(exception, "HowLongToBeat could not refresh completion estimates.");
                if (!cancellationToken.IsCancellationRequested)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        HasResult = false;
                        StatusMessage = "Completion estimates are unavailable.";
                    });
                }
            }
        }

        private void ApplyResult(Game game, CompletionTimeResult result)
        {
            if (result?.Found != true || !result.HasAnyTime)
            {
                HasResult = false;
                StatusMessage = "No completion estimates found.";
                return;
            }

            var mainStorySeconds = result.GetMainStorySeconds(settings.TimeProfile);
            var mainExtraSeconds = result.GetMainExtraSeconds(settings.TimeProfile);
            var completionistSeconds = result.GetCompletionistSeconds(settings.TimeProfile);
            var hasSelectedEstimate = (settings.ShowMainStory && mainStorySeconds > 0) ||
                                      (settings.ShowMainExtras && mainExtraSeconds > 0) ||
                                      (settings.ShowCompletionist && completionistSeconds > 0);
            if (!hasSelectedEstimate)
            {
                HasResult = false;
                StatusMessage = "Selected completion estimates are unavailable.";
                return;
            }

            MainStoryRow.Visibility = settings.ShowMainStory ? Visibility.Visible : Visibility.Collapsed;
            MainExtraRow.Visibility = settings.ShowMainExtras ? Visibility.Visible : Visibility.Collapsed;
            CompletionistRow.Visibility = settings.ShowCompletionist ? Visibility.Visible : Visibility.Collapsed;
            UpdateRowGeometry();
            MainStoryText = CompletionTimeFormatting.Format(mainStorySeconds);
            MainExtraText = CompletionTimeFormatting.Format(mainExtraSeconds);
            CompletionistText = CompletionTimeFormatting.Format(completionistSeconds);
            MainStoryProgress = CompletionTimeFormatting.ProgressPercent(game?.Playtime ?? 0, mainStorySeconds);
            MainExtraProgress = CompletionTimeFormatting.ProgressPercent(game?.Playtime ?? 0, mainExtraSeconds);
            CompletionistProgress = CompletionTimeFormatting.ProgressPercent(game?.Playtime ?? 0, completionistSeconds);
            TimeProfileText = settings.TimeProfile;
            StatusMessage = string.Empty;
            HasResult = true;
        }

        private void UpdateRowGeometry()
        {
            PrepareRow(MainStoryRow, settings.ShowMainStory);
            PrepareRow(MainExtraRow, settings.ShowMainExtras);
            PrepareRow(CompletionistRow, settings.ShowCompletionist);

            var lastVisibleRow = settings.ShowCompletionist
                ? CompletionistRow
                : settings.ShowMainExtras
                    ? MainExtraRow
                    : MainStoryRow;
            lastVisibleRow.Margin = new Thickness(0);
            lastVisibleRow.CornerRadius = new CornerRadius(0, 0, 12, 12);
        }

        private static void PrepareRow(Border row, bool isVisible)
        {
            row.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            row.Margin = new Thickness(0, 0, 0, 8);
            row.CornerRadius = new CornerRadius(0);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            CancelPendingLoad();
            if (subscribedToSettings)
            {
                gameSettingsStore.SettingsChanged -= OnGameSettingsChanged;
                settings.SettingsChanged -= OnGlobalSettingsChanged;
                subscribedToSettings = false;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (subscribedToSettings)
            {
                return;
            }

            gameSettingsStore.SettingsChanged += OnGameSettingsChanged;
            settings.SettingsChanged += OnGlobalSettingsChanged;
            subscribedToSettings = true;
        }

        private void OnGlobalSettingsChanged()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var current = activeGame;
                if (current != null)
                {
                    GameContextChanged(current, current);
                }
            }));
        }

        private void OnGameSettingsChanged(Guid gameId)
        {
            var current = activeGame;
            if (current == null || current.Id != gameId)
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                var latest = activeGame;
                if (latest != null && latest.Id == gameId)
                {
                    GameContextChanged(latest, latest);
                }
            }));
        }

        private void CancelPendingLoad()
        {
            var previous = Interlocked.Exchange(ref loadCancellation, null);
            if (previous == null)
            {
                return;
            }

            previous.Cancel();
            previous.Dispose();
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
