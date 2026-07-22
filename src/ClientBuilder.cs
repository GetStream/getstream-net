using System;
using System.IO;
using DotNetEnv;
using Microsoft.Extensions.Logging;

namespace GetStream
{
    /// <summary>
    /// Builder class for creating GetStream clients with environment variable support
    /// </summary>
    public class ClientBuilder
    {
        private string? _apiKey;
        private string? _apiSecret;
        private string _baseUrl = "https://chat.stream-io-api.com";

        /// <summary>
        /// Get the loaded API key (after calling LoadCredentials)
        /// </summary>
        public string? ApiKeyValue => _apiKey;

        /// <summary>
        /// Get the loaded API secret (after calling LoadCredentials)
        /// </summary>
        public string? ApiSecretValue => _apiSecret;

        /// <summary>
        /// Get the loaded base URL (after calling LoadCredentials)
        /// </summary>
        public string BaseUrlValue => _baseUrl;
        private bool _loadEnv = true;
        private string? _envPath;
        private string? _envFilePath;
        private int _maxConnsPerHost = 5;
        private TimeSpan _idleTimeout = TimeSpan.FromSeconds(55);
        private TimeSpan _connectTimeout = TimeSpan.FromSeconds(10);
        private TimeSpan _requestTimeout = TimeSpan.FromSeconds(30);
        private System.Net.Http.HttpClient? _httpClient;
        private ILogger? _logger;
        private bool _logBodies;

        /// <summary>
        /// Set the API key
        /// </summary>
        public ClientBuilder ApiKey(string apiKey)
        {
            _apiKey = apiKey;
            return this;
        }

        /// <summary>
        /// Set the API secret
        /// </summary>
        public ClientBuilder ApiSecret(string apiSecret)
        {
            _apiSecret = apiSecret;
            return this;
        }

        /// <summary>
        /// Set the base URL
        /// </summary>
        public ClientBuilder BaseUrl(string baseUrl)
        {
            _baseUrl = baseUrl;
            return this;
        }

        /// <summary>
        /// Disable loading from environment variables and .env file
        /// </summary>
        public ClientBuilder SkipEnvLoad()
        {
            _loadEnv = false;
            return this;
        }

        /// <summary>
        /// Set custom path for .env file (default is current directory)
        /// </summary>
        public ClientBuilder EnvPath(string path)
        {
            _envPath = path;
            return this;
        }

        /// <summary>CHA-2956: max concurrent TCP connections per host (default 5).</summary>
        public ClientBuilder MaxConnsPerHost(int n) { _maxConnsPerHost = n; return this; }

        /// <summary>CHA-2956: how long an idle pooled connection lingers (default 55s).</summary>
        public ClientBuilder IdleTimeout(TimeSpan d) { _idleTimeout = d; return this; }

        /// <summary>CHA-2956: TCP+TLS handshake cap (default 10s).</summary>
        public ClientBuilder ConnectTimeout(TimeSpan d) { _connectTimeout = d; return this; }

        /// <summary>CHA-2956: per-request timeout (default 30s).</summary>
        public ClientBuilder RequestTimeout(TimeSpan d) { _requestTimeout = d; return this; }

        /// <summary>Escape hatch. When set, NONE of the 4 pool knobs apply.</summary>
        public ClientBuilder HttpClient(System.Net.Http.HttpClient httpClient) { _httpClient = httpClient; return this; }

        /// <summary>Opt-in INFO log on construction.</summary>
        public ClientBuilder Logger(ILogger logger) { _logger = logger; return this; }

        /// <summary>Opt-in body logging (DEBUG events); shallow secret-key redaction still applies. Default false.</summary>
        public ClientBuilder LogBodies(bool enabled) { _logBodies = enabled; return this; }

        /// <summary>
        /// Create a client builder that loads configuration from environment variables
        /// </summary>
        public static ClientBuilder FromEnv(string? envPath = null)
        {
            var builder = new ClientBuilder();
            if (envPath != null)
            {
                builder.EnvPath(envPath);
            }
            return builder;
        }

        /// <summary>
        /// Create a client builder that loads from a specific .env file
        /// </summary>
        /// <param name="envFilePath">Full path to the .env file</param>
        /// <returns>ClientBuilder instance</returns>
        public static ClientBuilder FromEnvFile(string envFilePath)
        {
            return new ClientBuilder
            {
                _loadEnv = true,
                _envFilePath = envFilePath
            };
        }

        /// <summary>
        /// Build the StreamClient.
        /// </summary>
        /// <remarks>
        /// CHA-2956: the generated <see cref="StreamClient"/> now exposes a <c>StreamClient(StreamOptions)</c>
        /// constructor (emitted by the chat/ dotnet <c>common.tpl</c> template), which chains
        /// <c>BaseClient(StreamOptions)</c>. As a result, custom pool knobs set on this builder flow through
        /// <see cref="Build"/> the same way they flow through the IClient-wrapping entry points
        /// (<see cref="BuildChatClient"/>, <see cref="BuildVideoClient"/>, <see cref="BuildFeedsClient"/>,
        /// <see cref="BuildModerationClient"/>). When <see cref="StreamOptions.HttpClient"/> is set
        /// (escape hatch), none of the knobs apply.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when required credentials are missing</exception>
        public StreamClient Build()
        {
            LoadCredentials();
            return new StreamClient(BuildStreamOptions());
        }

        /// <summary>
        /// Build the FeedsV3Client. Pool knobs configured on this builder are applied.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when required credentials are missing</exception>
        public FeedsV3Client BuildFeedsClient()
        {
            LoadCredentials();
            return new FeedsV3Client(BuildConfiguredClient());
        }

        /// <summary>
        /// Build the ChatClient. Pool knobs configured on this builder are applied.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when required credentials are missing</exception>
        public ChatClient BuildChatClient()
        {
            LoadCredentials();
            return new ChatClient(BuildConfiguredClient());
        }

        /// <summary>
        /// Build the VideoClient. Pool knobs configured on this builder are applied.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when required credentials are missing</exception>
        public VideoClient BuildVideoClient()
        {
            LoadCredentials();
            return new VideoClient(BuildConfiguredClient());
        }

        /// <summary>
        /// Build the ModerationClient. Pool knobs configured on this builder are applied.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when required credentials are missing</exception>
        public ModerationClient BuildModerationClient()
        {
            LoadCredentials();
            return new ModerationClient(BuildConfiguredClient());
        }

        /// <summary>
        /// CHA-2956: build the pool-configured transport via the hand-written <see cref="BaseClient"/> seam.
        /// Returns an <see cref="IClient"/> so it can back every generated wrapper client through their existing
        /// generated <c>(IClient)</c> constructors, without touching generated code. When
        /// <see cref="StreamOptions.HttpClient"/> is set (escape hatch), none of the knobs apply.
        /// </summary>
        private IClient BuildConfiguredClient()
        {
            return new BaseClient(BuildStreamOptions());
        }

        private StreamOptions BuildStreamOptions()
        {
            return new StreamOptions
            {
                ApiKey = _apiKey!,
                ApiSecret = _apiSecret!,
                BaseUrl = _baseUrl,
                MaxConnsPerHost = _maxConnsPerHost,
                IdleTimeout = _idleTimeout,
                ConnectTimeout = _connectTimeout,
                RequestTimeout = _requestTimeout,
                HttpClient = _httpClient,
                Logger = _logger,
                LogBodies = _logBodies,
            };
        }

        public void LoadCredentials()
        {
            // Load environment variables if enabled
            if (_loadEnv)
            {
                LoadEnvironmentVariables();
            }

            // Get credentials from environment if not set (only when env loading is enabled)
            var apiKey = _apiKey ?? (_loadEnv ? GetEnvVar("STREAM_API_KEY") : null);
            var apiSecret = _apiSecret ?? (_loadEnv ? GetEnvVar("STREAM_API_SECRET") : null);
            var baseUrl = _baseUrl;

            // Override baseUrl with environment variable if not explicitly set and env var exists
            if (_loadEnv && _baseUrl == "https://chat.stream-io-api.com")
            {
                var envBaseUrl = GetEnvVar("STREAM_BASE_URL");
                if (!string.IsNullOrEmpty(envBaseUrl))
                {
                    baseUrl = envBaseUrl;
                }
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                var message = _loadEnv
                    ? "API key not provided. Set STREAM_API_KEY environment variable or call ApiKey() method."
                    : "API key not provided. Call ApiKey() method or remove SkipEnvLoad() to load from environment.";
                throw new InvalidOperationException(message);
            }

            if (string.IsNullOrEmpty(apiSecret))
            {
                var message = _loadEnv
                    ? "API secret not provided. Set STREAM_API_SECRET environment variable or call ApiSecret() method."
                    : "API secret not provided. Call ApiSecret() method or remove SkipEnvLoad() to load from environment.";
                throw new InvalidOperationException(message);
            }

            _apiKey = apiKey;
            _apiSecret = apiSecret;
            _baseUrl = baseUrl;
        }

        /// <summary>
        /// Load environment variables from .env file
        /// </summary>
        private void LoadEnvironmentVariables()
        {
            try
            {
                string envFilePath;

                if (_envFilePath != null)
                {
                    // Use explicit file path if provided
                    envFilePath = _envFilePath;
                }
                else if (_envPath != null)
                {
                    // Use explicit directory path if provided
                    envFilePath = Path.Combine(_envPath, ".env");
                }
                else
                {
                    // Search for .env file starting from current directory and going up
                    envFilePath = FindEnvFile();
                }

                if (!string.IsNullOrEmpty(envFilePath) && File.Exists(envFilePath))
                {
                    Env.Load(envFilePath);
                }
            }
            catch (Exception)
            {
                // Silently ignore if .env file doesn't exist or can't be loaded
                // Environment variables might be set through other means
            }
        }

        /// <summary>
        /// Find .env file by searching up the directory tree
        /// </summary>
        private string FindEnvFile()
        {
            var currentDir = Directory.GetCurrentDirectory();

            while (currentDir != null)
            {
                var envPath = Path.Combine(currentDir, ".env");
                if (File.Exists(envPath))
                {
                    return envPath;
                }

                var parent = Directory.GetParent(currentDir);
                currentDir = parent?.FullName;
            }

            return string.Empty;
        }

        /// <summary>
        /// Get environment variable value
        /// </summary>
        private static string? GetEnvVar(string name)
        {
            return Environment.GetEnvironmentVariable(name);
        }
    }
}
