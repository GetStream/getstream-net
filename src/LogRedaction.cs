using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace GetStream
{
    /// <summary>Redaction helpers for the SDK's structured log events.</summary>
    internal static class LogRedaction
    {
        internal const string Redacted = "<redacted>";
        private static readonly string[] QueryParams = { "api_key", "api_secret", "token" };
        private static readonly string[] SensitiveKeySubstrings =
        {
            "secret",
            "token",
            "password",
            "credential",
            "private_key",
            "auth_key",
            "p12_cert",
            "stream_key",
        };

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

        private static bool IsSensitiveKey(string key)
        {
            var lower = key.ToLowerInvariant();
            foreach (var sub in SensitiveKeySubstrings)
            {
                if (lower.Contains(sub, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static bool RedactNode(JsonNode? node)
        {
            var changed = false;
            if (node is JsonObject obj)
            {
                var keys = obj.Select(kv => kv.Key).ToList();
                foreach (var key in keys)
                {
                    if (IsSensitiveKey(key))
                    {
                        obj[key] = Redacted;
                        changed = true;
                    }
                    else if (RedactNode(obj[key]))
                    {
                        changed = true;
                    }
                }
            }
            else if (node is JsonArray arr)
            {
                foreach (var item in arr)
                {
                    if (RedactNode(item))
                    {
                        changed = true;
                    }
                }
            }
            return changed;
        }

        internal static string RedactJsonBody(string body)
        {
            if (string.IsNullOrEmpty(body)) return body;
            try
            {
                var node = JsonNode.Parse(body);
                if (node is not (JsonObject or JsonArray)) return body;
                var changed = RedactNode(node);
                return changed ? node.ToJsonString() : body;
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
