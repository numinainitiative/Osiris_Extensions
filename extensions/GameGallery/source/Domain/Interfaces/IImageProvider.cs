using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace SteamScreenshots.Domain.Interfaces
{
    public interface IImageProvider
    {
        BitmapImage LoadImage(string path, CancellationToken cancellationToken = default);
        BitmapImage LoadImageWithDecodeMaxDimensions(
            string url,
            int decodeMaxWidth = 0,
            int decodeMaxHeight = 0,
            CancellationToken cancellationToken = default);
        BitmapImage LoadTransientImageWithDecodeMaxDimensions(
            string url,
            int decodeMaxWidth = 0,
            int decodeMaxHeight = 0,
            CancellationToken cancellationToken = default);
    }
}
