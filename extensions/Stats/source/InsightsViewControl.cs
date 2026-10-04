using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Controls;

namespace Osiris.Extensions.Stats
{
    public sealed class InsightsViewControl : PluginUserControl
    {
        public static readonly DependencyProperty HasInsightsProperty = DependencyProperty.Register(
            nameof(HasInsights), typeof(bool), typeof(InsightsViewControl), new PropertyMetadata(false));
        public bool HasInsights { get => (bool)GetValue(HasInsightsProperty); private set => SetValue(HasInsightsProperty, value); }
        public ObservableCollection<InsightRow> Insights { get; } = new ObservableCollection<InsightRow>();
        private readonly IPlayniteAPI api;
        private readonly StatsInsightsProvider provider;
        private readonly DispatcherTimer timer;
        private Guid gameId;
        internal InsightsViewControl(IPlayniteAPI api, StatsInsightsProvider provider)
        {
            this.api = api; this.provider = provider;
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (s, e) => Refresh();
            Loaded += (s, e) => { Refresh(); timer.Start(); };
            Unloaded += (s, e) => timer.Stop();
        }
        public override void GameContextChanged(Game oldContext, Game newContext)
        { gameId = newContext?.Id ?? Guid.Empty; Refresh(); }
        private void Refresh()
        {
            var texts = provider.ForGame(api.Database.Games.Get(gameId));
            if (string.Join("\n", texts) != string.Join("\n", Insights))
            { Insights.Clear(); foreach (var text in texts) Insights.Add(InsightRow.FromText(text)); }
            HasInsights = Insights.Count > 0;
        }
    }
}
