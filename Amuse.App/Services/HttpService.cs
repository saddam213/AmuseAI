using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.WPF.Services;

namespace Amuse.App.Services
{
    public sealed class HttpService : IHttpService
    {
        private readonly Settings _settings;

        public HttpService(Settings settings)
        {
            _settings = settings;
            Client = new HttpClient(new InlineAuthHandler(GetTokenForUrl)
            {
                InnerHandler = new HttpClientHandler()
                {
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
                }
            });
            Client.DefaultRequestHeaders.UserAgent.ParseAdd(settings.UserAgent);
        }

        public HttpClient Client { get; }


        private string GetTokenForUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            {
                // Match the host exactly or a subdomain, never a substring of the full url
                var host = uri.Host;
                var accessToken = _settings.AccessTokens?.FirstOrDefault(x => !string.IsNullOrEmpty(x.Domain)
                    && (host.Equals(x.Domain, StringComparison.OrdinalIgnoreCase) || host.EndsWith($".{x.Domain}", StringComparison.OrdinalIgnoreCase)));
                if (accessToken != null)
                    return accessToken.Token;
            }
            return null;
        }


        private class InlineAuthHandler : DelegatingHandler
        {
            private readonly Func<string, string> _tokenLookup;

            public InlineAuthHandler(Func<string, string> tokenLookup)
            {
                _tokenLookup = tokenLookup;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string url = request.RequestUri?.ToString() ?? string.Empty;
                string token = _tokenLookup(url);
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                return await base.SendAsync(request, cancellationToken);
            }
        }
    }
}
