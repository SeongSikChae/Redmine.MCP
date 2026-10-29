using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Redmine.MCP.Server
{
    public sealed class RedmineClient
    {
        public const int DefaultLimit = 25;
        public const int MaxLimit = 100;

        private readonly HttpClient _httpClient;

        public RedmineClient(HttpClient httpClient)
        {
            ArgumentNullException.ThrowIfNull(httpClient);
            _httpClient = httpClient;
        }

        public static int NormalizeLimit(int? limit)
        {
            int value = limit ?? DefaultLimit;
            if (value < 1)
            {
                return 1;
            }

            if (value > MaxLimit)
            {
                return MaxLimit;
            }

            return value;
        }

        public static int NormalizeOffset(int? offset)
        {
            if (offset is null || offset.Value < 0)
            {
                return 0;
            }

            return offset.Value;
        }

        public Task<string> GetAsync(string path, CancellationToken cancellationToken)
        {
            return SendAsync(HttpMethod.Get, path, query: null, body: null, cancellationToken);
        }

        public Task<string> GetAsync(
            string path,
            CancellationToken cancellationToken,
            params (string Key, string? Value)[] query)
        {
            return SendAsync(HttpMethod.Get, path, query, body: null, cancellationToken);
        }

        public Task<string> GetPagedAsync(
            string path,
            int? limit,
            int? offset,
            CancellationToken cancellationToken,
            params (string Key, string? Value)[] filters)
        {
            List<(string Key, string? Value)> query = new(filters);
            query.Add(("limit", NormalizeLimit(limit).ToString(CultureInfo.InvariantCulture)));
            query.Add(("offset", NormalizeOffset(offset).ToString(CultureInfo.InvariantCulture)));
            return SendAsync(HttpMethod.Get, path, query, body: null, cancellationToken);
        }

        public Task<string> PostAsync(string path, string wrapper, JsonObject payload, CancellationToken cancellationToken)
        {
            return SendWrappedAsync(HttpMethod.Post, path, wrapper, payload, cancellationToken);
        }

        public Task<string> PutAsync(string path, string wrapper, JsonObject payload, CancellationToken cancellationToken)
        {
            return SendWrappedAsync(HttpMethod.Put, path, wrapper, payload, cancellationToken);
        }

        public async Task<byte[]> GetBytesAsync(string url, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(url);
            Uri requestUri = ResolveDownloadUri(url);

            using HttpRequestMessage request = new(HttpMethod.Get, requestUri);
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            byte[] bytes = response.Content is null
                ? []
                : await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new RedmineApiException((int)response.StatusCode, Encoding.UTF8.GetString(bytes));
            }

            return bytes;
        }

        private Uri ResolveDownloadUri(string url)
        {
            if (_httpClient.BaseAddress is null)
            {
                throw new InvalidOperationException("HttpClient BaseAddress is required for attachment download.");
            }

            Uri baseAddress = _httpClient.BaseAddress;
            if (Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri? absolute))
            {
                EnsureSameOrigin(baseAddress, absolute);
                return absolute;
            }

            string relative = url.Trim().TrimStart('/');
            return new Uri(baseAddress, relative);
        }

        internal static void EnsureSameOrigin(Uri baseAddress, Uri candidate)
        {
            ArgumentNullException.ThrowIfNull(baseAddress);
            ArgumentNullException.ThrowIfNull(candidate);

            if (!string.Equals(baseAddress.Scheme, candidate.Scheme, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(baseAddress.IdnHost, candidate.IdnHost, StringComparison.OrdinalIgnoreCase)
                || baseAddress.Port != candidate.Port)
            {
                throw new InvalidOperationException(
                    $"Attachment URL origin must match the Redmine API endpoint ({baseAddress.GetLeftPart(UriPartial.Authority)}).");
            }
        }

        private Task<string> SendWrappedAsync(
            HttpMethod method,
            string path,
            string wrapper,
            JsonObject payload,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(wrapper);
            ArgumentNullException.ThrowIfNull(payload);
            JsonObject body = new()
            {
                [wrapper] = payload
            };
            return SendAsync(method, path, query: null, body, cancellationToken);
        }

        private async Task<string> SendAsync(
            HttpMethod method,
            string path,
            IReadOnlyList<(string Key, string? Value)>? query,
            JsonObject? body,
            CancellationToken cancellationToken)
        {
            using HttpRequestMessage request = new(method, BuildRelativeUri(path, query));
            if (body is not null)
            {
                request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            }

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            string responseBody = response.Content is null
                ? string.Empty
                : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new RedmineApiException((int)response.StatusCode, responseBody);
            }

            return responseBody;
        }

        internal static string BuildRelativeUri(string path, IReadOnlyList<(string Key, string? Value)>? query)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            string relative = path.TrimStart('/');
            if (query is null || query.Count == 0)
            {
                return relative;
            }

            List<string> pairs = [];
            foreach ((string key, string? value) in query)
            {
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                pairs.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
            }

            if (pairs.Count == 0)
            {
                return relative;
            }

            return relative + "?" + string.Join("&", pairs);
        }
    }
}
