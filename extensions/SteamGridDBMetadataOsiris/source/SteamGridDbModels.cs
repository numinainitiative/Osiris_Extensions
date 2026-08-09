using System.Collections.Generic;
using Newtonsoft.Json;

namespace Osiris.Extensions.SteamGridDBMetadata
{
    internal sealed class SteamGridDbResponse<T>
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("data")]
        public T Data { get; set; }

        [JsonProperty("errors")]
        public List<string> Errors { get; set; }
    }

    internal sealed class SteamGridDbGame
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("verified")]
        public bool Verified { get; set; }
    }

    internal sealed class SteamGridDbImage
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("score")]
        public double Score { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("thumb")]
        public string ThumbnailUrl { get; set; }

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("upvotes")]
        public int Upvotes { get; set; }

        [JsonProperty("downvotes")]
        public int Downvotes { get; set; }
    }

    internal sealed class SteamGridDbTarget
    {
        public string Kind { get; set; }

        public long Id { get; set; }
    }
}
