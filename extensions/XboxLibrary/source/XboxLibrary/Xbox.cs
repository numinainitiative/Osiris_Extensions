using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Playnite.Common;

namespace XboxLibrary;

public class Xbox
{
	private const string xboxPassAppFN = "Microsoft.GamingApp_8wekyb3d8bbwe";

	private static bool? isXboxPassAppInstalled;

	public static bool IsXboxPassAppInstalled
	{
		get
		{
			if (isXboxPassAppInstalled.HasValue)
			{
				return isXboxPassAppInstalled.Value;
			}
			isXboxPassAppInstalled = false;
			if (Computer.WindowsVersion >= WindowsVersion.Win10)
			{
				Program program = Programs.GetUWPApps().FirstOrDefault((Program a) => a.AppId == "Microsoft.GamingApp_8wekyb3d8bbwe");
				isXboxPassAppInstalled = program != null;
			}
			return isXboxPassAppInstalled.Value;
		}
	}

	public static string Icon { get; } = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "icon.png");

	public static void OpenXboxPassApp()
	{
		if (Computer.WindowsVersion >= WindowsVersion.Win10)
		{
			Program program = Programs.GetUWPApps().FirstOrDefault((Program a) => a.AppId == "Microsoft.GamingApp_8wekyb3d8bbwe");
			if (program == null)
			{
				throw new NotSupportedException("Xbox PC Pass app is not installed.");
			}
			ProcessStarter.StartProcess(program.Path, program.Arguments);
			return;
		}
		throw new NotSupportedException("Only supported on Windows 10 and newer.");
	}
}
