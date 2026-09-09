using PluginsCommon;
using SteamScreenshots.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace SteamScreenshots.Domain.ValueObjects
{
    public class Screenshot : GalleryMediaItem
    {
        private const int StageImageWidth = 1280;
        private const int StageImageHeight = 720;
        private readonly string _thumbnailPath;
        private readonly string _fullImagePath;
        private readonly IImageProvider _imageProvider;

        private readonly object _thumbnailLock = new object();
        private readonly object _stageImageLock = new object();
        private readonly object _fullImageLock = new object();
        private BitmapImage _thumbnailImage;
        private BitmapImage _stageImage;
        private BitmapImage _fullImage;

        public override BitmapImage ThumbnailImage => GetThumbnail(CancellationToken.None);
        public BitmapImage FullImage => GetFullImage(CancellationToken.None);
        public override bool IsVideo => false;
        public override string Name => "Screenshot";

        public Screenshot(string thumbnailPath, string fullImagePath, IImageProvider imageProvider)
        {
            _thumbnailPath = thumbnailPath;
            _fullImagePath = fullImagePath;
            _imageProvider = imageProvider;
        }

        private BitmapImage GetThumbnail(CancellationToken cancellationToken)
        {
            if (_thumbnailImage != null)
            {
                return _thumbnailImage;
            }

            lock (_thumbnailLock)
            {
                if (_thumbnailImage == null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var source = !_thumbnailPath.IsNullOrEmpty() ? _thumbnailPath : _fullImagePath;
                    _thumbnailImage = _imageProvider.LoadImageWithDecodeMaxDimensions(
                        source,
                        216,
                        216,
                        cancellationToken);
                }

                return _thumbnailImage;
            }
        }

        public BitmapImage GetFullImage(CancellationToken cancellationToken)
        {
            if (_fullImage != null)
            {
                return _fullImage;
            }

            lock (_fullImageLock)
            {
                if (_fullImage == null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _fullImage = _imageProvider.LoadImage(_fullImagePath, cancellationToken);
                }

                return _fullImage;
            }
        }

        public BitmapImage GetStageImage(CancellationToken cancellationToken)
        {
            if (_stageImage != null)
            {
                return _stageImage;
            }

            lock (_stageImageLock)
            {
                if (_stageImage == null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _stageImage = _imageProvider.LoadTransientImageWithDecodeMaxDimensions(
                        _fullImagePath,
                        StageImageWidth,
                        StageImageHeight,
                        cancellationToken);
                }

                return _stageImage;
            }
        }

        public void InitializeThumbnail(CancellationToken cancellationToken = default)
        {
            _ = GetThumbnail(cancellationToken);
        }

        public void InitializeFullImage(CancellationToken cancellationToken = default)
        {
            _ = GetFullImage(cancellationToken);
        }

        public void InitializeStageImage(CancellationToken cancellationToken = default)
        {
            _ = GetStageImage(cancellationToken);
        }

        public void InitializeImages(CancellationToken cancellationToken = default)
        {
            InitializeThumbnail(cancellationToken);
            InitializeFullImage(cancellationToken);
        }

        public void ReleaseFullImage()
        {
            var released = false;
            lock (_fullImageLock)
            {
                released = _fullImage != null;
                _fullImage = null;
            }
            if (released)
            {
                GalleryBitmapMemory.RequestCollection();
            }
        }

        public void ReleaseStageImage()
        {
            var released = false;
            lock (_stageImageLock)
            {
                released = _stageImage != null;
                _stageImage = null;
            }
            if (released)
            {
                GalleryBitmapMemory.RequestCollection();
            }
        }

        public void ReleaseTransientImages()
        {
            var released = false;
            lock (_stageImageLock)
            {
                released = _stageImage != null;
                _stageImage = null;
            }
            lock (_fullImageLock)
            {
                released |= _fullImage != null;
                _fullImage = null;
            }
            if (released)
            {
                GalleryBitmapMemory.RequestCollection();
            }
        }
    }

    internal static class GalleryBitmapMemory
    {
        private static int _releasedImages;
        private static int _collectionScheduled;

        public static void RequestCollection()
        {
            if (Environment.Is64BitProcess)
            {
                return;
            }

            Interlocked.Increment(ref _releasedImages);
            ScheduleCollection();
        }

        private static void ScheduleCollection()
        {
            if (Interlocked.CompareExchange(ref _collectionScheduled, 1, 0) != 0)
            {
                return;
            }

            Task.Run(async () =>
            {
                await Task.Delay(400).ConfigureAwait(false);
                Interlocked.Exchange(ref _releasedImages, 0);

                // BitmapImage pixel buffers are predominantly native WIC/WPF
                // allocations. The x86 GC cannot see their size, so it otherwise
                // waits far too long before finalizing released image wrappers.
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false);

                Interlocked.Exchange(ref _collectionScheduled, 0);
                if (Volatile.Read(ref _releasedImages) > 0)
                {
                    ScheduleCollection();
                }
            });
        }
    }

}
