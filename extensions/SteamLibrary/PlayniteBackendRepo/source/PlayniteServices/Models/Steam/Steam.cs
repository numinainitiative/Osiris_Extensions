#nullable enable

using System.Collections.Generic;

namespace Playnite.Backend.Steam
{
    public class SteamDbItemsRequest
    {
        public List<uint>? AppIds { get; set; }
    }

    public class SteamDbItem
    {
        public uint AppId { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public Dictionary<string, string>? LocalizedNames { get; set; }
    }
}