using System.IO;
using System.Reflection;
using Playnite.SDK;
using Playnite.SDK.Plugins;

namespace Osiris.Extensions.Stats
{
    public sealed class StatsGlobalPageSidebarItem : SidebarItem
    {
        public bool OsirisGlobalPage => true;

        public string OsirisGlobalPageIconPath { get; }

        internal StatsGlobalPageSidebarItem(IPlayniteAPI api, StatsSessionLedger sessionLedger)
        {
            OsirisGlobalPageIconPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,
                "icon.png");

            Type = SiderbarItemType.View;
            Title = "Stats";
            Icon = OsirisGlobalPageIconPath;
            Opened = () => new StatsGlobalPageControl(api, sessionLedger);
        }
    }
}
