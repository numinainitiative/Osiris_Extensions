using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IgnMetadata.Api;
using PlayniteExtensions.Common;

class Program
{
    static int checks;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static void Main(string[] args)
    {
        var handler = new Handler();
        var client = new IgnApiClient(new WebDownloader(handler));
        handler.Json = "{\"errors\":null,\"data\":{\"objectSelectByTypeAndSlug\":{\"slug\":\"fixture\",\"metadata\":{\"names\":{\"name\":\"Fixture\"}}}}}";
        Check(client.Get("fixture", "uk").Name == "Fixture", "usable response permits null error array");
        var query = Uri.UnescapeDataString(handler.Url);
        Check(query.Contains("query OsirisGame") && !query.Contains("hrefLangs") && !query.Contains("persistedQuery"), "details uses owned supported query");
        handler.Json = "{\"errors\":[{\"message\":\"Unavailable\"}],\"data\":null}";
        Check(client.Get("fixture", "uk") == null, "GraphQL error is not metadata");
        handler.Json = "{\"data\":{\"objectSelectByTypeAndSlug\":null}}";
        Check(client.GetImages("fixture") == null, "missing gallery object does not crash");
        handler.Json = "{\"data\":{\"objectSelectByTypeAndSlug\":{\"imageGallery\":{\"images\":null}}}}";
        Check(client.GetImages("fixture") == null, "missing image list does not crash");
        if (args.Length > 0 && args[0] == "--live")
        {
            var live = new IgnApiClient(new WebDownloader());
            foreach (var slug in new[] { "need-for-speed-hot-pursuit-remastered", "assassins-creed-brotherhood" })
            {
                var game = live.Get(slug, "us");
                Check(game != null && game.Slug == slug && game.Names.Count > 0 && !string.IsNullOrWhiteSpace(game.Metadata.Descriptions.Long), "live title and description: " + slug);
                Check(game.Genres.Length > 0 && game.Publishers.Length > 0, "live genres and publishers: " + slug);
                Check(game.ReleaseDate.HasValue && game.PrimaryImage?.Url != null, "live release and cover: " + slug);
                Console.WriteLine("Live metadata retrieved: " + game.Name);
            }
        }
        Console.WriteLine(checks + " IGN checks passed.");
    }
    class Handler : HttpMessageHandler
    {
        public string Json, Url;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Url = request.RequestUri.AbsoluteUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = new StringContent(Json) });
        }
    }
}
