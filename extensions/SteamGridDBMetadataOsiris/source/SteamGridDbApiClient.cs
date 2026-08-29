using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using Newtonsoft.Json;

namespace Osiris.Extensions.SteamGridDBMetadata
{
    internal sealed class SteamGridDbApiClient
    {
        private const string BaseAddress = "https://www.steamgriddb.com/api/v2/";
        private static readonly HttpClient HttpClient = CreateHttpClient();
        private readonly string apiKey;

        public SteamGridDbApiClient(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "Add your SteamGridDB API key in the extension's Account settings first.");
            }

            this.apiKey = apiKey.Trim();
        }

        public IReadOnlyList<SteamGridDbGame> SearchGames(string query, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Array.Empty<SteamGridDbGame>();
            }

            return Get<List<SteamGridDbGame>>(
                    "search/autocomplete/" + Uri.EscapeDataString(query.Trim()),
                    cancellationToken)
                ?? new List<SteamGridDbGame>();
        }

        public IReadOnlyList<SteamGridDbImage> GetImages(
            string artworkKind,
            SteamGridDbTarget target,
            bool allowAdult,
            bool allowHumor,
            IEnumerable<string> dimensions,
            CancellationToken cancellationToken)
        {
            if (target == null)
            {
                return Array.Empty<SteamGridDbImage>();
            }

            return GetImagePage(
                artworkKind,
                target,
                allowAdult,
                allowHumor,
                dimensions,
                "static",
                0,
                cancellationToken).Items;
        }

        public SteamGridDbImagePage GetImagePage(
            string artworkKind,
            SteamGridDbTarget target,
            bool allowAdult,
            bool allowHumor,
            IEnumerable<string> dimensions,
            string assetType,
            int page,
            CancellationToken cancellationToken)
        {
            if (target == null)
            {
                return new SteamGridDbImagePage();
            }

            var query = new List<string>
            {
                "types=" + (string.Equals(assetType, "animated", StringComparison.OrdinalIgnoreCase)
                    ? "animated"
                    : "static"),
                "nsfw=" + (allowAdult ? "any" : "false"),
                "humor=" + (allowHumor ? "any" : "false"),
                "page=" + Math.Max(0, page).ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
            var dimensionList = dimensions == null
                ? Array.Empty<string>()
                : dimensions.Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();
            if (dimensionList.Length > 0)
            {
                query.Add("dimensions=" + Uri.EscapeDataString(string.Join(",", dimensionList)));
            }

            var path = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0}/{1}/{2}?{3}",
                artworkKind,
                target.Kind,
                target.Id,
                string.Join("&", query));
            var response = GetResponse<List<SteamGridDbImage>>(path, cancellationToken);
            var items = (response?.Data ?? new List<SteamGridDbImage>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Url))
                .OrderByDescending(item => item.Score)
                .ThenByDescending(item => item.Upvotes - item.Downvotes)
                .ThenByDescending(item => item.Id)
                .ToList();
            return new SteamGridDbImagePage
            {
                Items = items,
                Total = response == null || response.Total <= 0 ? items.Count : response.Total,
                Limit = response == null || response.Limit <= 0 ? Math.Max(1, items.Count) : response.Limit
            };
        }

        private T Get<T>(string relativePath, CancellationToken cancellationToken)
        {
            var response = GetResponse<T>(relativePath, cancellationToken);
            return response == null ? default(T) : response.Data;
        }

        private SteamGridDbResponse<T> GetResponse<T>(string relativePath, CancellationToken cancellationToken)
        {
            string json;
            var statusCode = HttpStatusCode.OK;
            using (var request = new HttpRequestMessage(HttpMethod.Get, relativePath))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                if (SteamGridDbFallbackTransport.ShouldPreferAlternateRoute)
                {
                    var fallback = SteamGridDbFallbackTransport.GetApi(
                        new Uri(HttpClient.BaseAddress, relativePath),
                        "Bearer " + apiKey,
                        cancellationToken);
                    statusCode = (HttpStatusCode)fallback.StatusCode;
                    json = System.Text.Encoding.UTF8.GetString(fallback.Body);
                }
                else
                {
                    try
                    {
                        using (var response = HttpClient.SendAsync(request, cancellationToken).GetAwaiter().GetResult())
                        {
                            statusCode = response.StatusCode;
                            json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        }
                    }
                    catch (Exception exception)
                    {
                        if (cancellationToken.IsCancellationRequested ||
                            !SteamGridDbFallbackTransport.IsRetryableNetworkFailure(exception))
                        {
                            throw;
                        }

                        var fallback = SteamGridDbFallbackTransport.GetApi(
                            new Uri(HttpClient.BaseAddress, relativePath),
                            "Bearer " + apiKey,
                            cancellationToken);
                        statusCode = (HttpStatusCode)fallback.StatusCode;
                        json = System.Text.Encoding.UTF8.GetString(fallback.Body);
                    }
                }

                SteamGridDbResponse<T> payload;
                try
                {
                    payload = JsonConvert.DeserializeObject<SteamGridDbResponse<T>>(json);
                }
                catch (JsonException exception)
                {
                    throw new InvalidOperationException(
                        "SteamGridDB returned an unreadable response.",
                        exception);
                }

                if ((int)statusCode < 200 || (int)statusCode >= 300 || payload == null || !payload.Success)
                {
                    var detail = payload?.Errors == null
                        ? null
                        : string.Join(" ", payload.Errors.Where(item => !string.IsNullOrWhiteSpace(item)));
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail)
                        ? "SteamGridDB request failed (HTTP " + (int)statusCode + ")."
                        : detail);
                }

                return payload;
            }
        }

        private static HttpClient CreateHttpClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var client = new HttpClient
            {
                BaseAddress = new Uri(BaseAddress),
                Timeout = TimeSpan.FromSeconds(7)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Osiris-SteamGridDBMetadata/1.0");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }
    }
}
