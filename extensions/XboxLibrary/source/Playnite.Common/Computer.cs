using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using Playnite.Native;
using Playnite.SDK;

namespace Playnite.Common;

public static class Computer
{
	private static readonly ILogger logger = LogManager.GetLogger();

	public static WindowsVersion WindowsVersion
	{
		get
		{
			Version version = Environment.OSVersion.Version;
			if (version.Major == 6 && version.Minor == 1)
			{
				return WindowsVersion.Win7;
			}
			if (version.Major == 6 && (version.Minor == 2 || version.Minor == 3))
			{
				return WindowsVersion.Win8;
			}
			if (version.Major == 10)
			{
				if (version.Build >= 22000)
				{
					return WindowsVersion.Win11;
				}
				return WindowsVersion.Win10;
			}
			return WindowsVersion.Unknown;
		}
	}

	public static bool IsTLS13SystemWideEnabled()
	{
		try
		{
			using RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Control\\SecurityProviders\\SCHANNEL\\Protocols\\TLS 1.3\\Client");
			if (registryKey != null)
			{
				object value = registryKey.GetValue("Enabled");
				if (value != null)
				{
					return Convert.ToBoolean(value);
				}
			}
		}
		catch (Exception exception)
		{
			logger.Error(exception, "Failed to test TLS 1.3 state.");
		}
		return false;
	}

	public static int GetWindowsReleaseId()
	{
		object value = Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "ReleaseId", "");
		if (value != null && value.ToString().IsNullOrEmpty())
		{
			return 0;
		}
		return Convert.ToInt32(value);
	}

	public static string GetWindowsProductName()
	{
		return Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "ProductName", "").ToString();
	}

	public static Guid GetMachineGuid()
	{
		RegistryKey registryKey = null;
		registryKey = ((!Environment.Is64BitOperatingSystem) ? RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32) : RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64));
		try
		{
			using RegistryKey registryKey2 = registryKey.OpenSubKey("SOFTWARE\\Microsoft\\Cryptography");
			return Guid.Parse((string)registryKey2.GetValue("MachineGuid"));
		}
		finally
		{
			registryKey.Dispose();
		}
	}

	public static SystemInfo GetSystemInfo()
	{
		SystemInfo systemInfo = new SystemInfo
		{
			Is64Bit = Environment.Is64BitOperatingSystem,
			WindowsVersion = Environment.OSVersion.VersionString,
			ActualWindowsVersion = WindowsVersion.ToString(),
			WindowsBuildVersion = GetWindowsReleaseId(),
			WindowsEdition = GetWindowsProductName()
		};
		using (ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor"))
		{
			using ManagementObjectCollection.ManagementObjectEnumerator managementObjectEnumerator = managementObjectSearcher.Get().GetEnumerator();
			if (managementObjectEnumerator.MoveNext())
			{
				ManagementBaseObject current = managementObjectEnumerator.Current;
				systemInfo.Cpu = current["Name"].ToString().Trim();
			}
		}
		using (ManagementObjectSearcher managementObjectSearcher2 = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem"))
		{
			double num = 0.0;
			foreach (ManagementBaseObject item in managementObjectSearcher2.Get())
			{
				num += Convert.ToDouble(item["TotalPhysicalMemory"]);
			}
			systemInfo.Ram = Convert.ToInt32(num / 1048576.0);
		}
		using (ManagementObjectSearcher managementObjectSearcher3 = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController"))
		{
			systemInfo.Gpus = new List<string>();
			foreach (ManagementBaseObject item2 in managementObjectSearcher3.Get())
			{
				systemInfo.Gpus.Add(item2["Name"].ToString());
			}
		}
		systemInfo.Screens = GetScreens();
		return systemInfo;
	}

	public static List<ComputerScreen> GetScreens()
	{
		return Screen.AllScreens.Select((Screen a) => a.ToComputerScreen()).ToList();
	}

	public static ComputerScreen GetPrimaryScreen()
	{
		return Screen.PrimaryScreen?.ToComputerScreen();
	}

	public static int GetGetPrimaryScreenIndex()
	{
		Screen[] allScreens = Screen.AllScreens;
		for (int i = 0; i < allScreens.Length; i++)
		{
			if (allScreens[i].Primary)
			{
				return i;
			}
		}
		return 0;
	}

	public static void SetMouseCursorVisibility(bool show)
	{
		if (show)
		{
			while (User32.ShowCursor(bShow: true) < 0)
			{
			}
		}
		else
		{
			while (User32.ShowCursor(bShow: false) >= 0)
			{
			}
		}
	}

	public static void Shutdown()
	{
		if (WindowsVersion == WindowsVersion.Win7)
		{
			ProcessStarter.StartProcess("shutdown.exe", "-s -t 0");
		}
		else
		{
			ProcessStarter.StartProcess("shutdown.exe", "-s -hybrid -t 0");
		}
	}

	public static void Restart()
	{
		ProcessStarter.StartProcess("shutdown.exe", "-r -t 0");
	}

	public static bool Sleep()
	{
		return Powrprof.SetSuspendState(hiberate: false, forceCritical: true, disableWakeEvent: false);
	}

	public static bool Hibernate()
	{
		return Powrprof.SetSuspendState(hiberate: true, forceCritical: true, disableWakeEvent: false);
	}

	public static ComputerScreen ToComputerScreen(this Screen screen)
	{
		if (screen == null)
		{
			return null;
		}
		return new ComputerScreen(screen);
	}

	public static List<HwCompany> GetGpuVendors()
	{
		List<string> list = new List<string>();
		List<HwCompany> list2 = new List<HwCompany>();
		try
		{
			using (ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController"))
			{
				foreach (ManagementBaseObject item in managementObjectSearcher.Get())
				{
					list.Add(item["Name"].ToString());
				}
			}
			foreach (string item2 in list)
			{
				if (item2.Contains("intel", StringComparison.OrdinalIgnoreCase))
				{
					list2.AddMissing(HwCompany.Intel);
					continue;
				}
				if (item2.Contains("nvidia", StringComparison.OrdinalIgnoreCase))
				{
					list2.AddMissing(HwCompany.Nvidia);
					continue;
				}
				if (item2.Contains("amd", StringComparison.OrdinalIgnoreCase))
				{
					list2.AddMissing(HwCompany.AMD);
					continue;
				}
				if (item2.Contains("vmware", StringComparison.OrdinalIgnoreCase))
				{
					list2.AddMissing(HwCompany.VMware);
					continue;
				}
				return new List<HwCompany> { HwCompany.Uknown };
			}
			if (list2.Count > 0)
			{
				return list2;
			}
		}
		catch (Exception exception)
		{
			logger.Error(exception, "Failed to get GPU vendor.");
		}
		return new List<HwCompany> { HwCompany.Uknown };
	}

	private static string GetMonitorFriendlyName(LUID adapterId, uint targetId)
	{
		DISPLAYCONFIG_TARGET_DEVICE_NAME deviceName = new DISPLAYCONFIG_TARGET_DEVICE_NAME
		{
			header =
			{
				size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_TARGET_DEVICE_NAME)),
				adapterId = adapterId,
				id = targetId,
				type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME
			}
		};
		int num = User32.DisplayConfigGetDeviceInfo(ref deviceName);
		if (num != 0)
		{
			throw new Win32Exception(num);
		}
		return deviceName.monitorFriendlyDeviceName;
	}

	private static IEnumerable<string> GetAllMonitorsFriendlyNames()
	{
		int displayConfigBufferSizes = User32.GetDisplayConfigBufferSizes(QUERY_DEVICE_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS, out var numPathArrayElements, out var modeCount);
		if (displayConfigBufferSizes != 0)
		{
			throw new Win32Exception(displayConfigBufferSizes);
		}
		DISPLAYCONFIG_PATH_INFO[] pathInfoArray = new DISPLAYCONFIG_PATH_INFO[numPathArrayElements];
		DISPLAYCONFIG_MODE_INFO[] displayModes = new DISPLAYCONFIG_MODE_INFO[modeCount];
		displayConfigBufferSizes = User32.QueryDisplayConfig(QUERY_DEVICE_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS, ref numPathArrayElements, pathInfoArray, ref modeCount, displayModes, IntPtr.Zero);
		if (displayConfigBufferSizes != 0)
		{
			throw new Win32Exception(displayConfigBufferSizes);
		}
		for (int i = 0; i < modeCount; i++)
		{
			if (displayModes[i].infoType == DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_TARGET)
			{
				yield return GetMonitorFriendlyName(displayModes[i].adapterId, displayModes[i].id);
			}
		}
	}

	public static string DeviceFriendlyName(this Screen screen)
	{
		try
		{
			IEnumerable<string> allMonitorsFriendlyNames = GetAllMonitorsFriendlyNames();
			for (int i = 0; i < Screen.AllScreens.Length; i++)
			{
				if (object.Equals(screen, Screen.AllScreens[i]))
				{
					return allMonitorsFriendlyNames.ToArray()[i];
				}
			}
		}
		catch (Exception exception)
		{
			logger.Error(exception, "Failed to get display name.");
		}
		return screen.DeviceName;
	}

	public static bool GetScreenReaderActive()
	{
		if (Process.GetProcessesByName("narrator").HasItems())
		{
			return true;
		}
		if (Process.GetProcessesByName("nvda").HasItems())
		{
			return true;
		}
		if (Process.GetProcessesByName("jfw").HasItems())
		{
			return true;
		}
		return false;
	}
}
