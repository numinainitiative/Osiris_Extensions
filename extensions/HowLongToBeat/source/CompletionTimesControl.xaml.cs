using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
        private CancellationTokenSource loadCancellation;
        private Game activeGame;
        private bool subscribedToSettings;
        private bool isCardVisible;
        private bool hasResult;
        private string statusMessage = "Select a game to view completion estimates.";
        private string mainStoryText = "—";
        private string mainExtraText = "—";
        private string completionistText = "—";

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

        internal CompletionTimesControl(
            HowLongToBeatClient client,
            CompletionTimeCache cache,
            CompletionTimeGameSettingsStore gameSettingsStore)
        {
            this.client = client;
            this.cache = cache;
            this.gameSettingsStore = gameSettingsStore;
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
            IsCardVisible = newContext != null && gameSettings.Enabled;
            HasResult = false;
            StatusMessage = newContext == null
                ? "Select a game to view completion estimates."
                : "Loading completion estimates...";

            if (newContext == null || !gameSettings.Enabled)
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
                var result = gameSettings.ManualResult?.Clone();
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

                cancellationToken.ThrowIfCancellationRequested();
                await Dispatcher.InvokeAsync(() => ApplyResult(result), System.Windows.Threading.DispatcherPriority.DataBind, cancellationToken);
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

        private void ApplyResult(CompletionTimeResult result)
        {
            if (result?.Found != true || !result.HasAnyTime)
            {
                HasResult = false;
                StatusMessage = "No completion estimates found.";
                return;
            }

            MainStoryText = CompletionTimeFormatting.Format(result.MainStorySeconds);
            MainExtraText = CompletionTimeFormatting.Format(result.MainExtraSeconds);
            CompletionistText = CompletionTimeFormatting.Format(result.CompletionistSeconds);
            StatusMessage = string.Empty;
            HasResult = true;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            CancelPendingLoad();
            if (subscribedToSettings)
            {
                gameSettingsStore.SettingsChanged -= OnGameSettingsChanged;
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
            subscribedToSettings = true;
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
