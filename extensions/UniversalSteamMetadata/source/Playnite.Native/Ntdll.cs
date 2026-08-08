using System;
using System.Runtime.InteropServices;

namespace Playnite.Native;

public class Ntdll
{
	private const string dllName = "Ntdll.dll";

	[DllImport("Ntdll.dll", SetLastError = true)]
	public static extern int NtQueryInformationProcess(IntPtr hProcess, PROCESSINFOCLASS pic, ref PROCESS_BASIC_INFORMATION pbi, int cb, out int pSize);
}
