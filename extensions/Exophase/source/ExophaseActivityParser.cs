using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Osiris.Extensions.Exophase
{
    internal static class ExophaseActivityParser
    {
        private static readonly Regex BodyPattern = new Regex(
            @"<body\b[^>]*>(?<content>[\s\S]*?)</body\s*>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex PrePattern = new Regex(
            @"^\s*<pre\b[^>]*>(?<content>[\s\S]*?)</pre\s*>\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ProfileIdPattern = new Regex(
            "playerProfileId(?:&quot;|[\\\"'])?\\s*[:=]\\s*(?:&quot;|[\\\"'])?(?<id>[0-9]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ProfileLinkPattern = new Regex(
            "(?:https?://(?:www\\.)?exophase\\.com)?/user/(?<slug>[^/\\\"'?#&<]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex NonAlphaNumericPattern = new Regex(
            @"[^a-z0-9]+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        internal static string UnwrapJsonPage(string pageSource)
        {
            if (string.IsNullOrWhiteSpace(pageSource))
            {
                throw new InvalidOperationException("Exophase returned an empty response.");
            }

            var value = pageSource.Trim();
            if (value.StartsWith("<", StringComparison.Ordinal))
            {
                var body = BodyPattern.Match(value);
                if (body.Success)
                {
                    value = body.Groups["content"].Value;
                }

                var pre = PrePattern.Match(value);
                if (pre.Success)
                {
                    value = pre.Groups["content"].Value;
                }

                value = WebUtility.HtmlDecode(value).Trim();
            }

            if (!value.StartsWith("{", StringComparison.Ordinal) ||
                !value.EndsWith("}", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Exophase did not return JSON. Its browser verification may need to be completed again.");
            }

            return value;
        }

        internal static string FindPlayerProfileId(string pageSource)
        {
            if (string.IsNullOrWhiteSpace(pageSource))
            {
                return null;
            }

            var decoded = WebUtility.HtmlDecode(pageSource);
            var match = ProfileIdPattern.Match(decoded);
            return match.Success ? match.Groups["id"].Value : null;
        }

        internal static string FindProfileSlug(string pageSource)
        {
            if (string.IsNullOrWhiteSpace(pageSource))
            {
                return null;
            }

            var decoded = WebUtility.HtmlDecode(pageSource);
            var match = ProfileLinkPattern.Match(decoded);
            return match.Success
                ? Uri.UnescapeDataString(match.Groups["slug"].Value)
                : null;
        }

        internal static bool TryParseNumericProfileId(string value, out string profileId)
        {
            profileId = null;
            var trimmed = value?.Trim();
            long parsed;
            if (string.IsNullOrEmpty(trimmed) ||
                !long.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out parsed) ||
                parsed <= 0)
            {
                return false;
            }

            profileId = parsed.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        internal static string GetProfilePageUrl(string profileReference)
        {
            var value = profileReference?.Trim();
            if (string.IsNullOrEmpty(value))
            {
                return ExophaseAuthenticationService.AccountUrl;
            }

            Uri uri;
            if (Uri.TryCreate(value, UriKind.Absolute, out uri))
            {
                var isExophase = string.Equals(uri.Host, "exophase.com", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(uri.Host, "www.exophase.com", StringComparison.OrdinalIgnoreCase);
                if (!isExophase)
                {
                    throw new ArgumentException("Use an Exophase username or profile URL.", nameof(profileReference));
                }

                return uri.AbsoluteUri;
            }

            return "https://www.exophase.com/user/" + Uri.EscapeDataString(value) + "/";
        }

        internal static List<ExophaseGameActivity> ParseGames(JObject document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (document.Value<bool?>("success") == false)
            {
                throw new InvalidOperationException("Exophase rejected the profile activity request.");
            }

            var games = document["games"] as JArray;
            if (games == null)
            {
                throw new InvalidOperationException("Exophase returned an unexpected games response.");
            }

            var result = new List<ExophaseGameActivity>();
            foreach (var item in games.OfType<JObject>())
            {
                var meta = item["meta"] as JObject;
                var title = meta?.Value<string>("title")?.Trim();
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                result.Add(new ExophaseGameActivity
                {
                    RemoteGameId = ReadLong(item["master_id"]) ??
                                   ReadLong(meta?["master_id"]) ?? 0,
                    Title = title,
                    Platform = ReadPlatform(item, meta),
                    PlaytimeSeconds = ReadPlaytimeSeconds(item),
                    EarnedAwards = ReadInt(item["earned_awards"]) ?? 0,
                    TotalAwards = ReadInt(item["total_awards"]) ?? 0,
                    CompletionPercent = ReadDouble(item["percent"]) ?? 0d,
                    LastPlayedUtc = ReadLong(item["lastplayed_utc"]) ?? 0
                });
            }

            return result;
        }

        internal static List<ExophaseGameAggregate> AggregateGames(
            IEnumerable<ExophaseGameActivity> records)
        {
            return (records ?? Enumerable.Empty<ExophaseGameActivity>())
                .Where(record => !string.IsNullOrWhiteSpace(record.Title))
                .GroupBy(record => NormalizeTitle(record.Title), StringComparer.Ordinal)
                .Where(group => !string.IsNullOrEmpty(group.Key))
                .Select(group => new ExophaseGameAggregate
                {
                    MatchKey = group.Key,
                    Title = group.Select(record => record.Title)
                        .FirstOrDefault(title => !string.IsNullOrWhiteSpace(title)),
                    TotalPlaytimeSeconds = SaturatingSum(group.Select(record => record.PlaytimeSeconds)),
                    Platforms = group
                        .GroupBy(record => string.IsNullOrWhiteSpace(record.Platform)
                            ? "Unknown"
                            : record.Platform.Trim(), StringComparer.OrdinalIgnoreCase)
                        .Select(platform => new ExophasePlatformActivity
                        {
                            Platform = platform.Key,
                            PlaytimeSeconds = SaturatingSum(platform.Select(record => record.PlaytimeSeconds)),
                            EarnedAwards = platform.Sum(record => Math.Max(0, record.EarnedAwards)),
                            TotalAwards = platform.Sum(record => Math.Max(0, record.TotalAwards))
                        })
                        .OrderBy(platform => platform.Platform, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                })
                .OrderBy(game => game.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal static string NormalizeTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return string.Empty;
            }

            var normalized = title.Normalize(NormalizationForm.FormD);
            var characters = normalized
                .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) !=
                                    UnicodeCategory.NonSpacingMark)
                .ToArray();
            normalized = new string(characters).Normalize(NormalizationForm.FormC).ToLowerInvariant();
            return NonAlphaNumericPattern.Replace(normalized, string.Empty);
        }

        private static ulong ReadPlaytimeSeconds(JObject item)
        {
            var units = item["playtimeUnits"] as JObject;
            if (units == null)
            {
                return 0;
            }

            var days = Math.Max(0, ReadLong(units["days"]) ?? 0);
            var hours = Math.Max(0, ReadLong(units["hours"]) ?? 0);
            var minutes = Math.Max(0, ReadLong(units["minutes"]) ?? 0);
            var seconds = Math.Max(0, ReadLong(units["seconds"]) ?? 0);
            try
            {
                checked
                {
                    return (ulong)days * 86400UL +
                           (ulong)hours * 3600UL +
                           (ulong)minutes * 60UL +
                           (ulong)seconds;
                }
            }
            catch (OverflowException)
            {
                return ulong.MaxValue;
            }
        }

        private static string ReadPlatform(JObject item, JObject meta)
        {
            var environment = item["environment"];
            var environmentName = environment?.Type == JTokenType.String
                ? environment.Value<string>()
                : (environment as JObject)?.Value<string>("name");
            if (!string.IsNullOrWhiteSpace(environmentName))
            {
                return environmentName.Trim();
            }

            var platforms = meta?["platforms"] as JArray;
            var names = platforms?
                .OfType<JObject>()
                .Select(platform => platform.Value<string>("name")?.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return names?.Length > 0 ? string.Join(", ", names) : "Unknown";
        }

        private static int? ReadInt(JToken token)
        {
            long? value = ReadLong(token);
            if (!value.HasValue)
            {
                return null;
            }

            return value.Value > int.MaxValue
                ? int.MaxValue
                : value.Value < int.MinValue
                    ? int.MinValue
                    : (int)value.Value;
        }

        private static long? ReadLong(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            long value;
            return long.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                ? value
                : (long?)null;
        }

        private static double? ReadDouble(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            double value;
            return double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                ? value
                : (double?)null;
        }

        private static ulong SaturatingSum(IEnumerable<ulong> values)
        {
            ulong sum = 0;
            foreach (var value in values)
            {
                if (ulong.MaxValue - sum < value)
                {
                    return ulong.MaxValue;
                }

                sum += value;
            }

            return sum;
        }
    }
}
