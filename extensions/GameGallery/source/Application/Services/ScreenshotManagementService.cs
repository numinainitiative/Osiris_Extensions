using Playnite.SDK;
using SteamScreenshots.Domain.Enums;
using SteamScreenshots.Domain.Interfaces;
using SteamScreenshots.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SteamScreenshots.Application.Services
{
    public class ScreenshotManagementService : IDisposable
    {
        private bool _disposed = false;
        private readonly IDictionary<ScreenshotServiceType, IScreenshotProvider> _providers;
        private readonly IImageProvider _imageProvider;
        private readonly ILogger _logger;
        private readonly string _pluginDataPath;
        private readonly string _pluginInstallPath;
        private SemaphoreSlim _semaphore;

        public ScreenshotManagementService(
            IDictionary<ScreenshotServiceType, IScreenshotProvider> providers,
            IImageProvider imageProvider,
            ILogger logger,
            string pluginDataPath,
            string pluginInstallPath)
        {
            _providers = providers;
            _imageProvider = imageProvider;
            _logger = logger;
            _pluginDataPath = pluginDataPath;
            _pluginInstallPath = pluginInstallPath;
            _semaphore = new SemaphoreSlim(4);
        }

        public async Task<List<Screenshot>> GetScreenshots(ScreenshotServiceType serviceType,
            string id,
            ScreenshotInitializationOptions screenshotInitializationOptions,
            CancellationToken cancellationToken = default)
        {
            if (!_providers.ContainsKey(serviceType))
            {
                throw new NotSupportedException($"Screenshot service {serviceType} is not supported.");
            }

            var screenshotsData = _providers[serviceType].GetScreenshots(id, cancellationToken);
            if (!screenshotsData.HasItems())
            {
                return new List<Screenshot>();
            }

            var screenshots = screenshotsData
                .Select(s => new Screenshot(s.ThumbnailUrl, s.FullImageUrl, _imageProvider))
                .ToList();

            var initializeTasks = new List<Task>();
            foreach (var screenshot in screenshots)
            {
                if (!screenshotInitializationOptions.LazyLoadThumbnail)
                {
                    initializeTasks.Add(InitializeWithSemaphoreAsync(screenshot.InitializeThumbnail, _semaphore));
                }

                if (!screenshotInitializationOptions.LazyLoadFullImage)
                {
                    initializeTasks.Add(InitializeWithSemaphoreAsync(screenshot.InitializeFullImage, _semaphore));
                }
            }

            if (initializeTasks.Any())
            {
                // Initialize at least the first image so it's not loaded synchronously when first displayed
                if (screenshotInitializationOptions.LazyLoadFullImage)
                {
                    initializeTasks.Add(InitializeWithSemaphoreAsync(screenshots[0].InitializeFullImage, _semaphore));
                }

                await Task.WhenAll(initializeTasks);
            }

            return screenshots;
        }

        public async Task<List<Trailer>> GetTrailers(
            ScreenshotServiceType serviceType,
            string id,
            CancellationToken cancellationToken = default)
        {
            if (!_providers.ContainsKey(serviceType))
            {
                throw new NotSupportedException($"Screenshot service {serviceType} is not supported.");
            }

            var trailerData = _providers[serviceType].GetTrailers(id, cancellationToken);
            var trailers = trailerData
                .Select(item => new Trailer(item.Name, item.ThumbnailUrl, item.VideoUrl, _imageProvider))
                .ToList();
            await Task.WhenAll(trailers.Select(trailer =>
                InitializeWithSemaphoreAsync(trailer.InitializeThumbnail, _semaphore)));
            return trailers;
        }

        public Screenshot CreateLocalScreenshot(string path)
        {
            return new Screenshot(path, path, _imageProvider);
        }

        public Trailer CreateLocalTrailer(string path)
        {
            return new Trailer(
                Path.GetFileNameWithoutExtension(path),
                GetLocalTrailerThumbnailPath(path),
                new Uri(path).AbsoluteUri,
                _imageProvider);
        }

        private string GetLocalTrailerThumbnailPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            var ffmpeg = FindGalleryFfmpeg();
            if (string.IsNullOrWhiteSpace(ffmpeg) || !File.Exists(ffmpeg))
            {
                return null;
            }

            var sourceInfo = new FileInfo(path);
            var cacheRoot = GetLocalGalleryVideoThumbnailCacheRoot();
            if (string.IsNullOrWhiteSpace(cacheRoot))
            {
                return null;
            }

            Directory.CreateDirectory(cacheRoot);

            string cacheName;
            using (var sha = SHA256.Create())
            {
                var key = "v2|" + path + "|" + sourceInfo.Length.ToString(CultureInfo.InvariantCulture) + "|" +
                    sourceInfo.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture);
                cacheName = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(key)))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }

            var finalPath = Path.Combine(cacheRoot, cacheName + ".jpg");
            if (File.Exists(finalPath) && new FileInfo(finalPath).Length > 0)
            {
                return finalPath;
            }

            var temporaryPath = finalPath + "." + Guid.NewGuid().ToString("N") + ".tmp.jpg";
            TryDeleteLocalGalleryVideoThumbnailFile(temporaryPath);
            var candidateTimes = new[] { "00:00:08", "00:00:15", "00:00:25", "00:00:35", "00:00:05", "00:00:01" };
            string bestCandidate = null;
            var bestCandidateLength = 0L;

            foreach (var candidateTime in candidateTimes)
            {
                var candidatePath = temporaryPath + "." + candidateTime.Replace(":", string.Empty) + ".jpg";
                TryDeleteLocalGalleryVideoThumbnailFile(candidatePath);
                var arguments =
                    "-nostdin -hide_banner -loglevel error -y -ss " + candidateTime +
                    " -i " + QuoteLocalGalleryProcessArgument(path) +
                    " -frames:v 1 -vf " + QuoteLocalGalleryProcessArgument("scale=320:-2") + " " +
                    QuoteLocalGalleryProcessArgument(candidatePath);

                using (var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpeg,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    }
                })
                {
                    process.Start();
                    if (!process.WaitForExit(15000))
                    {
                        try { process.Kill(); } catch { }
                        TryDeleteLocalGalleryVideoThumbnailFile(candidatePath);
                        continue;
                    }

                    if (process.ExitCode != 0 || !File.Exists(candidatePath))
                    {
                        TryDeleteLocalGalleryVideoThumbnailFile(candidatePath);
                        continue;
                    }
                }

                var candidateLength = new FileInfo(candidatePath).Length;
                if (candidateLength <= 0)
                {
                    TryDeleteLocalGalleryVideoThumbnailFile(candidatePath);
                    continue;
                }

                if (candidateLength > bestCandidateLength)
                {
                    if (!string.IsNullOrEmpty(bestCandidate))
                    {
                        TryDeleteLocalGalleryVideoThumbnailFile(bestCandidate);
                    }

                    bestCandidate = candidatePath;
                    bestCandidateLength = candidateLength;
                }
                else
                {
                    TryDeleteLocalGalleryVideoThumbnailFile(candidatePath);
                }
            }

            if (string.IsNullOrEmpty(bestCandidate) || !File.Exists(bestCandidate))
            {
                return null;
            }

            File.Move(bestCandidate, temporaryPath);
            TryDeleteLocalGalleryVideoThumbnailFile(finalPath);
            File.Move(temporaryPath, finalPath);
            return finalPath;
        }

        private string FindGalleryFfmpeg()
        {
            var candidates = new[]
            {
                Path.Combine(_pluginDataPath ?? string.Empty, "Tools", "ffmpeg.exe"),
                Path.Combine(_pluginInstallPath ?? string.Empty, "Tools", "ffmpeg.exe"),
                Path.Combine(_pluginInstallPath ?? string.Empty, "ffmpeg.exe")
            };
            var bundled = candidates.FirstOrDefault(File.Exists);
            if (!string.IsNullOrWhiteSpace(bundled))
            {
                return bundled;
            }

            var environmentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var directory in environmentPath.Split(Path.PathSeparator))
            {
                try
                {
                    var candidate = Path.Combine(directory.Trim(), "ffmpeg.exe");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private string GetLocalGalleryVideoThumbnailCacheRoot()
        {
            return string.IsNullOrWhiteSpace(_pluginDataPath)
                ? null
                : Path.Combine(_pluginDataPath, "Cache", "VideoThumbs");
        }

        private static string QuoteLocalGalleryProcessArgument(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static void TryDeleteLocalGalleryVideoThumbnailFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
            }
        }

        private async Task InitializeWithSemaphoreAsync(Action initializeFunc, SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync();
            try
            {
                await Task.Run(() => initializeFunc());
            }
            finally
            {
                semaphore.Release();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _semaphore.Dispose();
                _semaphore = null;
                _disposed = true;
            }
        }

    }

}
