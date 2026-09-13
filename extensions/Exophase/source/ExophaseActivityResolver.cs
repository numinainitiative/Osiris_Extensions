using System;
using System.Collections.Generic;
using System.Linq;
using Playnite.SDK.Models;

namespace Osiris.Extensions.Exophase
{
    internal sealed class ExophaseResolvedActivity
    {
        public bool Enabled { get; set; }

        public bool HasSnapshot { get; set; }

        public bool SnapshotIsInvalid { get; set; }

        public DateTime FetchedUtc { get; set; }

        public List<ExophaseResolvedPlatform> Platforms { get; set; } =
            new List<ExophaseResolvedPlatform>();

        public ulong TotalPlaytimeSeconds { get; set; }

        public int PositivePlatformCount => Platforms.Count(item => item.PlaytimeSeconds > 0);

        public bool CanDisplayTotal
        {
            get
            {
                var hasNativeBaseline = Platforms.Any(item => item.IsBaseline);
                return hasNativeBaseline
                    ? Platforms.Any(item => !item.IsBaseline && item.PlaytimeSeconds > 0)
                    : PositivePlatformCount >= 2;
            }
        }
    }

    internal sealed class ExophaseResolvedPlatform
    {
        public string Id { get; set; }

        public string SourceKey { get; set; }

        public string Platform { get; set; }

        public ulong PlaytimeSeconds { get; set; }

        public ulong OriginalPlaytimeSeconds { get; set; }

        public int EarnedAwards { get; set; }

        public int TotalAwards { get; set; }

        public bool IsManual { get; set; }

        public bool IsBaseline { get; set; }
    }

    internal sealed class ExophaseActivityResolver
    {
        private static readonly Guid SteamLibraryId =
            new Guid("cb91dfc9-b977-43bf-8e70-55f46e410fab");
        private static readonly Guid XboxLibraryId =
            new Guid("7e4fbb5e-2ae3-48d4-8ba0-6b30e7a4e287");

        private readonly ExophaseActivityStore activityStore;
        private readonly ExophaseGameSettingsStore gameSettingsStore;

        public ExophaseActivityResolver(
            ExophaseActivityStore activityStore,
            ExophaseGameSettingsStore gameSettingsStore)
        {
            this.activityStore = activityStore ?? throw new ArgumentNullException(nameof(activityStore));
            this.gameSettingsStore = gameSettingsStore ?? throw new ArgumentNullException(nameof(gameSettingsStore));
        }

        public ExophaseResolvedActivity Resolve(Game game, bool includeBaseline)
        {
            if (game == null)
            {
                return new ExophaseResolvedActivity();
            }

            var stored = gameSettingsStore.Load(game.Id);
            var lookup = activityStore.FindGame(game.Name);
            var imported = lookup.Game?.Platforms ?? new List<ExophasePlatformActivity>();
            var platforms = new List<ExophaseResolvedPlatform>();
            foreach (var source in imported)
            {
                var sourceKey = NormalizePlatformKey(source.Platform);
                var saved = stored.Platforms.FirstOrDefault(item =>
                    !item.IsManual &&
                    (string.Equals(item.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(
                         NormalizePlatformKey(item.Platform),
                         sourceKey,
                         StringComparison.OrdinalIgnoreCase)));
                platforms.Add(new ExophaseResolvedPlatform
                {
                    Id = "exophase:" + sourceKey,
                    SourceKey = sourceKey,
                    Platform = string.IsNullOrWhiteSpace(source.Platform) ? "Unknown" : source.Platform.Trim(),
                    PlaytimeSeconds = saved?.PlaytimeSeconds ?? source.PlaytimeSeconds,
                    OriginalPlaytimeSeconds = source.PlaytimeSeconds,
                    EarnedAwards = source.EarnedAwards,
                    TotalAwards = source.TotalAwards,
                    IsManual = false
                });
            }

            foreach (var manual in stored.Platforms.Where(item => item.IsManual))
            {
                platforms.Add(new ExophaseResolvedPlatform
                {
                    Id = manual.Id,
                    SourceKey = NormalizePlatformKey(manual.Platform),
                    Platform = manual.Platform,
                    PlaytimeSeconds = manual.PlaytimeSeconds,
                    OriginalPlaytimeSeconds = manual.PlaytimeSeconds,
                    IsManual = true
                });
            }

            platforms = platforms
                .GroupBy(item => NormalizePlatformKey(item.Platform), StringComparer.OrdinalIgnoreCase)
                .Select(MergeDuplicatePlatforms)
                .ToList();

            var looksLikeLegacyImportedTotal = game.PluginId == Guid.Empty &&
                                               lookup.Game != null &&
                                               lookup.Game.TotalPlaytimeSeconds > 0 &&
                                               game.Playtime == lookup.Game.TotalPlaytimeSeconds;
            if (includeBaseline && !looksLikeLegacyImportedTotal)
            {
                ApplyNativeBaseline(platforms, game);
            }

            platforms = platforms
                .OrderByDescending(item => item.PlaytimeSeconds)
                .ThenBy(item => item.Platform, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var result = new ExophaseResolvedActivity
            {
                Enabled = stored.Enabled,
                HasSnapshot = lookup.HasSnapshot,
                SnapshotIsInvalid = lookup.SnapshotIsInvalid,
                FetchedUtc = lookup.FetchedUtc,
                Platforms = platforms
            };
            result.TotalPlaytimeSeconds = SaturatingSum(platforms.Select(item => item.PlaytimeSeconds));
            return result;
        }

        public static string NormalizePlatformKey(string platform)
        {
            var canonical = ExophasePlatformVisualCatalog.Resolve(platform).DisplayName;
            return ExophaseActivityParser.NormalizeTitle(canonical ?? string.Empty);
        }

        private static void ApplyNativeBaseline(List<ExophaseResolvedPlatform> platforms, Game game)
        {
            var baselinePlatform = ResolveBaselinePlatform(game);
            var baselineKey = NormalizePlatformKey(baselinePlatform);
            var existing = platforms.FirstOrDefault(item =>
                string.Equals(
                    NormalizePlatformKey(item.Platform),
                    baselineKey,
                    StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                // The native database value is authoritative for the game's
                // current library source. Exophase only contributes time from
                // different platforms, so the matching remote bucket is
                // deliberately replaced instead of added or max-merged.
                existing.PlaytimeSeconds = game.Playtime;
                existing.IsBaseline = true;
                return;
            }

            platforms.Add(new ExophaseResolvedPlatform
            {
                Id = "baseline:" + baselineKey,
                SourceKey = baselineKey,
                Platform = baselinePlatform,
                PlaytimeSeconds = game.Playtime,
                OriginalPlaytimeSeconds = game.Playtime,
                IsBaseline = true
            });
        }

        internal static string ResolveBaselinePlatform(Game game)
        {
            if (game == null || game.PluginId == Guid.Empty)
            {
                return "Osiris";
            }

            if (game.PluginId == SteamLibraryId)
            {
                return "Steam";
            }

            if (game.PluginId == XboxLibraryId)
            {
                return "Xbox";
            }

            return "Integrated Library";
        }

        private static ExophaseResolvedPlatform MergeDuplicatePlatforms(
            IGrouping<string, ExophaseResolvedPlatform> group)
        {
            var values = group.ToList();
            var strongest = values
                .OrderByDescending(item => item.PlaytimeSeconds)
                .ThenBy(item => item.IsManual)
                .First();
            return new ExophaseResolvedPlatform
            {
                Id = strongest.Id,
                SourceKey = strongest.SourceKey,
                Platform = strongest.Platform,
                PlaytimeSeconds = strongest.PlaytimeSeconds,
                OriginalPlaytimeSeconds = strongest.OriginalPlaytimeSeconds,
                EarnedAwards = values.Max(item => item.EarnedAwards),
                TotalAwards = values.Max(item => item.TotalAwards),
                IsManual = values.All(item => item.IsManual),
                IsBaseline = values.Any(item => item.IsBaseline)
            };
        }

        private static ulong SaturatingSum(IEnumerable<ulong> values)
        {
            ulong total = 0;
            foreach (var value in values)
            {
                total = ulong.MaxValue - total < value ? ulong.MaxValue : total + value;
            }

            return total;
        }
    }
}
