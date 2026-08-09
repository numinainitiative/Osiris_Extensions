namespace Osiris.Extensions.SteamGridDBMetadata
{
    public sealed class SteamGridDbSettings
    {
        public bool AllowAdultContentArtwork { get; set; }

        public bool AllowHumorousArtwork { get; set; }

        public string SimilarMediaApplication { get; set; } = "Different";

        public string ApiKey { get; set; } = string.Empty;

        public SteamGridDbSettings Clone()
        {
            return new SteamGridDbSettings
            {
                AllowAdultContentArtwork = AllowAdultContentArtwork,
                AllowHumorousArtwork = AllowHumorousArtwork,
                SimilarMediaApplication = SimilarMediaApplication,
                ApiKey = ApiKey
            };
        }
    }
}
