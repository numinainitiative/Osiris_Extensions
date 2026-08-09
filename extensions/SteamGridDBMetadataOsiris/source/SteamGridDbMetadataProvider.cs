using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace Osiris.Extensions.SteamGridDBMetadata
{
    internal sealed class SteamGridDbMetadataProvider : OnDemandMetadataProvider
    {
        private static readonly string[] VerticalCoverDimensions =
        {
            "600x900",
            "342x482",
            "660x930"
        };

        private readonly MetadataRequestOptions options;
        private readonly SteamGridDbSettings settings;
        private SteamGridDbApiClient client;
        private SteamGridDbTarget target;
        private bool targetResolved;

        public override List<MetadataField> AvailableFields { get; } = new List<MetadataField>
        {
            MetadataField.Icon,
            MetadataField.CoverImage,
            MetadataField.BackgroundImage
        };

        public SteamGridDbMetadataProvider(
            MetadataRequestOptions options,
            SteamGridDbSettings settings)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.settings = settings ?? new SteamGridDbSettings();
        }

        public override MetadataFile GetCoverImage(GetMetadataFieldArgs args)
        {
            var resolved = ResolveTarget(args.CancelToken);
            var images = ApiClient.GetImages(
                "grids",
                resolved,
                settings.AllowAdultContentArtwork,
                settings.AllowHumorousArtwork,
                VerticalCoverDimensions,
                args.CancelToken);
            if (images.Count == 0)
            {
                images = ApiClient.GetImages(
                    "grids",
                    resolved,
                    settings.AllowAdultContentArtwork,
                    settings.AllowHumorousArtwork,
                    null,
                    args.CancelToken);
            }

            return ToMetadataFile(images.FirstOrDefault());
        }

        public override MetadataFile GetBackgroundImage(GetMetadataFieldArgs args)
        {
            return GetTopImage("heroes", args.CancelToken);
        }

        public override MetadataFile GetIcon(GetMetadataFieldArgs args)
        {
            return GetTopImage("icons", args.CancelToken);
        }

        private MetadataFile GetTopImage(string artworkKind, CancellationToken cancellationToken)
        {
            var images = ApiClient.GetImages(
                artworkKind,
                ResolveTarget(cancellationToken),
                settings.AllowAdultContentArtwork,
                settings.AllowHumorousArtwork,
                null,
                cancellationToken);
            return ToMetadataFile(images.FirstOrDefault());
        }

        private SteamGridDbTarget ResolveTarget(CancellationToken cancellationToken)
        {
            if (targetResolved)
            {
                if (target == null)
                {
                    throw new InvalidOperationException("SteamGridDB could not find this game.");
                }

                return target;
            }

            targetResolved = true;
            var game = options.GameData;
            if (game == null || string.IsNullOrWhiteSpace(game.Name))
            {
                throw new InvalidOperationException("The game has no title to search on SteamGridDB.");
            }

            long steamAppId;
            if (IsSteamGame(game) && long.TryParse(game.GameId, out steamAppId) && steamAppId > 0)
            {
                target = new SteamGridDbTarget { Kind = "steam", Id = steamAppId };
                return target;
            }

            var matches = ApiClient.SearchGames(game.Name, cancellationToken);
            var normalizedName = NormalizeTitle(game.Name);
            var selected = matches.FirstOrDefault(item =>
                    string.Equals(NormalizeTitle(item.Name), normalizedName, StringComparison.Ordinal))
                ?? matches.FirstOrDefault(item => item.Verified)
                ?? matches.FirstOrDefault();
            if (selected != null)
            {
                target = new SteamGridDbTarget { Kind = "game", Id = selected.Id };
            }

            if (target == null)
            {
                throw new InvalidOperationException("SteamGridDB could not find this game.");
            }

            return target;
        }

        private static bool IsSteamGame(Game game)
        {
            var sourceName = game.Source == null ? null : game.Source.Name;
            return (!string.IsNullOrWhiteSpace(sourceName) &&
                    sourceName.IndexOf("Steam", StringComparison.OrdinalIgnoreCase) >= 0) ||
                game.PluginId == new Guid("cb91dfc9-b977-43bf-8e70-55f46e410fab");
        }

        private SteamGridDbApiClient ApiClient =>
            client ?? (client = new SteamGridDbApiClient(settings.ApiKey));

        private static string NormalizeTitle(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            foreach (var character in value.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }

        private static MetadataFile ToMetadataFile(SteamGridDbImage image)
        {
            return image == null || string.IsNullOrWhiteSpace(image.Url)
                ? null
                : new MetadataFile(image.Url);
        }
    }
}
