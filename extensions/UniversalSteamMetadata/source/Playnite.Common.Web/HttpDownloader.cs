using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using Playnite.SDK;

namespace Playnite.Common.Web;

public class HttpDownloader
{
	private static ILogger logger = LogManager.GetLogger();

	private static readonly HttpClient httpClient = new HttpClient();

	private static readonly Downloader downloader = new Downloader();

	public static string DownloadString(IEnumerable<string> mirrors)
	{
		return downloader.DownloadString(mirrors);
	}

	public static string DownloadString(string url)
	{
		return downloader.DownloadString(url);
	}

	public static string DownloadString(string url, CancellationToken cancelToken)
	{
		return downloader.DownloadString(url, cancelToken);
	}

	public static string DownloadString(string url, Encoding encoding)
	{
		return downloader.DownloadString(url, encoding);
	}

	public static string DownloadString(string url, List<Cookie> cookies)
	{
		return downloader.DownloadString(url, cookies);
	}

	public static string DownloadString(string url, List<Cookie> cookies, Encoding encoding)
	{
		return downloader.DownloadString(url, cookies, encoding);
	}

	public static void DownloadString(string url, string path)
	{
		downloader.DownloadString(url, path);
	}

	public static void DownloadString(string url, string path, Encoding encoding)
	{
		downloader.DownloadString(url, path, encoding);
	}

	public static byte[] DownloadData(string url)
	{
		return downloader.DownloadData(url);
	}

	public static byte[] DownloadData(string url, CancellationToken cancelToken)
	{
		return downloader.DownloadData(url, cancelToken);
	}

	public static void DownloadFile(string url, string path)
	{
		downloader.DownloadFile(url, path);
	}

	public static void DownloadFile(string url, string path, CancellationToken cancelToken)
	{
		downloader.DownloadFile(url, path, cancelToken);
	}

	public static HttpStatusCode GetResponseCode(string url, out Dictionary<string, string> headers)
	{
		headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			HttpResponseMessage result = httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, url)).GetAwaiter().GetResult();
			foreach (KeyValuePair<string, IEnumerable<string>> header in result.Headers)
			{
				headers.Add(header.Key, string.Join(",", header.Value));
			}
			foreach (KeyValuePair<string, IEnumerable<string>> header2 in result.Content.Headers)
			{
				headers.Add(header2.Key, string.Join(",", header2.Value));
			}
			return result.StatusCode;
		}
		catch (Exception exception)
		{
			logger.Error(exception, "Failed to get HTTP response for " + url + ".");
			return HttpStatusCode.ServiceUnavailable;
		}
	}
}
