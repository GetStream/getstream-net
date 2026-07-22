using System.Text;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using GetStream.Models;

namespace GetStream
{
    public class BaseClient : IClient
    {
        private const string VersionName = "14.0.0";
        private static readonly string VersionHeader = $"getstream-net-{VersionName}";

        private readonly HttpClient _httpClient;
        private readonly Microsoft.Extensions.Logging.ILogger? _logger;
        private readonly bool _userHttpClient;
        private readonly bool _logBodies;
        protected string ApiKey;
        protected string ApiSecret;
        protected string BaseUrl;
        private readonly JsonSerializerOptions _jsonOptions;

        //getters
        // public string ApiKey => _apiKey;
        // public string ApiSecret => _apiSecret;
        // public string BaseUrl => _baseUrl;



        public BaseClient(string apiKey, string apiSecret, string baseUrl = "https://chat.stream-io-api.com")
            : this(new StreamOptions
            {
                ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey)),
                ApiSecret = apiSecret ?? throw new ArgumentNullException(nameof(apiSecret)),
                BaseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl)),
            })
        {
        }

        /// <summary>
        /// CHA-2956 canonical constructor. The positional <c>(apiKey, apiSecret, baseUrl)</c> constructor builds a default-valued <see cref="StreamOptions"/> and delegates here.
        /// When <see cref="StreamOptions.HttpClient"/> is set (escape hatch), the 5 pool knobs are NOT applied.
        /// </summary>
        public BaseClient(StreamOptions opts)
        {
            if (opts == null) throw new ArgumentNullException(nameof(opts));
            if (string.IsNullOrEmpty(opts.ApiKey)) throw new ArgumentNullException(nameof(opts.ApiKey));
            if (string.IsNullOrEmpty(opts.ApiSecret)) throw new ArgumentNullException(nameof(opts.ApiSecret));
            if (string.IsNullOrEmpty(opts.BaseUrl)) throw new ArgumentNullException(nameof(opts.BaseUrl));

            ApiKey = opts.ApiKey;
            ApiSecret = opts.ApiSecret;
            BaseUrl = opts.BaseUrl;
            _logger = opts.Logger;
            _logBodies = opts.LogBodies;

            if (opts.HttpClient != null)
            {
                _httpClient = opts.HttpClient;
                _userHttpClient = true;
            }
            else
            {
                _httpClient = BuildDefaultHttpClient(opts);
                _userHttpClient = false;
            }

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                Converters = { new NanosecondTimestampConverter(), new FeedOwnCapabilityConverter(), new ChannelOwnCapabilityConverter(), new OwnCapabilityConverter() }
            };

            LogInitialized(opts);
        }

        private static HttpClient BuildDefaultHttpClient(StreamOptions opts)
        {
            // KeepAlive is always-on; never set Connection: close.
            // PooledConnectionLifetime is left at the framework default (InfiniteTimeSpan);
            // PooledConnectionIdleTimeout governs idle eviction.
            var handler = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip,
                MaxConnectionsPerServer = opts.MaxConnsPerHost,
                PooledConnectionIdleTimeout = opts.IdleTimeout,
                ConnectTimeout = opts.ConnectTimeout,
            };
            return new HttpClient(handler) { Timeout = opts.RequestTimeout };
        }

        /// <summary>
        /// Emits the <c>client.initialized</c> INFO event exactly once (logging spec §6.1), then a one-shot
        /// WARN when <see cref="StreamOptions.LogBodies"/> is enabled. See README "Structured Logging" for
        /// the PascalCase-placeholder-to-canonical-field mapping.
        /// </summary>
        private void LogInitialized(StreamOptions opts)
        {
            if (_logger == null) return;

            // The SDK only wires gzip itself on the default-built handler (CHA-2961); when the caller
            // supplies their own HttpClient (escape hatch), gzip is entirely the caller's concern.
            // Bools are rendered lowercase explicitly: bool.ToString() defaults to "True"/"False",
            // which would be the only capitalized values among an otherwise all-lowercase field set
            // shared verbatim across the 6-SDK logging spec.
            var gzipEnabled = (!_userHttpClient).ToString().ToLowerInvariant();
            var userHttpClient = _userHttpClient.ToString().ToLowerInvariant();
            var logBodies = opts.LogBodies.ToString().ToLowerInvariant();

            _logger.LogInformation(
                "client.initialized stream.sdk.name={SdkName} stream.sdk.version={SdkVersion} stream.client.max_conns_per_host={MaxConnsPerHost} stream.client.idle_timeout_seconds={IdleTimeoutSeconds} stream.client.connect_timeout_seconds={ConnectTimeoutSeconds} stream.client.request_timeout_seconds={RequestTimeoutSeconds} stream.client.gzip_enabled={GzipEnabled} stream.client.user_http_client={UserHttpClient} stream.client.log_bodies={LogBodies}",
                "getstream-net", VersionName,
                opts.MaxConnsPerHost, opts.IdleTimeout.TotalSeconds, opts.ConnectTimeout.TotalSeconds, opts.RequestTimeout.TotalSeconds,
                gzipEnabled, userHttpClient, logBodies);

            if (opts.LogBodies)
            {
                _logger.LogWarning(
                    "log_bodies enabled: request and response bodies will be logged (known secret keys are shallow-redacted only; do not enable where bodies may carry sensitive data outside that set)");
            }
        }

        public async Task<StreamResponse<TResponse>> MakeRequestAsync<TRequest, TResponse>(
            string method,
            string path,
            Dictionary<string, string>? queryParams,
            TRequest? requestBody,
            Dictionary<string, string>? pathParams,
            CancellationToken cancellationToken = default)
        {
            var url = BuildUrl(path, queryParams, pathParams);
            var request = new HttpRequestMessage(new HttpMethod(method), url);

            // Add authentication headers
            var token = GenerateServerSideToken();
            request.Headers.Add("Authorization", token);
            request.Headers.Add("stream-auth-type", "jwt");
            request.Headers.Add("X-Stream-Client", VersionHeader);

            // Add request body if provided
            string? requestBodyForLog = null;
            if (requestBody != null)
            {
                // Handle multipart form data for file/image uploads
                if (requestBody is FileUploadRequest fileUploadRequest)
                {
                    request.Content = CreateMultipartContent(fileUploadRequest);
                }
                else if (requestBody is ImageUploadRequest imageUploadRequest)
                {
                    request.Content = CreateMultipartContent(imageUploadRequest);
                }
                else
                {
                    // Default to JSON for other request types
                    var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    requestBodyForLog = json;
                }
            }

            // Uri parses the same absolute URL BuildUrl just produced, so path/query for
            // logging never drift from what is actually sent on the wire.
            var uri = new Uri(url);
            var logPath = uri.AbsolutePath;
            var logQuery = LogRedaction.RedactQuery(uri.Query.TrimStart('?'));

            if (_logger != null)
            {
                if (_logBodies && requestBodyForLog != null)
                {
                    _logger.LogDebug("http.request.sent {Method} {Path} {Query} {Body}",
                        method, logPath, logQuery, LogRedaction.RedactJsonBody(requestBodyForLog));
                }
                else
                {
                    _logger.LogDebug("http.request.sent {Method} {Path} {Query}", method, logPath, logQuery);
                }
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            HttpResponseMessage response;
            string responseContent;
            try
            {
                response = await _httpClient.SendAsync(request, cancellationToken);
                responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception ex) when (IsTransportException(ex, cancellationToken))
            {
                stopwatch.Stop();
                var transportException = WrapTransportException(ex, cancellationToken);
                _logger?.LogError("http.request.failed {Method} {Path} {ErrorType} {DurationMs} {Message}",
                    method, logPath, transportException.ErrorType, stopwatch.ElapsedMilliseconds,
                    LogRedaction.RedactMessage(transportException.Message));
                throw transportException;
            }
            stopwatch.Stop();

            if (_logger != null)
            {
                var bodySize = Encoding.UTF8.GetByteCount(responseContent);
                if (_logBodies)
                {
                    _logger.LogDebug("http.response.received {Method} {Path} {StatusCode} {BodySize} {DurationMs} {Body}",
                        method, logPath, (int)response.StatusCode, bodySize, stopwatch.ElapsedMilliseconds,
                        LogRedaction.RedactJsonBody(responseContent));
                }
                else
                {
                    _logger.LogDebug("http.response.received {Method} {Path} {StatusCode} {BodySize} {DurationMs}",
                        method, logPath, (int)response.StatusCode, bodySize, stopwatch.ElapsedMilliseconds);
                }
            }

            if (!response.IsSuccessStatusCode)
            {
                throw BuildApiException(response, responseContent);
            }

            // Try to deserialize as the actual response type first
            var directResult = JsonSerializer.Deserialize<TResponse>(responseContent, _jsonOptions);

            if (directResult != null)
            {
                return new StreamResponse<TResponse> { Data = directResult };
            }

            return new StreamResponse<TResponse>();
        }

        // CHA-2957: near-duplicate send path, not exposed on IClient; intentionally not instrumented with structured logging.
        public async Task<StreamResponse<TResponse>> MakeRequestAsyncDebug<TRequest, TResponse>(
            string method,
            string path,
            Dictionary<string, string>? queryParams,
            TRequest? requestBody,
            Dictionary<string, string>? pathParams,
            CancellationToken cancellationToken = default)
        {
            var url = BuildUrl(path, queryParams, pathParams);
            var request = new HttpRequestMessage(new HttpMethod(method), url);

            // Add authentication headers
            var token = GenerateServerSideToken();
            request.Headers.Add("Authorization", token);
            request.Headers.Add("stream-auth-type", "jwt");
            request.Headers.Add("X-Stream-Client", VersionHeader);

            // Add request body if provided
            if (requestBody != null)
            {
                // Handle multipart form data for file/image uploads
                if (requestBody is FileUploadRequest fileUploadRequest)
                {
                    request.Content = CreateMultipartContent(fileUploadRequest);
                }
                else if (requestBody is ImageUploadRequest imageUploadRequest)
                {
                    request.Content = CreateMultipartContent(imageUploadRequest);
                }
                else
                {
                    // Default to JSON for other request types
                    var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }
            }

            HttpResponseMessage response;
            string responseContent;
            try
            {
                response = await _httpClient.SendAsync(request, cancellationToken);
                responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception ex) when (IsTransportException(ex, cancellationToken))
            {
                throw WrapTransportException(ex, cancellationToken);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw BuildApiException(response, responseContent);
            }

            // Try to deserialize as the actual response type first
            try
            {
                var directResult = JsonSerializer.Deserialize<TResponse>(responseContent, _jsonOptions);

                if (directResult != null)
                {
                    return new StreamResponse<TResponse> { Data = directResult };
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Failed to deserialize as TResponse: {ex.Message}");
            }

            // If that fails, try to deserialize as StreamResponse<TResponse>
            try
            {
                var result = JsonSerializer.Deserialize<StreamResponse<TResponse>>(responseContent, _jsonOptions);

                if (result != null)
                {
                    return result;
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Failed to deserialize as StreamResponse<TResponse>: {ex.Message}");
                Console.WriteLine($"Response content: {responseContent}");
            }

            // If both fail, return empty response
            return new StreamResponse<TResponse>();
        }

        private string BuildUrl(string path, Dictionary<string, string>? queryParams, Dictionary<string, string>? pathParams)
        {
            var url = BaseUrl + path;

            // Replace path parameters
            if (pathParams != null)
            {
                foreach (var param in pathParams)
                {
                    url = url.Replace($"{{{param.Key}}}", param.Value);
                }
            }

            // Always add API key as query parameter
            var allQueryParams = new Dictionary<string, string>();
            if (queryParams != null)
            {
                foreach (var param in queryParams)
                {
                    allQueryParams[param.Key] = param.Value;
                }
            }

            // Add API key to query parameters
            allQueryParams["api_key"] = ApiKey;

            // Add query parameters
            if (allQueryParams.Count > 0)
            {
                var queryString = new List<string>();
                foreach (var param in allQueryParams)
                {
                    queryString.Add($"{param.Key}={Uri.EscapeDataString(param.Value)}");
                }
                url += "?" + string.Join("&", queryString);
            }

            return url;
        }

        /// <summary>
        /// Creates a JWT token for the given user ID.
        /// </summary>
        /// <param name="userId">The user ID to create the token for.</param>
        /// <param name="expiration">Optional token lifetime. Defaults to 1 hour.</param>
        /// <returns>A signed JWT token string.</returns>
        public string CreateUserToken(string userId, TimeSpan? expiration = null)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));

            return CreateJwtToken(new SecurityTokenDescriptor
            {
                Claims = new Dictionary<string, object> { { "user_id", userId } },
                IssuedAt = DateTime.UtcNow.AddSeconds(-5),
                NotBefore = DateTime.UtcNow.AddSeconds(-5),
                Expires = DateTime.UtcNow.Add(expiration ?? TimeSpan.FromHours(1)),
            });
        }

        private string GenerateServerSideToken()
        {
            return CreateJwtToken(new SecurityTokenDescriptor
            {
                Subject = new System.Security.Claims.ClaimsIdentity(),
                IssuedAt = DateTime.UtcNow.AddSeconds(-5),
                NotBefore = DateTime.UtcNow.AddSeconds(-5),
                Expires = DateTime.UtcNow.AddHours(1),
            });
        }

        private string CreateJwtToken(SecurityTokenDescriptor descriptor)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(ApiSecret);
            descriptor.SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature);
            return tokenHandler.WriteToken(tokenHandler.CreateToken(descriptor));
        }

        /// <summary>
        /// Verify the HMAC-SHA256 signature of a webhook payload using this client's API secret.
        ///
        /// Dual API: the static <see cref="Webhook.VerifySignature(byte[], string, string)"/>
        /// overload takes an explicit secret. Prefer this instance method when you already
        /// have a <see cref="StreamClient"/> on hand.
        /// </summary>
        /// <param name="body">The raw request body as a byte array</param>
        /// <param name="signature">The signature from the X-Signature header</param>
        /// <returns>true if the signature is valid, false otherwise</returns>
        public bool VerifySignature(byte[] body, string signature)
        {
            return Webhook.VerifySignature(body, signature, this.ApiSecret);
        }

        /// <summary>
        /// Verify the HMAC-SHA256 signature of a webhook payload using this client's API secret.
        /// </summary>
        /// <param name="body">The raw request body as a string</param>
        /// <param name="signature">The signature from the X-Signature header</param>
        /// <returns>true if the signature is valid, false otherwise</returns>
        public bool VerifySignature(string body, string signature)
        {
            return Webhook.VerifySignature(body, signature, this.ApiSecret);
        }

        /// <summary>
        /// Verify the signature and parse the webhook payload in a single call, using this
        /// client's API secret. Transparent gzip decompression via magic-byte detection.
        ///
        /// Dual API: the static <see cref="Webhook.VerifyAndParseWebhook(byte[], string, string)"/>
        /// overload takes an explicit secret. Prefer this instance method when you already
        /// have a <see cref="StreamClient"/> on hand.
        /// </summary>
        /// <param name="body">The raw request body as a byte array</param>
        /// <param name="signature">The signature from the X-Signature header</param>
        /// <returns>A typed event object (or <see cref="Webhook.UnknownEvent"/> for unknown discriminators)</returns>
        /// <exception cref="Webhook.StreamInvalidWebhookException">If the signature mismatches or the body cannot be parsed</exception>
        public object VerifyAndParseWebhook(byte[] body, string signature)
        {
            return Webhook.VerifyAndParseWebhook(body, signature, this.ApiSecret);
        }

        /// <summary>
        /// Decode and parse a Stream-delivered SQS message body.
        /// </summary>
        /// <remarks>
        /// Convenience wrapper around <see cref="Webhook.ParseSqs"/>. No signature is
        /// required; SQS deliveries are authenticated via AWS IAM.
        /// </remarks>
        public object ParseSqs(string messageBody)
        {
            return Webhook.ParseSqs(messageBody);
        }

        /// <summary>
        /// Decode and parse a Stream-delivered SNS notification body.
        /// </summary>
        /// <remarks>
        /// Accepts either the raw SNS HTTP envelope JSON or the pre-extracted Message
        /// string. Convenience wrapper around <see cref="Webhook.ParseSns"/>. No
        /// signature is required; SNS deliveries are authenticated via AWS IAM.
        /// </remarks>
        public object ParseSns(string notificationBody)
        {
            return Webhook.ParseSns(notificationBody);
        }

        private MultipartFormDataContent CreateMultipartContent(FileUploadRequest request)
        {
            if (string.IsNullOrEmpty(request.File))
            {
                throw new ArgumentException("File path must be provided", nameof(request));
            }

            if (!File.Exists(request.File))
            {
                throw new FileNotFoundException($"File not found: {request.File}");
            }

            var content = new MultipartFormDataContent();

            // Add file
            var fileStream = File.OpenRead(request.File);
            var fileName = Path.GetFileName(request.File);
            content.Add(new StreamContent(fileStream), "file", fileName);

            // Add user field if present
            if (request.User != null)
            {
                var userJson = JsonSerializer.Serialize(request.User, _jsonOptions);
                content.Add(new StringContent(userJson, Encoding.UTF8), "user");
            }

            return content;
        }

        private MultipartFormDataContent CreateMultipartContent(ImageUploadRequest request)
        {
            if (string.IsNullOrEmpty(request.File))
            {
                throw new ArgumentException("File path must be provided", nameof(request));
            }

            if (!File.Exists(request.File))
            {
                throw new FileNotFoundException($"File not found: {request.File}");
            }

            var content = new MultipartFormDataContent();

            // Add file
            var fileStream = File.OpenRead(request.File);
            var fileName = Path.GetFileName(request.File);
            content.Add(new StreamContent(fileStream), "file", fileName);

            // Add upload_sizes field if present
            if (request.UploadSizes != null && request.UploadSizes.Count > 0)
            {
                var uploadSizesJson = JsonSerializer.Serialize(request.UploadSizes, _jsonOptions);
                content.Add(new StringContent(uploadSizesJson, Encoding.UTF8), "upload_sizes");
            }

            // Add user field if present
            if (request.User != null)
            {
                var userJson = JsonSerializer.Serialize(request.User, _jsonOptions);
                content.Add(new StringContent(userJson, Encoding.UTF8), "user");
            }

            return content;
        }

        private static bool IsTransportException(Exception ex, CancellationToken cancellationToken)
        {
            // Caller-supplied cancellation is not a transport error: let it bubble.
            if (cancellationToken.IsCancellationRequested && ex is OperationCanceledException)
            {
                return false;
            }
            return ex is HttpRequestException
                || ex is TaskCanceledException
                || ex is TimeoutException
                || ex is SocketException
                || ex is IOException;
        }

        private static GetStreamTransportException WrapTransportException(Exception ex, CancellationToken cancellationToken)
        {
            var errorType = ClassifyTransportError(ex);
            var message = $"transport error ({errorType}): {ex.Message}";
            return new GetStreamTransportException(message, errorType, ex);
        }

        private static string ClassifyTransportError(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                switch (e)
                {
                    case TaskCanceledException _:
                    case TimeoutException _:
                        return "timeout";
                    case SocketException sock:
                        return ClassifySocketError(sock.SocketErrorCode);
                }

                var typeName = e.GetType().FullName ?? string.Empty;
                if (typeName.Contains("Authentication", StringComparison.OrdinalIgnoreCase)
                    || typeName.EndsWith("AuthenticationException", StringComparison.OrdinalIgnoreCase))
                {
                    return "tls_handshake_failed";
                }
            }
            return "unknown";
        }

        private static string ClassifySocketError(SocketError code)
        {
            switch (code)
            {
                case SocketError.HostNotFound:
                case SocketError.NoData:
                case SocketError.TryAgain:
                    return "dns_failure";
                case SocketError.TimedOut:
                    return "timeout";
                case SocketError.ConnectionReset:
                case SocketError.ConnectionRefused:
                case SocketError.ConnectionAborted:
                case SocketError.NetworkReset:
                case SocketError.Shutdown:
                    return "connection_reset";
                default:
                    return "unknown";
            }
        }

        private GetStreamApiException BuildApiException(HttpResponseMessage response, string responseContent)
        {
            var statusCode = (int)response.StatusCode;
            APIError? parsed = null;
            Exception? parseError = null;

            if (!string.IsNullOrEmpty(responseContent))
            {
                try
                {
                    parsed = JsonSerializer.Deserialize<APIError>(responseContent, _jsonOptions);
                }
                catch (JsonException jx)
                {
                    parseError = jx;
                }
            }

            string message;
            int code;
            IReadOnlyDictionary<string, string> exceptionFields;
            bool unrecoverable;
            string? moreInfo;
            object? details;

            if (parsed != null && (parsed.Code != 0 || !string.IsNullOrEmpty(parsed.Message)))
            {
                message = !string.IsNullOrEmpty(parsed.Message)
                    ? parsed.Message
                    : $"HTTP {statusCode}";
                code = parsed.Code;
                exceptionFields = parsed.ExceptionFields ?? new Dictionary<string, string>();
                unrecoverable = parsed.Unrecoverable ?? false;
                moreInfo = string.IsNullOrEmpty(parsed.MoreInfo) ? null : parsed.MoreInfo;
                details = parsed.Details;
            }
            else
            {
                // Body cannot be parsed as APIError.
                message = "failed to parse error response";
                code = 0;
                exceptionFields = new Dictionary<string, string>();
                unrecoverable = false;
                moreInfo = null;
                details = null;
            }

            if (statusCode == 429)
            {
                var retryAfter = ParseRetryAfter(response, DateTimeOffset.UtcNow);
                return new GetStreamRateLimitException(
                    message: message,
                    statusCode: statusCode,
                    code: code,
                    exceptionFields: exceptionFields,
                    unrecoverable: unrecoverable,
                    rawResponseBody: responseContent ?? string.Empty,
                    retryAfter: retryAfter,
                    moreInfo: moreInfo,
                    details: details,
                    innerException: parseError);
            }

            return new GetStreamApiException(
                message: message,
                statusCode: statusCode,
                code: code,
                exceptionFields: exceptionFields,
                unrecoverable: unrecoverable,
                rawResponseBody: responseContent ?? string.Empty,
                moreInfo: moreInfo,
                details: details,
                innerException: parseError);
        }

        internal static TimeSpan? ParseRetryAfter(HttpResponseMessage response, DateTimeOffset now)
        {
            if (response.Headers == null) return null;
            if (!response.Headers.TryGetValues("Retry-After", out var values)) return null;
            var raw = values.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(raw)) return null;

            // Integer seconds first, then HTTP-date.
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
            {
                return TimeSpan.FromSeconds(Math.Max(seconds, 0));
            }
            if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var when))
            {
                var delta = when - now;
                return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
            }
            return null;
        }

        /// <summary>
        /// Poll an async task until it completes, fails, or the timeout elapses.
        /// </summary>
        /// <param name="taskId">Task ID returned by the operation that enqueued it.</param>
        /// <param name="pollInterval">Poll cadence. Defaults to 1 second.</param>
        /// <param name="timeout">Maximum total wait. Defaults to 60 seconds.</param>
        /// <param name="cancellationToken">Cancels the polling loop.</param>
        /// <returns>The completed <see cref="GetTaskResponse"/>.</returns>
        /// <exception cref="GetStreamTaskException">Task observed with <c>status == "failed"</c>.</exception>
        /// <exception cref="GetStreamTransportException">Timeout elapsed before terminal state.</exception>
        public async Task<GetTaskResponse> WaitForTaskAsync(
            string taskId,
            TimeSpan? pollInterval = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(taskId)) throw new ArgumentException("taskId is required", nameof(taskId));

            var interval = pollInterval ?? TimeSpan.FromSeconds(1);
            var max = timeout ?? TimeSpan.FromSeconds(60);
            if (interval <= TimeSpan.Zero) interval = TimeSpan.FromSeconds(1);

            var deadline = DateTime.UtcNow + max;
            var pathParams = new Dictionary<string, string> { ["id"] = taskId };

            while (true)
            {
                var resp = await MakeRequestAsync<object, GetTaskResponse>(
                    "GET", "/api/v2/tasks/{id}", null, null, pathParams, cancellationToken);

                var data = resp.Data;
                if (data != null)
                {
                    if (string.Equals(data.Status, "completed", StringComparison.OrdinalIgnoreCase))
                    {
                        return data;
                    }
                    if (string.Equals(data.Status, "failed", StringComparison.OrdinalIgnoreCase))
                    {
                        var err = data.Error;
                        throw new GetStreamTaskException(
                            taskId: taskId,
                            errorType: err?.Type ?? string.Empty,
                            description: err?.Description ?? string.Empty,
                            stackTrace: err?.Stacktrace,
                            version: err?.Version);
                    }
                }

                if (DateTime.UtcNow + interval > deadline)
                {
                    throw new GetStreamTransportException(
                        $"timed out waiting for task {taskId} after {max}",
                        "timeout",
                        new TimeoutException($"WaitForTaskAsync deadline of {max} exceeded"));
                }
                await Task.Delay(interval, cancellationToken);
            }
        }
    }
}