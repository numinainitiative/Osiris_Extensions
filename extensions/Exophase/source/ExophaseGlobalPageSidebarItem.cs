using System.IO;
using System.Reflection;
using Playnite.SDK;
using Playnite.SDK.Plugins;

namespace Osiris.Extensions.Exophase
{
    public sealed class ExophaseGlobalPageSidebarItem : SidebarItem
    {
        public bool OsirisGlobalPage => true;

        public string OsirisGlobalPageIconPath { get; }

        public ExophaseGlobalPageSidebarItem()
            : this(null, null, null, null, null, null)
        {
        }

        internal ExophaseGlobalPageSidebarItem(
            IPlayniteAPI api,
            ExophasePlugin plugin,
            ExophaseActivityStore activityStore,
            ExophaseGameSettingsStore gameSettingsStore,
            ExophaseActivityResolver activityResolver,
            ExophaseSettings settings)
        {
            OsirisGlobalPageIconPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,
                "icon.png");

            Type = SiderbarItemType.View;
            Title = "Exophase";
            Icon = OsirisGlobalPageIconPath;
            Opened = () => new ExophaseGlobalPageControl(
                api,
                plugin,
                activityStore,
                gameSettingsStore,
                activityResolver,
                settings);
        }
    }
}
