using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using XboxLibrary.Models;
using XboxLibrary.Services;

namespace XboxLibrary;

[LoadPlugin]
public class XboxLibrary : LibraryPluginBase<XboxLibrarySettingsViewModel>
{
	private readonly string pfnInfoCacheDir;

	public override LibraryClient Client => new XboxLibraryClient(base.SettingsViewModel);

	public XboxLibrary(IPlayniteAPI api)
		: base("Xbox", Guid.Parse("7e4fbb5e-2ae3-48d4-8ba0-6b30e7a4e287"), new LibraryPluginProperties
		{
			HasSettings = true
		}, (LibraryClient)null, Xbox.Icon, (Func<bool, UserControl>)((bool _) => new XboxLibrarySettingsView()), api)
	{
		base.SettingsViewModel = new XboxLibrarySettingsViewModel(this, api);
		pfnInfoCacheDir = Path.Combine(GetPluginUserDataPath(), "PfnInfoCache");
	}

	public override ISettings GetSettings(bool firstRunSettings)
	{
		base.SettingsViewModel.IsFirstRunUse = firstRunSettings;
		return base.SettingsViewModel;
	}

	internal GameMetadata GetGameMetadataFromTitle(TitleHistoryResponse.Title title)
	{
		bool containsPC;
		bool containsConsole;
		GameMetadata gameMetadata = new GameMetadata
		{
			GameId = title.pfn,
			Name = title.name.Replace("(PC)", "").Replace("(Windows)", "").Replace("for Windows 10", "")
				.Replace("- Windows 10", "")
				.RemoveTrademarks()
				.Trim(),
			Source = new MetadataNameProperty("Xbox"),
			Platforms = GetPlatforms(title.devices, out containsPC, out containsConsole)
		};
		if (title.detail != null)
		{
			if (title.detail.releaseDate.HasValue)
			{
				gameMetadata.ReleaseDate = new ReleaseDate(title.detail.releaseDate.Value);
			}
			if (!title.detail.publisherName.IsNullOrEmpty())
			{
				gameMetadata.Publishers = ListExtensions.ToHashSet((from a in title.detail.publisherName.Split('|')
					select new MetadataNameProperty(a.Trim())).Cast<MetadataProperty>());
			}
			if (!title.detail.developerName.IsNullOrEmpty())
			{
				gameMetadata.Developers = ListExtensions.ToHashSet((from a in title.detail.developerName.Split('|')
					select new MetadataNameProperty(a.Trim())).Cast<MetadataProperty>());
			}
		}
		if (title.titleHistory != null)
		{
			gameMetadata.LastActivity = title.titleHistory.lastTimePlayed;
		}
		if (title.minutesPlayed != null && ulong.TryParse(title.minutesPlayed, out var result))
		{
			gameMetadata.Playtime = result * 60;
		}
		return gameMetadata;
	}

	public List<TitleHistoryResponse.Title> GetAppDataCache()
	{
		List<TitleHistoryResponse.Title> list = new List<TitleHistoryResponse.Title>();
		if (Directory.Exists(pfnInfoCacheDir))
		{
			string[] files = Directory.GetFiles(pfnInfoCacheDir, "*.json", SearchOption.TopDirectoryOnly);
			foreach (string text in files)
			{
				try
				{
					TitleHistoryResponse.Title title = Serialization.FromJsonFile<TitleHistoryResponse.Title>(text);
					if (title == null)
					{
						Logger.Error("Failed to get app info from cache " + text + ", cache is empty.");
						File.Delete(text);
					}
					else
					{
						list.Add(title);
					}
				}
				catch (Exception exception)
				{
					Logger.Error(exception, "Failed to get app info from cache " + text + ".");
				}
			}
		}
		return list;
	}

	public void WriteAppDataCache(TitleHistoryResponse.Title data)
	{
		string path = Path.Combine(pfnInfoCacheDir, data.pfn + ".json");
		FileSystem.PrepareSaveFile(path);
		File.WriteAllText(path, Serialization.ToJson(data));
	}

	private static HashSet<MetadataProperty> GetPlatforms(List<string> devices, out bool containsPC, out bool containsConsole)
	{
		containsPC = false;
		containsConsole = false;
		HashSet<MetadataProperty> hashSet = new HashSet<MetadataProperty>();
		if (devices == null)
		{
			return hashSet;
		}
		foreach (string device in devices)
		{
			switch (device)
			{
			case "PC":
				containsPC = true;
				hashSet.Add(new MetadataSpecProperty("pc_windows"));
				break;
			case "Xbox360":
				containsConsole = true;
				hashSet.Add(new MetadataSpecProperty("xbox360"));
				break;
			case "XboxOne":
				containsConsole = true;
				hashSet.Add(new MetadataSpecProperty("xbox_one"));
				break;
			case "XboxSeries":
				containsConsole = true;
				hashSet.Add(new MetadataSpecProperty("xbox_series"));
				break;
			}
		}
		return hashSet;
	}

	public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
	{
		Dictionary<string, GameMetadata> dictionary = new Dictionary<string, GameMetadata>();
		Exception ex = null;
		List<GameMetadata> list = new List<GameMetadata>();
		if (!base.SettingsViewModel.Settings.ConnectAccount)
		{
			return list;
		}
		if (Computer.WindowsVersion < WindowsVersion.Win10)
		{
			throw new Exception("Xbox game library is only supported on Windows 10 and newer.");
		}
		List<TitleHistoryResponse.Title> list2 = new List<TitleHistoryResponse.Title>();
		XboxAccountClient xboxAccountClient = new XboxAccountClient(this);
		try
		{
			list2 = xboxAccountClient.GetLibraryTitles().GetAwaiter().GetResult();
		}
		catch (Exception ex2)
		{
			Logger.Error(ex2, "Failed to Xbox profile titles.");
			ex = ex2;
		}
		if (list2.Count > 0)
		{
			try
			{
				Dictionary<string, string> dictionary2 = xboxAccountClient.GetUserStatsMinutesPlayed(list2.Select((TitleHistoryResponse.Title t) => t.titleId)).GetAwaiter().GetResult()
					.ToDictionary((UserStatsResponse.Stats stat) => stat.titleid, (UserStatsResponse.Stats stat) => stat.value);
				foreach (TitleHistoryResponse.Title item in list2)
				{
					if (dictionary2.ContainsKey(item.titleId))
					{
						item.minutesPlayed = dictionary2[item.titleId];
					}
				}
			}
			catch (Exception ex3)
			{
				Logger.Error(ex3, "Failed to import Xbox user stats.");
				ex = ex3;
			}
		}
		List<TitleHistoryResponse.Title> appDataCache = GetAppDataCache();
		List<TitleHistoryResponse.Title> list3 = list2.Where((TitleHistoryResponse.Title title2) => !title2.pfn.IsNullOrEmpty() && title2.type == "Game" && (title2.devices?.Contains("PC") ?? false)).ToList();
		if (base.SettingsViewModel.Settings.ImportInstalledGames)
		{
			try
			{
				foreach (Program installedApp in Programs.GetUWPApps())
				{
					bool flag = false;
					TitleHistoryResponse.Title title = list3.FirstOrDefault((TitleHistoryResponse.Title a) => a.pfn == installedApp.AppId);
					if (title != null)
					{
						flag = true;
					}
					else
					{
						try
						{
							title = appDataCache.FirstOrDefault((TitleHistoryResponse.Title a) => a.pfn == installedApp.AppId);
							if (title == null)
							{
								title = xboxAccountClient.GetTitleInfo(installedApp.AppId).GetAwaiter().GetResult();
								WriteAppDataCache(title);
							}
							if (title.type == "Game")
							{
								flag = true;
							}
						}
						catch (Exception exception)
						{
							Logger.Error(exception, "Failed to get info about installed UWP package " + installedApp.AppId + ".");
							continue;
						}
					}
					if (flag)
					{
						string text = installedApp.WorkDir;
						try
						{
							text = Paths.GetFinalPathName(installedApp.WorkDir);
						}
						catch (Exception exception2)
						{
							Logger.Error(exception2, "Failed to get real path for Xbox game " + text);
						}
						GameMetadata gameMetadataFromTitle = GetGameMetadataFromTitle(title);
						gameMetadataFromTitle.IsInstalled = true;
						gameMetadataFromTitle.InstallDirectory = text;
						gameMetadataFromTitle.Icon = (installedApp.Icon.IsNullOrEmpty() ? null : new MetadataFile(installedApp.Icon));
						if (!dictionary.ContainsKey(title.pfn))
						{
							dictionary.Add(title.pfn, gameMetadataFromTitle);
						}
					}
				}
				Logger.Debug($"Found {dictionary.Count} installed Xbox games.");
				list.AddRange(dictionary.Values.ToList());
			}
			catch (Exception ex4)
			{
				Logger.Error(ex4, "Failed to import installed Xbox games.");
				ex = ex4;
			}
		}
		if (base.SettingsViewModel.Settings.ImportUninstalledGames)
		{
			try
			{
				Logger.Debug($"Found {list3.Count} Xbox PC games.");
				foreach (TitleHistoryResponse.Title item2 in list3)
				{
					if (!dictionary.TryGetValue(item2.pfn, out var _))
					{
						list.Add(GetGameMetadataFromTitle(item2));
					}
				}
				foreach (TitleHistoryResponse.Title item3 in list2)
				{
					GetPlatforms(item3.devices, out var containsPC, out var containsConsole);
					if (containsConsole && !containsPC && base.SettingsViewModel.Settings.ImportConsoleGames)
					{
						GameMetadata gameMetadataFromTitle2 = GetGameMetadataFromTitle(item3);
						gameMetadataFromTitle2.GameId = "CONSOLE_" + item3.titleId + "_" + item3.mediaItemType;
						list.Add(gameMetadataFromTitle2);
					}
				}
			}
			catch (Exception ex5)
			{
				Logger.Error(ex5, "Failed to import linked account Xbox games details.");
				ex = ex5;
			}
		}
		if (ex != null)
		{
			PlayniteApi.Notifications.Add(new NotificationMessage(base.ImportErrorMessageId, string.Format(PlayniteApi.Resources.GetString("LOCLibraryImportError"), Name) + Environment.NewLine + ex.Message, NotificationType.Error, delegate
			{
				OpenSettingsView();
			}));
		}
		else
		{
			PlayniteApi.Notifications.Remove(base.ImportErrorMessageId);
		}
		return list;
	}

	public override IEnumerable<InstallController> GetInstallActions(GetInstallActionsArgs args)
	{
		if (!(args.Game.PluginId != Id) && !args.Game.GameId.StartsWith("CONSOLE"))
		{
			yield return new XboxInstallController(args.Game, base.SettingsViewModel.Settings.XboxAppClientPriorityLaunch && Xbox.IsXboxPassAppInstalled);
		}
	}

	public override IEnumerable<UninstallController> GetUninstallActions(GetUninstallActionsArgs args)
	{
		if (!(args.Game.PluginId != Id) && !args.Game.GameId.StartsWith("CONSOLE"))
		{
			yield return new XboxUninstallController(args.Game);
		}
	}

	public override IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
	{
		if (!(args.Game.PluginId != Id) && !args.Game.GameId.StartsWith("CONSOLE"))
		{
			yield return new XboxPlayController(args.Game);
		}
	}
}
