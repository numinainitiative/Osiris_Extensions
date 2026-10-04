using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;

namespace Osiris.Extensions.ScreenshotsGallery
{
    public sealed class ScreenshotsPlugin : GenericPlugin
    {
        private readonly ScreenshotCollector collector;
        private readonly ScreenshotsSettingsViewModel settings;
        public ScreenshotsSettingsViewModel Settings => settings;
        public override Guid Id { get; } = Guid.Parse("f3dc3fd5-3d6d-4aa0-8762-2d325bb1d7fe");

        public ScreenshotsPlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new GenericPluginProperties { HasSettings = true };
            settings = new ScreenshotsSettingsViewModel(this);
            AddSettingsSupport(new AddSettingsSupportArgs { SourceName = "ScreenshotsGallery", SettingsRoot = "Settings.Settings" });
            collector = new ScreenshotCollector(api.Paths.ConfigurationPath, WindowsScreenshotFolder.Resolve,
                message => LogManager.GetLogger().Warn(message));
            AddCustomElementSupport(new AddCustomElementSupportArgs
            {
                SourceName = "ScreenshotsGallery",
                ElementList = new List<string> { "ScreenshotsViewControl" }
            });
        }

        public override Control GetGameViewControl(GetGameViewControlArgs args)
        {
            return args.Name == "ScreenshotsViewControl"
                ? new ScreenshotsViewControl(PlayniteApi.Paths.ConfigurationPath, () => settings.Settings.ExpandedExperience) : null;
        }

        public override ISettings GetSettings(bool firstRunSettings) => settings;
        public override UserControl GetSettingsView(bool firstRunSettings) => new ScreenshotsSettingsView { DataContext = settings };

        public override void OnGameStarted(OnGameStartedEventArgs args)
        {
            if (args?.Game != null) collector.Start(args.Game.Id);
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            if (args?.Game != null) collector.Stop(args.Game.Id);
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            collector.Dispose();
        }
    }
}
