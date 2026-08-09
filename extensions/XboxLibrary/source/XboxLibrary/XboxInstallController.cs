using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Playnite.Common;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace XboxLibrary;

public class XboxInstallController : InstallController
{
	private readonly bool userXboxApp;

	private CancellationTokenSource watcherToken;

	public XboxInstallController(Game game, bool useXboxApp)
		: base(game)
	{
		userXboxApp = useXboxApp;
		base.Name = (useXboxApp ? "Install using Xbox app" : "Install using MS Store");
	}

	public override void Dispose()
	{
		watcherToken?.Cancel();
	}

	public override void Install(InstallActionArgs args)
	{
		Dispose();
		if (userXboxApp)
		{
			Xbox.OpenXboxPassApp();
		}
		else
		{
			ProcessStarter.StartUrl("ms-windows-store://pdp/?PFN=" + base.Game.GameId);
		}
		StartInstallWatcher();
	}

	public async void StartInstallWatcher()
	{
		watcherToken = new CancellationTokenSource();
		await Task.Run(async delegate
		{
			Program program;
			while (true)
			{
				if (watcherToken.IsCancellationRequested)
				{
					return;
				}
				program = Programs.GetUWPApps().FirstOrDefault((Program a) => a.AppId == base.Game.GameId);
				if (program != null)
				{
					break;
				}
				await Task.Delay(10000);
			}
			GameInstallationData installData = new GameInstallationData
			{
				InstallDirectory = program.WorkDir
			};
			InvokeOnInstalled(new GameInstalledEventArgs(installData));
		});
	}
}
