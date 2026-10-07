using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Playnite.SDK.Models;

namespace Osiris.Extensions.Exophase
{
    public sealed class ExophaseAutoMatch
    {
        public string Url { get; set; }
        public string RemoteId { get; set; }
        public string Title { get; set; }
        public string ProfileId { get; set; }
        public string AwardPlayerId { get; set; }
        public string Platform { get; set; }
        private static string Key(string text) => new string((text ?? "").Normalize(System.Text.NormalizationForm.FormD)
            .Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c)).Select(char.ToLowerInvariant).ToArray());
        public static ExophaseAutoMatch Resolve(Game game, JObject settings, JObject snapshot, JObject raw)
        {
            var matches=ResolveAll(game,settings,snapshot,raw);
            if (matches.Count!=1) throw new InvalidDataException("Use all platform pages belonging to the saved Exophase edition.");
            return matches[0];
        }
        public static List<ExophaseAutoMatch> ResolveAll(Game game, JObject settings, JObject snapshot, JObject raw)
        {
            if (game == null || game.PluginId != Guid.Empty) throw new InvalidDataException("Exophase trophies are only synchronised for local games.");
            if (settings==null || (bool?)settings["enabled"]==false) throw new InvalidDataException("Enable this game in the Exophase extension first.");
            var profile=(string)snapshot?["PlayerProfileId"];
            if (!long.TryParse(profile,out var profileId) || profileId<=0 || profile!=(string)raw?["playerProfileId"]) throw new InvalidDataException("Synchronise the current profile in Exophase first.");
            var names=(settings["matchedTitles"] as JArray ?? new JArray()).Values<string>().Where(t=>!string.IsNullOrWhiteSpace(t)).ToList();
            if (names.Count==0 && !string.IsNullOrWhiteSpace((string)settings["matchedTitle"])) names.Add((string)settings["matchedTitle"]);
            if (names.Count==0) names.Add(game.Name);
            var keys=new HashSet<string>(names.Select(Key));
            var records=(snapshot["Games"] as JArray ?? new JArray()).OfType<JObject>().Where(g=>keys.Contains(Key((string)g["Title"]))).ToList();
            var candidates=new List<ExophaseAutoMatch>();
            foreach (var page in raw["pages"] as JArray ?? new JArray())
            foreach (var item in page["games"] as JArray ?? new JArray())
            {
                var title=(string)item["meta"]?["title"];
                var id=(string)item["master_id"] ?? (string)item["meta"]?["master_id"];
                if (!long.TryParse(id,out var remote) || remote<=0 || !keys.Contains(Key(title)) || !records.Any(r=>(string)r["RemoteGameId"]==id && Key((string)r["Title"])==Key(title))) continue;
                var endpoint=(string)item["meta"]?["endpoint_awards"];
                if (string.IsNullOrWhiteSpace(endpoint)) continue;
                var url = ExophasePageParser.ValidateUrl(endpoint);
                var awardPlayer = new Uri(endpoint).Fragment.TrimStart('#');
                if (!long.TryParse(awardPlayer, NumberStyles.None, CultureInfo.InvariantCulture, out var awardPlayerId) || awardPlayerId <= 0)
                    throw new InvalidDataException("Refresh your Exophase profile to obtain this platform's achievement player ID.");
                candidates.Add(new ExophaseAutoMatch { Url=url,RemoteId=id,Title=title,ProfileId=profile,AwardPlayerId=awardPlayer,
                    Platform=(string)records.First(r=>(string)r["RemoteGameId"]==id && Key((string)r["Title"])==Key(title))["Platform"] });
            }
            var unique=candidates.GroupBy(c=>c.RemoteId+"|"+c.Url).Select(g=>g.First()).ToList();
            if (unique.Count==0) throw new InvalidDataException("No trophies found for the saved Exophase edition. Refresh your Exophase profile first.");
            return unique.OrderBy(c=>c.RemoteId,StringComparer.Ordinal).ToList();
        }
    }
}
