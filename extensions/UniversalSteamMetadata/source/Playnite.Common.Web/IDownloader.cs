using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Playnite.Common.Web;

public interface IDownloader
{
	string DownloadString(IEnumerable<string> mirrors);

	string DownloadString(string url);

	string DownloadString(string url, Encoding encoding);

	string DownloadString(string url, List<Cookie> cookies);

	string DownloadString(string url, List<Cookie> cookies, Encoding encoding);

	void DownloadString(string url, string path);

	void DownloadString(string url, string path, Encoding encoding);

	byte[] DownloadData(string url);

	void DownloadFile(string url, string path);

	void DownloadFile(IEnumerable<string> mirrors, string path);

	Task DownloadFileAsync(string url, string path, Action<DownloadProgressChangedEventArgs> progressHandler);

	Task DownloadFileAsync(IEnumerable<string> mirrors, string path, Action<DownloadProgressChangedEventArgs> progressHandler);
}
