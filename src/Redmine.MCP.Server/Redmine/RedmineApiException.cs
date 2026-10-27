namespace Redmine.MCP.Server
{
    public sealed class RedmineApiException : Exception
    {
        public RedmineApiException(int statusCode, string responseBody)
            : base($"Redmine API request failed with status {statusCode}. {responseBody}")
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }

        public int StatusCode { get; }

        public string ResponseBody { get; }
    }
}
