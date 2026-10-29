using System.Net.Http.Headers;

namespace Redmine.MCP.Server
{
    public static class RedmineHttpClientSetup
    {
        public const string ApiKeyHeaderName = "X-Redmine-API-Key";

        public static void Configure(HttpClient client, Configuration configuration)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(configuration);

            client.BaseAddress = CreateBaseAddress(configuration.Redmine!.RedmineApiEndpoint);
            client.DefaultRequestHeaders.Remove(ApiKeyHeaderName);
            if (!client.DefaultRequestHeaders.TryAddWithoutValidation(ApiKeyHeaderName, configuration.Redmine.RedmineApiKey))
            {
                throw new InvalidOperationException("Failed to set the Redmine API key header.");
            }

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public static Uri CreateBaseAddress(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new ArgumentException("Redmine API endpoint is required.", nameof(endpoint));
            }

            string trimmed = endpoint.Trim();
            if (!trimmed.EndsWith('/'))
            {
                trimmed += "/";
            }

            return new Uri(trimmed, UriKind.Absolute);
        }
    }
}
