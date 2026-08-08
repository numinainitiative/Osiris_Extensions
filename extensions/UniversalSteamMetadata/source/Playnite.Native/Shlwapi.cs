using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Playnite.Native;

public class Shlwapi
{
	private const string dllName = "Shlwapi.dll";

	[DllImport("Shlwapi.dll", BestFitMapping = false, CharSet = CharSet.Unicode, ExactSpelling = true, ThrowOnUnmappableChar = true)]
	public static extern int SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, int cchOutBuf, IntPtr ppvReserved);

	[DllImport("Shlwapi.dll")]
	public static extern int PathMatchSpecExW([MarshalAs(UnmanagedType.LPWStr)] string file, [MarshalAs(UnmanagedType.LPWStr)] string spec, MatchPatternFlags flags);
}
