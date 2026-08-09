using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Playnite.Native;

namespace System.Diagnostics;

public static class ProcessExtensions
{
	public static bool TryGetMainModuleFileName(this Process process, out string fileName, int buffer = 1024)
	{
		fileName = null;
		IntPtr intPtr = Kernel32.OpenProcess(ProcessAccessFlags.QueryLimitedInformation, bInheritHandle: false, process.Id);
		if (intPtr == IntPtr.Zero)
		{
			return false;
		}
		try
		{
			StringBuilder stringBuilder = new StringBuilder(buffer);
			uint lpdwSize = (uint)(stringBuilder.Capacity + 1);
			bool flag = Kernel32.QueryFullProcessImageName(intPtr, 0u, stringBuilder, ref lpdwSize);
			fileName = (flag ? stringBuilder.ToString() : null);
			return flag;
		}
		finally
		{
			Kernel32.CloseHandle(intPtr);
		}
	}

	public static bool TryGetParentId(this Process process, out int processId)
	{
		processId = 0;
		IntPtr intPtr = Kernel32.OpenProcess(ProcessAccessFlags.QueryLimitedInformation, bInheritHandle: false, process.Id);
		if (intPtr == IntPtr.Zero)
		{
			return false;
		}
		try
		{
			PROCESS_BASIC_INFORMATION pbi = default(PROCESS_BASIC_INFORMATION);
			if (Ntdll.NtQueryInformationProcess(intPtr, PROCESSINFOCLASS.ProcessBasicInformation, ref pbi, Marshal.SizeOf(pbi), out var _) != 0)
			{
				return false;
			}
			processId = pbi.InheritedFromUniqueProcessId.ToInt32();
			return true;
		}
		finally
		{
			Kernel32.CloseHandle(intPtr);
		}
	}

	public static bool IsRunning(string processPattern)
	{
		return Process.GetProcesses().FirstOrDefault((Process a) => Regex.IsMatch(a.ProcessName, processPattern, RegexOptions.IgnoreCase)) != null;
	}

	public static string GetCommandLine(this Process process)
	{
		using ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + process.Id);
		using ManagementObjectCollection source = managementObjectSearcher.Get();
		return source.Cast<ManagementBaseObject>().SingleOrDefault()?["CommandLine"]?.ToString();
	}
}
