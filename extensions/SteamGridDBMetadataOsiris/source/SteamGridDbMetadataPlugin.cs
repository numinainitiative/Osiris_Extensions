using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Controls;
using Playnite.SDK;
using Playnite.SDK.Plugins;

namespace Osiris.Extensions.SteamGridDBMetadata
{
    public sealed class SteamGridDbMetadataPlugin : MetadataPlugin
    {
        private readonly SteamGridDbSettingsViewModel settingsViewModel;
        private readonly SteamGridDbOsirisRuntime osirisRuntime;

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
            osirisRuntime = new SteamGridDbOsirisRuntime(() => settingsViewModel.Settings.Clone());
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

        // Runtime contract consumed by Osiris's media picker. Keeping the API key
        // and all SteamGridDB requests in this extension makes disable/uninstall
        // authoritative and prevents the theme from discovering stale settings.
        public bool IsConfiguredForOsiris() => osirisRuntime.IsConfigured;

        public string SearchGamesForOsiris(string query) => osirisRuntime.SearchGamesJson(query);

        public string SearchGamesForOsirisCancelable(string query, CancellationToken cancellationToken) =>
            osirisRuntime.SearchGamesJson(query, cancellationToken);

        public string GetArtworkPageForOsiris(
            string artworkKind,
            string gameIds,
            string assetType,
            int requestedPage,
            string dimensions) =>
            osirisRuntime.GetArtworkPageJson(
                artworkKind,
                gameIds,
                assetType,
                requestedPage,
                dimensions);

        public string GetArtworkPageForOsirisCancelable(
            string artworkKind,
            string gameIds,
            string assetType,
            int requestedPage,
            string dimensions,
            CancellationToken cancellationToken) =>
            osirisRuntime.GetArtworkPageJson(
                artworkKind,
                gameIds,
                assetType,
                requestedPage,
                dimensions,
                cancellationToken);

        public string GetArtworkPageForOsirisCancelableV2(
            string artworkKind,
            string gameIds,
            string assetType,
            int requestedPage,
            string dimensions,
            int displayPageSize,
            CancellationToken cancellationToken) =>
            osirisRuntime.GetArtworkPageJson(
                artworkKind,
                gameIds,
                assetType,
                requestedPage,
                dimensions,
                displayPageSize,
                cancellationToken);

        public string GetPreferredArtworkUrlForOsiris(
            string artworkKind,
            string gameIds,
            string dimensions,
            bool useAlternateForSimilarRole) =>
            osirisRuntime.GetPreferredArtworkUrl(
                artworkKind,
                gameIds,
                dimensions,
                useAlternateForSimilarRole);

        public byte[] DownloadArtworkForOsiris(string url, CancellationToken cancellationToken) =>
            osirisRuntime.DownloadArtwork(url, cancellationToken);
    }
}
