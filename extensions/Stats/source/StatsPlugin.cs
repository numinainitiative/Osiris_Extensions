using System;
using System.Collections.Generic;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;

namespace Osiris.Extensions.Stats
{
    public sealed class StatsPlugin : GenericPlugin
    {
        public static StatsPlugin Current { get; private set; }

        private readonly StatsGlobalPageSidebarItem globalPage;
        private readonly StatsSessionLedger sessionLedger;
        private readonly StatsHomeWeeklyProvider homeWeeklyProvider;
        private readonly StatsInsightsProvider insights;
        private readonly StatsSettingsViewModel settings;

        public override Guid Id { get; } = Guid.Parse("511681f5-d2f5-41ae-8b73-fa3adcc86c85");

        public StatsPlugin(IPlayniteAPI api) : base(api)
        {
            Current = this;
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };
            sessionLedger = new StatsSessionLedger(api);
            homeWeeklyProvider = new StatsHomeWeeklyProvider(api, sessionLedger);
            settings = new StatsSettingsViewModel(this);
            insights = new StatsInsightsProvider(api, sessionLedger, () => settings.Committed);
            AddCustomElementSupport(new AddCustomElementSupportArgs { SourceName = "Stats",
                ElementList = new List<string> { "InsightsViewControl" } });
            globalPage = new StatsGlobalPageSidebarItem(api, sessionLedger);
        }

        public string GetHomeWeeklyStatsForOsiris()
        {
            return homeWeeklyProvider.GetSnapshotJson();
        }

        public string GetHomeInsightForOsiris() => insights.Home();
        public string GetHomeInsightSegmentsForOsiris() => insights.HomeJson();
        public override ISettings GetSettings(bool firstRunSettings) => settings;
        public override System.Windows.Controls.UserControl GetSettingsView(bool firstRunSettings) => new StatsSettingsView { DataContext = settings };
        internal void InsightsSettingsChanged() => insights.ResetHome();
        public override System.Windows.Controls.Control GetGameViewControl(GetGameViewControlArgs args) =>
            args.Name == "InsightsViewControl" ? new InsightsViewControl(PlayniteApi, insights) : null;

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            yield return globalPage;
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            sessionLedger.Record(args?.Game, args?.ElapsedSeconds ?? 0UL, DateTime.UtcNow);
        }
    }
}
