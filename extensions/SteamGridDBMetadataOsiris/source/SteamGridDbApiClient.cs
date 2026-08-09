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

            var query = new List<string>
            {
                "types=static",
                "nsfw=" + (allowAdult ? "any" : "false"),
                "humor=" + (allowHumor ? "any" : "false"),
                "page=0"
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
            return (Get<List<SteamGridDbImage>>(path, cancellationToken)
                    ?? new List<SteamGridDbImage>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Url))
                .OrderByDescending(item => item.Score)
                .ThenByDescending(item => item.Upvotes - item.Downvotes)
                .ThenByDescending(item => item.Id)
                .ToList();
        }

        private T Get<T>(string relativePath, CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, relativePath))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                using (var response = HttpClient.SendAsync(request, cancellationToken).GetAwaiter().GetResult())
                {
                    var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
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

                    if (!response.IsSuccessStatusCode || payload == null || !payload.Success)
                    {
                        var detail = payload?.Errors == null
                            ? null
                            : string.Join(" ", payload.Errors.Where(item => !string.IsNullOrWhiteSpace(item)));
                        throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail)
                            ? "SteamGridDB request failed (HTTP " + (int)response.StatusCode + ")."
                            : detail);
                    }

                    return payload.Data;
                }
            }
        }

        private static HttpClient CreateHttpClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var client = new HttpClient
            {
                BaseAddress = new Uri(BaseAddress),
                Timeout = TimeSpan.FromSeconds(30)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Osiris-SteamGridDBMetadata/0.1.0");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }
    }
}
