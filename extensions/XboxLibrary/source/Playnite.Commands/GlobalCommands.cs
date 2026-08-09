using System;
using System.Diagnostics;
using System.IO;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace Playnite.Commands;

public static class GlobalCommands
{
	private static ILogger logger = LogManager.GetLogger();

	public static RelayCommand<object> NavigateUrlCommand => new RelayCommand<object>(delegate(object url)
	{
		try
		{
			NavigateUrl(url);
		}
		catch (Exception exception) when (!Debugger.IsAttached)
		{
			logger.Error(exception, "Failed to open url.");
		}
	});

	public static RelayCommand<string> NavigateDirectoryCommand => new RelayCommand<string>(delegate(string path)
	{
		try
		{
			if (Directory.Exists(path))
			{
				Process.Start(path);
			}
		}
		catch (Exception exception) when (!Debugger.IsAttached)
		{
			logger.Error(exception, "Failed to open directory.");
		}
	});

	public static void NavigateUrl(object url)
	{
		if (url is string url2)
		{
			NavigateUrl(url2);
			return;
		}
		if (url is Link link)
		{
			NavigateUrl(link.Url);
			return;
		}
		if (url is Uri uri)
		{
			NavigateUrl(uri.OriginalString);
			return;
		}
		throw new Exception("Unsupported URL format.");
	}

	public static void NavigateUrl(string url)
	{
		if (url.IsNullOrEmpty())
		{
			throw new Exception("No URL was given.");
		}
		if (!url.IsUri())
		{
			url = "http://" + url;
		}
		ProcessStarter.StartUrl(url);
	}
}
