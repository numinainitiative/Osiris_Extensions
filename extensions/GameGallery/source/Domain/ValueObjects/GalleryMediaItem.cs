using System.Windows.Media.Imaging;

namespace SteamScreenshots.Domain.ValueObjects
{
    public abstract class GalleryMediaItem
    {
        public abstract BitmapImage ThumbnailImage { get; }
        public abstract bool IsVideo { get; }
        public abstract string Name { get; }
    }
}
