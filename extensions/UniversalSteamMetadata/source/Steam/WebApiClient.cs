using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Playnite.SDK.Data;
using Steam.Models;

namespace Steam;

public class WebApiClient : IDisposable
{
	private readonly WebClient webClient;

	public WebApiClient()
	{
		webClient = new WebClient
		{
			Encoding = Encoding.UTF8
		};
	}

	public AppReviewsResult GetUserRating(uint appId)
	{
		string address = $"https://store.steampowered.com/appreviews/{appId}?json=1&purchase_type=all&language=all";
		return Serialization.FromJson<AppReviewsResult>(webClient.DownloadString(address));
	}

	public StoreAppDetailsResult.AppDetails GetStoreAppDetail(uint appId, string languageKey)
	{
		string address = $"https://store.steampowered.com/api/appdetails?appids={appId}&l={languageKey}";
		StoreAppDetailsResult storeAppDetailsResult = Serialization.FromJson<Dictionary<string, StoreAppDetailsResult>>(webClient.DownloadString(address))[appId.ToString()];
		if (!storeAppDetailsResult.success)
		{
			return null;
		}
		return storeAppDetailsResult.data;
	}

	public void Dispose()
	{
		webClient.Dispose();
	}
}
