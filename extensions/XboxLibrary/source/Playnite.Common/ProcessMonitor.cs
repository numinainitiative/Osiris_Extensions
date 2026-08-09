using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Playnite.SDK;

namespace Playnite.Common;

public class ProcessMonitor : IDisposable
{
	public class TreeStartedEventArgs
	{
		public int StartedId { get; set; }
	}

	private SynchronizationContext execContext;

	private CancellationTokenSource watcherToken;

	private static ILogger logger = LogManager.GetLogger();

	private const int maxFailCount = 5;

	public event EventHandler<TreeStartedEventArgs> TreeStarted;

	public event EventHandler TreeDestroyed;

	public ProcessMonitor()
	{
		execContext = SynchronizationContext.Current;
	}

	public void Dispose()
	{
		StopWatching();
	}

	public async void WatchProcessTree(Process process)
	{
		await WatchProcess(process);
	}

	public async void WatchSingleProcess(Process process)
	{
		watcherToken = new CancellationTokenSource();
		while (!process.HasExited && !watcherToken.IsCancellationRequested)
		{
			await Task.Delay(1000);
		}
		OnTreeDestroyed();
	}

	public async void WatchDirectoryProcesses(string directory, bool alreadyRunning, bool byProcessNames = false, int trackingDelay = 2000)
	{
		logger.Debug($"Watching dir processes {directory}, {alreadyRunning}, {byProcessNames}");
		string directory2 = directory;
		try
		{
			directory2 = Paths.GetFinalPathName(directory);
		}
		catch (Exception exception)
		{
			logger.Error(exception, "Failed to get target path for a directory " + directory);
		}
		if (byProcessNames)
		{
			await WatchDirectoryByProcessNames(directory2, alreadyRunning, trackingDelay);
		}
		else
		{
			await WatchDirectory(directory2, alreadyRunning, trackingDelay);
		}
	}

	public void StopWatching()
	{
		watcherToken?.Cancel();
		watcherToken?.Dispose();
	}

	public async void WatchUwpApp(string familyName, bool alreadyRunning)
	{
		logger.Debug("Starting UWP " + familyName + " app watcher.");
		watcherToken = new CancellationTokenSource();
		bool startedCalled = false;
		bool processStarted = false;
		bool processFound = false;
		int foundProcessId = 0;
		int failCount = 0;
		string matchProcString = familyName.Replace("_", "_.+__");
		while (true)
		{
			if (watcherToken.IsCancellationRequested)
			{
				return;
			}
			if (failCount == 5)
			{
				OnTreeDestroyed();
				return;
			}
			try
			{
				processFound = false;
				foreach (Process item in from a in Process.GetProcesses()
					where a.SessionId != 0
					select a)
				{
					if (item.TryGetMainModuleFileName(out var fileName) && Regex.IsMatch(fileName, matchProcString))
					{
						processFound = true;
						processStarted = true;
						foundProcessId = item.Id;
						break;
					}
				}
			}
			catch (Exception exception) when (failCount < 5)
			{
				failCount++;
				logger.Error(exception, "WatchUwpApp failed to check processes.");
			}
			if (!alreadyRunning && processFound && !startedCalled)
			{
				OnTreeStarted(foundProcessId);
				startedCalled = true;
			}
			if (!processFound && processStarted)
			{
				break;
			}
			await Task.Delay(2000);
		}
		OnTreeDestroyed();
	}

	public static bool IsWatchableByProcessNames(string directory)
	{
		string path = directory;
		try
		{
			path = Paths.GetFinalPathName(directory);
		}
		catch (Exception exception)
		{
			logger.Error(exception, "Failed to get target path for a directory " + directory);
		}
		return Directory.GetFiles(path, "*.exe", SearchOption.AllDirectories).Count() > 0;
	}

	private async Task WatchDirectoryByProcessNames(string directory, bool alreadyRunning, int trackingDelay = 2000)
	{
		if (!Directory.Exists(directory))
		{
			throw new DirectoryNotFoundException("Cannot watch directory processes, " + directory + " not found.");
		}
		string[] files = Directory.GetFiles(directory, "*.exe", SearchOption.AllDirectories);
		if (files.Count() == 0)
		{
			logger.Error("Cannot watch directory processes " + directory + ", no executables found.");
			OnTreeDestroyed();
		}
		List<string> procNames = files.Select((string a) => Path.GetFileName(a)).ToList();
		List<string> procNamesNoExt = files.Select((string a) => Path.GetFileNameWithoutExtension(a)).ToList();
		watcherToken = new CancellationTokenSource();
		bool startedCalled = false;
		bool processStarted = false;
		int foundProcessId = 0;
		int failCount = 0;
		while (true)
		{
			if (watcherToken.IsCancellationRequested)
			{
				return;
			}
			if (failCount == 5)
			{
				OnTreeDestroyed();
				return;
			}
			bool flag = false;
			try
			{
				foreach (Process item in from a in Process.GetProcesses()
					where a.SessionId != 0
					select a)
				{
					if (item.TryGetMainModuleFileName(out var fileName))
					{
						if (procNames.Contains(Path.GetFileName(fileName)))
						{
							flag = true;
							processStarted = true;
							foundProcessId = item.Id;
							break;
						}
					}
					else if (procNamesNoExt.Contains(item.ProcessName))
					{
						flag = true;
						processStarted = true;
						foundProcessId = item.Id;
						break;
					}
				}
			}
			catch (Exception exception) when (failCount < 5)
			{
				failCount++;
				logger.Error(exception, "WatchDirectoryByProcessNames failed to check processes.");
			}
			if (!alreadyRunning && flag && !startedCalled)
			{
				OnTreeStarted(foundProcessId);
				startedCalled = true;
			}
			if (!flag && processStarted)
			{
				break;
			}
			await Task.Delay(trackingDelay);
		}
		OnTreeDestroyed();
	}

	private async Task WatchDirectory(string directory, bool alreadyRunning, int trackingDelay = 2000)
	{
		if (!Directory.Exists(directory))
		{
			throw new DirectoryNotFoundException("Cannot watch directory processes, " + directory + " not found.");
		}
		watcherToken = new CancellationTokenSource();
		bool startedCalled = false;
		bool processStarted = false;
		int foundProcessId = 0;
		int failCount = 0;
		while (true)
		{
			if (watcherToken.IsCancellationRequested)
			{
				return;
			}
			if (failCount == 5)
			{
				OnTreeDestroyed();
				return;
			}
			bool flag = false;
			try
			{
				foreach (Process item in from a in Process.GetProcesses()
					where a.SessionId != 0
					select a)
				{
					if (item.TryGetMainModuleFileName(out var fileName) && fileName.IndexOf(directory, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						flag = true;
						processStarted = true;
						foundProcessId = item.Id;
						break;
					}
				}
			}
			catch (Exception exception) when (failCount < 5)
			{
				failCount++;
				logger.Error(exception, "WatchDirectory failed to check processes.");
			}
			if (!alreadyRunning && flag && !startedCalled)
			{
				OnTreeStarted(foundProcessId);
				startedCalled = true;
			}
			if (!flag && processStarted)
			{
				break;
			}
			await Task.Delay(trackingDelay);
		}
		OnTreeDestroyed();
	}

	private async Task WatchProcess(Process process)
	{
		watcherToken = new CancellationTokenSource();
		List<int> ids = new List<int> { process.Id };
		int failCount = 0;
		while (true)
		{
			if (watcherToken.IsCancellationRequested)
			{
				return;
			}
			if (ids.Count == 0 || failCount == 5)
			{
				break;
			}
			try
			{
				IEnumerable<Process> enumerable = from a in Process.GetProcesses()
					where a.SessionId != 0
					select a;
				List<int> list = new List<int>();
				foreach (Process item in enumerable)
				{
					if (item.TryGetParentId(out var processId) && ids.Contains(processId) && !ids.Contains(item.Id))
					{
						ids.Add(item.Id);
					}
					if (ids.Contains(item.Id))
					{
						list.Add(item.Id);
					}
				}
				ids = list;
			}
			catch (Exception exception) when (failCount < 5)
			{
				failCount++;
				logger.Error(exception, "WatchProcess failed to check processes.");
			}
			await Task.Delay(500);
		}
		OnTreeDestroyed();
	}

	private void OnTreeStarted(int processId)
	{
		execContext.Post(delegate
		{
			this.TreeStarted?.Invoke(this, new TreeStartedEventArgs
			{
				StartedId = processId
			});
		}, null);
	}

	private void OnTreeDestroyed()
	{
		execContext.Post(delegate
		{
			this.TreeDestroyed?.Invoke(this, EventArgs.Empty);
		}, null);
	}
}
