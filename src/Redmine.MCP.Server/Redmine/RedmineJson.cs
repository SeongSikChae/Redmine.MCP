using System.Globalization;
using System.Text.Json.Nodes;

namespace Redmine.MCP.Server
{
    internal static class RedmineJson
    {
        public static void Set(JsonObject target, string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                target[name] = value;
            }
        }

        public static void Set(JsonObject target, string name, int? value)
        {
            if (value is not null)
            {
                target[name] = value.Value;
            }
        }

        public static void Set(JsonObject target, string name, double? value)
        {
            if (value is not null)
            {
                target[name] = value.Value;
            }
        }

        public static void SetId(JsonObject target, string name, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string trimmed = value.Trim();
            if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
            {
                target[name] = number;
            }
            else
            {
                target[name] = trimmed;
            }
        }
    }
}
