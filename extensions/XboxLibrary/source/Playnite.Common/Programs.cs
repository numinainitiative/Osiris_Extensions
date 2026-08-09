using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using IWshRuntimeLibrary;
using Microsoft.Win32;
using Playnite.SDK;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

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

	public static async Task<List<Program>> GetExecutablesFromFolder(string path, SearchOption searchOption, CancellationToken cancelToken)
	{
		return await Task.Run(delegate
		{
			List<Program> list = new List<Program>();
			foreach (FileSystemInfo item in new SafeFileEnumerator(path, "*.*", SearchOption.AllDirectories))
			{
				if (cancelToken.IsCancellationRequested)
				{
					return (List<Program>)null;
				}
				if (!item.Attributes.HasFlag(FileAttributes.Directory) && !IsFileScanExcluded(item.Name) && !item.Extension.IsNullOrEmpty() && (item.Extension.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || item.Extension.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) || item.Extension.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)))
				{
					list.Add(GetProgramData(item.FullName));
				}
			}
			return list;
		});
	}

	public static Program GetProgramData(string filePath)
	{
		FileInfo fileInfo = new FileInfo(filePath);
		string extension = fileInfo.Extension;
		if (extension != null && extension.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
		{
			FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(fileInfo.FullName);
			string name = ((!string.IsNullOrEmpty(versionInfo.ProductName?.Trim())) ? versionInfo.ProductName : new DirectoryInfo(Path.GetDirectoryName(fileInfo.FullName)).Name);
			return new Program
			{
				Path = fileInfo.FullName,
				Icon = fileInfo.FullName,
				WorkDir = Path.GetDirectoryName(fileInfo.FullName),
				Name = name,
				AppId = filePath.MD5()
			};
		}
		string extension2 = fileInfo.Extension;
		if (extension2 != null && extension2.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
		{
			Program lnkShortcutData = GetLnkShortcutData(fileInfo.FullName);
			string name2 = Path.GetFileNameWithoutExtension(fileInfo.Name);
			if (File.Exists(lnkShortcutData.Path))
			{
				FileVersionInfo versionInfo2 = FileVersionInfo.GetVersionInfo(lnkShortcutData.Path);
				name2 = ((!string.IsNullOrEmpty(versionInfo2.ProductName?.Trim())) ? versionInfo2.ProductName : Path.GetFileNameWithoutExtension(fileInfo.FullName));
			}
			Program program = new Program
			{
				Path = lnkShortcutData.Path,
				WorkDir = lnkShortcutData.WorkDir,
				Arguments = lnkShortcutData.Arguments,
				Name = name2,
				AppId = filePath.MD5()
			};
			if (!lnkShortcutData.Icon.IsNullOrEmpty())
			{
				Match match = Regex.Match(lnkShortcutData.Icon, "^(.+),(\\d+)$");
				if (match.Success)
				{
					program.Icon = match.Groups[1].Value;
					program.IconIndex = int.Parse(match.Groups[2].Value);
				}
				else
				{
					program.Icon = lnkShortcutData.Icon;
				}
			}
			else
			{
				program.Icon = lnkShortcutData.Path;
			}
			return program;
		}
		string extension3 = fileInfo.Extension;
		if (extension3 != null && extension3.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
		{
			return new Program
			{
				Path = fileInfo.FullName,
				Name = Path.GetFileNameWithoutExtension(fileInfo.FullName),
				WorkDir = Path.GetDirectoryName(fileInfo.FullName),
				AppId = filePath.MD5()
			};
		}
		throw new NotSupportedException("Only exe, bat and lnk files are supported.");
	}

	public static void CreateShortcut(string executablePath, string arguments, string iconPath, string shortcutPath)
	{
		WshShell wshShell = (WshShell)Activator.CreateInstance(Marshal.GetTypeFromCLSID(new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8")));
		IWshShortcut obj = (IWshShortcut)(dynamic)wshShell.CreateShortcut(shortcutPath);
		obj.TargetPath = executablePath;
		obj.WorkingDirectory = Path.GetDirectoryName(executablePath);
		obj.Arguments = arguments;
		obj.IconLocation = (string.IsNullOrEmpty(iconPath) ? (executablePath + ",0") : iconPath);
		obj.Save();
	}

	public static Program GetLnkShortcutData(string lnkPath)
	{
		WshShell wshShell = (WshShell)Activator.CreateInstance(Marshal.GetTypeFromCLSID(new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8")));
		IWshShortcut wshShortcut = (IWshShortcut)(dynamic)wshShell.CreateShortcut(lnkPath);
		return new Program
		{
			Path = wshShortcut.TargetPath,
			Icon = ((wshShortcut.IconLocation == ",0") ? wshShortcut.TargetPath : wshShortcut.IconLocation),
			Arguments = wshShortcut.Arguments,
			WorkDir = wshShortcut.WorkingDirectory,
			Name = wshShortcut.FullName,
			AppId = lnkPath.MD5()
		};
	}

	public static async Task<List<Program>> GetShortcutProgramsFromFolder(string path, CancellationTokenSource cancelToken = null)
	{
		return await Task.Run(delegate
		{
			string[] source = new string[7] { "\\Accessibility\\", "\\Accessories\\", "\\Administrative Tools\\", "\\Maintenance\\", "\\StartUp\\", "\\Windows ", "\\Microsoft " };
			string[] source2 = new string[2] { "\\system32\\", "\\windows\\" };
			WshShell wshShell = (WshShell)Activator.CreateInstance(Marshal.GetTypeFromCLSID(new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8")));
			List<Program> list = new List<Program>();
			foreach (FileSystemInfo shortcut in new SafeFileEnumerator(path, "*.lnk", SearchOption.AllDirectories))
			{
				CancellationTokenSource cancellationTokenSource = cancelToken;
				if (cancellationTokenSource != null && cancellationTokenSource.IsCancellationRequested)
				{
					return (List<Program>)null;
				}
				if (!shortcut.Attributes.HasFlag(FileAttributes.Directory))
				{
					_ = shortcut.Name;
					Path.GetDirectoryName(shortcut.FullName);
					if (source.FirstOrDefault((string a) => shortcut.FullName.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0) == null)
					{
						IWshShortcut wshShortcut = (IWshShortcut)(dynamic)wshShell.CreateShortcut(shortcut.FullName);
						string target = wshShortcut.TargetPath;
						if (source2.FirstOrDefault((string a) => target.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0) == null && !IsFileScanExcluded(Path.GetFileName(target)) && list.FirstOrDefault((Program a) => a.Path == target) == null && !(Path.GetExtension(target) != ".exe"))
						{
							Program item = new Program
							{
								Path = target,
								Icon = wshShortcut.IconLocation,
								Name = Path.GetFileNameWithoutExtension(shortcut.Name),
								WorkDir = wshShortcut.WorkingDirectory,
								AppId = path.MD5()
							};
							list.Add(item);
						}
					}
				}
			}
			return list;
		});
	}

	public static async Task<List<Program>> GetInstalledPrograms(CancellationToken cancelToken)
	{
		List<Program> apps = new List<Program>();
		List<Program> collection = await GetShortcutProgramsFromFolder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"));
		if (cancelToken.IsCancellationRequested)
		{
			return null;
		}
		apps.AddRange(collection);
		List<Program> collection2 = await GetShortcutProgramsFromFolder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"));
		if (cancelToken.IsCancellationRequested)
		{
			return null;
		}
		apps.AddRange(collection2);
		return apps;
	}

	private static string GetUWPGameIcon(string defPath)
	{
		if (File.Exists(defPath))
		{
			return defPath;
		}
		string directoryName = Path.GetDirectoryName(defPath);
		string searchPattern = Path.GetFileNameWithoutExtension(defPath) + ".scale*.png";
		string[] files = Directory.GetFiles(directoryName, searchPattern);
		if (files == null || files.Count() == 0)
		{
			return string.Empty;
		}
		IEnumerable<string> source = files.Where((string a) => Regex.IsMatch(a, "\\.scale-\\d+\\.png"));
		if (source.Any())
		{
			return source.OrderBy((string a) => a).Last();
		}
		return string.Empty;
	}

	public static List<Program> GetUWPApps()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Invalid comparison between Unknown and I4
		List<Program> list = new List<Program>();
		try
		{
			foreach (Package item2 in new PackageManager().FindPackagesForUser(WindowsIdentity.GetCurrent().User.Value))
			{
				if (item2.IsFramework || item2.IsResourcePackage || (int)item2.SignatureKind != 3)
				{
					continue;
				}
				try
				{
					if (item2.InstalledLocation == null)
					{
						continue;
					}
				}
				catch
				{
					continue;
				}
				try
				{
					string path = Path.Combine(path2: (!item2.IsBundle) ? "AppxManifest.xml" : "AppxMetadata\\AppxBundleManifest.xml", path1: item2.InstalledLocation.Path);
					XmlDocument xmlDocument = new XmlDocument();
					using (FileStream inStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
					{
						xmlDocument.Load(inStream);
					}
					XmlNode xmlNode = xmlDocument.SelectSingleNode("/*[local-name() = 'Package']/*[local-name() = 'Applications']//*[local-name() = 'Application'][1]");
					if (xmlNode.Attributes["Id"] == null)
					{
						continue;
					}
					string value = xmlNode.Attributes["Id"].Value;
					XmlNode xmlNode2 = xmlNode.SelectSingleNode("//*[local-name() = 'VisualElements']");
					string text = xmlNode2.Attributes["Square150x150Logo"]?.Value;
					if (text.IsNullOrEmpty())
					{
						text = xmlNode2.Attributes["Square70x70Logo"]?.Value;
						if (text.IsNullOrEmpty())
						{
							text = xmlNode2.Attributes["Square44x44Logo"]?.Value;
							if (text.IsNullOrEmpty())
							{
								text = xmlNode2.Attributes["Logo"]?.Value;
							}
						}
					}
					if (!text.IsNullOrEmpty())
					{
						text = Path.Combine(item2.InstalledLocation.Path, text);
						text = GetUWPGameIcon(text);
					}
					string text2 = xmlDocument.SelectSingleNode("/*[local-name() = 'Package']/*[local-name() = 'Properties']/*[local-name() = 'DisplayName']").InnerText;
					if (text2.StartsWith("ms-resource"))
					{
						text2 = Resources.GetIndirectResourceString(item2.Id.FullName, item2.Id.Name, text2);
						if (text2.IsNullOrEmpty())
						{
							text2 = xmlDocument.SelectSingleNode("/*[local-name() = 'Package']/*[local-name() = 'Identity']").Attributes["Name"].Value;
						}
					}
					Program item = new Program
					{
						Name = text2.NormalizeGameName(),
						WorkDir = item2.InstalledLocation.Path,
						Path = "explorer.exe",
						Arguments = "shell:AppsFolder\\" + item2.Id.FamilyName + "!" + value,
						Icon = text,
						AppId = item2.Id.FamilyName
					};
					list.Add(item);
				}
				catch (Exception exception)
				{
					logger.Error(exception, "Failed to parse UWP app " + item2.Id.FullName + " info.");
				}
			}
		}
		catch (Exception exception2) when (!Debugger.IsAttached)
		{
			logger.Error(exception2, "Failed to get list of installed UWP apps.");
		}
		return list;
	}
}
