using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite.SDK;

namespace Osiris.Extensions.Exophase
{
    public sealed class ExophaseUnlock
    {
        public string ProfileId { get; set; }
        public string Platform { get; set; }
        public string AwardId { get; set; }
        public DateTime UnlockedUtc { get; set; }
    }
    public sealed class ExophaseAward
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime? UnlockedUtc { get; set; }
    }
    public sealed class ExophasePage
    {
        public string GameTitle { get; set; }
        public string PlayerId { get; set; }
        public string GameId { get; set; }
        public string Platform { get; set; }
        public string Url { get; set; }
        public List<ExophaseAward> Awards { get; set; } = new List<ExophaseAward>();
    }
    // Original interoperability parser. Never executes saved HTML or copies website scripts.
    public static class ExophasePageParser
    {
        private static Regex Pattern(string value) => new Regex(value, RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        private static string Attribute(string tag, string name)
        {
            var match = Pattern(@"\b" + Regex.Escape(name) + "\\s*=\\s*[\"'](?<v>[^\"']*)[\"']").Match(tag);
            return match.Success ? WebUtility.HtmlDecode(match.Groups["v"].Value) : "";
        }
        private static string Text(string html) => WebUtility.HtmlDecode(Pattern("<[^>]*>").Replace(html ?? "", " ")).Trim();
        public static string Key(string text) => new string((text ?? "").Normalize().Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        public static string ValidateUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo) ||
                (uri.Host != "www.exophase.com" && uri.Host != "exophase.com") || !Pattern(@"^/game/[a-z0-9-]+/(achievements|trophies)/$").IsMatch(uri.AbsolutePath) || uri.Query.Length != 0)
                throw new InvalidDataException("Use an HTTPS Exophase game achievements or trophies URL.");
            return "https://www.exophase.com" + uri.AbsolutePath;
        }
        public static DateTime? Timestamp(string value)
        {
            if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || seconds <= 0) return null;
            try { var date = new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(seconds); return date <= DateTime.UtcNow.AddDays(1) ? date : (DateTime?)null; } catch { return null; }
        }
        public static ExophasePage Parse(string html)
        {
            if (string.IsNullOrEmpty(html) || html.Length > 5 * 1024 * 1024) throw new InvalidDataException("Exophase page is empty or too large.");
            var header = Pattern("<div\\b[^>]*class=[\"'][^\"']*col-game-information[^\"']*[\"'][^>]*>").Match(html).Value;
            if (string.IsNullOrEmpty(header)) throw new InvalidDataException("Exophase verification or an unsupported page prevented reading achievements.");
            var canonical = Pattern("<link\\b[^>]*rel=[\"']canonical[\"'][^>]*>").Match(html).Value;
            var title = Pattern("<h2\\b[^>]*>(?<v>.*?)</h2>").Match(html);
            var page = new ExophasePage { GameTitle = Text(title.Groups["v"].Value), PlayerId = Attribute(header,"data-player"), GameId = Attribute(header,"data-game"), Platform = Attribute(header,"data-environment"), Url = ValidateUrl(Attribute(canonical,"href")) };
            if (!long.TryParse(page.GameId,out var gameId) || gameId <= 0 || string.IsNullOrWhiteSpace(page.GameTitle) || !Pattern("^[a-z0-9-]+$").IsMatch(page.Platform)) throw new InvalidDataException("Missing Exophase game or platform identity.");
            page.Platform = PlatformName(page.Platform);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match row in Pattern("<li\\b(?<attrs>[^>]*)>(?<body>.*?)</li>").Matches(html))
            {
                var attrs = row.Groups["attrs"].Value;
                if (!Pattern(@"\baward\b").IsMatch(Attribute(attrs,"class"))) continue;
                var id = Attribute(attrs,"data-master");
                if (string.IsNullOrEmpty(id) || !ids.Add(id)) throw new InvalidDataException("Missing or duplicate Exophase award ID.");
                var body = row.Groups["body"].Value;
                var name = Pattern("<div\\b[^>]*class=[\"'][^\"']*award-title[^\"']*[\"'][^>]*>(?<v>.*?)</div>").Match(body);
                var description = Pattern("<div\\b[^>]*class=[\"'][^\"']*award-description[^\"']*[\"'][^>]*>(?<v>.*?)</div>").Match(body);
                page.Awards.Add(new ExophaseAward { Id=id, Name=Text(name.Groups["v"].Value), Description=Text(description.Groups["v"].Value), UnlockedUtc=Timestamp(Attribute(attrs,"data-earned")) });
            }
            if (page.Awards.Count == 0 || page.Awards.Any(a => string.IsNullOrWhiteSpace(a.Name))) throw new InvalidDataException("No usable Exophase achievements were found.");
            return page;
        }
        private static string PlatformName(string environment)
        {
            switch (environment) { case "xbox": return "Xbox"; case "steam": return "Steam"; case "psn": return "PlayStation"; case "gog": return "GOG"; default: return environment; }
        }
    }
}
