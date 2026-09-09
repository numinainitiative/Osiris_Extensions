using FlowHttp;
using Playnite.SDK;
using PluginsCommon;
using PluginsCommon.Converters;
using SteamScreenshots.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SteamScreenshots.Infrastructure.Providers
{
    public class UrlImageProvider : IImageProvider
    {
        private const int MaximumCachedImages = 24;
        private const long MaximumCachedImageBytes = 8L * 1024L * 1024L;
        private const long MemoryPressurePrivateBytes32Bit = 576L * 1024L * 1024L;
        private const int NormalFullImageWidth = 1920;
        private const int NormalFullImageHeight = 1080;
        private const int ReducedFullImageWidth = 1280;
        private const int ReducedFullImageHeight = 720;
        private readonly string _storageDirectory;
        private readonly ILogger _logger;
        private static readonly BoundedBitmapCache _imagesCacheManager =
            new BoundedBitmapCache(MaximumCachedImages, MaximumCachedImageBytes, TimeSpan.FromSeconds(45));
        private static readonly SemaphoreSlim[] DownloadGates = CreateDownloadGates();
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
        private static readonly BitmapImage _fallbackImage = CreateTransparentFallbackImage();

        public UrlImageProvider(string storageDirectory, ILogger logger)
        {
            _storageDirectory = storageDirectory ?? throw new ArgumentNullException(nameof(storageDirectory), "Storage directory cannot be null.");
            _logger = logger;
        }

        public bool DownloadUriToStorage(string url, CancellationToken cancellationToken = default)
        {
            var storagePath = GetUriStorageLocation(url);
            var gate = GetDownloadGate(storagePath);
            gate.Wait(cancellationToken);
            try
            {
                if (FileSystem.FileExists(storagePath) && new FileInfo(storagePath).Length > 0)
                {
                    return true;
                }

                for (var attempt = 0; attempt < 3; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var tempStoragePath = storagePath + "." + Guid.NewGuid().ToString("N") + ".download";
                    try
                    {
                        var request = HttpRequestFactory.GetHttpFileRequest()
                            .WithUrl(url)
                            .WithDownloadTo(tempStoragePath)
                            .WithTimeout(DefaultTimeout);

                        var result = request.DownloadFile(cancellationToken);
                        if (result.IsSuccess && HasSupportedImageSignature(tempStoragePath))
                        {
                            FileSystem.DeleteFileSafe(storagePath);
                            FileSystem.MoveFile(tempStoragePath, storagePath);
                            return true;
                        }
                    }
                    finally
                    {
                        FileSystem.DeleteFileSafe(tempStoragePath);
                        FileSystem.DeleteFileSafe(tempStoragePath + ".tmp");
                    }

                    if (attempt < 2 && cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(250 * (attempt + 1))))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }

                return false;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Error downloading file: {url}");
                return false;
            }
            finally
            {
                gate.Release();
            }
        }

        public BitmapImage LoadImage(string url, CancellationToken cancellationToken = default)
        {
            var underMemoryPressure = IsUnderMemoryPressure();
            if (underMemoryPressure)
            {
                _imagesCacheManager.Clear();
            }
            return LoadImageInternal(
                url,
                underMemoryPressure ? ReducedFullImageWidth : NormalFullImageWidth,
                underMemoryPressure ? ReducedFullImageHeight : NormalFullImageHeight,
                cancellationToken,
                false);
        }

        public BitmapImage LoadImageWithDecodeMaxDimensions(
            string url,
            int decodeMaxWidth = 0,
            int decodeMaxHeight = 0,
            CancellationToken cancellationToken = default)
        {
            if (IsUnderMemoryPressure())
            {
                _imagesCacheManager.Clear();
            }
            return LoadImageInternal(url, decodeMaxWidth, decodeMaxHeight, cancellationToken, true);
        }

        public BitmapImage LoadTransientImageWithDecodeMaxDimensions(
            string url,
            int decodeMaxWidth = 0,
            int decodeMaxHeight = 0,
            CancellationToken cancellationToken = default)
        {
            if (IsUnderMemoryPressure())
            {
                _imagesCacheManager.Clear();
            }
            return LoadImageInternal(url, decodeMaxWidth, decodeMaxHeight, cancellationToken, false);
        }

        private BitmapImage LoadImageInternal(
            string url,
            int decodeMaxWidth,
            int decodeMaxHeight,
            CancellationToken cancellationToken,
            bool cacheDecodedImage)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.IsNullOrEmpty(url) && File.Exists(url))
                {
                    return CreateResizedBitmapImageFromPath(
                        url,
                        decodeMaxWidth,
                        decodeMaxHeight,
                        cancellationToken,
                        false) ?? GetFallbackImage();
                }

                var fileName = GetUriStorageFilename(url);
                var storagePath = GetFilenameStorageLocation(fileName);

                var bitmapImage = LoadImageFromStorage(
                    url,
                    storagePath,
                    decodeMaxWidth,
                    decodeMaxHeight,
                    cancellationToken,
                    cacheDecodedImage);
                return bitmapImage;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, $"Error loading image {url}");
                return GetFallbackImage();
            }
        }

        private BitmapImage LoadImageFromStorage(
            string url,
            string storagePath,
            int decodeMaxWidth,
            int decodeMaxHeight,
            CancellationToken cancellationToken,
            bool cacheDecodedImage)
        {
            var key = $"{storagePath}_{decodeMaxWidth}_{decodeMaxHeight}";
            if (cacheDecodedImage && _imagesCacheManager.TryGetValue(key, out var cachedImage))
            {
                return cachedImage;
            }

            var shouldDownloadImage = true;
            if (FileSystem.FileExists(storagePath))
            {
                shouldDownloadImage = false;
                var fileSize = new FileInfo(storagePath).Length;
                if (fileSize == 0) // There were reports of images downloads being incomplete and stored so we delete those
                {
                    FileSystem.DeleteFileSafe(storagePath);
                    shouldDownloadImage = true;
                }
            }

            if (shouldDownloadImage)
            {
                var success = DownloadUriToStorage(url, cancellationToken);
                if (!success)
                {
                    return GetFallbackImage();
                }
            }

            var bitmapImage = CreateResizedBitmapImageFromPath(
                storagePath,
                decodeMaxWidth,
                decodeMaxHeight,
                cancellationToken,
                true);
            if (bitmapImage == null)
            {
                // A non-empty cached response can still be truncated or HTML. Delete it
                // and retry once in this same view rather than leaving a missing tile.
                FileSystem.DeleteFileSafe(storagePath);
                if (!DownloadUriToStorage(url, cancellationToken))
                {
                    return GetFallbackImage();
                }

                bitmapImage = CreateResizedBitmapImageFromPath(
                    storagePath,
                    decodeMaxWidth,
                    decodeMaxHeight,
                    cancellationToken,
                    true);
            }
            if (bitmapImage == null)
            {
                return GetFallbackImage();
            }
            if (cacheDecodedImage)
            {
                _imagesCacheManager.Add(key, bitmapImage);
            }
            return bitmapImage;
        }

        private static bool IsUnderMemoryPressure()
        {
            if (Environment.Is64BitProcess)
            {
                return false;
            }

            try
            {
                using (var process = System.Diagnostics.Process.GetCurrentProcess())
                {
                    return process.PrivateMemorySize64 >= MemoryPressurePrivateBytes32Bit;
                }
            }
            catch
            {
                return true;
            }
        }

        private static BitmapImage CreateResizedBitmapImageFromPath(
            string filePath,
            int decodeMaxWidth,
            int decodeMaxHeight,
            CancellationToken cancellationToken,
            bool deleteInvalidFile)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!FileSystem.FileExists(filePath))
                {
                    throw new FileNotFoundException("File does not exist at path.", filePath);
                }

                using (var fileStream = FileSystem.OpenReadFileStreamSafe(filePath))
                {
                    return GetBitmapImageFromBufferedStream(
                        fileStream,
                        decodeMaxWidth,
                        decodeMaxHeight,
                        cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating BitmapImage from path: {filePath} - {ex.Message}");
                if (deleteInvalidFile)
                {
                    FileSystem.DeleteFileSafe(filePath);
                }
                return null;
            }
        }

        private static BitmapImage GetBitmapImageFromBufferedStream(
            Stream stream,
            int decodeMaxWidth,
            int decodeMaxHeight,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;

            if (decodeMaxWidth != 0 || decodeMaxHeight != 0)
            {
                (int originalWidth, int originalHeight) = GetImageDimensions(stream);

                int newWidth = decodeMaxWidth;
                int newHeight = decodeMaxHeight;

                // Case 1: Only decodeMaxHeight is provided
                if (decodeMaxWidth == 0 && decodeMaxHeight != 0)
                {
                    newWidth = (int)(originalWidth * ((double)decodeMaxHeight / originalHeight));
                }
                // Case 2: Only decodeMaxWidth is provided
                else if (decodeMaxHeight == 0 && decodeMaxWidth != 0)
                {
                    newHeight = (int)(originalHeight * ((double)decodeMaxWidth / originalWidth));
                }
                // Case 3: Both values are provided, resize while preserving aspect ratio
                else if (decodeMaxWidth != 0 && decodeMaxHeight != 0)
                {
                    double widthScale = (double)decodeMaxWidth / originalWidth;
                    double heightScale = (double)decodeMaxHeight / originalHeight;

                    double scaleFactor = Math.Min(widthScale, heightScale);

                    newWidth = (int)(originalWidth * scaleFactor);
                    newHeight = (int)(originalHeight * scaleFactor);
                }

                bitmapImage.DecodePixelWidth = newWidth;
                bitmapImage.DecodePixelHeight = newHeight;
            }

            stream.Seek(0, SeekOrigin.Begin);
            cancellationToken.ThrowIfCancellationRequested();
            bitmapImage.StreamSource = stream;
            bitmapImage.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            return bitmapImage;
        }

        private static SemaphoreSlim[] CreateDownloadGates()
        {
            var gates = new SemaphoreSlim[16];
            for (var index = 0; index < gates.Length; index++)
            {
                gates[index] = new SemaphoreSlim(1, 1);
            }

            return gates;
        }

        private static SemaphoreSlim GetDownloadGate(string path)
        {
            var hash = StringComparer.OrdinalIgnoreCase.GetHashCode(path ?? string.Empty) & int.MaxValue;
            return DownloadGates[hash % DownloadGates.Length];
        }

        private static bool HasSupportedImageSignature(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length < 12)
                    {
                        return false;
                    }

                    var header = new byte[12];
                    if (stream.Read(header, 0, header.Length) != header.Length)
                    {
                        return false;
                    }

                    return
                        (header[0] == 0xFF && header[1] == 0xD8) ||
                        (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47) ||
                        (header[0] == (byte)'G' && header[1] == (byte)'I' && header[2] == (byte)'F') ||
                        (header[0] == (byte)'B' && header[1] == (byte)'M') ||
                        (header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' &&
                         header[3] == (byte)'F' && header[8] == (byte)'W' && header[9] == (byte)'E' &&
                         header[10] == (byte)'B' && header[11] == (byte)'P');
                }
            }
            catch
            {
                return false;
            }
        }


        private static (int width, int height) GetImageDimensions(Stream stream)
        {
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
            return (decoder.Frames[0].PixelWidth, decoder.Frames[0].PixelHeight);
        }

        private static BitmapImage GetFallbackImage()
        {
            return _fallbackImage;
        }

        private string GetUriStorageLocation(string url)
        {
            var fileName = GetUriStorageFilename(url);
            return GetFilenameStorageLocation(fileName);
        }

        private string GetUriStorageFilename(string url)
        {
            if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
            {
                var fileName = Path.GetFileName(uri.LocalPath);
                if (fileName.StartsWith("movie", StringComparison.OrdinalIgnoreCase))
                {
                    var segments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    var appsIndex = Array.FindIndex(
                        segments,
                        segment => string.Equals(segment, "apps", StringComparison.OrdinalIgnoreCase));
                    if (appsIndex >= 0 && appsIndex + 1 < segments.Length)
                    {
                        fileName = segments[appsIndex + 1] + "_" + fileName;
                    }
                }

                return Paths.ReplaceInvalidCharacters(fileName);
            }

            throw new UriFormatException("Invalid URL format.");
        }

        private string GetFilenameStorageLocation(string fileName)
        {
            return Path.Combine(_storageDirectory, Paths.GetSafePathName(fileName));
        }

        private static BitmapImage CreateTransparentFallbackImage()
        {
            var width = 160;
            var height = 90;
            double dpiX = 96;
            double dpiY = 96;

            var writeableBitmap = new WriteableBitmap(width, height, dpiX, dpiY, PixelFormats.Pbgra32, null);
            var pixels = new int[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = 0x00000000;
            }

            writeableBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * sizeof(int), 0);
            return WriteableBitmapToBitmapImage(writeableBitmap);
        }

        private static BitmapImage WriteableBitmapToBitmapImage(WriteableBitmap writeableBitmap)
        {
            var bitmapImage = new BitmapImage();
            using (var memoryStream = new MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(writeableBitmap));

                encoder.Save(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);

                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
            }

            return bitmapImage;
        }

        private sealed class BoundedBitmapCache
        {
            private readonly object _syncRoot = new object();
            private readonly Dictionary<string, CacheEntry> _entries =
                new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
            private readonly LinkedList<string> _leastRecentlyUsed = new LinkedList<string>();
            private readonly int _maximumItems;
            private readonly long _maximumBytes;
            private readonly TimeSpan _entryLifetime;
            private long _cachedBytes;

            public BoundedBitmapCache(int maximumItems, long maximumBytes, TimeSpan entryLifetime)
            {
                _maximumItems = maximumItems;
                _maximumBytes = maximumBytes;
                _entryLifetime = entryLifetime;
            }

            public bool TryGetValue(string key, out BitmapImage image)
            {
                lock (_syncRoot)
                {
                    if (!_entries.TryGetValue(key, out var entry))
                    {
                        image = null;
                        return false;
                    }

                    if (DateTime.UtcNow - entry.LastAccessUtc > _entryLifetime)
                    {
                        RemoveEntry(entry);
                        image = null;
                        return false;
                    }

                    entry.LastAccessUtc = DateTime.UtcNow;
                    _leastRecentlyUsed.Remove(entry.Node);
                    _leastRecentlyUsed.AddLast(entry.Node);
                    image = entry.Image;
                    return true;
                }
            }

            public void Add(string key, BitmapImage image)
            {
                if (image == null)
                {
                    return;
                }

                var imageBytes = EstimateImageBytes(image);
                if (imageBytes <= 0 || imageBytes > _maximumBytes)
                {
                    return;
                }

                lock (_syncRoot)
                {
                    if (_entries.TryGetValue(key, out var existingEntry))
                    {
                        RemoveEntry(existingEntry);
                    }

                    var node = new LinkedListNode<string>(key);
                    var entry = new CacheEntry(image, imageBytes, DateTime.UtcNow, node);
                    _entries.Add(key, entry);
                    _leastRecentlyUsed.AddLast(node);
                    _cachedBytes += imageBytes;

                    while (_entries.Count > _maximumItems || _cachedBytes > _maximumBytes)
                    {
                        var oldestNode = _leastRecentlyUsed.First;
                        if (oldestNode == null || !_entries.TryGetValue(oldestNode.Value, out var oldestEntry))
                        {
                            break;
                        }

                        RemoveEntry(oldestEntry);
                    }
                }
            }

            public void Clear()
            {
                lock (_syncRoot)
                {
                    _entries.Clear();
                    _leastRecentlyUsed.Clear();
                    _cachedBytes = 0;
                }
            }

            private void RemoveEntry(CacheEntry entry)
            {
                _entries.Remove(entry.Node.Value);
                _leastRecentlyUsed.Remove(entry.Node);
                _cachedBytes -= entry.Bytes;
            }

            private static long EstimateImageBytes(BitmapImage image)
            {
                try
                {
                    return checked((long)image.PixelWidth * image.PixelHeight * 4L);
                }
                catch (OverflowException)
                {
                    return long.MaxValue;
                }
            }

            private sealed class CacheEntry
            {
                public CacheEntry(
                    BitmapImage image,
                    long bytes,
                    DateTime lastAccessUtc,
                    LinkedListNode<string> node)
                {
                    Image = image;
                    Bytes = bytes;
                    LastAccessUtc = lastAccessUtc;
                    Node = node;
                }

                public BitmapImage Image { get; }
                public long Bytes { get; }
                public DateTime LastAccessUtc { get; set; }
                public LinkedListNode<string> Node { get; }
            }
        }


    }
}
