using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace GetStream
{
    /// <summary>Redaction helpers for the SDK's structured log events. Shallow by design.</summary>
    internal static class LogRedaction
    {
        internal const string Redacted = "<redacted>";
        private static readonly string[] QueryParams = { "api_key", "api_secret", "token" };
        private static readonly string[] BodyKeys = { "api_secret", "token", "password" };

        // CHA-2957 secret-leak guard: scrubs the same three secret query params wherever
        // they appear in a free-form string (e.g. a transport exception's Message), not
        // just in a well-formed query string.
        private static readonly Regex MessageSecretPattern = new(
            @"(api_key|api_secret|token)=[^&\s]*",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        internal static string RedactQuery(string query)
        {
            if (string.IsNullOrEmpty(query)) return query;
            return string.Join("&", query.Split('&').Select(pair =>
            {
                var idx = pair.IndexOf('=');
                if (idx < 0) return pair;
                var name = pair[..idx];
                return QueryParams.Contains(name.ToLowerInvariant()) ? $"{name}={Redacted}" : pair;
            }));
        }

        internal static string RedactJsonBody(string body)
        {
            if (string.IsNullOrEmpty(body)) return body;
            try
            {
                if (JsonNode.Parse(body) is not JsonObject obj) return body;
                var changed = false;
                foreach (var key in BodyKeys)
                {
                    if (obj.ContainsKey(key)) { obj[key] = Redacted; changed = true; }
                }
                return changed ? obj.ToJsonString() : body;
            }
            catch (JsonException)
            {
                return body;
            }
        }

        internal static string RedactMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return message;
            return MessageSecretPattern.Replace(message, m => $"{m.Groups[1].Value}={Redacted}");
        }
    }
}
