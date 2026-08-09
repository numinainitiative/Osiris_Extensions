using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace XboxLibrary;

public class XboxPlayController : PlayController
{
	private static ILogger logger = LogManager.GetLogger();

	private ProcessMonitor procMon;

	private Stopwatch stopWatch;

	public XboxPlayController(Game game)
		: base(game)
	{
		base.Name = game.Name;
	}

	public override void Dispose()
	{
		procMon?.Dispose();
	}

	public override void Play(PlayActionArgs args)
	{
		Dispose();
		if (base.Game.GameId.StartsWith("CONSOLE"))
		{
			throw new Exception("We can't start console only games, the technology is not there yet.");
		}
		Program program = Programs.GetUWPApps().FirstOrDefault((Program a) => a.AppId == base.Game.GameId);
		if (program == null)
		{
			throw new Exception("Cannot start UWP game, installation not found.");
		}
		ProcessStarter.StartProcess(program.Path, program.Arguments);
		procMon = new ProcessMonitor();
		procMon.TreeDestroyed += Monitor_TreeDestroyed;
		procMon.TreeStarted += ProcMon_TreeStarted;
		if (Directory.Exists(program.WorkDir) && ProcessMonitor.IsWatchableByProcessNames(program.WorkDir))
		{
			procMon.WatchDirectoryProcesses(program.WorkDir, alreadyRunning: false);
		}
		else
		{
			InvokeOnStopped(new GameStoppedEventArgs());
		}
	}

	private void ProcMon_TreeStarted(object sender, ProcessMonitor.TreeStartedEventArgs args)
	{
		stopWatch = Stopwatch.StartNew();
		InvokeOnStarted(new GameStartedEventArgs
		{
			StartedProcessId = args.StartedId
		});
	}

	private void Monitor_TreeDestroyed(object sender, EventArgs args)
	{
		stopWatch?.Stop();
		InvokeOnStopped(new GameStoppedEventArgs(Convert.ToUInt64(stopWatch?.Elapsed.TotalSeconds ?? 0.0)));
	}
}
