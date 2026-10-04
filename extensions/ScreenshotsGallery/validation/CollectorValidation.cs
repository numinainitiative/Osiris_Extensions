using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Osiris.Extensions.ScreenshotsGallery;

internal static class CollectorValidation
{
    private static int checks;
    [STAThread]
    private static void Main(string[] args)
    {
        var root = Path.GetFullPath(args[0]);
        if (Directory.Exists(root)) throw new Exception("Use a fresh isolated sandbox.");
        var input = Path.Combine(root, "WindowsScreenshots");
        var profile = Path.Combine(root, "Data");
        Directory.CreateDirectory(input);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var warnings = 0;
        var old = Path.Combine(input, "old.png");
        Save(old);
        using (var collector = new ScreenshotCollector(profile, () => input, _ => warnings++))
        {
            collector.Start(first);
            collector.Poll(); collector.Poll();
            Check(Count(profile, first) == 0, "old screenshots ignored");
            var fresh = Path.Combine(input, "new.png");
            Save(fresh);
            var original = File.ReadAllBytes(fresh);
            collector.Poll(); collector.Poll();
            Check(Count(profile, first) == 1, "new screenshot routed to running game");
            var saved = Directory.GetFiles(ScreenshotCollector.GameFolder(profile, first)).Single();
            Check(File.ReadAllBytes(saved).SequenceEqual(original), "copy byte-identical");
            Check(File.Exists(fresh), "Windows original preserved");
            collector.Poll(); collector.Poll();
            Check(Count(profile, first) == 1, "duplicate polls do not duplicate copy");
            var locked = Path.Combine(input, "locked.png");
            Save(locked);
            using (var stream = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                collector.Poll(); collector.Poll();
                Check(Count(profile, first) == 1, "incomplete/locked write deferred");
            }
            collector.Poll(); collector.Poll();
            Check(Count(profile, first) == 2, "locked write retried successfully");
            collector.Start(second);
            Save(Path.Combine(input, "ambiguous.png"));
            collector.Poll(); collector.Poll();
            Check(Count(profile, first) == 2 && Count(profile, second) == 0, "multiple games never guessed");
            collector.Stop(first);
            Save(Path.Combine(input, "second.png"));
            collector.Poll(); collector.Poll();
            Check(Count(profile, second) == 1, "remaining game receives its own screenshot");
            Save(Path.Combine(input, "just-before-stop.png"));
            collector.Stop(second);
            Check(Count(profile, second) == 2, "complete screenshot just before stop is flushed");
            Save(Path.Combine(input, "outside-session.png"));
            collector.Poll(); collector.Poll();
            Check(Count(profile, second) == 2, "screenshots outside sessions ignored");
            collector.Start(second);
            collector.Poll(); collector.Poll();
            Check(Count(profile, second) == 2, "new session does not backfill");
            var settings = Path.Combine(profile, "library", "files", second.ToString("D"), "OsirisScreenshotsGallery.ini");
            File.WriteAllText(settings, "Enabled=False\r\n");
            Check(!ScreenshotCollector.IsEnabled(profile, second), "per-game disable persisted");
            Save(Path.Combine(input, "disabled.png"));
            collector.Poll(); collector.Poll();
            Check(Count(profile, second) == 2, "disabled game does not collect screenshots");
            File.WriteAllText(settings, "Enabled=True\r\n");
            collector.Poll(); collector.Poll();
            Check(Count(profile, second) == 2, "enable does not backfill disabled-period captures");
            Save(Path.Combine(input, "enabled-again.png"));
            collector.Poll(); collector.Poll();
            Check(Count(profile, second) == 3, "reenabled game collects new screenshots");
            collector.Stop(second);
        }
        Check(warnings == 0, "expected locked-file retries do not emit failures");
        Console.WriteLine("PASS " + checks + " collector checks; synthetic PNGs only, no screen capture.");
    }

    private static int Count(string profile, Guid id)
    {
        var folder = ScreenshotCollector.GameFolder(profile, id);
        return Directory.Exists(folder) ? Directory.GetFiles(folder, "*.png").Length : 0;
    }

    private static void Save(string path)
    {
        var image = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null,
            new byte[] { 0, 0, 255, 255, 0, 255, 0, 255, 255, 0, 0, 255, 255, 255, 255, 255 }, 8);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using (var output = File.Create(path)) encoder.Save(output);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL " + message);
        checks++;
    }
}
