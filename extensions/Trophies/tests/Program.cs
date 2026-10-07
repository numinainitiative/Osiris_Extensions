using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Controls;
using Osiris.Extensions.Trophies;

internal static class Program
{
    private static int checks;
    private const string Fixture = "{\"game\":{\"gameName\":\"Fixture Game\",\"availableGameStats\":{\"achievements\":[{\"name\":\"A\",\"displayName\":\"First trophy\",\"description\":\"Description\",\"hidden\":0},{\"name\":\"SECRET\",\"displayName\":\"Secret trophy\",\"description\":\"Spoiler\",\"hidden\":1}]}}}";
    private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
    private static void Reject(Action action, string name) { try { action(); } catch { checks++; return; } throw new Exception(name); }
    [STAThread] private static void Main(string[] args)
    {
        Check(CatalogueService.ParseAppId("292030") == 292030, "numeric App ID");
        var legacySettings=Newtonsoft.Json.JsonConvert.DeserializeObject<TrophiesSettings>("{\"Enabled\":true,\"SynchroniseWithExophase\":false,\"ProtectedApiKey\":\"encrypted-fixture\"}");
        Check(legacySettings.Enabled && legacySettings.ProtectedApiKey=="encrypted-fixture", "legacy Exophase opt-out is ignored without losing visibility or encrypted key");
        Check(!Newtonsoft.Json.Linq.JObject.FromObject(legacySettings).Properties().Any(p=>p.Name=="SynchroniseWithExophase"), "settings no longer save an obsolete global Exophase gate");
        Check(CatalogueService.ParseAppId("https://store.steampowered.com/app/292030/The_Witcher_3/") == 292030, "store link");
        Reject(() => CatalogueService.ParseAppId("0"), "zero");
        Reject(() => CatalogueService.ParseAppId("https://example.com/app/292030"), "foreign link");
        Reject(() => CatalogueService.ParseAppId("../292030"), "path input");
        var parsed = CatalogueService.Parse(Fixture, 292030);
        Check(TrophySources.IsLocal(new Playnite.SDK.Models.Game()),"only empty plugin ID is a local game");
        Check(typeof(TrophiesPlugin).GetMethod("OnLibraryUpdated").DeclaringType==typeof(TrophiesPlugin),"SDK library updates trigger trophy sync");
        Check(typeof(TrophiesPlugin).GetMethod("OnApplicationStarted").DeclaringType==typeof(TrophiesPlugin),"startup catches up native trophy catalogues");
        Check(typeof(TrophiesPlugin).GetMethod("SyncIntegratedLibraryForOsiris")!=null,"Osiris custom update job has an explicit trophy sync bridge");
        Check(!TrophySources.IsLocal(new Playnite.SDK.Models.Game { PluginId=Guid.NewGuid() }),"unknown integration is not local");
        var autoSettings=Newtonsoft.Json.Linq.JObject.Parse("{\"enabled\":true}");
        var autoSnapshot=Newtonsoft.Json.Linq.JObject.Parse("{\"PlayerProfileId\":\"123\",\"Games\":[{\"RemoteGameId\":456,\"Title\":\"Fixture Game\"}]}");
        var autoRaw=Newtonsoft.Json.Linq.JObject.Parse("{\"playerProfileId\":\"123\",\"pages\":[{\"games\":[{\"master_id\":456,\"meta\":{\"title\":\"Fixture Game\",\"endpoint_awards\":\"https://www.exophase.com/game/fixture-game-xbox/achievements/#123\"}}]}]}");
        var autoGame=new Playnite.SDK.Models.Game { Name="Fixture Game" };
        var automatic=ExophaseAutoMatch.Resolve(autoGame,autoSettings,autoSnapshot,autoRaw);
        Check(automatic.RemoteId=="456" && automatic.Url.EndsWith("/achievements/"),"existing Exophase snapshot resolves URL and strips profile fragment");
        Reject(()=>ExophaseAutoMatch.Resolve(new Playnite.SDK.Models.Game { Name="Fixture Game",PluginId=TrophySources.Steam },autoSettings,autoSnapshot,autoRaw),"automatic Exophase matcher rejects integrated games");
        Reject(()=>ExophaseAutoMatch.Resolve(autoGame,autoSettings,autoSnapshot,Newtonsoft.Json.Linq.JObject.Parse(autoRaw.ToString().Replace("123","999"))),"automatic matcher rejects mixed profile snapshots");
        Reject(()=>ExophaseAutoMatch.Resolve(autoGame,Newtonsoft.Json.Linq.JObject.Parse("{\"enabled\":false}"),autoSnapshot,autoRaw),"automatic matcher respects per-game Exophase switch");
        Reject(()=>ExophaseAutoMatch.Resolve(new Playnite.SDK.Models.Game { Name="Unknown" },autoSettings,autoSnapshot,autoRaw),"automatic matching never guesses a nearby game name");
        Check(ExophaseAutoMatch.Resolve(new Playnite.SDK.Models.Game { Name="Custom local name" },Newtonsoft.Json.Linq.JObject.Parse("{\"matchedTitles\":[\"Fixture Game\"]}"),autoSnapshot,autoRaw).RemoteId=="456","explicit Exophase title selection reused");
        var ambiguousRaw=(Newtonsoft.Json.Linq.JObject)autoRaw.DeepClone();
        ((Newtonsoft.Json.Linq.JArray)ambiguousRaw["pages"][0]["games"]).Add(Newtonsoft.Json.Linq.JObject.Parse("{\"master_id\":457,\"meta\":{\"title\":\"Fixture Game\",\"endpoint_awards\":\"https://www.exophase.com/game/fixture-game-steam/achievements/\"}}"));
        var ambiguousSnapshot=(Newtonsoft.Json.Linq.JObject)autoSnapshot.DeepClone();
        ((Newtonsoft.Json.Linq.JArray)ambiguousSnapshot["Games"]).Add(Newtonsoft.Json.Linq.JObject.Parse("{\"RemoteGameId\":457,\"Title\":\"Fixture Game\"}"));
        Check(ExophaseAutoMatch.ResolveAll(autoGame,autoSettings,ambiguousSnapshot,ambiguousRaw).Count==2,"saved edition combines its platform pages without another selection");
        var native=CatalogueService.Parse(Fixture,292030);
        var steamJson="{\"playerstats\":{\"success\":true,\"achievements\":[{\"apiname\":\"A\",\"achieved\":1,\"unlocktime\":1700000000},{\"apiname\":\"SECRET\",\"achieved\":0,\"unlocktime\":0}]}}";
        TrophySources.ApplySteam(steamJson,native,292030,"76561198000000000");
        Check(native.Trophies[0].NativeUnlockedUtc.HasValue && !native.Trophies[1].NativeUnlockedUtc.HasValue,"Steam stable-ID native unlock snapshot");
        Reject(()=>TrophySources.ApplySteam(steamJson,native,1,"76561198000000000"),"Steam mismatched App ID rejected");
        Reject(()=>TrophySources.ApplySteam(steamJson.Replace("1700000000","0"),native,292030,"76561198000000000"),"unknown Steam unlock date rejected");
        Check(native.Trophies[0].NativeUnlockedUtc.HasValue,"invalid response leaves native snapshot unchanged");
        native.Trophies[1].UnlockedUtc=DateTime.UtcNow;
        native.Trophies[1].ExophaseUnlocks.Add(new ExophaseUnlock { Platform="Xbox",ProfileId="1",AwardId="1",UnlockedUtc=DateTime.UtcNow });
        TrophySources.ForDisplay(new Playnite.SDK.Models.Game { PluginId=TrophySources.Steam },native);
        Check(native.Trophies[0].UnlockedUtc.HasValue && !native.Trophies[1].UnlockedUtc.HasValue && native.Trophies[1].ExophaseUnlocks.Count==0,"integrated presentation ignores Exophase history");
        TrophySources.ForDisplay(new Playnite.SDK.Models.Game { PluginId=Guid.NewGuid() },native);
        Check(native.Trophies.All(t=>!t.UnlockedUtc.HasValue),"another provider cannot borrow Steam unlocks");
        var xboxJson="{\"Account\":\"1234\",\"TitleId\":\"42\",\"Achievements\":[{\"id\":\"1\",\"name\":\"First trophy\",\"description\":\"Description\",\"progressState\":\"Achieved\",\"titleAssociations\":[{\"id\":42}],\"progression\":{\"timeUnlocked\":\"2025-01-01T12:00:00Z\"}}]}";
        TrophySources.ApplyXbox(xboxJson,native);
        Check(native.NativeProvider==TrophySources.Xbox.ToString("D") && native.Trophies[0].NativeUnlockedUtc.Value.Year==2025,"Xbox title-scoped native unlock timestamp");
        Reject(()=>TrophySources.ApplyXbox(xboxJson.Replace("\"id\":42","\"id\":43"),native),"Xbox foreign title rejected");
        Reject(()=>TrophySources.ApplyXbox(xboxJson.Replace("First trophy","Unknown"),native),"Xbox unmatched catalogue never overwrites native state");
        Check(parsed.Trophies.Count == 2 && parsed.AppId == 292030, "schema parsed");
        Check(parsed.Trophies[0].Name == "First trophy" && parsed.Trophies[0].DisplayDescription == "Description", "normal description");
        Check(!parsed.Trophies[1].DisplayDescription.Contains("Spoiler"), "secret hidden");
        Check(parsed.Trophies[1].DisplayDescription == "Hidden", "secret label is Hidden only");
        var secretRow = new TrophyRow(parsed.Trophies[1], true);
        var cardRow = new TrophyRow(parsed.Trophies[1], false);
        secretRow.Execute(null);
        Check(secretRow.DisplayDescription == "Spoiler" && cardRow.DisplayDescription == "Hidden", "reveal is popup-only and isolated");
        Check(parsed.Trophies[1].DisplayDescription == "Hidden" && !parsed.Trophies[1].UnlockedUtc.HasValue, "reveal never changes catalogue or unlock");
        secretRow.Execute(null);
        Check(secretRow.DisplayDescription == "Hidden", "second click conceals description");
        CatalogueService.ApplyRarity("{\"achievementpercentages\":{\"achievements\":[{\"name\":\"SECRET\",\"percent\":12.34},{\"name\":\"A\",\"percent\":101}]}}", parsed);
        Check(parsed.Trophies[1].GlobalPercent == 12.34 && !parsed.Trophies[0].GlobalPercent.HasValue, "rarity matched by ID and range validated");
        Check(!string.IsNullOrEmpty(secretRow.RarityText), "rarity label available");
        var sorting = new[] {
            new Trophy { Id="C", Name="Zulu", GlobalPercent=4 },
            new Trophy { Id="B", Name="Alpha", GlobalPercent=80 },
            new Trophy { Id="A", Name="Earned", GlobalPercent=90, UnlockedUtc=DateTime.UtcNow },
            new Trophy { Id="H2", Name="Hidden Zulu", Hidden=true, GlobalPercent=1 },
            new Trophy { Id="H1", Name="Hidden Alpha", Hidden=true, UnlockedUtc=DateTime.UtcNow }
        };
        Check(TrophyRow.Group(sorting,true).Cast<TrophyRow>().Select(t=>t.Trophy.Id).SequenceEqual(new[] {"A","B","C","H1","H2"}), "title sort keeps categories and earned first");
        Check(TrophyRow.Group(sorting,true,rarity:true).Cast<TrophyRow>().Select(t=>t.Trophy.Id).SequenceEqual(new[] {"A","C","B","H1","H2"}), "rarity sort keeps categories and earned first");
        var previewDate = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var previewEntries = new[] {
            new Trophy { Id="old",Name="A old",UnlockedUtc=previewDate },
            new Trophy { Id="new",Name="Z new",Hidden=true,UnlockedUtc=previewDate.AddDays(2) },
            new Trophy { Id="middle",Name="B middle",UnlockedUtc=previewDate.AddDays(1) },
            new Trophy { Id="common",Name="Common",GlobalPercent=95 }
        };
        Check(TrophyRow.Preview(previewEntries).Select(t=>t.Trophy.Id).SequenceEqual(new[] {"new","middle","old"}), "card shows three latest unlocks across hidden and normal categories");
        var relevantEntries = new[] {
            new Trophy { Id="rare",Name="A rare",GlobalPercent=2 },
            new Trophy { Id="common",Name="Z common",GlobalPercent=90 },
            new Trophy { Id="middle",Name="B middle",GlobalPercent=50 },
            new Trophy { Id="secret",Name="Secret",Hidden=true,GlobalPercent=100 },
            new Trophy { Id="unknown",Name="Unknown" }
        };
        Check(TrophyRow.Preview(relevantEntries).Select(t=>t.Trophy.Id).SequenceEqual(new[] {"common","middle","rare"}), "no unlocks shows most common public achievements, not hidden spoilers or unknown rarity");
        previewEntries[0].UnlockedUtc=null; previewEntries[2].UnlockedUtc=null;
        Check(TrophyRow.Preview(previewEntries).Select(t=>t.Trophy.Id).SequenceEqual(new[] {"new","common","old"}), "one earned trophy fills remaining preview slots with relevant locked entries");
        Check(TrophyRow.Preview(new Trophy[0]).Count==0 && TrophyRow.Preview(new[]{relevantEntries[0]}).Count==1, "empty and small catalogues never invent preview rows");
        var simultaneous=new[] {
            new Trophy {Id="darkness",Name="Darkness and Fog",GlobalPercent=12.5,UnlockedUtc=previewDate},
            new Trophy {Id="father",Name="Father and Son",GlobalPercent=6.6,UnlockedUtc=previewDate},
            new Trophy {Id="unknown",Name="A unknown",UnlockedUtc=previewDate},
            new Trophy {Id="newer",Name="Newer",GlobalPercent=95,UnlockedUtc=previewDate.AddSeconds(1)}
        };
        Check(TrophyRow.Preview(simultaneous).Select(t=>t.Trophy.Id).SequenceEqual(new[]{"newer","father","darkness"}), "newest stays first and simultaneous unlocks prefer rarest with unknown rarity last");
        simultaneous[0].Hidden=true;
        Check(TrophyRow.Preview(simultaneous).Select(t=>t.Trophy.Id).SequenceEqual(new[]{"newer","father","darkness"}), "same-time rarity tie-break precedes hidden category on the card");
        var achievedGroups=TrophyRow.Group(simultaneous,true,lastAchieved:true);
        Check(achievedGroups.Cast<TrophyRow>().Select(t=>t.Trophy.Id).SequenceEqual(new[]{"newer","father","unknown","darkness"}) && achievedGroups.Groups.Count==2, "Last achieved full-view sort preserves categories and breaks earned ties by rarity");
        var achievedWithLocked=simultaneous.Concat(new[]{new Trophy {Id="locked",Name="A locked",GlobalPercent=1},new Trophy {Id="hiddenLocked",Name="A hidden locked",Hidden=true,GlobalPercent=0}});
        Check(TrophyRow.Group(achievedWithLocked,true,lastAchieved:true).Cast<TrophyRow>().Select(t=>t.Trophy.Id).SequenceEqual(new[]{"newer","father","unknown","locked","darkness","hiddenLocked"}), "Last achieved keeps locked entries below earned in each category");
        var shineEntries=Enumerable.Range(0,12).Select(i=>new Trophy { Id=i.ToString(),Name="Award "+i,Hidden=i%2==0,GlobalPercent=i,UnlockedUtc=previewDate.AddDays(i) }).ToList();
        var shineRows=TrophyRow.Group(shineEntries,true).Cast<TrophyRow>().ToList();
        Check(shineRows.Count(t=>t.Highlight==TrophyHighlight.Purple)==1 && shineRows.Count(t=>t.Highlight==TrophyHighlight.Gold)==4 && shineRows.Count(t=>t.Highlight==TrophyHighlight.Silver)==5, "single rarest purple remaining top-five gold and next-five silver across categories");
        Check(shineRows.Single(t=>t.Trophy.Id=="0").Highlight==TrophyHighlight.Purple && shineRows.Single(t=>t.Trophy.Id=="5").Highlight==TrophyHighlight.Silver && shineRows.Single(t=>t.Trophy.Id=="10").Highlight==TrophyHighlight.None, "rarity tier boundaries are exact");
        shineEntries[0].UnlockedUtc=null;
        var shineAfterLock=TrophyRow.Group(shineEntries,true,rarity:true).Cast<TrophyRow>().ToList();
        Check(shineAfterLock.Single(t=>t.Trophy.Id=="0").Highlight==TrophyHighlight.Grey && shineAfterLock.Single(t=>t.Trophy.Id=="5").Highlight==TrophyHighlight.Silver, "locked rare trophy keeps grey shine without promoting less rare ranks");
        Check(TrophyRow.Preview(shineEntries).Select(t=>t.Highlight).SequenceEqual(new[]{TrophyHighlight.None,TrophyHighlight.None,TrophyHighlight.Silver}), "card ranks rarity over complete catalogue rather than its three visible rows");
        var unknownRarity=new[] { new Trophy {Name="Unknown",UnlockedUtc=previewDate},new Trophy {Name="Invalid",GlobalPercent=101,UnlockedUtc=previewDate},new Trophy {Name="NaN",GlobalPercent=double.NaN,UnlockedUtc=previewDate} };
        Check(TrophyRow.Group(unknownRarity,true).Cast<TrophyRow>().All(t=>t.Highlight==TrophyHighlight.None), "unknown or invalid rarity never gains a shine");
        Check(new TrophyRow(new Trophy(),false,TrophyHighlight.Gold).Highlight==TrophyHighlight.Grey, "locked highlighted trophy renders its shine in grey");
        var rarityTie=new[]{new Trophy {Id="b",Name="Beta",GlobalPercent=1,UnlockedUtc=previewDate},new Trophy {Id="a",Name="Alpha",GlobalPercent=1,UnlockedUtc=previewDate}};
        Check(TrophyRow.Group(rarityTie,true).Cast<TrophyRow>().Count(t=>t.Highlight==TrophyHighlight.Purple)==1, "equal rarity still selects only one deterministic purple trophy");
        const string ExoFixture="<link rel=\"canonical\" href=\"https://www.exophase.com/game/fixture-game-xbox/achievements/\"><div class=\"row col-game-information\" data-player=\"123\" data-environment=\"xbox\" data-game=\"456\"><h2><a>Fixture Game</a></h2></div><ul><li class=\"award earned\" data-master=\"10\" data-earned=\"1700000000\"><div class=\"award-title\"><a>First trophy</a></div><div class=\"award-description\"><p>Description</p></div></li><li class=\"award locked\" data-master=\"11\" data-earned=\"0\"><div class=\"award-title\"><a>Secret trophy</a></div><div class=\"award-description\">Spoiler</div></li></ul>";
        var exoPage=ExophasePageParser.Parse(ExoFixture);
        Check(exoPage.Awards.Count==2 && exoPage.Platform=="Xbox", "original HTML parser reads award identity and source platform");
        Check(exoPage.Awards[0].UnlockedUtc==new DateTime(2023,11,14,22,13,20,DateTimeKind.Utc) && exoPage.Awards[1].UnlockedUtc==null, "Exophase UTC seconds parsed; zero is unearned");
        var exoCatalogue=CatalogueService.Parse(Fixture,292030);
        Check(ExophasePageParser.Apply(exoCatalogue,exoPage,"123")==1, "verified Exophase award matches Steam catalogue");
        Check(exoCatalogue.Trophies[0].UnlockedUtc==exoPage.Awards[0].UnlockedUtc && exoCatalogue.Trophies[0].ExophaseUnlocks.Count==1, "timestamp and provenance applied");
        Check(new TrophyRow(exoCatalogue.Trophies[0],true).PlatformText.Contains("Xbox") && new TrophyRow(exoCatalogue.Trophies[0],false).PlatformText=="", "platform provenance shown only in popup");
        ExophasePageParser.Apply(exoCatalogue,exoPage,"123");
        Check(exoCatalogue.Trophies[0].ExophaseUnlocks.Count==1, "repeated sync does not duplicate unlock");
        Reject(()=>ExophasePageParser.Apply(exoCatalogue,exoPage,"999"),"different profile rejected");
        Reject(()=>ExophasePageParser.Apply(CatalogueService.Parse(Fixture.Replace("Fixture Game","Other Game"),1),exoPage,"123"),"different game rejected");
        var changedDescription=CatalogueService.Parse(Fixture.Replace("Description","Different"),1);
        Check(ExophasePageParser.Apply(changedDescription,exoPage,"123")==0 && !changedDescription.Trophies[0].UnlockedUtc.HasValue,"cross-platform description mismatch not guessed");
        Check(ExophasePageParser.Apply(changedDescription,exoPage,"123", "Fixture Game", true)==1, "provider-verified game accepts unique exact award names despite platform description differences");
        var duplicateNames=CatalogueService.Parse(Fixture,1); duplicateNames.Trophies.Add(new Trophy { Id="X", Name="First trophy",Description="Description" });
        Check(ExophasePageParser.Apply(duplicateNames,exoPage,"123")==0,"ambiguous catalogue names ignored");
        Check(ExophasePageParser.Apply(duplicateNames,exoPage,"123", "Fixture Game", true)==0,"provider bridge never guesses ambiguous names");
        Reject(()=>ExophasePageParser.Parse("<html>Just a moment...</html>"),"verification page rejected");
        Reject(()=>ExophasePageParser.Parse(ExoFixture.Replace("data-master=\"11\"","data-master=\"10\"")),"duplicate Exophase IDs rejected");
        Reject(()=>ExophasePageParser.ValidateUrl("https://exophase.com.evil.test/game/a/achievements/"),"Exophase lookalike URL rejected");
        Reject(()=>ExophasePageParser.ValidateUrl("file:///C:/page.html"),"live source cannot be local executable content");
        Check(ExophasePageParser.Timestamp("99999999999999999")==null,"invalid timestamp rejected");
        var earnedJsonMethod=typeof(Trophy).Assembly.GetType("Osiris.Extensions.Trophies.ExophaseSyncService").GetMethod("ApplyEarnedJson");
        earnedJsonMethod.Invoke(null,new object[] { exoPage,"{\"success\":true,\"list\":[{\"awardid\":\"11\",\"timestamp\":1700000001}]}" });
        Check(exoPage.Awards[1].UnlockedUtc.HasValue && !exoPage.Awards[0].UnlockedUtc.HasValue,"observed live API fields map by Exophase award ID");
        Reject(()=>earnedJsonMethod.Invoke(null,new object[] { exoPage,"{\"success\":true,\"list\":[{\"awardid\":\"unknown\",\"timestamp\":1700000001}]}" }),"unknown live award rejected");
        Check(exoPage.Awards[1].UnlockedUtc.HasValue && !exoPage.Awards[0].UnlockedUtc.HasValue,"malformed live response never partially changes awards");
        Reject(()=>earnedJsonMethod.Invoke(null,new object[] { exoPage,"{\"success\":false,\"list\":[]}" }),"rejected live request preserves earned data");
        if (args.Length==1)
        {
            var savedPage=ExophasePageParser.Parse(File.ReadAllText(args[0]));
            Check(savedPage.Awards.Count>0 && savedPage.Awards.Any(a=>a.UnlockedUtc.HasValue),"user-supplied HTML has individual earned timestamps");
            Console.WriteLine("Supplied HTML: "+savedPage.Awards.Count+" awards, "+savedPage.Awards.Count(a=>a.UnlockedUtc.HasValue)+" earned, platform "+savedPage.Platform+". No profile data written.");
        }
        parsed.Trophies[0].UnlockedUtc = new DateTime(2026, 10, 6, 12, 34, 0, DateTimeKind.Utc);
        Check(parsed.Trophies[0].DisplayDescription == parsed.Trophies[0].UnlockedUtc.Value.ToLocalTime().ToString("g"), "unlocked description becomes local date/time");
        var earnedPopup=new TrophyRow(parsed.Trophies[0],true);
        Check(earnedPopup.DisplayDescription=="Description","popup earned rows keep achievement descriptions");
        Check(earnedPopup.UnlockDateText==parsed.Trophies[0].UnlockedUtc.Value.ToLocalTime().ToString("g"),"popup date exposed separately for header");
        Check(new TrophyRow(parsed.Trophies[0],false).UnlockDateText=="","compact card omits inline unlock metadata");
        var earnedSecret=new Trophy { Name="Secret",Hidden=true,Description="Secret description",UnlockedUtc=DateTime.UtcNow,NativePlatform="Steam" };
        Check(new TrophyRow(earnedSecret,true).DisplayDescription=="Secret description","earned secret description shown in full view");
        Check(new TrophyRow(earnedSecret,true).PlatformText=="Earned on Steam","earned platform stays available beside title");
        Reject(() => CatalogueService.Parse("{}", 1), "invalid schema");
        Reject(() => CatalogueService.Parse(Fixture.Replace("SECRET", "A"), 1), "duplicate IDs");
        Reject(() => CatalogueService.Parse("{\"game\":{\"availableGameStats\":{\"achievements\":{}}}}", 1), "wrong list type");
        Check(CatalogueService.Parse("{\"game\":{\"availableGameStats\":{}}}", 1).Trophies.Count == 0, "valid no achievements");
        Check(CatalogueService.SafeIcon("https://cdn.cloudflare.steamstatic.com/steamcommunity/public/images/apps/a.png") != null, "Steam HTTPS icon");
        Check(CatalogueService.SafeIcon("http://cdn.cloudflare.steamstatic.com/a") == null, "no plain HTTP");
        Check(CatalogueService.SafeIcon("https://evil-steamstatic.com/a") == null, "no lookalike host");
        Check(CatalogueService.SafeIcon("file:///C:/secret") == null, "no local icon URL");
        var root = Path.Combine(Path.GetTempPath(), "OsirisTrophiesValidation-" + Guid.NewGuid().ToString("N"));
        var game = Guid.NewGuid();
        try
        {
            var handler = new FixtureHandler { Json = Fixture };
            using (var service = new CatalogueService(root, handler))
            {
                Check(service.Load(game) == null && !Directory.Exists(root), "empty cache is read-only");
                var result = service.Import(game, 292030, "fixture-key", CancellationToken.None).GetAwaiter().GetResult();
                Check(result.Trophies.Count == 2 && service.Load(game).Trophies.Count == 2, "import and offline cache");
                Check(result.Trophies[0].GlobalPercent == 42.5, "import caches Steam rarity");
                var path = Path.Combine(root, "catalogues", game.ToString("D") + ".json");
                var original = File.ReadAllText(path);
                handler.Json = Fixture.Replace("Fixture Game", "Staged game");
                handler.RarityStatus = HttpStatusCode.ServiceUnavailable;
                var staged = service.Import(game, 292030, "fixture-key", CancellationToken.None, false).GetAwaiter().GetResult();
                Check(staged.GameName == "Staged game" && File.ReadAllText(path) == original, "fetch stages without committing catalogue");
                Check(!staged.Trophies[0].GlobalPercent.HasValue, "optional rarity failure does not block catalogue");
                service.Save(game, staged);
                Check(service.Load(game).GameName == "Staged game", "Save commits staged catalogue");
                service.Save(game, result);
                Check(!original.Contains("fixture-key") && !original.Contains("achieved"), "cache excludes credentials and invented unlock state");
                handler.Json = "{}";
                Reject(() => service.Import(game, 1, "fixture-key", CancellationToken.None).GetAwaiter().GetResult(), "bad reimport");
                Check(File.ReadAllText(path) == original, "bad reimport preserves cache");
                handler.Status = HttpStatusCode.Forbidden;
                Reject(() => service.Import(game, 1, "fixture-key", CancellationToken.None).GetAwaiter().GetResult(), "HTTP failure");
                Check(File.ReadAllText(path) == original, "HTTP failure preserves cache");
                Reject(() => service.Import(game, 1, "", CancellationToken.None).GetAwaiter().GetResult(), "missing API key");
                var cancel = new CancellationToken(true);
                Reject(() => service.Import(game, 1, "fixture-key", cancel).GetAwaiter().GetResult(), "cancel import");
                Check(!Directory.GetFiles(root, "*.tmp", SearchOption.AllDirectories).Any(), "no leftover temp files");
            }
            // Parse the real compiled view dictionaries without reading any profile.
            var resources = (ResourceDictionary)Application.LoadComponent(new Uri("/Osiris.Trophies;component/CanonicalStyles.xaml", UriKind.Relative));
            Check(resources.Contains("OsirisSettingsActionButtonStyle"), "canonical action style loads");
            Check(((Style)resources["TrophiesPasswordBoxStyle"]).TargetType == typeof(PasswordBox), "password style loads");
            // Synthetic WPF construction exercises the real compiled views without a live profile.
            var plugin = (TrophiesPlugin)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(TrophiesPlugin));
            var model = (TrophiesSettingsModel)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(TrophiesSettingsModel));
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(TrophiesSettingsModel).GetField("<Committed>k__BackingField", flags).SetValue(model, new TrophiesSettings());
            model.BeginEdit();
            typeof(TrophiesPlugin).GetField("Settings", flags).SetValue(plugin, model);
            using (var offline = new CatalogueService(root, new FixtureHandler { Json = Fixture }))
            {
                var exoGame=Guid.NewGuid(); offline.Save(exoGame,exoCatalogue);
                var nativeGame=Guid.NewGuid(); offline.Save(nativeGame,native);
                offline.Save(nativeGame,CatalogueService.Parse(Fixture,292030));
                Check(offline.Load(nativeGame).NativeProvider==TrophySources.Xbox.ToString("D") && offline.Load(nativeGame).Trophies[0].NativeUnlockedUtc.HasValue,"catalogue reimport preserves native provider and earned dates");
                var raw=offline.Load(exoGame);
                TrophySources.ForDisplay(new Playnite.SDK.Models.Game { PluginId=TrophySources.Steam },raw);
                Check(offline.Load(exoGame).Trophies[0].ExophaseUnlocks.Count==1,"presentation isolation never deletes cached Exophase provenance");
                offline.Save(exoGame,CatalogueService.Parse(Fixture,292030));
                Check(offline.Load(exoGame).Trophies[0].ExophaseUnlocks.Count==1 && offline.Load(exoGame).ExophasePageUrl==exoCatalogue.ExophasePageUrl,"Steam reimport preserves source provenance and Exophase URL");
                typeof(TrophiesPlugin).GetField("Catalogues", flags).SetValue(plugin, offline);
                var view = (TrophiesViewControl)Activator.CreateInstance(typeof(TrophiesViewControl), flags, null, new object[] { plugin }, null);
                view.GameContextChanged(null, new Playnite.SDK.Models.Game { Id = game, Name = "Local fixture" });
                Check(view.IsCardVisible, "local game card visible without Steam ownership");
                Check(((ItemsControl)view.FindName("TrophyList")).Items.Count == 2, "real WPF view loads cached rows");
                Check(view.CountText == "0/2", "recorded unlock counter");
                typeof(TrophiesViewControl).GetMethod("Synchronise",flags).Invoke(view,null);
                Check((DateTime)typeof(TrophiesViewControl).GetField("lastSyncAttempt",flags).GetValue(view)==default(DateTime), "local card never automatically requests Exophase trophies");
                view.Measure(new Size(550, 800)); view.Arrange(new Rect(0, 0, 550, 800));
                Check(view.DesiredSize.Width <= 550, "card fits details column");
                Check(view.FindName("ViewAllButton") is Button && ((Button)view.FindName("ViewAllButton")).Content.ToString() == "view all", "Details-style view all footer");
                Check(!Descendants(view).OfType<ScrollViewer>().Any(), "card has no scroll viewer");
                var many = CatalogueService.Parse(Fixture, 292030);
                for (var i = 0; i < 6; i++) many.Trophies.Add(new Trophy { Id = "MORE" + i, Name = "More " + i, Description = "Fixture" });
                offline.Save(game, many);
                view.GameContextChanged(null, new Playnite.SDK.Models.Game { Id = game, Name = "Local fixture" });
                Check(((ItemsControl)view.FindName("TrophyList")).Items.Count == 3, "preview limited to three complete rows");
                var popup = (Window)Activator.CreateInstance(typeof(TrophiesWindow), flags, null, new object[] { "Fixture", many.Trophies }, null);
                Check(((ItemsControl)popup.FindName("AllTrophies")).Items.Count == 8, "popup contains complete catalogue");
                var all = (ItemsControl)popup.FindName("AllTrophies");
                Check(all.Items.Groups.Count == 2 && all.GroupStyle.Count == 1, "popup grouped into Achievements and Hidden");
                Check(all.Items.OfType<TrophyRow>().Single(t => t.Category == "Hidden").CanReveal, "popup secret rows clickable");
                Check(!((ItemsControl)view.FindName("TrophyList")).Items.OfType<TrophyRow>().Any(t => t.CanReveal), "card never reveals secrets");
                Check(((ItemsControl)view.FindName("TrophyList")).GroupStyle.Count == 0, "card omits category heading");
                Check(!((TextBlock)popup.FindName("Heading")).Text.Contains("·"), "popup title has no dot");
                var selector = (ComboBox)popup.FindName("SortBy");
                Check(selector.Items.Count == 3 && selector.SelectedIndex == 0 && ((ComboBoxItem)selector.Items[2]).Content.ToString()=="Last achieved", "Title Rarity and Last achieved sorting selector");
                selector.SelectedIndex = 1;
                Check(all.Items.Groups.Count == 2 && all.Items.Count == 8, "changing sort preserves both groups and all rows");
                selector.SelectedIndex=2;
                Check(all.Items.Groups.Count==2 && all.Items.Count==8, "compiled Last achieved dropdown handler preserves groups and catalogue");
                Check(popup.Height == Math.Min(900,Math.Max(480,SystemParameters.WorkArea.Height-80)), "taller popup clamped to work area");
                Check(popup.WindowStyle == WindowStyle.None && popup.ResizeMode == ResizeMode.CanResize && popup.MinWidth == 760 && popup.MinHeight == 480, "canonical resizable Middle Window");
                Check(Descendants(popup).OfType<ScrollViewer>().Any(), "full-list popup scrolls");
                var achievementScroll=(ScrollViewer)popup.FindName("AchievementScroll");
                Check(achievementScroll.Margin.Right==0 && achievementScroll.Margin.Left==0 && achievementScroll.Padding==new Thickness(0), "scrollbar host reaches inner window edges without row inset");
                Check(all.Margin.Left==24 && all.Margin.Right==24, "achievement content retains inset separately from edge scrollbar");
                popup.Close();
                var achievementResources=(ResourceDictionary)Application.LoadComponent(new Uri("/Osiris.Trophies;component/AchievementStyles.xaml",UriKind.Relative));
                var achievementTemplate=(DataTemplate)achievementResources["AchievementRow"];
                var sampleRow=(Button)achievementTemplate.LoadContent();
                sampleRow.DataContext=new TrophyRow(new Trophy {Name="Earned",Description="Description",UnlockedUtc=previewDate,NativePlatform="Steam"},true,TrophyHighlight.Gold);
                sampleRow.Measure(new Size(900,double.PositiveInfinity)); sampleRow.Arrange(new Rect(sampleRow.DesiredSize)); sampleRow.UpdateLayout();
                var rowTexts=Descendants(sampleRow).OfType<TextBlock>().ToList();
                Check(rowTexts.Single(t=>t.Text=="Earned").FontSize==21 && rowTexts.Single(t=>t.Text==previewDate.ToLocalTime().ToString("g")).FontSize==21 && rowTexts.Single(t=>t.Text=="Earned on Steam").FontSize==21, "full-view title date and platform use identical typography size");
                var iconShine=Descendants(sampleRow).OfType<Border>().Single(t=>t.Name=="IconShine");
                Check(iconShine.Visibility==Visibility.Visible && iconShine.Effect is System.Windows.Media.Effects.DropShadowEffect, "gold shine is behind earned icon in compiled WPF row");
                sampleRow.DataContext=new TrophyRow(new Trophy {UnlockedUtc=previewDate},true,TrophyHighlight.Silver);
                sampleRow.UpdateLayout();
                Check(iconShine.Visibility==Visibility.Visible && ((System.Windows.Media.Effects.DropShadowEffect)iconShine.Effect).Color==System.Windows.Media.Color.FromRgb(0xC2,0xCD,0xD9), "silver shine has distinct silver halo");
                sampleRow.DataContext=new TrophyRow(new Trophy {UnlockedUtc=previewDate},true,TrophyHighlight.Purple);
                sampleRow.UpdateLayout();
                Check(iconShine.Visibility==Visibility.Visible && ((System.Windows.Media.Effects.DropShadowEffect)iconShine.Effect).Color==System.Windows.Media.Color.FromRgb(0xA3,0x7A,0xCC), "rarest earned trophy uses subdued purple halo");
                sampleRow.DataContext=new TrophyRow(new Trophy(),true,TrophyHighlight.Gold);
                sampleRow.UpdateLayout();
                Check(iconShine.Visibility==Visibility.Visible && ((System.Windows.Media.Effects.DropShadowEffect)iconShine.Effect).Color==System.Windows.Media.Color.FromRgb(0x8D,0x92,0x99), "locked highlighted row uses grey halo");
                sampleRow.DataContext=new TrophyRow(new Trophy(),true);
                sampleRow.UpdateLayout();
                Check(iconShine.Visibility==Visibility.Collapsed && iconShine.Effect==null, "non-highlighted trophy still has no shine");
                var settingsView = (UserControl)Activator.CreateInstance(typeof(TrophiesSettingsView), flags, null, new object[] { model }, null);
                Check(settingsView.FindName("ApiKeyBox") is PasswordBox, "settings use masked key field");
                Check(settingsView.FindName("ExophaseSwitch")==null,"obsolete global Exophase switch removed");
                var settingsPages=(TabControl)settingsView.FindName("SettingsPages");
                Check(settingsPages.Items.Count==2 && ((TabItem)settingsPages.Items[0]).Header.ToString()=="General" && ((TabItem)settingsPages.Items[1]).Header.ToString()=="Account", "settings exposes General and Account pages for shared host navigation");
                var generalPage=(TabItem)settingsView.FindName("GeneralPage");
                var accountPage=(TabItem)settingsView.FindName("AccountPage");
                var generalControls=Descendants(generalPage).ToList();
                Check(generalControls.OfType<CheckBox>().Count()==1 && generalControls.OfType<TextBlock>().Single().Text=="Show Trophies cards on Game Details", "General contains only the show cards switch without duplicate title or explanation");
                Check(!generalControls.OfType<PasswordBox>().Any() && Descendants(accountPage).Contains((DependencyObject)settingsView.FindName("ApiKeyBox")), "masked API key moved to Account, not General");
                Check(!Descendants(accountPage).OfType<CheckBox>().Any() && Descendants(accountPage).OfType<TextBlock>().Count()==2, "Account contains only API key title and description, with no Exophase switch or status");
                const string requestedKeyDescription="Your Steam API Key is used to export trophies catalogues to Osiris games, it is not used to synchronise your unlocked trophies, this get updated through integrated libraries and optionally through our Exophase extension";
                Check(((TextBlock)settingsView.FindName("ApiKeyDescription")).Text==requestedKeyDescription, "Account uses requested API key description verbatim");
                var accountForm=(StackPanel)((ScrollViewer)accountPage.Content).Content;
                accountForm.Measure(new Size(760,double.PositiveInfinity));
                accountForm.Arrange(new Rect(0,0,760,accountForm.DesiredSize.Height));
                accountForm.UpdateLayout();
                var accountTitle=Descendants(accountPage).OfType<TextBlock>().Single(t=>t.Text=="Steam Web API Key");
                var accountDescription=(TextBlock)settingsView.FindName("ApiKeyDescription");
                Check(Math.Abs(accountDescription.TranslatePoint(new Point(),accountForm).X-accountTitle.TranslatePoint(new Point(),accountForm).X)<0.1, "description starts at the API key title's exact left edge in the 760 px form");
                var accountKey=(PasswordBox)settingsView.FindName("ApiKeyBox");
                Check(accountKey.Width==388 && accountKey.Height==40, "Account retains canonical Backup password field dimensions");
                accountKey.Password=new string('a',32);
                settingsPages.SelectedIndex=1; settingsPages.SelectedIndex=0; settingsPages.SelectedIndex=1;
                Check(accountKey.Password==new string('a',32), "switching settings categories preserves pending key input");
                model.CancelEdit();
                Check(view.FindName("ImportButton") == null && view.FindName("AppIdBox") == null, "card has no fetch controls");
                var editor = (UserControl)Activator.CreateInstance(typeof(TrophiesGameEditView), flags, null, new object[] { plugin, new Playnite.SDK.Models.Game { Id = game, Name = "Local fixture" } }, null);
                Check(editor.FindName("ImportButton") is Button && editor.FindName("AppIdBox") is TextBox, "game editor owns fetch controls");
                Check(editor.FindName("SyncButton") is Button && editor.FindName("HtmlButton")==null && editor.FindName("ExophaseUrlBox")==null,"editor has one Sync action and no duplicate URL or HTML controls");
                var nativeEditor=(UserControl)Activator.CreateInstance(typeof(TrophiesGameEditView),flags,null,new object[]{plugin,new Playnite.SDK.Models.Game { Id=game,PluginId=TrophySources.Steam }},null);
                Check(((TextBlock)nativeEditor.FindName("SyncHint")).Text.Contains("connected library"),"integrated editor describes native sync");
                Check(((Button)editor.FindName("SyncButton")).Content.ToString()=="Sync","single clearly labelled Sync button");
                typeof(TrophiesGameEditView).GetField("staged",flags).SetValue(editor,parsed);
                typeof(TrophiesGameEditView).GetMethod("SyncClick",flags).Invoke(editor,new object[]{null,new RoutedEventArgs()});
                Check(((TextBlock)editor.FindName("SyncStatus")).Text=="Save the game's catalogue first, then Sync.", "manual local Sync proceeds without any global Exophase enable requirement");
                var imagePath = Path.Combine(root, "icon-test.png");
                var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(1,1,96,96,System.Windows.Media.PixelFormats.Bgra32,null,new byte[] {0,0,255,255},4);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using (var stream = File.Create(imagePath)) encoder.Save(stream);
                var trophy = new Trophy { LocalIcon = imagePath };
                var converter = new TrophyIconConverter();
                var grey = (System.Windows.Media.Imaging.BitmapSource)converter.Convert(trophy, typeof(object), null, System.Globalization.CultureInfo.InvariantCulture);
                Check(grey.Format == System.Windows.Media.PixelFormats.Gray8, "unknown icon is grayscale placeholder");
                trophy.UnlockedUtc = DateTime.UtcNow;
                var colour = (System.Windows.Media.Imaging.BitmapSource)converter.Convert(trophy, typeof(object), null, System.Globalization.CultureInfo.InvariantCulture);
                Check(colour.Format != System.Windows.Media.PixelFormats.Gray8, "known earned icon remains colour");
                model.Enabled = false; model.CancelEdit();
                Check(model.Enabled, "Cancel restores committed visibility");
                view.GameContextChanged(null, null);
                Check(!view.IsCardVisible, "no game hides card");
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        Console.WriteLine(checks + " Trophies checks passed.");
    }
    private sealed class FixtureHandler : HttpMessageHandler
    {
        public string Json;
        public HttpStatusCode Status = HttpStatusCode.OK;
        public HttpStatusCode RarityStatus = HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rarity = request.RequestUri.AbsolutePath.Contains("GetGlobalAchievementPercentagesForApp");
            return Task.FromResult(new HttpResponseMessage(rarity ? RarityStatus : Status) { Content = new StringContent(rarity ? "{\"achievementpercentages\":{\"achievements\":[{\"name\":\"A\",\"percent\":42.5}]}}" : Json) });
        }
    }
    private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        { yield return child; foreach (var descendant in Descendants(child)) yield return descendant; }
    }
}
