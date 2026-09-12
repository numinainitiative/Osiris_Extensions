using System.IO;
using System.Reflection;
using Playnite.SDK;
using Playnite.SDK.Plugins;

namespace Osiris.Extensions.HowLongToBeat
{
    public sealed class HowLongToBeatGlobalPageSidebarItem : SidebarItem
    {
        public bool OsirisGlobalPage => true;

        public string OsirisGlobalPageIconPath { get; }

        public HowLongToBeatGlobalPageSidebarItem()
            : this(null, null, null, null)
        {
        }

        internal HowLongToBeatGlobalPageSidebarItem(
            IPlayniteAPI api,
            CompletionTimeCache cache,
            CompletionTimeGameSettingsStore gameSettingsStore,
            HowLongToBeatSettings settings)
        {
            OsirisGlobalPageIconPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,
                "icon.png");

            Type = SiderbarItemType.View;
            Title = "How Long To Beat";
            Icon = OsirisGlobalPageIconPath;
            Opened = () => new HowLongToBeatGlobalPageControl(api, cache, gameSettingsStore, settings);
        }
    }
}
