using System;
using System.Collections.Generic;

namespace GetStream
{
    /// <summary>
    /// Abstract base for all SDK exceptions.
    /// </summary>
    public class GetStreamException : Exception
    {
        public GetStreamException(string message) : base(message) { }
        public GetStreamException(string message, Exception? innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Thrown when the backend returns 4xx/5xx with a parsable APIError envelope,
    /// or when an HTTP response is received but the body is unparseable.
    /// </summary>
    public class GetStreamApiException : GetStreamException
    {
        public int StatusCode { get; }
        public int Code { get; }
        public IReadOnlyDictionary<string, string> ExceptionFields { get; }
        public bool Unrecoverable { get; }
        public string RawResponseBody { get; }
        public string? MoreInfo { get; }
        public object? Details { get; }

        // Back-compat alias for RawResponseBody.
        [Obsolete("Use RawResponseBody")]
        public string ResponseBody => RawResponseBody;

        public GetStreamApiException(
            string message,
            int statusCode,
            int code,
            IReadOnlyDictionary<string, string> exceptionFields,
            bool unrecoverable,
            string rawResponseBody,
            string? moreInfo = null,
            object? details = null,
            Exception? innerException = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
            Code = code;
            ExceptionFields = exceptionFields ?? new Dictionary<string, string>();
            Unrecoverable = unrecoverable;
            RawResponseBody = rawResponseBody ?? string.Empty;
            MoreInfo = moreInfo;
            Details = details;
        }
    }

    /// <summary>
    /// Thrown on HTTP 429. Subclass of <see cref="GetStreamApiException"/> so a
    /// single <c>catch (GetStreamApiException)</c> still handles it.
    /// </summary>
    public class GetStreamRateLimitException : GetStreamApiException
    {
        /// <summary>
        /// Parsed from the <c>Retry-After</c> response header.
        /// Null when the header is absent or unparseable.
        /// </summary>
        public TimeSpan? RetryAfter { get; }

        public GetStreamRateLimitException(
            string message,
            int statusCode,
            int code,
            IReadOnlyDictionary<string, string> exceptionFields,
            bool unrecoverable,
            string rawResponseBody,
            TimeSpan? retryAfter,
            string? moreInfo = null,
            object? details = null,
            Exception? innerException = null)
            : base(message, statusCode, code, exceptionFields, unrecoverable, rawResponseBody, moreInfo, details, innerException)
        {
            RetryAfter = retryAfter;
        }
    }

    /// <summary>
    /// Thrown when a transport-layer failure prevents an HTTP response from
    /// being received (connection reset, timeout, DNS, TLS).
    /// </summary>
    public class GetStreamTransportException : GetStreamException
    {
        /// <summary>
        /// One of: <c>connection_reset</c>, <c>timeout</c>, <c>dns_failure</c>,
        /// <c>tls_handshake_failed</c>, <c>unknown</c>. Matches the logged
        /// <c>error.type</c> enum.
        /// </summary>
        public string ErrorType { get; }

        public GetStreamTransportException(string message, string errorType, Exception? innerException = null)
            : base(message, innerException)
        {
            ErrorType = errorType;
        }
    }

    /// <summary>
    /// Thrown by <c>WaitForTaskAsync</c> when an async task completes with
    /// status <c>failed</c>. Fields are extracted from the task's
    /// <c>ErrorResult</c>.
    /// </summary>
    public class GetStreamTaskException : GetStreamException
    {
        public string TaskId { get; }
        public string ErrorType { get; }
        public string Description { get; }
        public new string? StackTrace { get; }
        public string? Version { get; }

        public GetStreamTaskException(
            string taskId,
            string errorType,
            string description,
            string? stackTrace = null,
            string? version = null,
            Exception? innerException = null)
            : base($"task {taskId} failed: {description}", innerException)
        {
            TaskId = taskId;
            ErrorType = errorType;
            Description = description;
            StackTrace = stackTrace;
            Version = version;
        }
    }
}
