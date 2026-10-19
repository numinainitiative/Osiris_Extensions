using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Media.Imaging;

namespace Osiris.Extensions.ScreenshotsGallery
{
    internal sealed class ScreenshotCollector : IDisposable
    {
        private readonly object gate = new object();
        private readonly string profile;
        private readonly Func<string> resolveSource;
        private readonly Action<string> warn;
        private readonly HashSet<Guid> games = new HashSet<Guid>();
        private readonly Dictionary<string, string> known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> pending = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private Timer timer;
        private string source;
        private bool disposed;
        private bool warned;

        internal ScreenshotCollector(string profile, Func<string> resolveSource, Action<string> warn)
        {
            this.profile = Path.GetFullPath(profile);
            this.resolveSource = resolveSource;
            this.warn = warn;
        }

        internal static string GameFolder(string profile, Guid gameId) =>
            Path.Combine(profile, "library", "files", gameId.ToString("D"), "Screenshots");

        internal static bool IsEnabled(string profile, Guid gameId)
        {
            var path = Path.Combine(profile, "library", "files", gameId.ToString("D"), "OsirisScreenshotsGallery.ini");
            return !File.Exists(path) || !File.ReadAllLines(path).Any(line =>
                string.Equals(line.Trim(), "Enabled=False", StringComparison.OrdinalIgnoreCase));
        }

        internal void Start(Guid gameId)
        {
            lock (gate)
            {
                if (disposed || gameId == Guid.Empty || !games.Add(gameId)) return;
                source = resolveSource();
                Snapshot();
                if (timer == null) timer = new Timer(_ => Poll(), null, 500, 500);
            }
        }

        internal void Stop(Guid gameId)
        {
            lock (gate)
            {
                // Flush files already complete before ending their session.
                Poll(true);
                games.Remove(gameId);
                Snapshot();
                if (games.Count == 0) { timer?.Dispose(); timer = null; }
            }
        }

        private IEnumerable<FileInfo> Files()
        {
            return !string.IsNullOrWhiteSpace(source) && Directory.Exists(source)
                ? new DirectoryInfo(source).EnumerateFiles("*.png", SearchOption.TopDirectoryOnly)
                : Enumerable.Empty<FileInfo>();
        }

        private static string Stamp(FileInfo file) => file.Length + ":" + file.LastWriteTimeUtc.Ticks;
        // Explorer may reset creation on a copy; retain the older preserved timestamp.
        internal static DateTime CaptureTimeUtc(FileInfo file) =>
            file.CreationTimeUtc < file.LastWriteTimeUtc ? file.CreationTimeUtc : file.LastWriteTimeUtc;
        private static string Hash(Stream stream)
        {
            using (var algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "");
        }

        private void Snapshot()
        {
            known.Clear();
            pending.Clear();
            try { foreach (var file in Files()) known[file.FullName] = Stamp(file); }
            catch (Exception error) { Report(error); }
        }

        internal void Poll(bool flushCompleteFiles = false)
        {
            lock (gate)
            {
                if (disposed || games.Count == 0) return;
                try
                {
                    foreach (var file in Files())
                    {
                        var stamp = Stamp(file);
                        if (known.TryGetValue(file.FullName, out var saved) && saved == stamp) continue;
                        if (games.Count != 1)
                        {
                            // Never guess which of two running games owns an image.
                            known[file.FullName] = stamp;
                            continue;
                        }
                        if (!IsEnabled(profile, games.Single()))
                        {
                            known[file.FullName] = stamp;
                            continue;
                        }
                        if (!flushCompleteFiles &&
                            (!pending.TryGetValue(file.FullName, out var previous) || previous != stamp))
                        {
                            pending[file.FullName] = stamp;
                            continue;
                        }
                        try
                        {
                            using (var input = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
                            {
                                // Validate a complete image; partial Windows writes are retried.
                                var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                                if (decoder.Frames.Count == 0) continue;
                                input.Position = 0;
                                var hash = Hash(input);
                                input.Position = 0;
                                var folder = GameFolder(profile, games.Single());
                                Directory.CreateDirectory(folder);
                                // Stable identity avoids duplicate copies if source deletion must retry.
                                var destination = Path.Combine(folder, "Screenshot-" +
                                    CaptureTimeUtc(file).ToString("yyyyMMdd-HHmmssfff") + "-" + hash.Substring(0, 32) + ".png");
                                if (!File.Exists(destination))
                                using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                                {
                                    try { input.CopyTo(output); }
                                    catch { output.Dispose(); File.Delete(destination); throw; }
                                }
                                using (var savedImage = File.OpenRead(destination))
                                    if (Hash(savedImage) != hash) throw new IOException("Screenshot destination verification failed; original retained.");
                                File.SetCreationTimeUtc(destination, file.CreationTimeUtc);
                                File.SetLastWriteTimeUtc(destination, file.LastWriteTimeUtc);
                                // Source writers remain excluded by the open read handle. Never delete
                                // before the complete destination is closed and byte-verified.
                                File.Delete(file.FullName);
                                known[file.FullName] = stamp;
                                pending.Remove(file.FullName);
                            }
                        }
                        catch (IOException) { } // File is still being written or temporarily locked.
                        catch (FileFormatException) { } // Wait for a complete PNG.
                        catch (Exception error) { Report(error); }
                    }
                }
                catch (Exception error) { Report(error); }
            }
        }

        private void Report(Exception error)
        {
            if (warned) return;
            warned = true;
            warn?.Invoke("Screenshots Gallery could not collect a Windows screenshot: " + error.Message);
        }

        public void Dispose()
        {
            lock (gate) { disposed = true; timer?.Dispose(); timer = null; games.Clear(); }
        }
    }
}
