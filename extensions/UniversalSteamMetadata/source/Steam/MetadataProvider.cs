using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using Playnite.Common.Web;
using Playnite.SDK;
using Playnite.SDK.Models;
using PlayniteExtensions.Common;
using Steam.Models;
using SteamKit2;
using SteamLibrary.SteamShared;

namespace Steam;

public class MetadataProvider
{
	private static readonly ILogger logger = LogManager.GetLogger();

	private readonly SteamApiClient apiClient;

	private readonly WebApiClient webApiClient;

	private readonly SteamTagNamer tagNamer;

	private readonly SharedSteamSettings settings;

	private static readonly string[] backgroundUrls = new string[2] { "https://steamcdn-a.akamaihd.net/steam/apps/{0}/page.bg.jpg", "https://steamcdn-a.akamaihd.net/steam/apps/{0}/page_bg_generated.jpg" };

	private static readonly string[] childGameTypes = new string[4] { "Demo", "Beta", "Tool", "Video" };

	public MetadataProvider(SteamApiClient apiClient, WebApiClient webApiClient, SteamTagNamer tagNamer, SharedSteamSettings settings)
	{
		this.apiClient = apiClient;
		this.webApiClient = webApiClient;
		this.tagNamer = tagNamer;
		this.settings = settings;
	}

	public static string GetWorkshopUrl(uint appId)
	{
		return $"https://steamcommunity.com/app/{appId}/workshop/";
	}

	public static string GetAchievementsUrl(uint appId)
	{
		return $"https://steamcommunity.com/stats/{appId}/achievements";
	}

	internal KeyValue GetAppInfo(uint appId)
	{
		try
		{
			return apiClient.GetProductInfo(appId).GetAwaiter().GetResult();
		}
		catch (Exception exception) when (!Debugger.IsAttached)
		{
			logger.Error(exception, $"Failed to get Steam appinfo {appId}");
			return null;
		}
	}

	private T SendDelayedStoreRequest<T>(Func<T> request, uint appId) where T : class
	{
		for (int i = 0; i < 10; i++)
		{
			try
			{
				return request();
			}
			catch (WebException ex)
			{
				if (i + 1 == 10)
				{
					logger.Error($"Reached download timeout for Steam store game {appId}");
					return null;
				}
				if (ex.Message.Contains("429"))
				{
					Thread.Sleep(2500);
					continue;
				}
				throw;
			}
		}
		return null;
	}

	private int CalculateUserScore(AppReviewsResult.QuerySummary reviews)
	{
		int num = reviews.total_positive + reviews.total_negative;
		double num2 = (double)reviews.total_positive / (double)num;
		return Convert.ToInt32((num2 - (num2 - 0.5) * Math.Pow(2.0, 0.0 - Math.Log10(num + 1))) * 100.0);
	}

	internal StoreAppDetailsResult.AppDetails GetStoreData(uint appId)
	{
		return SendDelayedStoreRequest(() => webApiClient.GetStoreAppDetail(appId, settings.LanguageKey), appId);
	}

	internal AppReviewsResult.QuerySummary GetUserReviewsData(uint appId)
	{
		AppReviewsResult appReviewsResult = SendDelayedStoreRequest(() => webApiClient.GetUserRating(appId), appId);
		if (appReviewsResult != null && appReviewsResult.success == 1)
		{
			return appReviewsResult.query_summary;
		}
		return null;
	}

	internal SteamGameMetadata DownloadGameMetadata(uint appId, BackgroundSource backgroundSource, bool downloadVerticalCovers)
	{
		SteamGameMetadata steamGameMetadata = new SteamGameMetadata();
		KeyValue keyValue = (steamGameMetadata.ProductDetails = GetAppInfo(appId));
		try
		{
			steamGameMetadata.StoreDetails = GetStoreData(appId);
		}
		catch (Exception exception)
		{
			logger.Error(exception, $"Failed to download Steam store metadata {appId}");
		}
		try
		{
			steamGameMetadata.UserReviewDetails = GetUserReviewsData(appId);
		}
		catch (Exception exception2)
		{
			logger.Error(exception2, $"Failed to download Steam user reviews metadata {appId}");
		}
		if (keyValue != null)
		{
			string format = "https://steamcdn-a.akamaihd.net/steamcommunity/public/images/apps/{0}/{1}.ico";
			KeyValue keyValue2 = keyValue["common"]["clienticon"];
			string text = string.Empty;
			if (!string.IsNullOrEmpty(keyValue2.Value))
			{
				text = string.Format(format, appId, keyValue2.Value);
			}
			else
			{
				KeyValue keyValue3 = keyValue["common"]["icon"];
				if (!string.IsNullOrEmpty(keyValue3.Value))
				{
					format = "https://steamcdn-a.akamaihd.net/steamcommunity/public/images/apps/{0}/{1}.jpg";
					text = string.Format(format, appId, keyValue3.Value);
				}
			}
			if (!string.IsNullOrEmpty(text))
			{
				steamGameMetadata.Icon = new MetadataFile(text);
			}
		}
		Dictionary<string, string> headers;
		if (downloadVerticalCovers)
		{
			string text2 = $"https://steamcdn-a.akamaihd.net/steam/apps/{appId}/library_600x900_2x.jpg";
			if (HttpDownloader.GetResponseCode(text2, out headers).IsSuccess())
			{
				steamGameMetadata.CoverImage = new MetadataFile(text2);
			}
		}
		else
		{
			string text3 = $"https://steamcdn-a.akamaihd.net/steam/apps/{appId}/header.jpg";
			if (HttpDownloader.GetResponseCode(text3, out headers).IsSuccess())
			{
				steamGameMetadata.CoverImage = new MetadataFile(text3);
			}
		}
		string text4 = $"https://steamcdn-a.akamaihd.net/steam/apps/{appId}/library_hero.jpg";
		string text5 = $"https://steamcdn-a.akamaihd.net/steam/apps/{appId}/page_bg_generated_v6b.jpg";
		switch (backgroundSource)
		{
		case BackgroundSource.Image:
		{
			string gameBackground = GetGameBackground(appId);
			if (string.IsNullOrEmpty(gameBackground))
			{
				steamGameMetadata.BackgroundImage = new MetadataFile(text4);
			}
			else
			{
				steamGameMetadata.BackgroundImage = new MetadataFile(gameBackground);
			}
			break;
		}
		case BackgroundSource.StoreScreenshot:
			if (steamGameMetadata.StoreDetails != null)
			{
				steamGameMetadata.BackgroundImage = new MetadataFile(Regex.Replace(steamGameMetadata.StoreDetails.screenshots.First().path_full, "\\?.*$", ""));
			}
			break;
		case BackgroundSource.StoreBackground:
			if (HttpDownloader.GetResponseCode(text5, out headers).IsSuccess())
			{
				steamGameMetadata.BackgroundImage = new MetadataFile(text5);
			}
			break;
		case BackgroundSource.Banner:
			if (HttpDownloader.GetResponseCode(text4, out headers).IsSuccess())
			{
				steamGameMetadata.BackgroundImage = new MetadataFile(text4);
			}
			break;
		}
		return steamGameMetadata;
	}

	public static string ParseDescription(string description)
	{
		description = description.Replace("%CDN_HOST_MEDIA_SSL%", "steamcdn-a.akamaihd.net");
		IHtmlDocument htmlDocument = new HtmlParser().Parse(description);
		foreach (IElement item in htmlDocument.QuerySelectorAll("video"))
		{
			string attribute = item.GetAttribute("poster");
			if (!attribute.IsNullOrWhiteSpace())
			{
				IElement element = htmlDocument.CreateElement("img");
				element.SetAttribute("src", attribute);
				item.Parent.ReplaceChild(element, item);
			}
			else
			{
				item.Parent.RemoveChild(item);
			}
		}
		foreach (IElement item2 in htmlDocument.QuerySelectorAll("img"))
		{
			item2.RemoveAttribute("height");
			item2.RemoveAttribute("width");
		}
		return htmlDocument.Body.InnerHtml;
	}

	public SteamGameMetadata GetGameMetadata(uint appId, BackgroundSource backgroundSource, bool downloadVerticalCovers, bool downloadParentMetadata = true)
	{
		logger.Trace($"Getting metadata for {appId}");
		SteamGameMetadata steamGameMetadata = DownloadGameMetadata(appId, backgroundSource, downloadVerticalCovers);
		string text = steamGameMetadata.ProductDetails?["common"]["name_localized"][settings.LanguageKey]?.Value;
		if (text != null)
		{
			steamGameMetadata.Name = text;
		}
		else
		{
			steamGameMetadata.Name = steamGameMetadata.ProductDetails?["common"]["name"]?.Value ?? steamGameMetadata.StoreDetails?.name;
		}
		if (steamGameMetadata.Name.IsNullOrWhiteSpace())
		{
			return steamGameMetadata;
		}
		steamGameMetadata.Name = steamGameMetadata.Name.RemoveTrademarks().Trim();
		steamGameMetadata.Links = new List<Link>
		{
			new Link(ResourceProvider.GetString("LOCSteamLinksCommunityHub"), $"https://steamcommunity.com/app/{appId}"),
			new Link(ResourceProvider.GetString("LOCSteamLinksDiscussions"), $"https://steamcommunity.com/app/{appId}/discussions/"),
			new Link(ResourceProvider.GetString("LOCSteamLinksGuides"), $"https://steamcommunity.com/app/{appId}/guides/"),
			new Link(ResourceProvider.GetString("LOCCommonLinksNews"), $"https://store.steampowered.com/news/?appids={appId}"),
			new Link(ResourceProvider.GetString("LOCCommonLinksStorePage"), $"https://store.steampowered.com/app/{appId}"),
			new Link("PCGamingWiki", $"https://pcgamingwiki.com/api/appid.php?appid={appId}")
		};
		if (steamGameMetadata.StoreDetails?.categories?.FirstOrDefault((StoreAppDetailsResult.AppDetails.Category a) => a.id == 22) != null)
		{
			steamGameMetadata.Links.Add(new Link(ResourceProvider.GetString("LOCCommonLinksAchievements"), GetAchievementsUrl(appId)));
		}
		if (steamGameMetadata.StoreDetails?.categories?.FirstOrDefault((StoreAppDetailsResult.AppDetails.Category a) => a.id == 30) != null)
		{
			steamGameMetadata.Links.Add(new Link(ResourceProvider.GetString("LOCSteamLinksWorkshop"), GetWorkshopUrl(appId)));
		}
		HashSet<MetadataProperty> hashSet = new HashSet<MetadataProperty>();
		IEnumerable<string> enumerable = null;
		IEnumerable<string> enumerable2 = null;
		if (steamGameMetadata.StoreDetails != null)
		{
			steamGameMetadata.Description = ParseDescription(steamGameMetadata.StoreDetails.about_the_game);
			TextInfo textInfo = new CultureInfo("en-US", useUserOverride: false).TextInfo;
			steamGameMetadata.ReleaseDate = DateHelper.ParseReleaseDate(steamGameMetadata.StoreDetails.release_date.date);
			steamGameMetadata.CriticScore = steamGameMetadata.StoreDetails.metacritic?.score;
			AppReviewsResult.QuerySummary userReviewDetails = steamGameMetadata.UserReviewDetails;
			if (userReviewDetails != null && userReviewDetails.total_reviews > 0)
			{
				steamGameMetadata.CommunityScore = CalculateUserScore(steamGameMetadata.UserReviewDetails);
			}
			enumerable = steamGameMetadata.StoreDetails.publishers?.Where((string a) => !a.IsNullOrWhiteSpace() && !a.Equals("N/A"));
			if (enumerable.HasItems())
			{
				steamGameMetadata.Publishers = ListExtensions.ToHashSet(enumerable.Select((string a) => new MetadataNameProperty(a)).Cast<MetadataProperty>());
			}
			enumerable2 = steamGameMetadata.StoreDetails.developers?.Where((string a) => !a.IsNullOrWhiteSpace() && !a.Equals("N/A"));
			if (enumerable2.HasItems())
			{
				steamGameMetadata.Developers = ListExtensions.ToHashSet(enumerable2.Select((string a) => new MetadataNameProperty(a)).Cast<MetadataProperty>());
			}
			steamGameMetadata.Features = hashSet;
			if (steamGameMetadata.StoreDetails.categories.HasItems())
			{
				foreach (StoreAppDetailsResult.AppDetails.Category category in steamGameMetadata.StoreDetails.categories)
				{
					if (category.id != 31)
					{
						if (category.description == "Steam Cloud")
						{
							category.description = "Cloud Saves";
						}
						hashSet.Add(new MetadataNameProperty(textInfo.ToTitleCase(category.description.Replace("steam", "", StringComparison.OrdinalIgnoreCase).Trim())));
					}
				}
			}
			if (steamGameMetadata.StoreDetails.genres.HasItems())
			{
				steamGameMetadata.Genres = ListExtensions.ToHashSet(steamGameMetadata.StoreDetails.genres.Select((StoreAppDetailsResult.AppDetails.Genre a) => new MetadataNameProperty(a.description)).Cast<MetadataProperty>());
			}
			if (steamGameMetadata.StoreDetails.platforms != null)
			{
				steamGameMetadata.Platforms = new HashSet<MetadataProperty>();
				if (steamGameMetadata.StoreDetails.platforms.windows)
				{
					steamGameMetadata.Platforms.Add(new MetadataSpecProperty("pc_windows"));
				}
				if (steamGameMetadata.StoreDetails.platforms.mac)
				{
					steamGameMetadata.Platforms.Add(new MetadataSpecProperty("macintosh"));
				}
				if (steamGameMetadata.StoreDetails.platforms.linux)
				{
					steamGameMetadata.Platforms.Add(new MetadataSpecProperty("pc_linux"));
				}
			}
			else
			{
				steamGameMetadata.Platforms = new HashSet<MetadataProperty>
				{
					new MetadataSpecProperty("pc_windows")
				};
			}
		}
		if (steamGameMetadata.ProductDetails != null)
		{
			bool flag = false;
			foreach (KeyValue child in steamGameMetadata.ProductDetails["common"]["playareavr"].Children)
			{
				if (child.Name == "seated" && child.Value == "1")
				{
					hashSet.Add(new MetadataNameProperty("VR Seated"));
					flag = true;
				}
				else if (child.Name == "standing" && child.Value == "1")
				{
					hashSet.Add(new MetadataNameProperty("VR Standing"));
					flag = true;
				}
				if (child.Name.Contains("roomscale"))
				{
					hashSet.Add(new MetadataNameProperty("VR Room-Scale"));
					flag = true;
				}
			}
			foreach (KeyValue child2 in steamGameMetadata.ProductDetails["common"]["controllervr"].Children)
			{
				if (child2.Name == "kbm" && child2.Value == "1")
				{
					hashSet.Add(new MetadataNameProperty("VR Keyboard / Mouse"));
					flag = true;
				}
				else if (child2.Name == "xinput" && child2.Value == "1")
				{
					hashSet.Add(new MetadataNameProperty("VR Gamepad"));
					flag = true;
				}
				if ((child2.Name == "oculus" && child2.Value == "1") || (child2.Name == "steamvr" && child2.Value == "1"))
				{
					hashSet.Add(new MetadataNameProperty("VR Motion Controllers"));
					flag = true;
				}
			}
			if (flag)
			{
				hashSet.Add(new MetadataNameProperty("VR"));
			}
			foreach (KeyValue child3 in steamGameMetadata.ProductDetails["common"]["associations"].Children)
			{
				if (child3["type"].Value == "franchise")
				{
					string value = HttpUtility.HtmlDecode(child3["name"].Value);
					if (enumerable != null && enumerable.Any((string x) => x.Contains(value, StringComparison.OrdinalIgnoreCase) || value.Contains(x, StringComparison.OrdinalIgnoreCase)))
					{
						logger.Debug("Franchise value \"" + value + "\" of game \"" + steamGameMetadata.Name + "\" matched a publisher name and was skipped");
					}
					else if (enumerable2 != null && enumerable2.Any((string x) => x.Contains(value, StringComparison.OrdinalIgnoreCase) || value.Contains(x, StringComparison.OrdinalIgnoreCase)))
					{
						logger.Debug("Franchise value \"" + value + "\" of game \"" + steamGameMetadata.Name + "\" matched a developer name and was skipped");
					}
					else
					{
						steamGameMetadata.Series = new HashSet<MetadataProperty>
						{
							new MetadataNameProperty(value)
						};
					}
					break;
				}
			}
			Dictionary<int, string> tagNames = tagNamer.GetTagNames();
			Dictionary<int, string> dictionary = null;
			List<KeyValue> list = steamGameMetadata.ProductDetails["common"]["store_tags"]?.Children;
			logger.Debug($"Setting tags for {appId}, found {list?.Count}");
			if (list != null)
			{
				steamGameMetadata.Tags = new HashSet<MetadataProperty>();
				if (settings.LimitTagsToFixedAmount)
				{
					list = list.Take(settings.FixedTagCount).ToList();
				}
				foreach (KeyValue item in list)
				{
					if (!int.TryParse(item.Value, out var result) || settings.BlacklistedTags.Contains(result))
					{
						continue;
					}
					if (!tagNames.TryGetValue(result, out var value2))
					{
						if (dictionary == null)
						{
							logger.Debug($"Tag {result} not found. Fetching new ones.");
							dictionary = tagNamer.UpdateAndGetTagNames();
						}
						if (!dictionary.TryGetValue(result, out value2))
						{
							logger.Warn($"Could not find tag name for tag {result}");
							continue;
						}
					}
					value2 = tagNamer.GetFinalTagName(value2, result);
					steamGameMetadata.Tags.Add(new MetadataNameProperty(value2));
				}
			}
			string steamDeckCompatibility = GetSteamDeckCompatibility(steamGameMetadata.ProductDetails);
			AddProperty(steamGameMetadata, settings.SteamDeckCompatibilityField, steamDeckCompatibility);
		}
		string value3 = steamGameMetadata.ProductDetails?["common"]["type"]?.Value;
		string s = steamGameMetadata.ProductDetails?["common"]["parent"]?.Value;
		if (downloadParentMetadata && childGameTypes.Contains(value3) && uint.TryParse(s, out var result2))
		{
			logger.Debug($"Getting parent metadata for {appId} from {result2}");
			SteamGameMetadata gameMetadata = GetGameMetadata(result2, backgroundSource, downloadVerticalCovers, downloadParentMetadata: false);
			steamGameMetadata.Links = gameMetadata.Links;
			steamGameMetadata.CoverImage = steamGameMetadata.CoverImage ?? gameMetadata.CoverImage;
			steamGameMetadata.BackgroundImage = steamGameMetadata.BackgroundImage ?? gameMetadata.BackgroundImage;
			if (string.IsNullOrWhiteSpace(steamGameMetadata.Description))
			{
				steamGameMetadata.Description = gameMetadata.Description;
			}
			if (steamGameMetadata.Tags == null || steamGameMetadata.Tags.Count == 0)
			{
				steamGameMetadata.Tags = gameMetadata.Tags;
			}
		}
		return steamGameMetadata;
	}

	private string GetGameBackground(uint appId)
	{
		string[] array = backgroundUrls;
		for (int i = 0; i < array.Length; i++)
		{
			string text = string.Format(array[i], appId);
			if (HttpDownloader.GetResponseCode(text, out var headers).IsSuccess() && headers.TryGetValue("Content-Length", out var value) && int.TryParse(value, out var result) && result < 10000000)
			{
				return text;
			}
		}
		return null;
	}

	private string GetSteamDeckCompatibility(KeyValue productDetails)
	{
		string value = productDetails["common"]["steam_deck_compatibility"]["category"].Value;
		SteamDeckCompatibility steamDeckCompatibility = SteamDeckCompatibility.Unknown;
		if (int.TryParse(value, out var result) && Enum.IsDefined(typeof(SteamDeckCompatibility), result))
		{
			steamDeckCompatibility = (SteamDeckCompatibility)result;
		}
		return $"Steam Deck {steamDeckCompatibility}";
	}

	private void AddProperty(GameMetadata game, GameField field, string value)
	{
		if (value == null)
		{
			return;
		}
		switch (field)
		{
		case GameField.Features:
			if (game.Features == null)
			{
				game.Features = new HashSet<MetadataProperty>();
			}
			game.Features.Add(new MetadataNameProperty(value));
			break;
		case GameField.Tags:
			if (game.Tags == null)
			{
				game.Tags = new HashSet<MetadataProperty>();
			}
			game.Tags.Add(new MetadataNameProperty(value));
			break;
		}
	}
}
