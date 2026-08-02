using SteamScreenshots.Domain.Interfaces;
using System;
using System.Threading;
using System.Windows.Media.Imaging;

namespace SteamScreenshots.Domain.ValueObjects
{
    public class Trailer : GalleryMediaItem
    {
        private readonly IImageProvider _imageProvider;
        private readonly Lazy<BitmapImage> _lazyThumbnail;

        public override string Name { get; }
        public string VideoUrl { get; }
        public override bool IsVideo => true;
        public override BitmapImage ThumbnailImage => _lazyThumbnail.Value;

        public Trailer(string name, string thumbnailUrl, string videoUrl, IImageProvider imageProvider)
        {
            Name = name;
            VideoUrl = videoUrl;
            _imageProvider = imageProvider;
            _lazyThumbnail = new Lazy<BitmapImage>(
                () => string.IsNullOrEmpty(thumbnailUrl)
                    ? null
                    : _imageProvider.LoadImageWithDecodeMaxDimensions(thumbnailUrl, 216, 216),
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public void InitializeThumbnail()
        {
            if (!_lazyThumbnail.IsValueCreated)
            {
                _ = _lazyThumbnail.Value;
            }
        }
    }
}
