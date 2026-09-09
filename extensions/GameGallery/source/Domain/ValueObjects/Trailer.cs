using SteamScreenshots.Domain.Interfaces;
using System;
using System.Threading;
using System.Windows.Media.Imaging;

namespace SteamScreenshots.Domain.ValueObjects
{
    public class Trailer : GalleryMediaItem
    {
        private readonly IImageProvider _imageProvider;
        private readonly object _thumbnailLock = new object();
        private BitmapImage _thumbnailImage;

        public override string Name { get; }
        public string VideoUrl { get; }
        public override bool IsVideo => true;
        public override BitmapImage ThumbnailImage => GetThumbnail(CancellationToken.None);

        public Trailer(string name, string thumbnailUrl, string videoUrl, IImageProvider imageProvider)
        {
            Name = name;
            VideoUrl = videoUrl;
            _imageProvider = imageProvider;
            _thumbnailUrl = thumbnailUrl;
        }

        private readonly string _thumbnailUrl;

        private BitmapImage GetThumbnail(CancellationToken cancellationToken)
        {
            if (_thumbnailImage != null || string.IsNullOrEmpty(_thumbnailUrl))
            {
                return _thumbnailImage;
            }

            lock (_thumbnailLock)
            {
                if (_thumbnailImage == null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _thumbnailImage = _imageProvider.LoadImageWithDecodeMaxDimensions(
                        _thumbnailUrl,
                        216,
                        216,
                        cancellationToken);
                }

                return _thumbnailImage;
            }
        }

        public void InitializeThumbnail(CancellationToken cancellationToken = default)
        {
            _ = GetThumbnail(cancellationToken);
        }
    }
}
