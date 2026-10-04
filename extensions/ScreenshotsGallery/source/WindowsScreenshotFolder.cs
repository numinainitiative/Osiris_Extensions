using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Osiris.Extensions.ScreenshotsGallery
{
    internal static class WindowsScreenshotFolder
    {
        // Windows resolves redirected/localized/OneDrive locations for us.
        [DllImport("shell32.dll")]
        private static extern int SHGetKnownFolderPath(ref Guid id, uint flags, IntPtr token, out IntPtr path);

        internal static string Resolve()
        {
            var id = new Guid("b7bede81-df94-4682-a7d8-57a52620b86f");
            IntPtr pointer = IntPtr.Zero;
            try
            {
                if (SHGetKnownFolderPath(ref id, 0x4000, IntPtr.Zero, out pointer) == 0)
                    return Marshal.PtrToStringUni(pointer);
            }
            finally { if (pointer != IntPtr.Zero) Marshal.FreeCoTaskMem(pointer); }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");
        }
    }
}
