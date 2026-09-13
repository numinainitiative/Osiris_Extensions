using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using Playnite.SDK;
using Playnite.SDK.Controls;
using Playnite.SDK.Models;

namespace Osiris.Extensions.Exophase
{
    public partial class ExophaseActivityControl : PluginUserControl, INotifyPropertyChanged
    {
        private readonly IPlayniteAPI api;
        private readonly ExophaseActivityStore activityStore;
        private readonly ExophaseGameSettingsStore gameSettingsStore;
        private readonly ExophaseActivityResolver activityResolver;
        private readonly ExophaseSettings settings;
        private Game activeGame;
        private bool subscribed;
        private bool isCardVisible;
        private bool hasActivity;
        private bool hasOverflow;
        private string statusMessage = "Select a game to view Exophase activity.";
        private IReadOnlyList<ExophasePlatformRow> platforms =
            new List<ExophasePlatformRow>();
        private bool useTotalPlaytime;
        private string displayPlaytimeText = string.Empty;
        private string displayPlaytimeLabel = "TIME PLAYED";

        public event PropertyChangedEventHandler PropertyChanged;

        public bool IsCardVisible
        {
            get => isCardVisible;
            private set => SetField(ref isCardVisible, value);
        }

        public bool HasActivity
        {
            get => hasActivity;
            private set => SetField(ref hasActivity, value);
        }

        public bool HasOverflow
        {
            get => hasOverflow;
            private set => SetField(ref hasOverflow, value);
        }

        public string StatusMessage
        {
            get => statusMessage;
            private set => SetField(ref statusMessage, value);
        }

        public IReadOnlyList<ExophasePlatformRow> Platforms
        {
            get => platforms;
            private set => SetField(ref platforms, value);
        }

        public bool UseTotalPlaytime
        {
            get => useTotalPlaytime;
            private set => SetField(ref useTotalPlaytime, value);
        }

        public string DisplayPlaytimeText
        {
            get => displayPlaytimeText;
            private set => SetField(ref displayPlaytimeText, value);
        }

        public string DisplayPlaytimeLabel
        {
            get => displayPlaytimeLabel;
            private set => SetField(ref displayPlaytimeLabel, value);
        }

        internal ExophaseActivityControl(
            IPlayniteAPI api,
            ExophaseActivityStore activityStore,
            ExophaseGameSettingsStore gameSettingsStore,
            ExophaseActivityResolver activityResolver,
            ExophaseSettings settings)
        {
            this.api = api ?? throw new ArgumentNullException(nameof(api));
            this.activityStore = activityStore ?? throw new ArgumentNullException(nameof(activityStore));
            this.gameSettingsStore = gameSettingsStore ?? throw new ArgumentNullException(nameof(gameSettingsStore));
            this.activityResolver = activityResolver ?? throw new ArgumentNullException(nameof(activityResolver));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            InitializeComponent();
            DataContext = this;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public override void GameContextChanged(Game oldContext, Game newContext)
        {
            activeGame = newContext;
            IsCardVisible = false;
            Refresh();
        }

        internal static bool HasDisplayableActivity(ExophaseResolvedActivity activity)
        {
            return activity != null &&
                   activity.Enabled &&
                   activity.Platforms != null &&
                   activity.Platforms.Count > 0;
        }

        internal static string FormatCompactPlaytime(ulong seconds)
        {
            var totalMinutes = seconds / 60UL;
            if (totalMinutes == 0)
            {
                return "0m";
            }

            var hours = totalMinutes / 60UL;
            var minutes = totalMinutes % 60UL;
            if (hours == 0)
            {
                return minutes.ToString(CultureInfo.InvariantCulture) + "m";
            }

            return minutes == 0
                ? hours.ToString(CultureInfo.InvariantCulture) + "h"
                : hours.ToString(CultureInfo.InvariantCulture) + "h " +
                  minutes.ToString(CultureInfo.InvariantCulture) + "m";
        }

        internal static string FormatDisplayPlaytime(ulong seconds)
        {
            var totalMinutes = seconds / 60UL;
            var hours = totalMinutes / 60UL;
            var minutes = totalMinutes % 60UL;
            if (hours == 0)
            {
                return minutes.ToString(CultureInfo.InvariantCulture) +
                       (minutes == 1 ? " Minute" : " Minutes");
            }

            if (minutes == 0)
            {
                return hours.ToString(CultureInfo.InvariantCulture) +
                       (hours == 1 ? " Hour" : " Hours");
            }

            return hours.ToString(CultureInfo.InvariantCulture) + "h " +
                   minutes.ToString(CultureInfo.InvariantCulture) + "m";
        }

        internal static string GetSourceTag(ExophaseResolvedPlatform platform)
        {
            if (platform != null && platform.IsBaseline)
            {
                return "Local";
            }

            return platform != null && platform.IsManual
                ? "Manual"
                : "Exophase";
        }

        private void Refresh()
        {
            var game = activeGame;
            IsCardVisible = false;
            HasActivity = false;
            HasOverflow = false;
            Platforms = new List<ExophasePlatformRow>();
            UseTotalPlaytime = false;
            DisplayPlaytimeText = string.Empty;
            DisplayPlaytimeLabel = "TIME PLAYED";
            if (game == null)
            {
                IsCardVisible = false;
                StatusMessage = "Select a game to view Exophase activity.";
                return;
            }

            var editableActivity = activityResolver.Resolve(game, false);
            var resolvedActivity = activityResolver.Resolve(game, true);
            if (!resolvedActivity.Enabled)
            {
                StatusMessage = "Exophase is disabled for this game.";
                return;
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
                    StatusMessage = "More than one Osiris game matches this title, so Exophase activity cannot be assigned safely.";
                    return;
                }
            }

            if (!HasDisplayableActivity(editableActivity))
            {
                StatusMessage = editableActivity.SnapshotIsInvalid
                    ? "Exophase activity data could not be read. Synchronize again in extension settings."
                    : editableActivity.HasSnapshot
                        ? "No Exophase activity was found for this game. You can add a platform in Game Edit."
                        : "Synchronize Exophase in extension settings to load platform activity for this game.";
                return;
            }

            UseTotalPlaytime = settings.OverrideDisplayedPlaytime &&
                               resolvedActivity.CanDisplayTotal;
            if (UseTotalPlaytime)
            {
                DisplayPlaytimeText = FormatDisplayPlaytime(resolvedActivity.TotalPlaytimeSeconds);
                DisplayPlaytimeLabel = "TOTAL TIME PLAYED";
            }

            var sourcePlatforms = resolvedActivity.Platforms
                .OrderByDescending(platform => platform.PlaytimeSeconds)
                .ThenBy(platform => platform.Platform, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var rows = sourcePlatforms
                .Select(platform =>
                {
                    var visual = ExophasePlatformVisualCatalog.Resolve(platform.Platform);
                    return new ExophasePlatformRow
                    {
                        Platform = visual.DisplayName,
                        SourceTag = GetSourceTag(platform),
                        PlaytimeText = FormatCompactPlaytime(platform.PlaytimeSeconds),
                        IconGeometry = visual.IconGeometry,
                        UseOsirisLogo = visual.UseOsirisLogo
                    };
                })
                .ToList();
            for (var index = 0; index < rows.Count; index++)
            {
                rows[index].IsLast = index == rows.Count - 1;
            }

            Platforms = rows;
            HasOverflow = rows.Count > 2;
            StatusMessage = string.Empty;
            HasActivity = true;
            IsCardVisible = rows.Count > 0;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (subscribed)
            {
                return;
            }

            activityStore.SnapshotChanged += OnSnapshotChanged;
            gameSettingsStore.SettingsChanged += OnGameSettingsChanged;
            settings.PropertyChanged += OnSettingsChanged;
            subscribed = true;
            Refresh();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (!subscribed)
            {
                return;
            }

            activityStore.SnapshotChanged -= OnSnapshotChanged;
            gameSettingsStore.SettingsChanged -= OnGameSettingsChanged;
            settings.PropertyChanged -= OnSettingsChanged;
            subscribed = false;
        }

        private void OnSnapshotChanged()
        {
            Dispatcher.BeginInvoke(new Action(Refresh));
        }

        private void OnGameSettingsChanged(Guid gameId)
        {
            if (activeGame != null && activeGame.Id == gameId)
            {
                Dispatcher.BeginInvoke(new Action(Refresh));
            }
        }

        private void OnSettingsChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(ExophaseSettings.OverrideDisplayedPlaytime), StringComparison.Ordinal))
            {
                Dispatcher.BeginInvoke(new Action(Refresh));
            }
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

    public sealed class ExophasePlatformRow
    {
        public string Platform { get; set; }

        public string PlaytimeText { get; set; }

        public string SourceTag { get; set; }

        public Geometry IconGeometry { get; set; }

        public bool UseOsirisLogo { get; set; }

        public bool IsLast { get; set; }
    }
}
