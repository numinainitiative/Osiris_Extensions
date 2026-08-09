using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Playnite.Common;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace XboxLibrary;

public class XboxUninstallController : UninstallController
{
	private CancellationTokenSource watcherToken;

	public XboxUninstallController(Game game)
		: base(game)
	{
		base.Name = "Uninstall";
	}

	public override void Dispose()
	{
		watcherToken?.Cancel();
	}

	public override void Uninstall(UninstallActionArgs args)
	{
		Dispose();
		ProcessStarter.StartUrl("ms-settings:appsfeatures");
		StartUninstallWatcher();
	}

	public async void StartUninstallWatcher()
	{
		watcherToken = new CancellationTokenSource();
		while (true)
		{
			if (watcherToken.IsCancellationRequested)
			{
				return;
			}
			if (Programs.GetUWPApps().FirstOrDefault((Program a) => a.AppId == base.Game.GameId) == null)
			{
				break;
			}
			await Task.Delay(10000);
		}
		InvokeOnUninstalled(new GameUninstalledEventArgs());
	}
}
