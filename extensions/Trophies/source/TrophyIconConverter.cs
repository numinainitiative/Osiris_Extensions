using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace Osiris.Extensions.Trophies
{
    public sealed class TrophyIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var trophy = value as Trophy;
            if (string.IsNullOrEmpty(trophy?.LocalIcon)) return null;
            try
            {
                var image = new BitmapImage();
                using (var stream = File.OpenRead(trophy.LocalIcon))
                { image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 144; image.StreamSource = stream; image.EndInit(); image.Freeze(); }
                if (trophy.UnlockedUtc.HasValue) return image;
                var grey = new FormatConvertedBitmap(image, PixelFormats.Gray8, null, 0); grey.Freeze(); return grey;
            }
            catch { return null; }
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
