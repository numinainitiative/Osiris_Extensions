using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Playnite.Common;
using Playnite.Common.Web;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using PlayniteExtensions.Common;
using Steam;
using Steam.Models;
using SteamLibrary.SteamShared;

namespace UniversalSteamMetadata;

public class UniversalSteamMetadataProvider : OnDemandMetadataProvider
{
	public class SteamImageOption : ImageFileOption
	{
		public string Image { get; set; }
	}

	private static readonly ILogger logger = LogManager.GetLogger();

	private readonly MetadataRequestOptions options;

	private readonly UniversalSteamMetadata plugin;

	private readonly IDownloader downloader;

	private SteamGameMetadata currentMetadata;

	private readonly SteamApiClient apiClient;

	private readonly WebApiClient webApiClient;

	public override List<MetadataField> AvailableFields { get; } = new List<MetadataField>
	{
		MetadataField.Description,
		MetadataField.BackgroundImage,
		MetadataField.CommunityScore,
		MetadataField.CoverImage,
		MetadataField.CriticScore,
		MetadataField.Developers,
		MetadataField.Genres,
		MetadataField.Icon,
		MetadataField.Links,
		MetadataField.Publishers,
		MetadataField.ReleaseDate,
		MetadataField.Features,
		MetadataField.Name,
		MetadataField.Platform,
		MetadataField.Series,
		MetadataField.Tags
	};

	public UniversalSteamMetadataProvider(MetadataRequestOptions options, UniversalSteamMetadata plugin, IDownloader downloader)
	{
		this.options = options;
		this.plugin = plugin;
		this.downloader = downloader;
		apiClient = new SteamApiClient(plugin.SettingsViewModel.Settings);
		webApiClient = new WebApiClient();
	}

	public override void Dispose()
	{
		try
		{
			apiClient.Logout();
		}
		catch (Exception exception)
		{
			logger.Error(exception, "Failed to logout Steam client.");
		}
		webApiClient.Dispose();
	}

	public override string GetName(GetMetadataFieldArgs args)
	{
		GetGameData();
		if (currentMetadata != null)
		{
			return currentMetadata.Name;
		}
		return base.GetName(args);
	}

	public override string GetDescription(GetMetadataFieldArgs args)
	{
		GetGameData();
		if (currentMetadata != null)
		{
			return currentMetadata.Description;
		}
		return base.GetDescription(args);
	}

	public override MetadataFile GetBackgroundImage(GetMetadataFieldArgs args)
	{
		GetGameData();
		if (currentMetadata != null)
		{
			if (plugin.SettingsViewModel.Settings.BackgroundSource == BackgroundSource.StoreScreenshot && !options.IsBackgroundDownload)
			{
				StoreAppDetailsResult.AppDetails storeDetails = currentMetadata.StoreDetails;
				if (storeDetails != null && storeDetails.screenshots?.Count > 1)
				{
					List<ImageFileOption> list = new List<ImageFileOption>();
					foreach (StoreAppDetailsResult.AppDetails.Screenshot screenshot in currentMetadata.StoreDetails.screenshots)
					{
						list.Add(new SteamImageOption
						{
							Path = Regex.Replace(screenshot.path_thumbnail, "\\?.*$", ""),
							Image = Regex.Replace(screenshot.path_full, "\\?.*$", "")
						});
					}
					if (plugin.PlayniteApi.Dialogs.ChooseImageFile(list, "selectImage") is SteamImageOption steamImageOption)
					{
						return new MetadataFile(steamImageOption.Image);
					}
					goto IL_015b;
				}
			}
			return currentMetadata.BackgroundImage;
		}
		goto IL_015b;
		IL_015b:
		return base.GetBackgroundImage(args);
	}

	public override MetadataFile GetIcon(GetMetadataFieldArgs args)
	{
		GetGameData();
		if (currentMetadata != null)
		{
			return currentMetadata.Icon;
		}
		return base.GetIcon(args);
	}

	public override MetadataFile GetCoverImage(GetMetadataFieldArgs args)
	{
		GetGameData();
		if (currentMetadata != null)
		{
			return currentMetadata.CoverImage;
		}
		return base.GetCoverImage(args);
	}

	public override int? GetCommunityScore(GetMetadataFieldArgs args)
	{
		GetGameData();
		if (currentMetadata != null)
		{
			return currentMetadata.CommunityScore;
		}
		return base.GetCommunityScore(args);
	}

	public override int? GetCriticScore(GetMetadataFieldArgs args)
	{
		GetGameData();
		if (currentMetadata != null)
		{
			return currentMetadata.CriticScore;
		}
		return base.GetCriticScore(args);
	}

	public override IEnumerable<MetadataProperty> GetDevelopers(GetMetadataFieldArgs args)
	{
		GetGameData();
		if (currentMetadata != null)
		{
			return currentMetadata.Developers;
		}
		return base.GetDevelopers(args);
	}

	public override IEnumerable<MetadataProperty> GetGenres(GetMetadataFieldArgs args)
	{
		GetGameData();
		if (currentMetadata != null)
		{
			return currentMetadata.Genres;
		}
		return base.GetGenres(args);
	}

	public override IEnumerable<Link> GetLinks(GetMetadataFieldArgs args)
	{
		GetGameData();
		if (currentMetadata != null)
		{
			return currentMetadata.Links;
		}
		return base.GetLinks(args);
	}

	public override IEnumerable<MetadataProperty> GetPublishers(GetMetadataFieldArgs args)
	{
		GetGameData();
		if (currentMetadata != null)
		{
			return currentMetadata.Publishers;
		}
		return base.GetPublishers(args);
	}

	public override ReleaseDate? GetReleaseDate(GetMetadataFieldArgs args)
	{
		GetGameData();
		if (currentMetadata != null)
		{
			return currentMetadata.ReleaseDate;
		}
		return base.GetReleaseDate(args);
	}

	public override IEnumerable<MetadataProperty> GetFeatures(GetMetadataFieldArgs args)
	{
		GetGameData();
		if (currentMetadata != null)
		{
			return currentMetadata.Features;
		}
		return base.GetFeatures(args);
	}

	public override IEnumerable<MetadataProperty> GetSeries(GetMetadataFieldArgs args)
	{
		GetGameData();
		if (currentMetadata != null)
		{
			return currentMetadata.Series;
		}
		return base.GetSeries(args);
	}

	public override IEnumerable<MetadataProperty> GetPlatforms(GetMetadataFieldArgs args)
	{
		GetGameData();
		IEnumerable<MetadataProperty> enumerable = currentMetadata?.Platforms;
		return enumerable ?? base.GetPlatforms(args);
	}

	public override IEnumerable<MetadataProperty> GetTags(GetMetadataFieldArgs args)
	{
		GetGameData();
		if (currentMetadata != null)
		{
			return currentMetadata.Tags;
		}
		return base.GetTags(args);
	}

	internal void GetGameData()
	{
		if (currentMetadata != null)
		{
			return;
		}
		try
		{
			MetadataProvider metadataProvider = new MetadataProvider(apiClient, webApiClient, new SteamTagNamer(plugin, plugin.SettingsViewModel.Settings, downloader), plugin.SettingsViewModel.Settings);
			if (BuiltinExtensions.GetExtensionFromId(options.GameData.PluginId) == BuiltinExtension.SteamLibrary)
			{
				uint appId = uint.Parse(options.GameData.GameId);
				currentMetadata = metadataProvider.GetGameMetadata(appId, plugin.SettingsViewModel.Settings.BackgroundSource, plugin.SettingsViewModel.Settings.DownloadVerticalCovers);
				return;
			}
			if (options.IsBackgroundDownload)
			{
				uint matchingGame = GetMatchingGame(options.GameData);
				if (matchingGame != 0)
				{
					currentMetadata = metadataProvider.GetGameMetadata(matchingGame, plugin.SettingsViewModel.Settings.BackgroundSource, plugin.SettingsViewModel.Settings.DownloadVerticalCovers);
				}
				else
				{
					currentMetadata = new SteamGameMetadata();
				}
				return;
			}
			GenericItemOption genericItemOption = plugin.PlayniteApi.Dialogs.ChooseItemWithSearch(null, delegate(string a)
			{
				if (uint.TryParse(a, out var result))
				{
					try
					{
						StoreAppDetailsResult.AppDetails storeAppDetail = webApiClient.GetStoreAppDetail(result, plugin.SettingsViewModel.Settings.LanguageKey);
						return new List<GenericItemOption>
						{
							new StoreSearchResult
							{
								GameId = result,
								Name = storeAppDetail.name
							}
						};
					}
					catch (Exception exception2)
					{
						logger.Error(exception2, $"Failed to get Steam app info {result}");
						return new List<GenericItemOption>();
					}
				}
				try
				{
					return new List<GenericItemOption>(UniversalSteamMetadata.GetSearchResults(a.NormalizeGameName()));
				}
				catch (Exception exception3)
				{
					logger.Error(exception3, "Failed to get Steam search data for " + a);
					return new List<GenericItemOption>();
				}
			}, options.GameData.Name, string.Empty);
			if (genericItemOption == null)
			{
				currentMetadata = new SteamGameMetadata();
			}
			else
			{
				currentMetadata = metadataProvider.GetGameMetadata(((StoreSearchResult)genericItemOption).GameId, plugin.SettingsViewModel.Settings.BackgroundSource, plugin.SettingsViewModel.Settings.DownloadVerticalCovers);
			}
		}
		catch (Exception exception)
		{
			logger.Error(exception, "Failed to get Steam metadata.");
			currentMetadata = new SteamGameMetadata();
		}
	}

	internal uint MatchFun(string matchName, List<StoreSearchResult> list)
	{
		return list.FirstOrDefault((StoreSearchResult a) => string.Equals(matchName, a.Name, StringComparison.InvariantCultureIgnoreCase))?.GameId ?? 0;
	}

	internal string ReplaceNumsForRomans(Match m)
	{
		return Roman.To(int.Parse(m.Value));
	}

	internal uint GetMatchingGame(Game gameInfo)
	{
		string normalizedName = gameInfo.Name.NormalizeGameName();
		List<StoreSearchResult> searchResults = UniversalSteamMetadata.GetSearchResults(normalizedName);
		string alphanumericKey = GameNameMatcher.ToAlphanumericLower(gameInfo.Name);
		StoreSearchResult storeSearchResult = searchResults.FirstOrDefault((StoreSearchResult x) => GameNameMatcher.ToAlphanumericLower(x.Name) == alphanumericKey);
		if (storeSearchResult != null)
		{
			return storeSearchResult.GameId;
		}
		string gameKey = GameNameMatcher.ToGameKey(gameInfo.Name);
		storeSearchResult = searchResults.FirstOrDefault((StoreSearchResult x) => GameNameMatcher.ToGameKey(x.Name) == gameKey);
		if (storeSearchResult != null)
		{
			return storeSearchResult.GameId;
		}
		searchResults.ForEach(delegate(StoreSearchResult a)
		{
			a.Name = a.Name.NormalizeGameName();
		});
		string empty = string.Empty;
		uint num = 0u;
		num = MatchFun(normalizedName, searchResults);
		if (num != 0)
		{
			return num;
		}
		empty = Regex.Replace(normalizedName, "\\d+", ReplaceNumsForRomans);
		num = MatchFun(empty, searchResults);
		if (num != 0)
		{
			return num;
		}
		empty = "The " + normalizedName;
		num = MatchFun(empty, searchResults);
		if (num != 0)
		{
			return num;
		}
		empty = Regex.Replace(normalizedName, "\\s+and\\s+", " & ", RegexOptions.IgnoreCase);
		num = MatchFun(empty, searchResults);
		if (num != 0)
		{
			return num;
		}
		List<StoreSearchResult> clone = Serialization.GetClone(searchResults);
		clone.ForEach(delegate(StoreSearchResult a)
		{
			a.Name = a.Name.Replace("'", "");
		});
		num = MatchFun(normalizedName, clone);
		if (num != 0)
		{
			return num;
		}
		empty = Regex.Replace(normalizedName, "\\s*(:|-)\\s*", " ");
		clone = Serialization.GetClone(searchResults);
		foreach (StoreSearchResult item in clone)
		{
			item.Name = Regex.Replace(item.Name, "\\s*(:|-)\\s*", " ");
		}
		num = MatchFun(empty, clone);
		if (num != 0)
		{
			return num;
		}
		return searchResults.FirstOrDefault((StoreSearchResult a) => !string.IsNullOrEmpty(a.Name) && a.Name.Contains(":") && string.Equals(normalizedName, a.Name.Split(':')[0], StringComparison.InvariantCultureIgnoreCase))?.GameId ?? 0;
	}
}
