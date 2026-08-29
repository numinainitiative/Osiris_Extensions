using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Osiris.Extensions.SteamGridDBMetadata
{
    internal static class SteamGridDbFallbackTransport
    {
        private const int MaximumHeaderBytes = 64 * 1024;
        private const int MaximumApiBytes = 8 * 1024 * 1024;
        private const int MaximumArtworkBytes = 96 * 1024 * 1024;
        private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(7);
        private static readonly IPAddress[] AlternateCloudflareEdges =
        {
            IPAddress.Parse("104.21.49.38"),
            IPAddress.Parse("172.67.141.71")
        };
        private static readonly HttpClient ArtworkClient = CreateArtworkClient();
        private static long alternateRoutePreferredUntilUtcTicks;

        internal static bool ShouldPreferAlternateRoute
        {
            get
            {
                return Interlocked.Read(ref alternateRoutePreferredUntilUtcTicks) > DateTime.UtcNow.Ticks;
            }
        }

        internal static FallbackResponse GetApi(
            Uri uri,
            string authorization,
            CancellationToken cancellationToken)
        {
            return GetViaAlternateEdge(
                uri,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Authorization", authorization },
                    { "Accept", "application/json" }
                },
                MaximumApiBytes,
                cancellationToken);
        }

        internal static byte[] DownloadArtwork(string url, CancellationToken cancellationToken)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) || !IsSteamGridDbHost(uri.Host))
            {
                throw new InvalidOperationException("The artwork URL is not a SteamGridDB HTTPS address.");
            }

            if (!ShouldPreferAlternateRoute)
            {
                try
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                    using (var response = ArtworkClient.SendAsync(request, cancellationToken).GetAwaiter().GetResult())
                    {
                        response.EnsureSuccessStatusCode();
                        var bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                        if (bytes.Length > MaximumArtworkBytes)
                        {
                            throw new InvalidDataException("SteamGridDB artwork exceeds the 96 MB safety limit.");
                        }
                        return bytes;
                    }
                }
                catch (Exception exception)
                {
                    if (cancellationToken.IsCancellationRequested || !IsRetryableNetworkFailure(exception))
                    {
                        throw;
                    }
                }
            }

            var fallback = GetViaAlternateEdge(
                uri,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Accept", "image/png,image/jpeg,image/webp,image/gif,image/bmp,video/webm,*/*;q=0.8" }
                },
                MaximumArtworkBytes,
                cancellationToken);
            if (fallback.StatusCode < 200 || fallback.StatusCode >= 300)
            {
                throw new WebException(
                    "SteamGridDB artwork request failed (HTTP " +
                    fallback.StatusCode.ToString(CultureInfo.InvariantCulture) + ").");
            }
            return fallback.Body;
        }

        internal static bool IsRetryableNetworkFailure(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is HttpRequestException || current is WebException ||
                    current is SocketException || current is IOException ||
                    current is TimeoutException || current is TaskCanceledException)
                {
                    return true;
                }
            }
            return false;
        }

        private static FallbackResponse GetViaAlternateEdge(
            Uri uri,
            IDictionary<string, string> headers,
            int maximumBodyBytes,
            CancellationToken cancellationToken)
        {
            if (uri == null || uri.Scheme != Uri.UriSchemeHttps || !IsSteamGridDbHost(uri.Host))
            {
                throw new InvalidOperationException("SteamGridDB fallback only accepts its official HTTPS hosts.");
            }

            Exception lastError = null;
            foreach (var address in AlternateCloudflareEdges)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var response = GetFromAddress(uri, address, headers, maximumBodyBytes, cancellationToken);
                    Interlocked.Exchange(
                        ref alternateRoutePreferredUntilUtcTicks,
                        DateTime.UtcNow.AddMinutes(10).Ticks);
                    return response;
                }
                catch (Exception exception)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    lastError = exception;
                }
            }

            throw new WebException(
                "SteamGridDB could not be reached through its normal or alternate network route.",
                lastError);
        }

        private static FallbackResponse GetFromAddress(
            Uri uri,
            IPAddress address,
            IDictionary<string, string> headers,
            int maximumBodyBytes,
            CancellationToken cancellationToken)
        {
            using (var client = new TcpClient(AddressFamily.InterNetwork))
            {
                var connect = client.ConnectAsync(address, 443);
                WaitFor(connect, AttemptTimeout, cancellationToken, client);

                using (var ssl = new SslStream(client.GetStream(), false))
                {
                    var authenticate = ssl.AuthenticateAsClientAsync(
                        uri.Host,
                        null,
                        SslProtocols.Tls12,
                        false);
                    WaitFor(authenticate, AttemptTimeout, cancellationToken, client);

                    var request = BuildRequest(uri, headers);
                    ssl.Write(request, 0, request.Length);
                    ssl.Flush();
                    return ReadResponse(ssl, maximumBodyBytes, cancellationToken);
                }
            }
        }

        private static byte[] BuildRequest(Uri uri, IDictionary<string, string> headers)
        {
            var builder = new StringBuilder();
            builder.Append("GET ").Append(string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery)
                .Append(" HTTP/1.1\r\nHost: ").Append(uri.Host)
                .Append("\r\nUser-Agent: Osiris-SteamGridDBMetadata/1.0")
                .Append("\r\nAccept-Encoding: identity")
                .Append("\r\nConnection: close\r\n");
            foreach (var header in headers ?? new Dictionary<string, string>())
            {
                if (string.IsNullOrWhiteSpace(header.Key) || string.IsNullOrWhiteSpace(header.Value) ||
                    header.Key.IndexOfAny(new[] { '\r', '\n', ':' }) >= 0 ||
                    header.Value.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                {
                    continue;
                }
                builder.Append(header.Key).Append(": ").Append(header.Value).Append("\r\n");
            }
            builder.Append("\r\n");
            return Encoding.ASCII.GetBytes(builder.ToString());
        }

        private static FallbackResponse ReadResponse(
            Stream stream,
            int maximumBodyBytes,
            CancellationToken cancellationToken)
        {
            var headerBytes = new List<byte>();
            var matched = 0;
            while (matched < 4)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var value = stream.ReadByte();
                if (value < 0) throw new EndOfStreamException("SteamGridDB ended the response before its headers.");
                headerBytes.Add((byte)value);
                if (headerBytes.Count > MaximumHeaderBytes)
                    throw new InvalidDataException("SteamGridDB returned oversized response headers.");
                var expected = matched == 0 || matched == 2 ? '\r' : '\n';
                matched = value == expected ? matched + 1 : (value == '\r' ? 1 : 0);
            }

            var headerText = Encoding.ASCII.GetString(headerBytes.ToArray());
            var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
            if (lines.Length == 0 || !lines[0].StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("SteamGridDB returned an invalid HTTP response.");
            var statusParts = lines[0].Split(' ');
            int statusCode;
            if (statusParts.Length < 2 || !int.TryParse(statusParts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out statusCode))
                throw new InvalidDataException("SteamGridDB returned an invalid HTTP status.");

            var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in lines.Skip(1))
            {
                var separator = line.IndexOf(':');
                if (separator <= 0) continue;
                responseHeaders[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim();
            }

            byte[] body;
            string transferEncoding;
            string contentLengthText;
            if (responseHeaders.TryGetValue("Transfer-Encoding", out transferEncoding) &&
                transferEncoding.IndexOf("chunked", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                body = ReadChunkedBody(stream, maximumBodyBytes, cancellationToken);
            }
            else if (responseHeaders.TryGetValue("Content-Length", out contentLengthText))
            {
                int contentLength;
                if (!int.TryParse(contentLengthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out contentLength) ||
                    contentLength < 0 || contentLength > maximumBodyBytes)
                {
                    throw new InvalidDataException("SteamGridDB returned an invalid or oversized response body.");
                }
                body = ReadExact(stream, contentLength, cancellationToken);
            }
            else
            {
                body = ReadToEnd(stream, maximumBodyBytes, cancellationToken);
            }

            return new FallbackResponse(statusCode, body);
        }

        private static byte[] ReadChunkedBody(Stream stream, int maximumBytes, CancellationToken cancellationToken)
        {
            using (var output = new MemoryStream())
            {
                while (true)
                {
                    var line = ReadAsciiLine(stream, cancellationToken);
                    var separator = line.IndexOf(';');
                    var sizeText = (separator < 0 ? line : line.Substring(0, separator)).Trim();
                    int size;
                    if (!int.TryParse(sizeText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out size) || size < 0)
                        throw new InvalidDataException("SteamGridDB returned an invalid chunked response.");
                    if (size == 0)
                    {
                        while (!string.IsNullOrEmpty(ReadAsciiLine(stream, cancellationToken))) { }
                        break;
                    }
                    if (output.Length + size > maximumBytes)
                        throw new InvalidDataException("SteamGridDB response exceeds the safety limit.");
                    var chunk = ReadExact(stream, size, cancellationToken);
                    output.Write(chunk, 0, chunk.Length);
                    if (ReadAsciiLine(stream, cancellationToken).Length != 0)
                        throw new InvalidDataException("SteamGridDB returned an invalid chunk terminator.");
                }
                return output.ToArray();
            }
        }

        private static string ReadAsciiLine(Stream stream, CancellationToken cancellationToken)
        {
            using (var output = new MemoryStream())
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var value = stream.ReadByte();
                    if (value < 0) throw new EndOfStreamException();
                    if (value == '\r')
                    {
                        if (stream.ReadByte() != '\n') throw new InvalidDataException("Invalid HTTP line ending.");
                        return Encoding.ASCII.GetString(output.ToArray());
                    }
                    if (output.Length >= MaximumHeaderBytes) throw new InvalidDataException("Oversized HTTP line.");
                    output.WriteByte((byte)value);
                }
            }
        }

        private static byte[] ReadExact(Stream stream, int count, CancellationToken cancellationToken)
        {
            var buffer = new byte[count];
            var offset = 0;
            while (offset < count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = stream.Read(buffer, offset, count - offset);
                if (read <= 0) throw new EndOfStreamException();
                offset += read;
            }
            return buffer;
        }

        private static byte[] ReadToEnd(Stream stream, int maximumBytes, CancellationToken cancellationToken)
        {
            using (var output = new MemoryStream())
            {
                var buffer = new byte[81920];
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0) break;
                    if (output.Length + read > maximumBytes)
                        throw new InvalidDataException("SteamGridDB response exceeds the safety limit.");
                    output.Write(buffer, 0, read);
                }
                return output.ToArray();
            }
        }

        private static void WaitFor(Task task, TimeSpan timeout, CancellationToken cancellationToken, IDisposable abortTarget)
        {
            var delay = Task.Delay(timeout, cancellationToken);
            if (!ReferenceEquals(Task.WhenAny(task, delay).GetAwaiter().GetResult(), task))
            {
                abortTarget.Dispose();
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException("SteamGridDB alternate route timed out.");
            }
            task.GetAwaiter().GetResult();
        }

        private static bool IsSteamGridDbHost(string host)
        {
            return string.Equals(host, "www.steamgriddb.com", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(host, "steamgriddb.com", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(host, "cdn.steamgriddb.com", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(host, "cdn2.steamgriddb.com", StringComparison.OrdinalIgnoreCase);
        }

        private static HttpClient CreateArtworkClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(7) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Osiris-SteamGridDBMetadata/1.0");
            return client;
        }

        internal sealed class FallbackResponse
        {
            internal FallbackResponse(int statusCode, byte[] body)
            {
                StatusCode = statusCode;
                Body = body ?? new byte[0];
            }

            internal int StatusCode { get; private set; }
            internal byte[] Body { get; private set; }
        }
    }
}
