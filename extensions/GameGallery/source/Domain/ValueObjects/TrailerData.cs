namespace SteamScreenshots.Domain.ValueObjects
{
    public class TrailerData
    {
        public string Name { get; }
        public string ThumbnailUrl { get; }
        public string VideoUrl { get; }

        public TrailerData(string name, string thumbnailUrl, string videoUrl)
        {
            Name = name;
            ThumbnailUrl = thumbnailUrl;
            VideoUrl = videoUrl;
        }
    }
}
