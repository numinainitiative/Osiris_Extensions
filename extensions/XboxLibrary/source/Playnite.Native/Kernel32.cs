using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Playnite.Native;

public class Kernel32
{
	private const string dllName = "Kernel32.dll";

	[DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	public static extern uint GetFinalPathNameByHandle(IntPtr hFile, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder lpszFilePath, uint cchFilePath, uint dwFlags);

	[DllImport("Kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool CloseHandle(IntPtr hObject);

	[DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	public static extern IntPtr CreateFile([MarshalAs(UnmanagedType.LPTStr)] string filename, [MarshalAs(UnmanagedType.U4)] uint access, [MarshalAs(UnmanagedType.U4)] FileShare share, IntPtr securityAttributes, [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition, [MarshalAs(UnmanagedType.U4)] uint flagsAndAttributes, IntPtr templateFile);

	[DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	public static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

	[DllImport("Kernel32.dll", SetLastError = true)]
	public static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);

	[DllImport("Kernel32.dll", SetLastError = true)]
	public static extern IntPtr FindResource(IntPtr hModule, string lpName, string lpType);

	[DllImport("Kernel32.dll", SetLastError = true)]
	public static extern uint SizeofResource(IntPtr hModule, IntPtr hResInfo);

	[DllImport("Kernel32.dll", SetLastError = true)]
	public static extern bool FreeLibrary(IntPtr hModule);

	[DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern bool EnumResourceNames(IntPtr hModule, IntPtr lpszType, ENUMRESNAMEPROC lpEnumFunc, IntPtr lParam);

	[DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern IntPtr FindResource(IntPtr hModule, IntPtr lpName, IntPtr lpType);

	[DllImport("Kernel32.dll", SetLastError = true)]
	public static extern IntPtr LockResource(IntPtr hResData);

	[DllImport("Kernel32.dll", SetLastError = true)]
	public static extern IntPtr GetCurrentProcess();

	[DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern int QueryDosDevice(string lpDeviceName, StringBuilder lpTargetPath, int ucchMax);

	[DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	public static extern bool CreateProcess(string lpApplicationName, string lpCommandLine, ref SECURITY_ATTRIBUTES lpProcessAttributes, ref SECURITY_ATTRIBUTES lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, [In] ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

	[DllImport("Kernel32.dll", SetLastError = true)]
	public static extern IntPtr OpenProcess(ProcessAccessFlags processAccess, bool bInheritHandle, int processId);

	[DllImport("Kernel32.dll", CharSet = CharSet.Auto)]
	public static extern bool QueryFullProcessImageName([In] IntPtr hProcess, [In] uint dwFlags, [Out] StringBuilder lpExeName, [In][Out] ref uint lpdwSize);

	[DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	[return: MarshalAs(UnmanagedType.U4)]
	public static extern uint GetFileAttributesW(string lpFileName);

	[DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern uint GetCompressedFileSizeW([In][MarshalAs(UnmanagedType.LPWStr)] string lpFileName, [MarshalAs(UnmanagedType.U4)] out uint lpFileSizeHigh);

	[DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern int GetDiskFreeSpaceW([In][MarshalAs(UnmanagedType.LPWStr)] string lpRootPathName, out uint lpSectorsPerCluster, out uint lpBytesPerSector, out uint lpNumberOfFreeClusters, out uint lpTotalNumberOfClusters);
}
