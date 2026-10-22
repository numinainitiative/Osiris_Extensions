using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Playnite.SDK;
using Playnite.SDK.Plugins;

namespace Osiris.Extensions.FunGames
{
    public sealed class FunGamesPlugin : GenericPlugin
    {
        private readonly FunGamesSidebarItem page;
        public override Guid Id { get; } = Guid.Parse("9ac287c1-0c9c-4453-b668-9857c4e7d7e6");

        public FunGamesPlugin(IPlayniteAPI api) : base(api)
        {
            page = new FunGamesSidebarItem(api,GetPluginUserDataPath());
            Properties = new GenericPluginProperties { HasSettings = false };
        }

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            yield return page;
        }
    }

    public sealed class FunGamesSidebarItem : SidebarItem
    {
        public bool OsirisGlobalPage => true;
        public string OsirisGlobalPageIconPath { get; }

        public FunGamesSidebarItem() : this(null,null) { }

        internal FunGamesSidebarItem(IPlayniteAPI api,string dataFolder)
        {
            OsirisGlobalPageIconPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,
                "icon.png");
            Type = SiderbarItemType.View;
            Title = "Fun Games";
            Icon = OsirisGlobalPageIconPath;
            Opened = () => new FunGamesPage(api,dataFolder);
        }
    }
}
