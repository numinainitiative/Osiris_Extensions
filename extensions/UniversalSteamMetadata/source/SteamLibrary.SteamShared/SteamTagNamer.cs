using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web;
using Playnite.Common;
using Playnite.Common.Web;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Plugins;

namespace SteamLibrary.SteamShared;

public class SteamTagNamer
{
	private static string extensionFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

	private const string getTagListApiTemplate = "https://api.steampowered.com/IStoreService/GetTagList/v1?language={0}&have_version_hash={1}";

	private const string steamSharedFolder = "SteamShared";

	private readonly Plugin plugin;

	private readonly SharedSteamSettings settings;

	private readonly IDownloader downloader;

	private readonly Dictionary<int, TagCategory> tagCategoriesDictionary;

	private readonly Dictionary<TagCategory, string> tagCategoryStringMapping;

	private ILogger logger = LogManager.GetLogger();

	public SteamTagNamer(Plugin plugin, SharedSteamSettings settings, IDownloader downloader)
	{
		this.plugin = plugin;
		this.settings = settings;
		this.downloader = downloader;
		List<TagIdCategory> list = Serialization.FromJsonFile<List<TagIdCategory>>(Path.Combine(extensionFolder, "SteamShared", "TagCategories", "tagsCategories.json"));
		tagCategoriesDictionary = new Dictionary<int, TagCategory>();
		foreach (TagIdCategory item in list)
		{
			if (!tagCategoriesDictionary.ContainsKey(item.Id))
			{
				tagCategoriesDictionary[item.Id] = item.Category;
			}
		}
		tagCategoryStringMapping = new Dictionary<TagCategory, string>
		{
			{
				TagCategory.Assessments,
				ResourceProvider.GetString("LOCSteamTagCategoryAssessments")
			},
			{
				TagCategory.Features,
				ResourceProvider.GetString("LOCSteamTagCategoryFeatures")
			},
			{
				TagCategory.FundingEtc,
				ResourceProvider.GetString("LOCSteamTagCategoryFundingEtc")
			},
			{
				TagCategory.Genres,
				ResourceProvider.GetString("LOCSteamTagCategoryGenres")
			},
			{
				TagCategory.HardwareInput,
				ResourceProvider.GetString("LOCSteamTagCategoryHardwareInput")
			},
			{
				TagCategory.OtherTags,
				ResourceProvider.GetString("LOCSteamTagCategoryOtherTags")
			},
			{
				TagCategory.Players,
				ResourceProvider.GetString("LOCSteamTagCategoryPlayers")
			},
			{
				TagCategory.RatingsEtc,
				ResourceProvider.GetString("LOCSteamTagCategoryRatingsEtc")
			},
			{
				TagCategory.Software,
				ResourceProvider.GetString("LOCSteamTagCategorySoftware")
			},
			{
				TagCategory.SubGenres,
				ResourceProvider.GetString("LOCSteamTagCategorySubGenres")
			},
			{
				TagCategory.ThemesMoods,
				ResourceProvider.GetString("LOCSteamTagCategoryThemesMoods")
			},
			{
				TagCategory.TopLevelGenres,
				ResourceProvider.GetString("LOCSteamTagCategoryTopLevelGenres")
			},
			{
				TagCategory.VisualsViewpoint,
				ResourceProvider.GetString("LOCSteamTagCategoryVisualsViewpoint")
			}
		};
	}

	private string GetTagNameFilePath()
	{
		return Paths.FixPathLength(Path.Combine(plugin.GetPluginUserDataPath(), "tagnames-" + settings?.LanguageKey + ".json"));
	}

	private string GetPackedTagNameFilePath()
	{
		return Paths.FixPathLength(Path.Combine(extensionFolder, "SteamShared", "TagLocalization", settings?.LanguageKey + ".json"));
	}

	public Dictionary<int, string> GetTagNames()
	{
		string path = GetTagNameFilePath();
		if (!FileSystem.FileExists(path))
		{
			logger.Trace("No new tag names found for '" + settings?.LanguageKey + "'");
			path = GetPackedTagNameFilePath();
			if (!FileSystem.FileExists(path))
			{
				logger.Warn("No tag names found for language '" + settings?.LanguageKey + "'");
				return new Dictionary<int, string>();
			}
		}
		return GetFileContents(path).response.tags.ToDictionary((SteamTag x) => x.tagid, (SteamTag x) => x.name);
	}

	private SteamTagFile GetFileContents()
	{
		return GetFileContents(GetTagNameFilePath()) ?? GetFileContents(GetPackedTagNameFilePath());
	}

	private SteamTagFile GetFileContents(string path)
	{
		if (!FileSystem.FileExists(path))
		{
			return null;
		}
		logger.Debug("Opening " + path);
		try
		{
			return Serialization.FromJson<SteamTagFile>(File.ReadAllText(path, Encoding.UTF8));
		}
		catch (Exception exception)
		{
			logger.Error(exception, "Failed to read " + path);
			throw;
		}
	}

	public Dictionary<int, string> UpdateAndGetTagNames()
	{
		SteamTagFile fileContents = GetFileContents();
		string text = $"https://api.steampowered.com/IStoreService/GetTagList/v1?language={settings?.LanguageKey}&have_version_hash={fileContents?.response?.version_hash}";
		logger.Debug("Downloading " + text);
		string text2 = downloader.DownloadString(text);
		if (!text2.IsNullOrWhiteSpace())
		{
			File.WriteAllText(GetTagNameFilePath(), text2, Encoding.UTF8);
		}
		return GetFileContents().response.tags.ToDictionary((SteamTag x) => x.tagid, (SteamTag x) => x.name);
	}

	public string GetFinalTagName(string tagName, int tagId)
	{
		tagName = HttpUtility.HtmlDecode(tagName).Trim();
		if (settings.SetTagCategoryAsPrefix)
		{
			if (!tagCategoriesDictionary.TryGetValue(tagId, out var value))
			{
				value = TagCategory.OtherTags;
			}
			tagName = tagCategoryStringMapping[value] + ": " + tagName;
		}
		else if (settings.UseTagPrefix && !settings.TagPrefix.IsNullOrEmpty())
		{
			tagName = settings.TagPrefix + tagName;
		}
		return tagName;
	}
}
