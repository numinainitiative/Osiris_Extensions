using Playnite.SDK;
using Playnite.SDK.Events;
using SteamLibrary.Models;
using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;

namespace SteamLibrary.Services
{
    public class SteamStoreService
    {
        private const string recommendationQueueUrl = "https://store.steampowered.com/explore/";
        private readonly ILogger logger = LogManager.GetLogger();
        private IPlayniteAPI PlayniteApi { get; }
        private SteamUserToken? userTokenFromLogin;

        public SteamStoreService(IPlayniteAPI playniteApi)
        {
            PlayniteApi = playniteApi;
        }

        public async Task<SteamUserDataRoot> GetUserDataAsync()
        {
            var str = await DownloadPageSourceAsync("https://store.steampowered.com/dynamicstore/userdata/");

            if (str.Trim().StartsWith("<html", StringComparison.InvariantCultureIgnoreCase) &&
                str.IndexOf("<body", StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
                var body = Regex.Match(
                    str,
                    @"<body\b[^>]*>(?<content>[\s\S]*?)</body\s*>",
                    RegexOptions.IgnoreCase);
                if (body.Success)
                {
                    str = body.Groups["content"].Value;
                    str = Regex.Replace(
                        str,
                        @"^\s*<pre\b[^>]*>|</pre\s*>\s*$",
                        string.Empty,
                        RegexOptions.IgnoreCase);
                    str = HttpUtility.HtmlDecode(str);
                }
            }

            var serializer = new JavaScriptSerializer
            {
                MaxJsonLength = 16 * 1024 * 1024,
                RecursionLimit = 64
            };
            return serializer.Deserialize<SteamUserDataRoot>(str);
        }

        private async Task<string> DownloadPageSourceAsync(string url)
        {
            using var webView = PlayniteApi.WebViews.CreateOffscreenView();
            webView.NavigateAndWait(url);
            return await webView.GetPageSourceAsync();
        }

        private async Task<SteamUserToken?> GetSteamUserTokenFromWebViewAsync(IWebView webView)
        {
            var url = webView.GetCurrentAddress();
            if (url.Contains("/login"))
                return null;

            var source = await webView.GetPageSourceAsync();
            var userIdMatch = Regex.Match(source, "&quot;steamid&quot;:&quot;(?<id>[0-9]+)&quot;");
            var tokenMatch = Regex.Match(source, "&quot;webapi_token&quot;:&quot;(?<token>[^&]+)&quot;");

            if (!userIdMatch.Success || !tokenMatch.Success)
            {
                logger.Warn("Could not find Steam user ID or token");
                return null;
            }

            return new SteamUserToken
            {
                UserId = ulong.Parse(userIdMatch.Groups["id"].Value),
                AccessToken = tokenMatch.Groups["token"].Value,
            };
        }

        public async Task<SteamUserToken> GetAccessTokenAsync()
        {
            using var view = PlayniteApi.WebViews.CreateOffscreenView();

            view.NavigateAndWait(recommendationQueueUrl);

            return await GetSteamUserTokenFromWebViewAsync(view)
                   ?? throw new Exception(PlayniteApi.Resources.GetString(LOC.SteamNotLoggedInError));
        }

        public SteamUserToken? Login()
        {
            var view = PlayniteApi.WebViews.CreateView(1225, 700);
            SteamAuthenticationWindow authenticationWindow = null;
            try
            {
                authenticationWindow = new SteamAuthenticationWindow(view);
                view.LoadingChanged += CloseWhenLoggedIn;
                view.DeleteDomainCookies(".steamcommunity.com");
                view.DeleteDomainCookies("steamcommunity.com");
                view.DeleteDomainCookies("steampowered.com");
                view.DeleteDomainCookies("store.steampowered.com");
                view.DeleteDomainCookies("help.steampowered.com");
                view.DeleteDomainCookies("login.steampowered.com");
                authenticationWindow.Navigate(recommendationQueueUrl);

                userTokenFromLogin = null;
                authenticationWindow.OpenDialog();
                return userTokenFromLogin;
            }
            catch (Exception e) when (!Debugger.IsAttached)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(PlayniteApi.Resources.GetString(LOC.SteamNotLoggedInError), "");
                logger.Error(e, "Failed to authenticate user.");
                return null;
            }
            finally
            {
                authenticationWindow?.Dispose();
                if (view != null)
                {
                    view.LoadingChanged -= CloseWhenLoggedIn;
                    view.Dispose();
                }
            }
        }

        private async void CloseWhenLoggedIn(object sender, WebViewLoadingChangedEventArgs e)
        {
            try
            {
                if (e.IsLoading)
                    return;

                var view = (IWebView)sender;
                var token = await GetSteamUserTokenFromWebViewAsync(view);
                if (token?.AccessToken != null)
                {
                    userTokenFromLogin = token;
                    view.Close();
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to check authentication status");
            }
        }
    }
}
