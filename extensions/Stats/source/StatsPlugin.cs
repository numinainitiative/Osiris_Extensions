using System;
using System.Collections.Generic;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;

namespace Osiris.Extensions.Stats
{
    public sealed class StatsPlugin : GenericPlugin
    {
        private readonly StatsGlobalPageSidebarItem globalPage;
        private readonly StatsSessionLedger sessionLedger;

        public override Guid Id { get; } = Guid.Parse("511681f5-d2f5-41ae-8b73-fa3adcc86c85");

        public StatsPlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new GenericPluginProperties
            {
                HasSettings = false
            };
            sessionLedger = new StatsSessionLedger(api);
            globalPage = new StatsGlobalPageSidebarItem(api, sessionLedger);
        }

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
