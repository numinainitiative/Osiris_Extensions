using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Playnite.Native;

public class Psapi
{
	private const string dllName = "Psapi.dll";

	[DllImport("Psapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern int GetMappedFileName(IntPtr hProcess, IntPtr lpv, StringBuilder lpFilename, int nSize);
}
