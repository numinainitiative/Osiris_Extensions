using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Osiris.Extensions.HowLongToBeat
{
    internal sealed class CompletionTimeResult
    {
        public bool Found { get; set; }
        public long RemoteGameId { get; set; }
        public string MatchedName { get; set; }
        public long MainStorySeconds { get; set; }
        public long MainExtraSeconds { get; set; }
        public long CompletionistSeconds { get; set; }
        public long MainStoryRushedSeconds { get; set; }
        public long MainStoryAverageSeconds { get; set; }
        public long MainStoryMedianSeconds { get; set; }
        public long MainStoryLeisureSeconds { get; set; }
        public long MainExtraRushedSeconds { get; set; }
        public long MainExtraAverageSeconds { get; set; }
        public long MainExtraMedianSeconds { get; set; }
        public long MainExtraLeisureSeconds { get; set; }
        public long CompletionistRushedSeconds { get; set; }
        public long CompletionistAverageSeconds { get; set; }
        public long CompletionistMedianSeconds { get; set; }
        public long CompletionistLeisureSeconds { get; set; }
        public DateTime FetchedUtc { get; set; }

        public bool HasAnyTime => MainStorySeconds > 0 || MainExtraSeconds > 0 || CompletionistSeconds > 0 ||
                                  HasDetailedProfiles;

        public bool HasDetailedProfiles =>
            MainStoryRushedSeconds > 0 || MainStoryAverageSeconds > 0 ||
            MainStoryMedianSeconds > 0 || MainStoryLeisureSeconds > 0 ||
            MainExtraRushedSeconds > 0 || MainExtraAverageSeconds > 0 ||
            MainExtraMedianSeconds > 0 || MainExtraLeisureSeconds > 0 ||
            CompletionistRushedSeconds > 0 || CompletionistAverageSeconds > 0 ||
            CompletionistMedianSeconds > 0 || CompletionistLeisureSeconds > 0;

        public long GetMainStorySeconds(string profile)
        {
            if (string.Equals(profile, CompletionTimeProfiles.Rushed, StringComparison.Ordinal))
            {
                return MainStoryRushedSeconds;
            }

            if (string.Equals(profile, CompletionTimeProfiles.Median, StringComparison.Ordinal))
            {
                return MainStoryMedianSeconds;
            }

            if (string.Equals(profile, CompletionTimeProfiles.Leisure, StringComparison.Ordinal))
            {
                return MainStoryLeisureSeconds;
            }

            return MainStoryAverageSeconds > 0 ? MainStoryAverageSeconds : MainStorySeconds;
        }

        public long GetMainExtraSeconds(string profile)
        {
            if (string.Equals(profile, CompletionTimeProfiles.Rushed, StringComparison.Ordinal))
            {
                return MainExtraRushedSeconds;
            }

            if (string.Equals(profile, CompletionTimeProfiles.Median, StringComparison.Ordinal))
            {
                return MainExtraMedianSeconds;
            }

            if (string.Equals(profile, CompletionTimeProfiles.Leisure, StringComparison.Ordinal))
            {
                return MainExtraLeisureSeconds;
            }

            return MainExtraAverageSeconds > 0 ? MainExtraAverageSeconds : MainExtraSeconds;
        }

        public long GetCompletionistSeconds(string profile)
        {
            if (string.Equals(profile, CompletionTimeProfiles.Rushed, StringComparison.Ordinal))
            {
                return CompletionistRushedSeconds;
            }

            if (string.Equals(profile, CompletionTimeProfiles.Median, StringComparison.Ordinal))
            {
                return CompletionistMedianSeconds;
            }

            if (string.Equals(profile, CompletionTimeProfiles.Leisure, StringComparison.Ordinal))
            {
                return CompletionistLeisureSeconds;
            }

            return CompletionistAverageSeconds > 0 ? CompletionistAverageSeconds : CompletionistSeconds;
        }

        public bool HasProfile(string profile)
        {
            return GetMainStorySeconds(profile) > 0 ||
                   GetMainExtraSeconds(profile) > 0 ||
                   GetCompletionistSeconds(profile) > 0;
        }

        public bool HasSameTimesAs(CompletionTimeResult other)
        {
            return other != null &&
                   MainStorySeconds == other.MainStorySeconds &&
                   MainExtraSeconds == other.MainExtraSeconds &&
                   CompletionistSeconds == other.CompletionistSeconds &&
                   MainStoryRushedSeconds == other.MainStoryRushedSeconds &&
                   MainStoryAverageSeconds == other.MainStoryAverageSeconds &&
                   MainStoryMedianSeconds == other.MainStoryMedianSeconds &&
                   MainStoryLeisureSeconds == other.MainStoryLeisureSeconds &&
                   MainExtraRushedSeconds == other.MainExtraRushedSeconds &&
                   MainExtraAverageSeconds == other.MainExtraAverageSeconds &&
                   MainExtraMedianSeconds == other.MainExtraMedianSeconds &&
                   MainExtraLeisureSeconds == other.MainExtraLeisureSeconds &&
                   CompletionistRushedSeconds == other.CompletionistRushedSeconds &&
                   CompletionistAverageSeconds == other.CompletionistAverageSeconds &&
                   CompletionistMedianSeconds == other.CompletionistMedianSeconds &&
                   CompletionistLeisureSeconds == other.CompletionistLeisureSeconds;
        }

        public CompletionTimeResult Clone()
        {
            return (CompletionTimeResult)MemberwiseClone();
        }
    }

    internal sealed class CompletionTimeCacheRecord
    {
        public string GameName { get; set; }
        public int? ReleaseYear { get; set; }
        public CompletionTimeResult Result { get; set; }
    }

    internal sealed class CompletionTimeGameSettings
    {
        public bool Enabled { get; set; } = true;
        public CompletionTimeResult ManualResult { get; set; }
        public CompletionTimeResult AutomaticResult { get; set; }
        public string AutomaticGameName { get; set; }
        public int? AutomaticReleaseYear { get; set; }

        public CompletionTimeGameSettings Clone()
        {
            return new CompletionTimeGameSettings
            {
                Enabled = Enabled,
                ManualResult = ManualResult?.Clone(),
                AutomaticResult = AutomaticResult?.Clone(),
                AutomaticGameName = AutomaticGameName,
                AutomaticReleaseYear = AutomaticReleaseYear
            };
        }
    }

    internal sealed class SearchCandidate
    {
        public long GameId { get; set; }
        public string GameName { get; set; }
        public string GameType { get; set; }
        public int ReleaseWorld { get; set; }
        public long MainStorySeconds { get; set; }
        public long MainExtraSeconds { get; set; }
        public long CompletionistSeconds { get; set; }

        public bool HasAnyTime => MainStorySeconds > 0 || MainExtraSeconds > 0 || CompletionistSeconds > 0;

        public static SearchCandidate FromJson(JToken token)
        {
            return new SearchCandidate
            {
                GameId = token.Value<long?>("game_id") ?? 0,
                GameName = token.Value<string>("game_name") ?? string.Empty,
                GameType = token.Value<string>("game_type") ?? string.Empty,
                ReleaseWorld = token.Value<int?>("release_world") ?? 0,
                MainStorySeconds = token.Value<long?>("comp_main") ?? 0,
                MainExtraSeconds = token.Value<long?>("comp_plus") ?? 0,
                CompletionistSeconds = token.Value<long?>("comp_100") ?? 0
            };
        }
    }

    internal sealed class CompletionDatabaseUpdateSummary
    {
        public int LibraryGames { get; set; }
        public int CheckedGames { get; set; }
        public int UpdatedGames { get; set; }
        public int UnchangedGames { get; set; }
        public int FailedGames { get; set; }
        public int SkippedGames => Math.Max(0, LibraryGames - CheckedGames);
    }

    internal static class CompletionTimeMatching
    {
        private static readonly Regex NonAlphaNumeric = new Regex("[^a-z0-9]+", RegexOptions.Compiled);

        public static SearchCandidate SelectBest(string requestedName, int? releaseYear, IEnumerable<SearchCandidate> candidates)
        {
            var requested = Normalize(requestedName);
            if (string.IsNullOrEmpty(requested))
            {
                return null;
            }

            return candidates
                .Where(candidate => candidate != null && candidate.HasAnyTime && !string.IsNullOrWhiteSpace(candidate.GameName))
                .Select(candidate => new { Candidate = candidate, Score = Score(requested, releaseYear, candidate) })
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Candidate.GameName.Length)
                .Select(item => item.Candidate)
                .FirstOrDefault();
        }

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(character);
                }
            }

            return NonAlphaNumeric.Replace(builder.ToString(), " ").Trim();
        }

        private static double Score(string requested, int? releaseYear, SearchCandidate candidate)
        {
            var candidateName = Normalize(candidate.GameName);
            double score = 0;

            if (candidateName == requested)
            {
                score += 10000;
            }
            else if (candidateName.StartsWith(requested + " ", StringComparison.Ordinal) ||
                     requested.StartsWith(candidateName + " ", StringComparison.Ordinal))
            {
                score += 1800;
            }

            var requestedTokens = new HashSet<string>(requested.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            var candidateTokens = new HashSet<string>(candidateName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            var intersection = requestedTokens.Intersect(candidateTokens).Count();
            var union = requestedTokens.Union(candidateTokens).Count();
            if (union > 0)
            {
                score += 1200d * intersection / union;
            }

            if (string.Equals(candidate.GameType, "game", StringComparison.OrdinalIgnoreCase))
            {
                score += 150;
            }
            else if (!string.IsNullOrEmpty(candidate.GameType))
            {
                score -= 75;
            }

            if (releaseYear.HasValue && candidate.ReleaseWorld > 0)
            {
                score += Math.Max(0, 100 - (Math.Abs(releaseYear.Value - candidate.ReleaseWorld) * 25));
            }

            return score;
        }
    }

    internal static class CompletionTimeFormatting
    {
        public static string Format(long seconds)
        {
            if (seconds <= 0)
            {
                return "—";
            }

            var hours = Math.Max(1, (int)Math.Round(seconds / 3600d, MidpointRounding.AwayFromZero));
            return hours == 1 ? "1 Hour" : $"{hours} Hours";
        }

        public static double ProgressPercent(ulong playedSeconds, long targetSeconds)
        {
            if (playedSeconds == 0 || targetSeconds <= 0)
            {
                return 0;
            }

            return Math.Min(100d, playedSeconds * 100d / targetSeconds);
        }
    }
}
