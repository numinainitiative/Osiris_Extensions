using System;
using System.Collections.Generic;

namespace Osiris.Extensions.Exophase
{
    internal sealed class ExophaseGameActivity
    {
        public long RemoteGameId { get; set; }

        public string Title { get; set; }

        public string Platform { get; set; }

        public ulong PlaytimeSeconds { get; set; }

        public int EarnedAwards { get; set; }

        public int TotalAwards { get; set; }

        public double CompletionPercent { get; set; }

        public long LastPlayedUtc { get; set; }
    }

    internal sealed class ExophaseActivitySnapshot
    {
        public int SchemaVersion { get; set; } = 1;

        public string PlayerProfileId { get; set; }

        public DateTime FetchedUtc { get; set; }

        public List<ExophaseGameActivity> Games { get; set; } =
            new List<ExophaseGameActivity>();
    }

    internal sealed class ExophaseGameAggregate
    {
        public string MatchKey { get; set; }

        public string Title { get; set; }

        public ulong TotalPlaytimeSeconds { get; set; }

        public List<ExophasePlatformActivity> Platforms { get; set; } =
            new List<ExophasePlatformActivity>();
    }

    internal sealed class ExophasePlatformActivity
    {
        public string Platform { get; set; }

        public ulong PlaytimeSeconds { get; set; }

        public int EarnedAwards { get; set; }

        public int TotalAwards { get; set; }
    }

    internal sealed class ExophaseImportSummary
    {
        public string PlayerProfileId { get; set; }

        public int Records { get; set; }

        public int Games { get; set; }

        public int MatchedGames { get; set; }

        public int UpdatedGames { get; set; }

        public int UnchangedGames { get; set; }

        public int UnmatchedGames { get; set; }

        public int AmbiguousGames { get; set; }

        public ulong ImportedPlaytimeSeconds { get; set; }
    }
}
