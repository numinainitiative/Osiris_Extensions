using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Osiris.Extensions.ScreenshotsGallery;
using Playnite.SDK.Models;

internal static class PresentationValidation
{
    private static int checks;
    [STAThread]
    private static void Main()
    {
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            var path = Path.Combine(@"C:\Development\Osiris Launcher\Programming\Development\Osiris\App", new AssemblyName(args.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        Run();
    }
    private static void Run()
    {
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        var root = Path.Combine(Path.GetTempPath(), "OsirisScreenshotPresentation-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var settings = new ScreenshotsSettings();
            Check(settings.ExpandedExperience == "FullScreen", "Default full screen");
            settings.ExpandedExperience = "Cinematic";
            Check(settings.ExpandedExperience == "Cinematic", "Cinema selection");
            settings.ExpandedExperience = "invalid";
            Check(settings.ExpandedExperience == "FullScreen", "Invalid mode normalized");
            var game = new Game { Id = Guid.NewGuid() };
            var control = new ScreenshotsViewControl(root, () => settings.ExpandedExperience);
            var theme = Assembly.LoadFrom(@"C:\Development\Osiris Launcher\Development\Contents and Stuff\Development (AI)\Development\ThemeSource\Code\media-build\OsirisTheme.dll");
            var presentation = theme.GetType("OsirisTheme.SteamGalleryPresentation", true);
            var countConverter = (System.Windows.Data.IMultiValueConverter)Activator.CreateInstance(theme.GetType("OsirisTheme.GalleryScreenshotCountConverter", true));
            var countedMedia = new[] { new { IsVideo = false }, new { IsVideo = false }, new { IsVideo = false }, new { IsVideo = false }, new { IsVideo = false }, new { IsVideo = true } };
            Check((string)countConverter.Convert(new object[] { countedMedia, countedMedia.Length }, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture) == "5 Screenshots",
                "Header screenshot count excludes trailers");
            Check((string)countConverter.Convert(new object[] { null, 0 }, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture) == "0 Screenshots",
                "Empty header count safe");
            var hooked = (bool)presentation.GetMethod("TryApply", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { new ContentControl { Content = control } });
            Check(hooked && control.ConfigureExpandedWindow != null, "Shared Game Gallery viewer hook attached");
            var layout = (Grid)control.Content;
            Check(layout.ColumnDefinitions.Count == 3 && layout.ColumnDefinitions[1].Width.Value == 8 && layout.ColumnDefinitions[2].Width.Value == 190,
                "Exact Game Gallery rail width and gap");
            Check(((Border)layout.Children[0]).CornerRadius.TopLeft == 11, "Exact Game Gallery stage corners");
            Check(control.CreateExpandedCloseButton != null, "Shared Game Gallery close control");
            Check(control.CreateExpandedNavigationControls != null, "Shared expanded navigation factory");
            var navigation = (Grid)control.CreateExpandedNavigationControls(new
            {
                BackCommand = control.SelectPreviousScreenshotCommand, NextCommand = control.SelectNextScreenshotCommand,
                HasMultipleImages = true, ImagePositionLabel = "1 of 2"
            });
            var back = (Button)navigation.Children[0];
            var forward = (Button)navigation.Children[1];
            Check(back.Content is System.Windows.Shapes.Path && back.BorderThickness.Left == 0 && ((SolidColorBrush)back.Background).Color.A == 0,
                "Shared borderless chevron navigation");
            Check(back.HorizontalAlignment == HorizontalAlignment.Left && forward.HorizontalAlignment == HorizontalAlignment.Right &&
                back.VerticalAlignment == VerticalAlignment.Center && back.Width == 72 && back.Height == 72,
                "Exact card chevrons at opposite image sides");
            Pump(100);
            var position = (TextBlock)navigation.Children[2];
            Check(position.Text == "1 / 2" && position.FontSize == 19 && position.VerticalAlignment == VerticalAlignment.Bottom,
                "Slash counter three points larger at bottom center");
            var closeButton = control.CreateExpandedCloseButton();
            Check(closeButton.BorderThickness.Left == 0 && ((SolidColorBrush)closeButton.Background).Color.A == 0,
                "Background-free shared close");
            var settingsView = new ScreenshotsSettingsView { DataContext = new { Settings = settings } };
            var tab = (TabControl)settingsView.Content;
            var form = (StackPanel)((TabItem)tab.Items[0]).Content;
            var combo = (ComboBox)((Grid)form.Children[0]).Children[1];
            Pump(100);
            combo.GetBindingExpression(System.Windows.Controls.Primitives.Selector.SelectedValueProperty).UpdateTarget();
            combo.SelectedValue = "Cinematic";
            combo.GetBindingExpression(System.Windows.Controls.Primitives.Selector.SelectedValueProperty).UpdateSource();
            Check(settings.ExpandedExperience == "Cinematic" && control.UseCinematicExpandedExperience, "Settings UI drives actual viewer mode");
            var json = Assembly.LoadFrom(@"C:\Development\Osiris Launcher\Programming\Development\Osiris\App\Newtonsoft.Json.dll").GetType("Newtonsoft.Json.JsonConvert");
            var saved = (string)json.GetMethod("SerializeObject", new[] { typeof(object) }).Invoke(null, new object[] { settings });
            var restored = (ScreenshotsSettings)json.GetMethod("DeserializeObject", new[] { typeof(string), typeof(Type) }).Invoke(null, new object[] { saved, typeof(ScreenshotsSettings) });
            Check(restored.ExpandedExperience == "Cinematic", "Cinema setting persists through JSON");
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var ownerWindow = new Window { Width = 1400, Height = 900, Opacity = 0, ShowActivated = false, ShowInTaskbar = false, Left = -32000, Top = -32000 };
            app.MainWindow = ownerWindow;
            ownerWindow.Show();
            var cinemaWindow = new Window { WindowState = WindowState.Normal, Content = new Grid() };
            control.ConfigureExpandedWindow(cinemaWindow);
            Check(cinemaWindow.WindowState == WindowState.Normal && cinemaWindow.Content is Grid && cinemaWindow.WindowStyle == WindowStyle.None,
                "Cinema window is not maximized and uses shared frame");
            cinemaWindow.Close();
            var fullWindow = new Window { WindowState = WindowState.Maximized, Content = new Grid() };
            control.ConfigureExpandedWindow(fullWindow);
            Check(fullWindow.WindowState == WindowState.Maximized, "Full screen remains maximized");
            fullWindow.Close();
            ownerWindow.Close();
            control.GameContextChanged(null, game);
            Pump(200);
            Check(!control.HasScreenshots, "Empty card hidden");
            var folder = Path.Combine(root, "library", "files", game.Id.ToString(), "Screenshots");
            Directory.CreateDirectory(folder);
            WriteImage(Path.Combine(folder, "older.png"));
            File.SetLastWriteTimeUtc(Path.Combine(folder, "older.png"), DateTime.UtcNow.AddMinutes(-5));
            WriteImage(Path.Combine(folder, "newest.png"));
            Invoke(control, "Refresh");
            Pump(500);
            Check(control.HasScreenshots, "Hidden card discovers first screenshots");
            var images = (FileInfo[])Field(control, "files");
            Check(images[0].Name == "newest.png", "Newest first");
            Check(control.CaptureDateText == images[0].LastWriteTime.ToString("g"), "Current capture date");
            Check(control.MediaItems.Count == 2 && control.SelectedMediaItem == control.MediaItems[0], "Thumbnail rail population and selection");
            Check(((Image)Field(control, "currentImage")).Source != null, "Large current preview decoded");
            Check(((Image)Field(control, "nextImage")).Source != null, "Next preview decoded");
            Invoke(control, "Select", 1);
            Pump(250);
            Check((int)Field(control, "index") == 1, "Next navigation");
            Check(control.CaptureDateText == images[1].LastWriteTime.ToString("g"), "Date follows navigation");
            control.SelectNextScreenshotCommand.Execute(null);
            Check(control.CurrentImageBitmap != null, "End-boundary command preserves image");
            Check(!((Button)Field(control, "next")).IsEnabled, "End boundary");
            Check(((Button)Field(control, "previous")).IsEnabled, "Previous available");
            Check(((Image)Field(control, "nextImage")).Source == null, "No duplicate end preview");
            settings.ExpandedExperience = "Cinematic";
            Check(control.UseCinematicExpandedExperience, "Live setting used by viewer");
            File.WriteAllText(Path.Combine(root, "library", "files", game.Id.ToString(), "OsirisScreenshotsGallery.ini"), "Enabled=False");
            Invoke(control, "Refresh");
            Pump(250);
            Check(!control.HasScreenshots && control.MediaItems.Count == 0, "Per-game disable hides card without deleting files");
            Check(Directory.GetFiles(folder, "*.png").Length == 2, "Disable preserves screenshots");
            control.GameContextChanged(game, new Game { Id = Guid.NewGuid() });
            Pump(250);
            Check(!control.HasScreenshots, "Empty next game hides card");
            Check(((Image)Field(control, "currentImage")).Source == null, "Old game image cleared");
            Console.WriteLine(checks + " screenshot presentation checks passed.");
        }
        finally { Directory.Delete(root, true); }
    }
    private static void WriteImage(string path)
    {
        var bitmap = BitmapSource.Create(16, 9, 96, 96, PixelFormats.Bgra32, null, new byte[16 * 9 * 4], 16 * 4);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(path)) encoder.Save(stream);
    }
    private static object Field(object obj, string name) => obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj);
    private static void Invoke(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(obj, args);
    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, __) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
}
