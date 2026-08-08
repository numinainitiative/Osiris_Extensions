using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Web;
using System.Windows.Controls;
using AngleSharp.Dom;
using AngleSharp.Parser.Html;
using Playnite.Common.Web;
using Playnite.SDK;
using Playnite.SDK.Plugins;
using Steam.Models;

namespace UniversalSteamMetadata;

[LoadPlugin]
public class UniversalSteamMetadata : MetadataPluginBase<UniversalSteamMetadataSettingsViewModel>
{
	private const string searchUrl = "https://store.steampowered.com/search/?term={0}&ignore_preferences=1&category1=998&ndl=1";

	private readonly string[] backgroundUrls = new string[2] { "https://steamcdn-a.akamaihd.net/steam/apps/{0}/page.bg.jpg", "https://steamcdn-a.akamaihd.net/steam/apps/{0}/page_bg_generated.jpg" };

	private IDownloader downloader;

	public UniversalSteamMetadata(IPlayniteAPI api)
		: base("Steam Metadata", Guid.Parse("f2db8fb1-4981-4dc4-b087-05c782215b72"), new List<MetadataField>
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
			MetadataField.Tags
		}, (Func<UserControl>)(() => new UniversalSteamMetadataSettingsView()), (Func<MetadataRequestOptions, OnDemandMetadataProvider>)null, api)
	{
		base.Properties = new MetadataPluginProperties
		{
			HasSettings = true
		};
		base.SettingsViewModel = new UniversalSteamMetadataSettingsViewModel(this, api);
		downloader = new Downloader();
	}

	public override OnDemandMetadataProvider GetMetadataProvider(MetadataRequestOptions options)
	{
		return new UniversalSteamMetadataProvider(options, this, downloader);
	}

	public static List<StoreSearchResult> GetSearchResults(string searchTerm)
	{
		List<StoreSearchResult> list = new List<StoreSearchResult>();
		using WebClient webClient = new WebClient
		{
			Encoding = Encoding.UTF8
		};
		string source = webClient.DownloadString($"https://store.steampowered.com/search/?term={Uri.EscapeDataString(searchTerm)}&ignore_preferences=1&category1=998&ndl=1");
		foreach (IElement item in new HtmlParser().Parse(source).QuerySelectorAll(".search_result_row"))
		{
			string innerHtml = item.QuerySelector(".title").InnerHtml;
			string s = item.QuerySelector(".search_released").InnerHtml.Trim();
			if (!item.HasAttribute("data-ds-packageid"))
			{
				string attribute = item.GetAttribute("data-ds-appid");
				list.Add(new StoreSearchResult
				{
					Name = HttpUtility.HtmlDecode(innerHtml),
					Description = HttpUtility.HtmlDecode(s),
					GameId = uint.Parse(attribute)
				});
			}
		}
		return list;
	}
}
