using System;
using System.Diagnostics;
using Playnite.SDK;
using Playnite.SDK.Events;

namespace Osiris.Extensions.Exophase
{
    internal sealed class ExophaseAuthenticationService
    {
        internal const string AccountUrl = "https://www.exophase.com/account/";
        internal const string LoginUrl = "https://www.exophase.com/login/";

        private readonly IPlayniteAPI api;
        private readonly ILogger logger = LogManager.GetLogger();
        private bool loginCompleted;

        public ExophaseAuthenticationService(IPlayniteAPI api)
        {
            this.api = api ?? throw new ArgumentNullException(nameof(api));
        }

        public bool HasAuthenticatedSession()
        {
            using (var view = api.WebViews.CreateOffscreenView())
            {
                view.NavigateAndWait(AccountUrl);
                return IsAuthenticatedAddress(view.GetCurrentAddress());
            }
        }

        public bool Login()
        {
            var view = api.WebViews.CreateView(1225, 700);
            ExophaseAuthenticationWindow authenticationWindow = null;
            try
            {
                authenticationWindow = new ExophaseAuthenticationWindow(view);
                loginCompleted = false;
                view.LoadingChanged += CloseWhenLoggedIn;
                authenticationWindow.Navigate(AccountUrl);
                authenticationWindow.OpenDialog();
                return loginCompleted;
            }
            catch (Exception exception) when (!Debugger.IsAttached)
            {
                logger.Error(exception, "Failed to authenticate the Exophase account.");
                api.Dialogs.ShowErrorMessage(
                    "Osiris could not complete the Exophase sign-in.",
                    "Exophase");
                return false;
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

        public void ClearSession()
        {
            using (var view = api.WebViews.CreateOffscreenView())
            {
                view.DeleteDomainCookies(".exophase.com");
                view.DeleteDomainCookies("exophase.com");
                view.DeleteDomainCookies("www.exophase.com");
            }
        }

        internal static bool IsAuthenticatedAddress(string address)
        {
            Uri uri;
            if (!Uri.TryCreate(address, UriKind.Absolute, out uri))
            {
                return false;
            }

            var isExophase = string.Equals(uri.Host, "exophase.com", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(uri.Host, "www.exophase.com", StringComparison.OrdinalIgnoreCase);
            return isExophase && uri.AbsolutePath.StartsWith("/account", StringComparison.OrdinalIgnoreCase);
        }

        private void CloseWhenLoggedIn(object sender, WebViewLoadingChangedEventArgs e)
        {
            if (e.IsLoading)
            {
                return;
            }

            try
            {
                var view = (IWebView)sender;
                if (IsAuthenticatedAddress(view.GetCurrentAddress()))
                {
                    loginCompleted = true;
                    view.Close();
                }
            }
            catch (Exception exception)
            {
                logger.Warn(exception, "Failed to check Exophase authentication status.");
            }
        }
    }
}
