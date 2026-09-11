using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Osiris.Extensions.HowLongToBeat
{
    internal sealed class HowLongToBeatClient : IDisposable
    {
        private const string SiteRoot = "https://howlongtobeat.com/";
        private const string SearchEndpoint = "https://howlongtobeat.com/api/search/site";
        private static readonly Regex NextDataPattern = new Regex(
            "<script[^>]+id=[\\\"']__NEXT_DATA__[\\\"'][^>]*>(?<json>.*?)</script>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private readonly HttpClient httpClient;
        private readonly SemaphoreSlim requestGate = new SemaphoreSlim(1, 1);

        public HowLongToBeatClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                CookieContainer = new CookieContainer(),
                UseCookies = true
            };
            httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(20)
            };
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36");
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
            httpClient.DefaultRequestHeaders.Referrer = new Uri(SiteRoot);
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Origin", "https://howlongtobeat.com");
        }

        public async Task<CompletionTimeResult> SearchAsync(string gameName, int? releaseYear, CancellationToken cancellationToken)
        {
            var candidates = await SearchCandidatesAsync(gameName, cancellationToken).ConfigureAwait(false);
            var best = CompletionTimeMatching.SelectBest(gameName, releaseYear, candidates);
            if (best == null)
            {
                return new CompletionTimeResult
                {
                    Found = false,
                    FetchedUtc = DateTime.UtcNow
                };
            }

            var summary = ToResult(best);
            try
            {
                var details = await GetDetailsAsync(best.GameId, cancellationToken).ConfigureAwait(false);
                return details?.Found == true ? details : summary;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return summary;
            }
        }

        internal async Task<CompletionTimeResult> GetDetailsAsync(long gameId, CancellationToken cancellationToken)
        {
            if (gameId <= 0)
            {
                return null;
            }

            await requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using (var response = await httpClient.GetAsync(
                    SiteRoot + "game/" + gameId,
                    cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new HttpRequestException($"Completion-time details failed ({(int)response.StatusCode}).");
                    }

                    var html = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return ParseDetailPage(html, gameId);
                }
            }
            finally
            {
                requestGate.Release();
            }
        }

        internal static CompletionTimeResult ParseDetailPage(string html, long requestedGameId)
        {
            var match = NextDataPattern.Match(html ?? string.Empty);
            if (!match.Success)
            {
                throw new InvalidOperationException("The completion-time details page did not contain game data.");
            }

            var document = JObject.Parse(WebUtility.HtmlDecode(match.Groups["json"].Value));
            var game = document.SelectToken("props.pageProps.game.data.game[0]") as JObject;
            if (game == null)
            {
                throw new InvalidOperationException("The completion-time details response did not contain a game record.");
            }

            var result = new CompletionTimeResult
            {
                Found = true,
                RemoteGameId = game.Value<long?>("game_id") ?? requestedGameId,
                MatchedName = game.Value<string>("game_name") ?? string.Empty,
                MainStorySeconds = Positive(game, "comp_main"),
                MainExtraSeconds = Positive(game, "comp_plus"),
                CompletionistSeconds = Positive(game, "comp_100"),
                MainStoryRushedSeconds = Positive(game, "comp_main_l"),
                MainStoryAverageSeconds = Positive(game, "comp_main_avg"),
                MainStoryMedianSeconds = Positive(game, "comp_main_med"),
                MainStoryLeisureSeconds = Positive(game, "comp_main_h"),
                MainExtraRushedSeconds = Positive(game, "comp_plus_l"),
                MainExtraAverageSeconds = Positive(game, "comp_plus_avg"),
                MainExtraMedianSeconds = Positive(game, "comp_plus_med"),
                MainExtraLeisureSeconds = Positive(game, "comp_plus_h"),
                CompletionistRushedSeconds = Positive(game, "comp_100_l"),
                CompletionistAverageSeconds = Positive(game, "comp_100_avg"),
                CompletionistMedianSeconds = Positive(game, "comp_100_med"),
                CompletionistLeisureSeconds = Positive(game, "comp_100_h"),
                FetchedUtc = DateTime.UtcNow
            };

            if (!result.HasAnyTime)
            {
                throw new InvalidOperationException("The completion-time details response contained no estimates.");
            }

            return result;
        }

        private static long Positive(JObject source, string propertyName)
        {
            return Math.Max(0, source.Value<long?>(propertyName) ?? 0);
        }

        internal async Task<List<SearchCandidate>> SearchCandidatesAsync(string gameName, CancellationToken cancellationToken)
        {
            await requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var response = await SendSearchAsync(gameName, cancellationToken).ConfigureAwait(false);
                return (response["data"] as JArray ?? new JArray())
                    .Select(SearchCandidate.FromJson)
                    .Where(candidate => candidate != null && candidate.GameId > 0 && candidate.HasAnyTime)
                    .ToList();
            }
            finally
            {
                requestGate.Release();
            }
        }

        internal static CompletionTimeResult ToResult(SearchCandidate candidate)
        {
            if (candidate == null)
            {
                return null;
            }

            return new CompletionTimeResult
            {
                Found = true,
                RemoteGameId = candidate.GameId,
                MatchedName = candidate.GameName,
                MainStorySeconds = candidate.MainStorySeconds,
                MainExtraSeconds = candidate.MainExtraSeconds,
                CompletionistSeconds = candidate.CompletionistSeconds,
                FetchedUtc = DateTime.UtcNow
            };
        }

        private async Task<JObject> SendSearchAsync(string gameName, CancellationToken cancellationToken)
        {
            using (var initResponse = await httpClient.GetAsync(
                SearchEndpoint + "/init?t=" + GetUnixMilliseconds(), cancellationToken).ConfigureAwait(false))
            {
                if (!initResponse.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Completion-time search initialization failed ({(int)initResponse.StatusCode}).");
                }
                var initJson = JObject.Parse(await initResponse.Content.ReadAsStringAsync().ConfigureAwait(false));
                var token = initJson.Value<string>("token");
                var key = initJson.Value<string>("hpKey");
                var value = initJson.Value<string>("hpVal");
                if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(key) || value == null)
                {
                    throw new InvalidOperationException("The completion-time search handshake was incomplete.");
                }

                var payload = BuildPayload(gameName, key, value);
                using (var request = new HttpRequestMessage(HttpMethod.Post, SearchEndpoint))
                {
                    request.Headers.Referrer = new Uri(SiteRoot);
                    request.Headers.TryAddWithoutValidation("Origin", "https://howlongtobeat.com");
                    request.Headers.TryAddWithoutValidation("x-auth-token", token);
                    request.Headers.TryAddWithoutValidation("x-hp-key", key);
                    request.Headers.TryAddWithoutValidation("x-hp-val", value);
                    request.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");

                    using (var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            throw new HttpRequestException($"Completion-time search failed ({(int)response.StatusCode}).");
                        }
                        return JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                    }
                }
            }
        }

        internal static JObject BuildPayload(string gameName, string proofKey, string proofValue)
        {
            var terms = new JArray((gameName ?? string.Empty)
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            var emptySelection = new Func<JObject>(() => new JObject
            {
                ["mode"] = "include",
                ["values"] = new JArray()
            });

            var payload = new JObject
            {
                ["searchType"] = "games",
                ["searchTerms"] = terms,
                ["searchPage"] = 1,
                ["size"] = 20,
                ["searchOptions"] = new JObject
                {
                    ["games"] = new JObject
                    {
                        ["userId"] = 0,
                        ["platform"] = emptySelection(),
                        ["sortCategory"] = "popular",
                        ["rangeCategory"] = "main",
                        ["rangeTime"] = new JObject { ["min"] = 0, ["max"] = 0 },
                        ["gameplay"] = new JObject
                        {
                            ["perspective"] = emptySelection(),
                            ["flow"] = emptySelection(),
                            ["genre"] = emptySelection(),
                            ["difficulty"] = string.Empty
                        },
                        ["year"] = emptySelection(),
                        ["modifier"] = string.Empty
                    },
                    ["users"] = new JObject { ["sortCategory"] = "postcount" },
                    ["lists"] = new JObject { ["sortCategory"] = "follows" },
                    ["filter"] = string.Empty,
                    ["sort"] = 0,
                    ["randomizer"] = 0
                },
                ["useCache"] = true
            };
            payload[proofKey] = proofValue;
            return payload;
        }

        private static long GetUnixMilliseconds()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        }

        public void Dispose()
        {
            requestGate.Dispose();
            httpClient.Dispose();
        }
    }
}
