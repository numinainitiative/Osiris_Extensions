using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace Osiris.Extensions.Trophies
{
    public static class TrophySources
    {
        public static readonly Guid Steam = Guid.Parse("cb91dfc9-b977-43bf-8e70-55f46e410fab");
        public static readonly Guid Xbox = Guid.Parse("7e4fbb5e-2ae3-48d4-8ba0-6b30e7a4e287");
        public static bool IsLocal(Game game) => game != null && game.PluginId == Guid.Empty;
        public static Catalogue ForDisplay(Game game, Catalogue value)
        {
            if (value == null || IsLocal(game)) return value;
            foreach (var trophy in value.Trophies)
            {
                trophy.UnlockedUtc = value.NativeProvider == game.PluginId.ToString("D") ? trophy.NativeUnlockedUtc : null;
                trophy.ExophaseUnlocks=new List<ExophaseUnlock>();
                trophy.NativePlatform=game.PluginId==Steam ? "Steam" : game.PluginId==Xbox ? "Xbox" : null;
            }
            return value;
        }
        public static void ApplySteam(string json, Catalogue value, uint app, string account)
        {
            var stats = JObject.Parse(json)["playerstats"];
            if (value.AppId != app || (bool?)stats?["success"] != true || stats?["achievements"] is not JArray entries)
                throw new InvalidDataException("Steam did not return achievements for this game/account.");
            var unlocked = new Dictionary<string, DateTime?>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                var id = (string)entry["apiname"];
                var earned = (int?)entry["achieved"];
                if (string.IsNullOrEmpty(id) || unlocked.ContainsKey(id) || (earned != 0 && earned != 1)) throw new InvalidDataException("Invalid Steam achievement response.");
                unlocked.Add(id, earned == 1 ? Timestamp((long?)entry["unlocktime"]) : null);
            }
            if (value.Trophies.Any(t=>!unlocked.ContainsKey(t.Id))) throw new InvalidDataException("Steam returned an incomplete or changed catalogue. Re-fetch the catalogue before synchronising.");
            Apply(value, Steam, account, unlocked);
        }
        private static DateTime Timestamp(long? seconds)
        {
            if (!seconds.HasValue || seconds <= 0 || seconds > DateTimeOffset.UtcNow.ToUnixTimeSeconds()+86400) throw new InvalidDataException("The provider did not supply a valid unlock date.");
            return DateTimeOffset.FromUnixTimeSeconds(seconds.Value).UtcDateTime;
        }
        public static void ApplyXbox(string json, Catalogue value)
        {
            var root = JObject.Parse(json);
            var account = (string)root["Account"];
            var title = (string)root["TitleId"];
            if (!ulong.TryParse(account, out _) || !uint.TryParse(title, out _) || root["Achievements"] is not JArray entries) throw new InvalidDataException("Invalid Xbox achievement response.");
            var unlocked = new Dictionary<string, DateTime?>();
            var unique = value.Trophies.GroupBy(t=>ExophasePageParser.Key(t.Name)).Where(g=>g.Count()==1).ToDictionary(g=>g.Key,g=>g.Single());
            var names = new HashSet<string>();
            foreach (var entry in entries)
            {
                if (entry["titleAssociations"] is not JArray associations || !associations.Any(a=>(string)a["id"]==title)) throw new InvalidDataException("Xbox returned achievements from another game.");
                var name = ExophasePageParser.Key((string)entry["name"]);
                var state=(string)entry["progressState"];
                if (string.IsNullOrEmpty(name) || (state!="Achieved" && state!="NotStarted" && state!="InProgress")) throw new InvalidDataException("Invalid Xbox achievement state.");
                if (!names.Add(name)) throw new InvalidDataException("Ambiguous Xbox achievement names; no changes applied.");
                if (!unique.TryGetValue(name, out var trophy)) continue;
                if (!string.IsNullOrWhiteSpace(trophy.Description) && ExophasePageParser.Key(trophy.Description)!=ExophasePageParser.Key((string)entry["description"])) continue;
                DateTime? date = null;
                if ((string)entry["progressState"]=="Achieved" && (bool?)entry["isRevoked"]!=true)
                {
                    if (!DateTimeOffset.TryParse((string)entry["progression"]?["timeUnlocked"], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed) || parsed.Year<1970 || parsed.UtcDateTime>DateTime.UtcNow.AddDays(1)) throw new InvalidDataException("Invalid Xbox unlock date.");
                    date=parsed.UtcDateTime;
                }
                unlocked.Add(trophy.Id,date);
            }
            if (entries.Count>0 && unlocked.Count==0) throw new InvalidDataException("No unambiguous Xbox trophies match this catalogue; existing unlocks unchanged.");
            Apply(value, Xbox, account, unlocked);
        }
        private static void Apply(Catalogue value, Guid provider, string account, Dictionary<string,DateTime?> unlocked)
        {
            if (string.IsNullOrWhiteSpace(account)) throw new InvalidDataException("No connected library account.");
            // A complete validated snapshot replaces the previous account's native state, never Exophase history.
            value.NativeProvider=provider.ToString("D"); value.NativeAccount=account;
            foreach (var trophy in value.Trophies) trophy.NativeUnlockedUtc=unlocked.TryGetValue(trophy.Id,out var date) ? date : null;
        }
        internal static async Task Fetch(IPlayniteAPI api, CatalogueService service, Game game, Catalogue value, string fallbackKey, CancellationToken token)
        {
            var integration=api.Addons.Plugins.FirstOrDefault(p=>p.Id==game.PluginId);
            if (integration==null) throw new InvalidDataException("This game's library integration is unavailable.");
            if (game.PluginId==Steam)
            {
                var model=integration.GetSettings(false);
                var settings=model?.GetType().GetProperty("Settings")?.GetValue(model);
                var account=settings?.GetType().GetProperty("UserId")?.GetValue(settings) as string;
                var key=settings?.GetType().GetProperty("RuntimeApiKey")?.GetValue(settings) as string;
                if (!ulong.TryParse(account,out _) || !uint.TryParse(game.GameId,out var app) || app!=value.AppId) throw new InvalidDataException("Connect the Steam library account and use this game's Steam App ID.");
                // The explicitly saved Trophies key takes precedence over an older integration key.
                key=string.IsNullOrWhiteSpace(fallbackKey) ? key : fallbackKey;
                if (string.IsNullOrWhiteSpace(key)) throw new InvalidDataException("Set a Steam Web API key to synchronise Steam achievements.");
                var json=await service.FetchJson("https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v1/?appid="+app+"&steamid="+account+"&l=english&key="+Uri.EscapeDataString(key),token);
                ApplySteam(json,value,app,account);
            }
            else if (game.PluginId==Xbox)
            {
                var bridge=integration.GetType().GetMethod("GetAchievementsForOsiris",new[]{typeof(Guid),typeof(CancellationToken)});
                if (bridge==null) throw new InvalidDataException("Update the Xbox library integration to enable direct trophy synchronisation.");
                var json=await (Task<string>)bridge.Invoke(integration,new object[]{game.Id,token});
                token.ThrowIfCancellationRequested(); ApplyXbox(json,value);
            }
            else throw new InvalidDataException("Direct trophy synchronisation is not yet supported for this library. Exophase is only used for local games.");
        }
    }
}
