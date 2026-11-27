using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Amuse.App.Tools
{
    public static class ToolHelper
    {
        private static readonly Regex _removeBlocksRegex = new Regex(@"<(script|style|noscript|svg|head|template)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex _commentRegex = new Regex(@"<!--.*?-->", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex _blockTagRegex = new Regex(@"<\s*(br|/p|/div|/li|/tr|/h[1-6]|/section|/article|/header|/footer|/table|/ul|/ol|/blockquote|/pre)\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex _listItemRegex = new Regex(@"<\s*li\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex _tagRegex = new Regex(@"<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex _titleRegex = new Regex(@"<title\b[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex _spacesRegex = new Regex(@"[ \t\f\v ]+", RegexOptions.Compiled);
        private static readonly Regex _newLinesRegex = new Regex(@"\s*\n\s*(\n\s*)+", RegexOptions.Compiled);
        private static readonly TimeSpan _connectAttemptTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Converts HTML to readable plain text.
        /// </summary>
        /// <param name="html">The HTML.</param>
        public static string HtmlToText(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            // Remove non-content
            var text = _commentRegex.Replace(html, string.Empty);
            text = _removeBlocksRegex.Replace(text, string.Empty);

            // Structure
            text = _listItemRegex.Replace(text, "\n- ");
            text = _blockTagRegex.Replace(text, "\n");
            text = _tagRegex.Replace(text, " ");

            // Normalize
            text = WebUtility.HtmlDecode(text).Replace("\r", string.Empty);
            text = _spacesRegex.Replace(text, " ");
            text = _newLinesRegex.Replace(text, "\n\n");
            return text.Trim();
        }


        /// <summary>
        /// Gets the HTML document title.
        /// </summary>
        /// <param name="html">The HTML.</param>
        public static string HtmlTitle(string html)
        {
            if (string.IsNullOrEmpty(html))
                return null;

            var match = _titleRegex.Match(html);
            return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value).Trim() : null;
        }


        /// <summary>
        /// Truncates the text to the maximum number of characters.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="maxChars">The maximum characters.</param>
        /// <param name="isTruncated">if set to <c>true</c> the text was truncated.</param>
        public static string Truncate(string text, int maxChars, out bool isTruncated)
        {
            isTruncated = false;
            if (string.IsNullOrEmpty(text) || maxChars <= 0 || text.Length <= maxChars)
                return text;

            isTruncated = true;
            return text[..maxChars];
        }


        /// <summary>
        /// Validates an absolute http or https url.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <exception cref="System.ArgumentException">Only absolute http or https urls are supported.</exception>
        public static Uri GetWebUri(string url)
        {
            if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new ArgumentException($"'{url}' is not a valid http or https url.");

            return uri;
        }


        /// <summary>
        /// Gets the full local path, network and device paths are rejected before the file system is touched.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <exception cref="System.ArgumentException">Network and device paths are not supported.</exception>
        public static string GetLocalPath(string path)
        {
            var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));
            if (fullPath.StartsWith(@"\\", StringComparison.Ordinal) || fullPath.StartsWith("//", StringComparison.Ordinal))
                throw new ArgumentException($"'{path}' is a network or device path, only local paths are supported.");

            return fullPath;
        }


        /// <summary>
        /// Creates the HTTP client used by tools, it carries no access tokens and only connects to public addresses.
        /// </summary>
        /// <param name="userAgent">The user agent.</param>
        public static HttpClient CreateHttpClient(string userAgent)
        {
            var client = new HttpClient(new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                ConnectCallback = ConnectPublicAsync
            });
            client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            return client;
        }


        /// <summary>
        /// Determines whether the address is a public internet address.
        /// </summary>
        /// <param name="address">The address.</param>
        public static bool IsPublicAddress(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6)
                address = address.MapToIPv4();

            if (IPAddress.IsLoopback(address))
                return false;

            var bytes = address.GetAddressBytes();
            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                var isUniqueLocal = (bytes[0] & 0xFE) == 0xFC;
                return !(isUniqueLocal || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast || address.Equals(IPAddress.IPv6Any));
            }

            return bytes[0] switch
            {
                0 or 10 or 127 => false,
                100 => bytes[1] < 64 || bytes[1] > 127,
                169 => bytes[1] != 254,
                172 => bytes[1] < 16 || bytes[1] > 31,
                192 => bytes[1] != 168,
                >= 224 => false,
                _ => true
            };
        }


        /// <summary>
        /// Determines whether the host name is a local name or a non-public address literal.
        /// </summary>
        /// <param name="host">The host.</param>
        public static bool IsLocalHost(string host)
        {
            if (IPAddress.TryParse(host, out var address))
                return !IsPublicAddress(address);

            return !host.Contains('.')
                || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
        }


        /// <summary>
        /// Connects to the first public address of the host that accepts the connection, checked after DNS so redirects cannot reach local services.
        /// The addresses are tried in turn, so a host that also has an IPv6 address still connects on a network that only routes IPv4.
        /// A connection to the system proxy is allowed, the proxy resolves the host so only local names and address literals are rejected.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <exception cref="System.Net.Http.HttpRequestException">Host is not a public internet address.</exception>
        private static async ValueTask<Stream> ConnectPublicAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
        {
            var endPoint = context.DnsEndPoint;
            var requestUri = context.InitialRequestMessage.RequestUri;
            var isProxy = IsProxyEndPoint(endPoint, requestUri);
            if (isProxy && IsLocalHost(requestUri.Host))
                throw new HttpRequestException($"'{requestUri.Host}' is not a public internet address.");

            var hostAddresses = await Dns.GetHostAddressesAsync(endPoint.Host, cancellationToken);
            var addresses = Array.FindAll(hostAddresses, x => isProxy || IsPublicAddress(x));
            if (addresses.Length == 0)
                throw new HttpRequestException($"'{endPoint.Host}' is not a public internet address.");

            var stream = default(Stream);
            for (var i = 0; i < addresses.Length && stream == null; i++)
            {
                var isLastAddress = i == addresses.Length - 1;
                stream = await ConnectAddressAsync(new IPEndPoint(addresses[i], endPoint.Port), isLastAddress, cancellationToken);
            }
            return stream;
        }


        /// <summary>
        /// Connects to an address of a host. While other addresses remain, a connection that fails or takes too long returns <c>null</c> so the next address is tried, the last address throws.
        /// </summary>
        /// <param name="endPoint">The address and port.</param>
        /// <param name="isLastAddress">if set to <c>true</c> there is no other address to try.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private static async Task<Stream> ConnectAddressAsync(IPEndPoint endPoint, bool isLastAddress, CancellationToken cancellationToken)
        {
            var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                using (var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    if (!isLastAddress)
                        timeoutSource.CancelAfter(_connectAttemptTimeout);

                    await socket.ConnectAsync(endPoint, timeoutSource.Token);
                    return new NetworkStream(socket, true);
                }
            }
            catch (Exception ex) when (!isLastAddress && !cancellationToken.IsCancellationRequested && (ex is SocketException || ex is OperationCanceledException))
            {
                socket.Dispose();
                return null;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }


        /// <summary>
        /// Determines whether the endpoint is the system proxy for the request and not the request host itself.
        /// </summary>
        /// <param name="endPoint">The end point being connected.</param>
        /// <param name="requestUri">The request URI.</param>
        private static bool IsProxyEndPoint(DnsEndPoint endPoint, Uri requestUri)
        {
            var proxy = HttpClient.DefaultProxy;
            if (proxy == null || proxy.IsBypassed(requestUri))
                return false;

            var proxyUri = proxy.GetProxy(requestUri);
            if (proxyUri == null)
                return false;

            var isRequestHost = requestUri.Host.Equals(endPoint.Host, StringComparison.OrdinalIgnoreCase) && requestUri.Port == endPoint.Port;
            return !isRequestHost
                && proxyUri.Host.Equals(endPoint.Host, StringComparison.OrdinalIgnoreCase)
                && proxyUri.Port == endPoint.Port;
        }


        /// <summary>
        /// Converts bytes to gigabytes.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        public static double ToGB(double bytes)
        {
            return Math.Round(bytes / 1024d / 1024d / 1024d, 1);
        }
    }
}
