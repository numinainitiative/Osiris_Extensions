using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Playnite.SDK;

namespace Playnite.Common;

public class Programs
{
	private static readonly string[] scanFileExclusionMasks = new string[14]
	{
		"uninst", "setup", "unins\\d+", "Config", "DXSETUP", "vc_redist\\.x64", "vc_redist\\.x86", "^UnityCrashHandler32\\.exe$", "^UnityCrashHandler64\\.exe$", "^notification_helper\\.exe$",
		"^python\\.exe$", "^pythonw\\.exe$", "^zsync\\.exe$", "^zsyncmake\\.exe$"
	};

	private static ILogger logger = LogManager.GetLogger();

	public static bool IsFileScanExcluded(string path)
	{
		return scanFileExclusionMasks.Any((string a) => Regex.IsMatch(path, a, RegexOptions.IgnoreCase));
	}

	public static void CreateUrlShortcut(string url, string iconPath, string shortcutPath)
	{
		FileSystem.PrepareSaveFile(shortcutPath);
		string text = "[InternetShortcut]\r\nIconIndex=0";
		if (!iconPath.IsNullOrEmpty())
		{
			text = text + Environment.NewLine + "IconFile=" + iconPath;
		}
		text = text + Environment.NewLine + "URL=" + url;
		File.WriteAllText(shortcutPath, text);
	}

	private static List<UninstallProgram> GetUninstallProgsFromView(RegistryView view)
	{
		string rootString = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\";
		List<UninstallProgram> list = new List<UninstallProgram>();
		SearchRoot(RegistryHive.LocalMachine, list);
		SearchRoot(RegistryHive.CurrentUser, list);
		return list;
		void SearchRoot(RegistryHive hive, List<UninstallProgram> programs)
		{
			using RegistryKey registryKey = RegistryKey.OpenBaseKey(hive, view);
			RegistryKey registryKey2 = registryKey.OpenSubKey(rootString);
			if (registryKey2 != null)
			{
				string[] subKeyNames = registryKey2.GetSubKeyNames();
				foreach (string text in subKeyNames)
				{
					try
					{
						using RegistryKey registryKey3 = registryKey.OpenSubKey(rootString + text);
						if (registryKey3 != null)
						{
							UninstallProgram item = new UninstallProgram
							{
								DisplayIcon = registryKey3.GetValue("DisplayIcon")?.ToString(),
								DisplayVersion = registryKey3.GetValue("DisplayVersion")?.ToString(),
								DisplayName = registryKey3.GetValue("DisplayName")?.ToString(),
								InstallLocation = registryKey3.GetValue("InstallLocation")?.ToString(),
								Publisher = registryKey3.GetValue("Publisher")?.ToString(),
								UninstallString = registryKey3.GetValue("UninstallString")?.ToString(),
								URLInfoAbout = registryKey3.GetValue("URLInfoAbout")?.ToString(),
								Path = registryKey3.GetValue("Path")?.ToString(),
								RegistryKeyName = text
							};
							programs.Add(item);
						}
					}
					catch (SecurityException exception)
					{
						logger.Warn(exception, "Failed to read registry key " + rootString + text);
					}
				}
			}
		}
	}

	public static List<UninstallProgram> GetUnistallProgramsList()
	{
		List<UninstallProgram> list = new List<UninstallProgram>();
		if (Environment.Is64BitOperatingSystem)
		{
			list.AddRange(GetUninstallProgsFromView(RegistryView.Registry64));
		}
		list.AddRange(GetUninstallProgsFromView(RegistryView.Registry32));
		return list;
	}
}
