using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using GetStream.Models;

namespace GetStream
{
    // Exception types live in Exceptions.cs. For per-status handling use:
    // catch (GetStreamApiException e) when (e.StatusCode is 401 or 403).

    /// <summary>
    /// Reads timestamps sent as nanosecond integers (response date-times) or strings,
    /// and writes every DateTime as an RFC 3339 UTC string.
    /// </summary>
    public class NanosecondTimestampConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                var timestamp = reader.GetInt64();
                // Convert nanoseconds to DateTime (assuming Unix epoch)
                return DateTimeOffset.FromUnixTimeMilliseconds(timestamp / 1000000).DateTime;
            }
            else if (reader.TokenType == JsonTokenType.String)
            {
                var tsString = reader.GetString();
                return tsString == null ? DateTimeOffset.FromUnixTimeMilliseconds(0).DateTime : DateTimeOffset.Parse(tsString).DateTime;
            }

            throw new JsonException($"Cannot convert {reader.TokenType} to DateTime");
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            // The API spec types every request date-time as an RFC 3339 string, and the
            // server rejects a number ("Time.UnmarshalJSON: input is not a JSON string").
            // Unspecified kind is treated as UTC because Read returns server timestamps that
            // way, so a value read from a response must be sent back unchanged.
            var utc = value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
            writer.WriteStringValue(utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Handles FeedOwnCapability serialization/deserialization.
    /// This is a workaround for API inconsistencies where FeedOwnCapability is sometimes returned as a string.
    /// </summary>
    public class FeedOwnCapabilityConverter : JsonConverter<FeedOwnCapability>
    {
        public override FeedOwnCapability Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var stringValue = reader.GetString();
                return new FeedOwnCapability();
            }

            throw new JsonException($"Cannot convert {reader.TokenType} to FeedOwnCapability");
        }

        public override void Write(Utf8JsonWriter writer, FeedOwnCapability value, JsonSerializerOptions options)
        {
            if (value != null)
            {
                writer.WriteStringValue("feed_own");
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }

    /// <summary>
    /// Handles ChannelOwnCapability serialization/deserialization.
    /// This is a workaround for API inconsistencies where ChannelOwnCapability is sometimes returned as a string.
    /// </summary>
    public class ChannelOwnCapabilityConverter : JsonConverter<ChannelOwnCapability>
    {
        public override ChannelOwnCapability Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                reader.GetString();
                return new ChannelOwnCapability();
            }

            throw new JsonException($"Cannot convert {reader.TokenType} to ChannelOwnCapability");
        }

        public override void Write(Utf8JsonWriter writer, ChannelOwnCapability value, JsonSerializerOptions options)
        {
            if (value != null)
            {
                writer.WriteStringValue("channel_own");
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }

    /// <summary>
    /// Handles OwnCapability serialization/deserialization.
    /// This is a workaround for API inconsistencies where OwnCapability is sometimes returned as a string.
    /// </summary>
    public class OwnCapabilityConverter : JsonConverter<OwnCapability>
    {
        public override OwnCapability Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                reader.GetString();
                return new OwnCapability();
            }

            throw new JsonException($"Cannot convert {reader.TokenType} to OwnCapability");
        }

        public override void Write(Utf8JsonWriter writer, OwnCapability value, JsonSerializerOptions options)
        {
            if (value != null)
            {
                writer.WriteStringValue("own");
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }

    /// <summary>
    /// Custom StreamResponse wrapper for API responses
    /// </summary>
    public class StreamResponse<T>
    {
        public T? Data { get; set; }
        public string? Duration { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Helper class for extracting query parameters from request objects
    /// </summary>
    public static class QueryParamsHelper
    {
        public static Dictionary<string, string>? ExtractQueryParams(object? request)
        {
            if (request == null) return null;

            var queryParams = new Dictionary<string, string>();
            var properties = request.GetType().GetProperties();

            foreach (var property in properties)
            {
                var value = property.GetValue(request);
                if (value != null)
                {
                    queryParams[property.Name.ToLowerInvariant()] = value.ToString()!;
                }
            }

            return queryParams.Count > 0 ? queryParams : null;
        }
    }
}