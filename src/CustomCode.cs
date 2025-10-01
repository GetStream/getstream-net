using System.Text.Json;
using System.Text.Json.Serialization;
using GetStream.Models;

namespace GetStream
{
    /// <summary>
    /// Base exception for GetStream SDK errors
    /// </summary>
    public class GetStreamException : Exception
    {
        public GetStreamException(string message) : base(message) { }
        public GetStreamException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when API credentials are missing or invalid
    /// </summary>
    public class GetStreamAuthenticationException : GetStreamException
    {
        public GetStreamAuthenticationException(string message) : base(message) { }
        public GetStreamAuthenticationException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when API requests fail
    /// </summary>
    public class GetStreamApiException : GetStreamException
    {
        public int StatusCode { get; }
        public string ResponseBody { get; }

        public GetStreamApiException(string message, int statusCode, string responseBody) 
            : base(message)
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }

        public GetStreamApiException(string message, int statusCode, string responseBody, Exception innerException) 
            : base(message, innerException)
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }
    }

    /// <summary>
    /// Exception thrown when feed operations fail
    /// </summary>
    public class GetStreamFeedException : GetStreamException
    {
        public string FeedId { get; }

        public GetStreamFeedException(string message, string feedId) : base(message)
        {
            FeedId = feedId;
        }

        public GetStreamFeedException(string message, string feedId, Exception innerException) 
            : base(message, innerException)
        {
            FeedId = feedId;
        }
    }

    /// <summary>
    /// Exception thrown when validation fails
    /// </summary>
    public class GetStreamValidationException : GetStreamException
    {
        public string FieldName { get; }

        public GetStreamValidationException(string message, string fieldName) : base(message)
        {
            FieldName = fieldName;
        }

        public GetStreamValidationException(string message, string fieldName, Exception innerException) 
            : base(message, innerException)
        {
            FieldName = fieldName;
        }
    }

    /// <summary>
    /// Custom JSON converter for handling nanosecond timestamps
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
            } else if (reader.TokenType == JsonTokenType.String)
            {
                var tsString = reader.GetString();
                return tsString == null?DateTimeOffset.FromUnixTimeMilliseconds(0).DateTime:DateTimeOffset.Parse(tsString).DateTime;
            }
            
            throw new JsonException($"Cannot convert {reader.TokenType} to DateTime");
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            var timestamp = ((DateTimeOffset)value).ToUnixTimeMilliseconds() * 1000000;
            writer.WriteNumberValue(timestamp);
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