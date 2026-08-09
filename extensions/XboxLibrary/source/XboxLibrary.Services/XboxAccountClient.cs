using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Playnite.SDK;
using Playnite.SDK.Data;
using PlayniteExtensions.Common;
using XboxLibrary.Models;

namespace XboxLibrary.Services;

public class XboxAccountClient
{
	private static readonly ILogger logger = LogManager.GetLogger();

	private XboxLibrary library;

	private const string client_id = "38cd2fa8-66fd-4760-afb2-405eb65d5b0c";

	private const string redirect_uri = "https://login.live.com/oauth20_desktop.srf";

	private const string scope = "Xboxlive.signin Xboxlive.offline_access";

	private readonly string liveTokensPath;

	private readonly string xstsLoginTokesPath;

	public XboxAccountClient(XboxLibrary library)
	{
		this.library = library;
		liveTokensPath = Path.Combine(library.GetPluginUserDataPath(), "login.json");
		xstsLoginTokesPath = Path.Combine(library.GetPluginUserDataPath(), "xsts.json");
	}

	public async Task Login()
	{
		string callbackUrl = string.Empty;
		IWebView webView = library.PlayniteApi.WebViews.CreateView(1225, 700);
		XboxAuthenticationWindow authenticationWindow = null;
		try
		{
			authenticationWindow = new XboxAuthenticationWindow(webView);
			webView.LoadingChanged += delegate
			{
				string currentAddress = webView.GetCurrentAddress();
				if (currentAddress.Contains("code="))
				{
					callbackUrl = currentAddress;
					webView.Close();
				}
			};
			if (File.Exists(liveTokensPath))
			{
				File.Delete(liveTokensPath);
			}
			if (File.Exists(xstsLoginTokesPath))
			{
				File.Delete(xstsLoginTokesPath);
			}
			NameValueCollection nameValueCollection = HttpUtility.ParseQueryString(string.Empty);
			nameValueCollection.Add("client_id", "38cd2fa8-66fd-4760-afb2-405eb65d5b0c");
			nameValueCollection.Add("response_type", "code");
			nameValueCollection.Add("approval_prompt", "auto");
			nameValueCollection.Add("scope", "Xboxlive.signin Xboxlive.offline_access");
			nameValueCollection.Add("redirect_uri", "https://login.live.com/oauth20_desktop.srf");
			string url = "https://login.live.com/oauth20_authorize.srf?" + nameValueCollection.ToString();
			webView.DeleteDomainCookies(".live.com");
			webView.DeleteDomainCookies(".login.live.com");
			webView.DeleteDomainCookies("live.com");
			webView.DeleteDomainCookies("login.live.com");
			webView.DeleteDomainCookies(".xboxlive.com");
			webView.DeleteDomainCookies(".xbox.com");
			webView.DeleteDomainCookies(".microsoft.com");
			authenticationWindow.Navigate(url);
			authenticationWindow.OpenDialog();
		}
		finally
		{
			if (authenticationWindow != null)
			{
				authenticationWindow.Dispose();
			}
			if (webView != null)
			{
				webView.Dispose();
			}
		}
		if (!callbackUrl.IsNullOrEmpty())
		{
			string authorizationCode = HttpUtility.ParseQueryString(new Uri(callbackUrl).Query)["code"];
			RefreshTokenResponse refreshTokenResponse = await RequestOAuthToken(authorizationCode);
			AuthenticationData authenticationData = new AuthenticationData
			{
				AccessToken = refreshTokenResponse.access_token,
				RefreshToken = refreshTokenResponse.refresh_token,
				ExpiresIn = refreshTokenResponse.expires_in,
				CreationDate = DateTime.Now,
				TokenType = refreshTokenResponse.token_type,
				UserId = refreshTokenResponse.user_id
			};
			Encryption.EncryptToFile(liveTokensPath, Serialization.ToJson(authenticationData), Encoding.UTF8, WindowsIdentity.GetCurrent().User.Value);
			await Authenticate(authenticationData.AccessToken);
		}
	}

	public async Task<bool> GetIsUserLoggedIn()
	{
		try
		{
			if (!File.Exists(xstsLoginTokesPath))
			{
				return false;
			}
			AuthorizationData authorizationData;
			try
			{
				authorizationData = Serialization.FromJson<AuthorizationData>(Encryption.DecryptFromFile(xstsLoginTokesPath, Encoding.UTF8, WindowsIdentity.GetCurrent().User.Value));
			}
			catch (Exception exception)
			{
				logger.Error(exception, "Failed to load saved tokens.");
				return false;
			}
			using HttpClient client = new HttpClient();
			SetAuthenticationHeaders(client.DefaultRequestHeaders, authorizationData);
			ProfileRequest obj = new ProfileRequest
			{
				settings = new List<string> { "GameDisplayName" },
				userIds = new List<ulong> { ulong.Parse(authorizationData.DisplayClaims.xui[0].xid) }
			};
			return (await client.PostAsync("https://profile.xboxlive.com/users/batch/profile/settings", new StringContent(Serialization.ToJson(obj), Encoding.UTF8, "application/json"))).StatusCode == HttpStatusCode.OK;
		}
		catch (Exception exception2) when (!Debugger.IsAttached)
		{
			logger.Error(exception2, "Failed to check Xbox user loging status.");
			return false;
		}
	}

	private async Task<RefreshTokenResponse> RequestOAuthToken(string authorizationCode)
	{
		NameValueCollection nameValueCollection = HttpUtility.ParseQueryString(string.Empty);
		nameValueCollection.Add("grant_type", "authorization_code");
		nameValueCollection.Add("code", authorizationCode);
		return await ExecuteTokenRequest(nameValueCollection);
	}

	private async Task<RefreshTokenResponse> RefreshOAuthToken(string refreshToken)
	{
		NameValueCollection nameValueCollection = HttpUtility.ParseQueryString(string.Empty);
		nameValueCollection.Add("grant_type", "refresh_token");
		nameValueCollection.Add("refresh_token", refreshToken);
		return await ExecuteTokenRequest(nameValueCollection);
	}

	private async Task<RefreshTokenResponse> ExecuteTokenRequest(NameValueCollection requestData)
	{
		requestData.Add("scope", "Xboxlive.signin Xboxlive.offline_access");
		requestData.Add("client_id", "38cd2fa8-66fd-4760-afb2-405eb65d5b0c");
		requestData.Add("redirect_uri", "https://login.live.com/oauth20_desktop.srf");
		using HttpClient client = new HttpClient();
		HttpResponseMessage obj = await client.PostAsync("https://login.live.com/oauth20_token.srf", new StringContent(requestData.ToString(), Encoding.ASCII, "application/x-www-form-urlencoded"));
		obj.EnsureSuccessStatusCode();
		return Serialization.FromJson<RefreshTokenResponse>(await obj.Content.ReadAsStringAsync());
	}

	private async Task Authenticate(string accessToken)
	{
		using HttpClient client = new HttpClient();
		client.DefaultRequestHeaders.Add("x-xbl-contract-version", "1");
		string content = Serialization.ToJson(new AthenticationRequest
		{
			Properties =
			{
				RpsTicket = "d=" + accessToken
			}
		}, formatted: true);
		AuthorizationData authorizationData = Serialization.FromJson<AuthorizationData>(await (await client.PostAsync("https://user.auth.xboxlive.com/user/authenticate", new StringContent(content, Encoding.UTF8, "application/json"))).Content.ReadAsStringAsync());
		string content2 = Serialization.ToJson(new AuhtorizationRequest
		{
			Properties =
			{
				UserTokens = new List<string> { authorizationData.Token }
			}
		}, formatted: true);
		string text = await (await client.PostAsync("https://xsts.auth.xboxlive.com/xsts/authorize", new StringContent(content2, Encoding.UTF8, "application/json"))).Content.ReadAsStringAsync();
		Serialization.FromJson<AuthorizationData>(text);
		Encryption.EncryptToFile(xstsLoginTokesPath, text, Encoding.UTF8, WindowsIdentity.GetCurrent().User.Value);
	}

	internal async Task RefreshTokens()
	{
		logger.Debug("Refreshing xbox tokens.");
		AuthenticationData tokens;
		try
		{
			tokens = Serialization.FromJson<AuthenticationData>(Encryption.DecryptFromFile(liveTokensPath, Encoding.UTF8, WindowsIdentity.GetCurrent().User.Value));
		}
		catch (Exception exception)
		{
			logger.Error(exception, "Failed to load saved tokens.");
			return;
		}
		RefreshTokenResponse refreshTokenResponse = await RefreshOAuthToken(tokens.RefreshToken);
		tokens.AccessToken = refreshTokenResponse.access_token;
		tokens.RefreshToken = refreshTokenResponse.refresh_token;
		Encryption.EncryptToFile(liveTokensPath, Serialization.ToJson(tokens), Encoding.UTF8, WindowsIdentity.GetCurrent().User.Value);
		await Authenticate(tokens.AccessToken);
	}

	public async Task<List<TitleHistoryResponse.Title>> GetLibraryTitles()
	{
		if (!File.Exists(xstsLoginTokesPath))
		{
			throw new Exception("User is not authenticated.");
		}
		if (!(await GetIsUserLoggedIn()) && File.Exists(liveTokensPath))
		{
			await RefreshTokens();
		}
		if (!(await GetIsUserLoggedIn()))
		{
			throw new Exception("User is not authenticated.");
		}
		AuthorizationData savedXstsTokens = GetSavedXstsTokens();
		if (savedXstsTokens == null)
		{
			throw new Exception("User is not authenticated.");
		}
		using HttpClient client = new HttpClient();
		SetAuthenticationHeaders(client.DefaultRequestHeaders, savedXstsTokens);
		HttpResponseMessage obj = await client.GetAsync(string.Format("https://titlehub.xboxlive.com/users/xuid({0})/titles/titlehistory/decoration/{1}", savedXstsTokens.DisplayClaims.xui[0].xid, "detail"));
		if (obj.StatusCode != HttpStatusCode.OK)
		{
			throw new Exception("User is not authenticated.");
		}
		return Serialization.FromJson<TitleHistoryResponse>(await obj.Content.ReadAsStringAsync()).titles;
	}

	public async Task<List<UserStatsResponse.Stats>> GetUserStatsMinutesPlayed(IEnumerable<string> titleIds)
	{
		AuthorizationData savedXstsTokens = GetSavedXstsTokens();
		if (savedXstsTokens == null)
		{
			throw new Exception("User is not authenticated.");
		}
		using HttpClient client = new HttpClient();
		SetAuthenticationHeaders(client.DefaultRequestHeaders, savedXstsTokens);
		UserStatsRequest obj = new UserStatsRequest
		{
			arrangebyfield = "xuid",
			stats = titleIds.Select((string titleId) => new UserStatsRequest.Stats
			{
				name = "MinutesPlayed",
				titleid = titleId
			}).ToList(),
			xuids = new List<string> { savedXstsTokens.DisplayClaims.xui[0].xid }
		};
		HttpResponseMessage obj2 = await client.PostAsync("https://userstats.xboxlive.com/batch", new StringContent(Serialization.ToJson(obj), Encoding.UTF8, "application/json"));
		if (obj2.StatusCode != HttpStatusCode.OK)
		{
			throw new Exception("User is not authenticated.");
		}
		return Serialization.FromJson<UserStatsResponse>(await obj2.Content.ReadAsStringAsync())?.statlistscollection?.FirstOrDefault()?.stats ?? new List<UserStatsResponse.Stats>();
	}

	public async Task<TitleHistoryResponse.Title> GetTitleInfo(string pfn)
	{
		AuthorizationData savedXstsTokens = GetSavedXstsTokens();
		if (savedXstsTokens == null)
		{
			throw new Exception("User is not authenticated.");
		}
		using HttpClient client = new HttpClient();
		SetAuthenticationHeaders(client.DefaultRequestHeaders, savedXstsTokens);
		Dictionary<string, List<string>> obj = new Dictionary<string, List<string>>
		{
			{
				"pfns",
				new List<string> { pfn }
			},
			{
				"windowsPhoneProductIds",
				new List<string>()
			}
		};
		HttpResponseMessage httpResponseMessage = await client.PostAsync("https://titlehub.xboxlive.com/titles/batch/decoration/detail", new StringContent(Serialization.ToJson(obj), Encoding.UTF8, "application/json"));
		if (httpResponseMessage.StatusCode == HttpStatusCode.NotFound)
		{
			throw new Exception("Title info not available.");
		}
		if (httpResponseMessage.StatusCode != HttpStatusCode.OK)
		{
			throw new Exception("User is not authenticated.");
		}
		return Serialization.FromJson<TitleHistoryResponse>(await httpResponseMessage.Content.ReadAsStringAsync()).titles.First();
	}

	private void SetAuthenticationHeaders(HttpRequestHeaders headers, AuthorizationData auth)
	{
		headers.Add("x-xbl-contract-version", "2");
		headers.Add("Authorization", "XBL3.0 x=" + auth.DisplayClaims.xui[0].uhs + ";" + auth.Token);
		headers.Add("Accept-Language", "en-US");
	}

	private AuthorizationData GetSavedXstsTokens()
	{
		try
		{
			return Serialization.FromJson<AuthorizationData>(Encryption.DecryptFromFile(xstsLoginTokesPath, Encoding.UTF8, WindowsIdentity.GetCurrent().User.Value));
		}
		catch (Exception exception)
		{
			logger.Error(exception, "Failed to load saved tokens.");
			return null;
		}
	}
}
