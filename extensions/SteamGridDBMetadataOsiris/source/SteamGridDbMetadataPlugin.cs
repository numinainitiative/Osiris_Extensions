using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Playnite.SDK;
using Playnite.SDK.Plugins;

namespace Osiris.Extensions.SteamGridDBMetadata
{
    public sealed class SteamGridDbMetadataPlugin : MetadataPlugin
    {
        private readonly SteamGridDbSettingsViewModel settingsViewModel;

        public override Guid Id { get; } =
            new Guid("8d89f55b-826c-43dc-8946-4038a6e182a2");

        public override string Name => "SteamGridDB Metadata";

        public override List<MetadataField> SupportedFields { get; } = new List<MetadataField>
        {
            MetadataField.Icon,
            MetadataField.CoverImage,
            MetadataField.BackgroundImage
        };

        public SteamGridDbMetadataPlugin(IPlayniteAPI playniteApi)
            : base(playniteApi)
        {
            Properties = new MetadataPluginProperties { HasSettings = true };
            settingsViewModel = new SteamGridDbSettingsViewModel(this);
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settingsViewModel;
        }

        public override UserControl GetSettingsView(bool firstRunView)
        {
            return new SteamGridDbSettingsView { DataContext = settingsViewModel };
        }

        public override OnDemandMetadataProvider GetMetadataProvider(MetadataRequestOptions options)
        {
            return new SteamGridDbMetadataProvider(options, settingsViewModel.Settings.Clone());
        }
    }
}
