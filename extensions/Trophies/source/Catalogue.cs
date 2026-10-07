using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Osiris.Extensions.Trophies
{
    public sealed class Trophy
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Hidden { get; set; }
        public string Icon { get; set; }
        // Null means unknown, not a confirmed locked state. A future tracking provider owns this value.
        public DateTime? UnlockedUtc { get; set; }
        public DateTime? NativeUnlockedUtc { get; set; }
        public List<ExophaseUnlock> ExophaseUnlocks { get; set; } = new List<ExophaseUnlock>();
        public double? GlobalPercent { get; set; }
        [JsonIgnore] public string DisplayDescription => UnlockedUtc.HasValue ? UnlockedUtc.Value.ToLocalTime().ToString("g") : Hidden ? "Hidden" : Description;
        [JsonIgnore] public string LocalIcon { get; set; }
        [JsonIgnore] public string NativePlatform { get; set; }
    }
    public sealed class Catalogue
    {
        public uint AppId { get; set; }
        public string GameName { get; set; }
        public DateTime ImportedUtc { get; set; }
        public string ExophasePageUrl { get; set; }
        public string NativeProvider { get; set; }
        public string NativeAccount { get; set; }
        public List<Trophy> Trophies { get; set; } = new List<Trophy>();
    }
    public sealed class CatalogueService : IDisposable
    {
        private readonly HttpClient http;
        private readonly string root;
        private readonly SemaphoreSlim imports = new SemaphoreSlim(1, 1);
        public CatalogueService(string root) : this(root, new HttpClientHandler { AllowAutoRedirect = false }) { }
        public CatalogueService(string root, HttpMessageHandler handler)
        { this.root = root; http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) }; }
        public static uint ParseAppId(string value)
        {
            value = (value ?? "").Trim();
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Host == "store.steampowered.com")
            {
                var parts = uri.AbsolutePath.Split('/');
                value = parts.Length > 2 && parts[1] == "app" ? parts[2] : "";
            }
            if (!uint.TryParse(value, out var id) || id == 0) throw new ArgumentException("Enter a valid Steam App ID or Steam store game URL.");
            return id;
        }
        public static Catalogue Parse(string json, uint id)
        {
            var game = JObject.Parse(json)["game"] as JObject;
            if (game == null || game["availableGameStats"] is not JObject stats)
                throw new InvalidDataException("Steam did not return a usable achievement catalogue.");
            var result = new Catalogue { AppId = id, GameName = (string)game["gameName"] ?? "Steam app " + id, ImportedUtc = DateTime.UtcNow };
            var array = stats["achievements"];
            if (array == null) return result; // Valid game schema without achievements.
            if (!(array is JArray entries)) throw new InvalidDataException("Steam returned an invalid achievement list.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in entries)
            {
                var name = (string)item["name"];
                if (string.IsNullOrWhiteSpace(name) || !ids.Add(name)) throw new InvalidDataException("Steam returned invalid or duplicate achievement IDs.");
                result.Trophies.Add(new Trophy { Id = name, Name = (string)item["displayName"] ?? name,
                    Description = (string)item["description"] ?? "", Hidden = (int?)item["hidden"] == 1, Icon = SafeIcon((string)item["icon"]) });
            }
            return result;
        }
        public static string SafeIcon(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo)) return null;
            return uri.Host.EndsWith(".steamstatic.com", StringComparison.OrdinalIgnoreCase) || uri.Host == "steamcdn-a.akamaihd.net" ? uri.AbsoluteUri : null;
        }
        private string PathFor(Guid game) => Path.Combine(root, "catalogues", game.ToString("D") + ".json");
        internal DateTime Revision(Guid game) => File.GetLastWriteTimeUtc(PathFor(game));
        public Catalogue Load(Guid game)
        {
            try
            {
                var path = PathFor(game);
                if (!File.Exists(path)) return null;
                var value = JsonConvert.DeserializeObject<Catalogue>(File.ReadAllText(path));
                if (value == null || value.AppId == 0 || value.Trophies == null) return null;
                for (var i = 0; i < value.Trophies.Count; i++)
                {
                    var icon = IconPath(value.Trophies[i].Icon);
                    value.Trophies[i].LocalIcon = File.Exists(icon) ? icon : null;
                }
                return value;
            }
            catch { return null; }
        }
        private string IconPath(string url)
        {
            using (var hash = System.Security.Cryptography.SHA256.Create())
                return Path.Combine(root, "icons", BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(url ?? ""))).Replace("-", "") + ".png");
        }
        public async Task<Catalogue> Import(Guid game, uint app, string key, CancellationToken cancellation, bool persist = true)
        {
            if (game == Guid.Empty) throw new ArgumentException("Select a game first.");
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Set your Steam Web API key in Trophies settings and press Save first.");
            await imports.WaitAsync(cancellation).ConfigureAwait(false);
            try
            {
                var url = "https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?appid=" + app + "&l=english&key=" + Uri.EscapeDataString(key);
                var json = System.Text.Encoding.UTF8.GetString(await Download(url, 5 * 1024 * 1024, cancellation).ConfigureAwait(false));
                var value = Parse(json, app);
                try
                {
                    var rarity = await Download("https://api.steampowered.com/ISteamUserStats/GetGlobalAchievementPercentagesForApp/v2/?gameid=" + app, 5 * 1024 * 1024, cancellation).ConfigureAwait(false);
                    ApplyRarity(System.Text.Encoding.UTF8.GetString(rarity), value);
                }
                catch (OperationCanceledException) { throw; }
                catch { /* Rarity is optional; retain a usable catalogue if Steam cannot supply it. */ }
                for (var i = 0; i < value.Trophies.Count; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (value.Trophies[i].Icon == null) continue;
                    try
                    {
                        var bytes = await Download(value.Trophies[i].Icon, 512 * 1024, cancellation).ConfigureAwait(false);
                        // Validate image bytes before caching; hash URLs so a failed reimport cannot
                        // remap existing catalogue icons through a changed achievement ordering.
                        using (var stream = new MemoryStream(bytes))
                        {
                            var bitmap = System.Windows.Media.Imaging.BitmapDecoder.Create(stream,
                                System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                            if (bitmap.Frames.Count == 0 || bitmap.Frames[0].PixelWidth > 2048 || bitmap.Frames[0].PixelHeight > 2048)
                                throw new InvalidDataException("Invalid achievement icon.");
                        }
                        var path = IconPath(value.Trophies[i].Icon);
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        AtomicWrite(path, bytes);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch { /* A missing icon must not discard the catalogue. */ }
                }
                cancellation.ThrowIfCancellationRequested();
                if (persist) { Save(game, value); return Load(game); }
                return value;
            }
            finally { imports.Release(); }
        }
        public void Save(Guid game, Catalogue value)
        {
            if (game == Guid.Empty || value == null || value.AppId == 0 || value.Trophies == null) throw new ArgumentException("Invalid catalogue.");
            var previous = Load(game);
            if (previous?.AppId == value.AppId)
            {
                value.ExophasePageUrl = value.ExophasePageUrl ?? previous.ExophasePageUrl;
                var retainNative = value.NativeProvider == null;
                if (retainNative) { value.NativeProvider=previous.NativeProvider; value.NativeAccount=previous.NativeAccount; }
                foreach (var trophy in value.Trophies)
                {
                    var old = previous.Trophies.FirstOrDefault(t => t.Id == trophy.Id);
                    if (retainNative) trophy.NativeUnlockedUtc=old?.NativeUnlockedUtc;
                    if (old?.UnlockedUtc != null && (!trophy.UnlockedUtc.HasValue || old.UnlockedUtc.Value < trophy.UnlockedUtc.Value)) trophy.UnlockedUtc=old.UnlockedUtc;
                    if (trophy.ExophaseUnlocks == null) trophy.ExophaseUnlocks=new List<ExophaseUnlock>();
                    foreach (var record in old?.ExophaseUnlocks ?? new List<ExophaseUnlock>())
                        if (!trophy.ExophaseUnlocks.Any(u=>u.ProfileId==record.ProfileId && u.Platform==record.Platform && u.AwardId==record.AwardId)) trophy.ExophaseUnlocks.Add(record);
                    trophy.ExophaseUnlocks.RemoveAll(record => record.Platform == "PlayStation" &&
                        trophy.ExophaseUnlocks.Any(u => u.ProfileId == record.ProfileId && u.AwardId == record.AwardId &&
                            u.Platform?.StartsWith("PS", StringComparison.OrdinalIgnoreCase) == true));
                }
            }
            var target = PathFor(game);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            AtomicWrite(target, System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value, Formatting.Indented)));
        }
        public static void ApplyRarity(string json, Catalogue catalogue)
        {
            var entries = JObject.Parse(json)["achievementpercentages"]?["achievements"] as JArray;
            if (entries == null) return;
            foreach (var entry in entries)
            {
                var id = (string)entry["name"];
                if (!double.TryParse((string)entry["percent"], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var percent) || double.IsNaN(percent) || double.IsInfinity(percent) || percent < 0 || percent > 100) continue;
                var trophy = catalogue.Trophies.FirstOrDefault(t => t.Id == id);
                if (trophy != null) trophy.GlobalPercent = percent;
            }
        }
        private async Task<byte[]> Download(string url, int limit, CancellationToken token)
        {
            using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode) throw new InvalidDataException("Steam request failed. Check the API key and try again later.");
                if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("Steam response exceeded the size limit.");
                using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var output = new MemoryStream())
                {
                    var buffer = new byte[8192]; int read;
                    while ((read = await input.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
                    { if (output.Length + read > limit) throw new InvalidDataException("Steam response exceeded the size limit."); output.Write(buffer, 0, read); }
                    return output.ToArray();
                }
            }
        }
        internal async Task<string> FetchJson(string url, CancellationToken token) => System.Text.Encoding.UTF8.GetString(await Download(url,5*1024*1024,token).ConfigureAwait(false));
        private static void AtomicWrite(string path, byte[] bytes)
        {
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllBytes(temp, bytes); if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public void Dispose() { http.Dispose(); }
    }
}
