using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite.SDK;

namespace Osiris.Extensions.Exophase
{
    internal sealed class ExophaseExtractionService
    {
        private const int MaximumPages = 1000;
        private readonly IPlayniteAPI api;
        private readonly string userDataPath;
        private readonly ILogger logger = LogManager.GetLogger();

        public ExophaseExtractionService(IPlayniteAPI api, string userDataPath)
        {
            this.api = api ?? throw new ArgumentNullException(nameof(api));
            this.userDataPath = userDataPath ?? throw new ArgumentNullException(nameof(userDataPath));
        }

        public async Task<ExophaseActivitySnapshot> ExtractAsync(
            string profileReference,
            string knownProfileId,
            IProgress<string> progress,
            CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(userDataPath);
            using (var browser = api.WebViews.CreateOffscreenView())
            {
                var profileId = await ResolveProfileIdAsync(
                    browser,
                    profileReference,
                    knownProfileId,
                    progress,
                    cancellationToken).ConfigureAwait(true);
                var records = new List<ExophaseGameActivity>();
                var rawPages = new JArray();
                var seenRecords = new HashSet<string>(StringComparer.Ordinal);
                var reachedEnd = false;

                for (var page = 1; page <= MaximumPages; page++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report($"Reading Exophase activity page {page}...");
                    var url = "https://api.exophase.com/public/player/" + profileId +
                              "/games?page=" + page + "&environment=&sort=1&showHidden=0";
                    var document = await DownloadJsonAsync(
                        browser,
                        url,
                        cancellationToken).ConfigureAwait(true);
                    if (document.Value<bool?>("success") == false)
                    {
                        if (records.Count == 0)
                        {
                            throw new InvalidOperationException(
                                "Exophase rejected the profile activity request.");
                        }

                        reachedEnd = true;
                        break;
                    }

                    var pageRecords = ExophaseActivityParser.ParseGames(document);
                    if (pageRecords.Count == 0)
                    {
                        reachedEnd = true;
                        break;
                    }

                    var added = 0;
                    foreach (var record in pageRecords)
                    {
                        var identity = record.RemoteGameId + "|" +
                                       record.Title + "|" +
                                       record.Platform + "|" +
                                       record.LastPlayedUtc + "|" +
                                       record.PlaytimeSeconds;
                        if (seenRecords.Add(identity))
                        {
                            records.Add(record);
                            added++;
                        }
                    }

                    rawPages.Add(document);
                    if (added == 0)
                    {
                        throw new InvalidOperationException(
                            "Exophase repeated an activity page, so synchronization stopped safely.");
                    }
                }

                if (!reachedEnd)
                {
                    throw new InvalidOperationException(
                        "The Exophase profile exceeded the extractor's safe page limit.");
                }

                if (records.Count == 0)
                {
                    throw new InvalidOperationException(
                        "No Exophase game activity was available for this profile. Check profile privacy and connected platforms.");
                }

                var snapshot = new ExophaseActivitySnapshot
                {
                    PlayerProfileId = profileId,
                    FetchedUtc = DateTime.UtcNow,
                    Games = records
                };
                SaveSnapshots(snapshot, rawPages);
                return snapshot;
            }
        }

        internal async Task<string> ResolveProfileIdAsync(
            IWebView browser,
            string profileReference,
            string knownProfileId,
            IProgress<string> progress,
            CancellationToken cancellationToken)
        {
            string parsedId;
            if (ExophaseActivityParser.TryParseNumericProfileId(profileReference, out parsedId))
            {
                return parsedId;
            }

            if (string.IsNullOrWhiteSpace(profileReference) &&
                ExophaseActivityParser.TryParseNumericProfileId(knownProfileId, out parsedId))
            {
                return parsedId;
            }

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report("Finding the Exophase profile...");
            var profileUrl = ExophaseActivityParser.GetProfilePageUrl(profileReference);
            browser.NavigateAndWait(profileUrl);
            var pageSource = await browser.GetPageSourceAsync().ConfigureAwait(true);
            parsedId = ExophaseActivityParser.FindPlayerProfileId(pageSource);
            if (!string.IsNullOrWhiteSpace(parsedId))
            {
                return parsedId;
            }

            var currentAddress = browser.GetCurrentAddress();
            if (currentAddress?.IndexOf("/login", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new InvalidOperationException(
                    "Sign in to Exophase first, or enter a public Exophase username or profile URL.");
            }

            var profileSlug = ExophaseActivityParser.FindProfileSlug(pageSource);
            if (!string.IsNullOrWhiteSpace(profileSlug))
            {
                cancellationToken.ThrowIfCancellationRequested();
                browser.NavigateAndWait(
                    "https://www.exophase.com/user/" + Uri.EscapeDataString(profileSlug) + "/");
                pageSource = await browser.GetPageSourceAsync().ConfigureAwait(true);
                parsedId = ExophaseActivityParser.FindPlayerProfileId(pageSource);
                if (!string.IsNullOrWhiteSpace(parsedId))
                {
                    return parsedId;
                }
            }

            throw new InvalidOperationException(
                "Osiris could not find this profile's Exophase player ID. Enter the username or full public profile URL and try again.");
        }

        private async Task<JObject> DownloadJsonAsync(
            IWebView browser,
            string url,
            CancellationToken cancellationToken)
        {
            await EnsureExophasePageAsync(browser, cancellationToken).ConfigureAwait(true);
            var directJson = await TryDownloadDirectAsync(
                browser,
                url,
                cancellationToken).ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(directJson))
            {
                return JObject.Parse(directJson);
            }

            var browserRequestJson = await TryDownloadInBrowserContextAsync(
                browser,
                url,
                cancellationToken).ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(browserRequestJson))
            {
                return JObject.Parse(browserRequestJson);
            }

            cancellationToken.ThrowIfCancellationRequested();
            browser.NavigateAndWait(url);
            InvalidOperationException lastFailure = null;
            for (var attempt = 0; attempt < 30; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pageSource = await browser.GetPageSourceAsync().ConfigureAwait(true);
                try
                {
                    var browserJson = ExophaseActivityParser.UnwrapJsonPage(pageSource);
                    return JObject.Parse(browserJson);
                }
                catch (InvalidOperationException exception)
                {
                    lastFailure = exception;
                }

                await Task.Delay(500, cancellationToken).ConfigureAwait(true);
            }

            throw lastFailure ?? new InvalidOperationException(
                "Exophase did not return activity data.");
        }

        private async Task<string> TryDownloadDirectAsync(
            IWebView browser,
            string url,
            CancellationToken cancellationToken)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(20);
                    var userAgent = await ReadBrowserUserAgentAsync(browser).ConfigureAwait(true);
                    if (!string.IsNullOrWhiteSpace(userAgent))
                    {
                        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);
                    }

                    client.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.Referrer = new Uri("https://www.exophase.com/");
                    var cookieHeader = BuildExophaseCookieHeader(
                        browser.GetCookies(),
                        new Uri(url));
                    if (!string.IsNullOrWhiteSpace(cookieHeader))
                    {
                        client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookieHeader);
                    }

                    using (var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false))
                    {
                        if (response.StatusCode == HttpStatusCode.Forbidden ||
                            (int)response.StatusCode == 429)
                        {
                            return null;
                        }

                        response.EnsureSuccessStatusCode();
                        var source = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        try
                        {
                            return ExophaseActivityParser.UnwrapJsonPage(source);
                        }
                        catch (InvalidOperationException)
                        {
                            return null;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.Debug(exception, "Direct Exophase API request failed; using the Osiris browser session.");
                return null;
            }
        }

        private static async Task<string> ReadBrowserUserAgentAsync(IWebView browser)
        {
            if (!browser.CanExecuteJavascriptInMainFrame)
            {
                return null;
            }

            try
            {
                var result = await browser.EvaluateScriptAsync("navigator.userAgent").ConfigureAwait(true);
                return result?.Success == true ? Convert.ToString(result.Result) : null;
            }
            catch
            {
                return null;
            }
        }

        private static async Task EnsureExophasePageAsync(
            IWebView browser,
            CancellationToken cancellationToken)
        {
            Uri currentAddress;
            var address = browser.GetCurrentAddress();
            var hasWebsiteOrigin = Uri.TryCreate(address, UriKind.Absolute, out currentAddress) &&
                                   (string.Equals(currentAddress.Host, "exophase.com", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(currentAddress.Host, "www.exophase.com", StringComparison.OrdinalIgnoreCase));
            if (!hasWebsiteOrigin || !browser.CanExecuteJavascriptInMainFrame)
            {
                cancellationToken.ThrowIfCancellationRequested();
                browser.NavigateAndWait(ExophaseAuthenticationService.AccountUrl);
            }

            for (var attempt = 0; attempt < 20; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (browser.CanExecuteJavascriptInMainFrame)
                {
                    return;
                }

                await Task.Delay(250, cancellationToken).ConfigureAwait(true);
            }
        }

        private static async Task<string> TryDownloadInBrowserContextAsync(
            IWebView browser,
            string url,
            CancellationToken cancellationToken)
        {
            if (!browser.CanExecuteJavascriptInMainFrame)
            {
                return null;
            }

            const string stateName = "__osirisExophaseActivityRequest";
            var script =
                "(function(){window." + stateName + "={done:false,status:0,body:''};" +
                "fetch(" + JsonConvert.SerializeObject(url) + ",{credentials:'include',headers:{'Accept':'application/json'}})" +
                ".then(function(response){return response.text().then(function(body){window." + stateName +
                "={done:true,status:response.status,body:body};});})" +
                ".catch(function(){window." + stateName + "={done:true,status:0,body:''};});return true;})()";
            JavaScriptEvaluationResult started;
            try
            {
                started = await browser.EvaluateScriptAsync(script).ConfigureAwait(true);
            }
            catch
            {
                return null;
            }
            if (started?.Success != true)
            {
                return null;
            }

            try
            {
                for (var attempt = 0; attempt < 40; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(250, cancellationToken).ConfigureAwait(true);
                    var stateResult = await browser.EvaluateScriptAsync(
                        "JSON.stringify(window." + stateName + ")").ConfigureAwait(true);
                    var stateJson = stateResult?.Success == true
                        ? Convert.ToString(stateResult.Result)
                        : null;
                    if (string.IsNullOrWhiteSpace(stateJson))
                    {
                        continue;
                    }

                    var state = JObject.Parse(stateJson);
                    if (state.Value<bool?>("done") != true)
                    {
                        continue;
                    }

                    var status = state.Value<int?>("status") ?? 0;
                    var body = state.Value<string>("body");
                    if (status < 200 || status >= 300 || string.IsNullOrWhiteSpace(body))
                    {
                        return null;
                    }

                    try
                    {
                        return ExophaseActivityParser.UnwrapJsonPage(body);
                    }
                    catch (InvalidOperationException)
                    {
                        return null;
                    }
                }

                return null;
            }
            finally
            {
                await browser.EvaluateScriptAsync(
                    "window." + stateName + "=null").ConfigureAwait(true);
            }
        }

        private static string BuildExophaseCookieHeader(
            IEnumerable<Playnite.SDK.HttpCookie> cookies,
            Uri requestUri)
        {
            if (cookies == null || requestUri == null)
            {
                return null;
            }

            return string.Join(
                "; ",
                cookies
                    .Where(cookie => cookie != null &&
                                     CookieAppliesToHost(cookie.Domain, requestUri.Host) &&
                                     IsSafeCookieName(cookie.Name) &&
                                     IsSafeCookieValue(cookie.Value))
                    .GroupBy(cookie => cookie.Name, StringComparer.Ordinal)
                    .Select(group => group.Last())
                    .Select(cookie => cookie.Name + "=" + cookie.Value));
        }

        private static bool CookieAppliesToHost(string domain, string requestHost)
        {
            var value = domain?.Trim().TrimStart('.');
            if (string.IsNullOrWhiteSpace(value) ||
                string.IsNullOrWhiteSpace(requestHost) ||
                (!string.Equals(value, "exophase.com", StringComparison.OrdinalIgnoreCase) &&
                 !value.EndsWith(".exophase.com", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            return string.Equals(requestHost, value, StringComparison.OrdinalIgnoreCase) ||
                   requestHost.EndsWith("." + value, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSafeCookieName(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOfAny(new[] { ';', '=', '\r', '\n' }) < 0;
        }

        private static bool IsSafeCookieValue(string value)
        {
            return value != null && value.IndexOfAny(new[] { ';', '\r', '\n' }) < 0;
        }

        private void SaveSnapshots(ExophaseActivitySnapshot snapshot, JArray rawPages)
        {
            var rawDocument = new JObject
            {
                ["schemaVersion"] = 1,
                ["playerProfileId"] = snapshot.PlayerProfileId,
                ["fetchedUtc"] = snapshot.FetchedUtc,
                ["pages"] = rawPages
            };
            WriteAtomically(
                Path.Combine(userDataPath, "exophase-raw-latest.json"),
                rawDocument.ToString(Formatting.Indented));
            WriteAtomically(
                Path.Combine(userDataPath, "exophase-activity-latest.json"),
                JsonConvert.SerializeObject(snapshot, Formatting.Indented));
        }

        private static void WriteAtomically(string path, string content)
        {
            var temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, content);
            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, null, true);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }
    }
}
