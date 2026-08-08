using System;
using System.Runtime.InteropServices;

namespace Playnite.Native;

public class Shell32
{
	private const string dllName = "Shell32.dll";

	[DllImport("Shell32.dll")]
	public static extern int ExtractIconEx(string libName, int iconIndex, IntPtr[] largeIcon, IntPtr[] smallIcon, uint nIcons);
}
