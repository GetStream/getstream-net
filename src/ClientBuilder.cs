using System;
using System.IO;
using DotNetEnv;

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
        /// Build the StreamClient
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when required credentials are missing</exception>
        public StreamClient Build()
        {
            LoadCredentials();
            return new StreamClient(_apiKey!, _apiSecret!);
        }

        /// <summary>
        /// Build the FeedsV3Client
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when required credentials are missing</exception>
        public FeedsV3Client BuildFeedsClient()
        {
            LoadCredentials();
            return new FeedsV3Client(_apiKey!, _apiSecret!);
        }

        public void LoadCredentials()
        {
            // Load environment variables if enabled
            if (_loadEnv)
            {
                LoadEnvironmentVariables();
            }

            // Get credentials from environment if not set
            var apiKey = _apiKey ?? GetEnvVar("STREAM_API_KEY");
            var apiSecret = _apiSecret ?? GetEnvVar("STREAM_API_SECRET");
            var baseUrl = _baseUrl;

            // Override baseUrl with environment variable if not explicitly set and env var exists
            if (_baseUrl == "https://chat.stream-io-api.com")
            {
                var envBaseUrl = GetEnvVar("STREAM_BASE_URL");
                if (!string.IsNullOrEmpty(envBaseUrl))
                {
                    baseUrl = envBaseUrl;
                }
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException(
                    "API key not provided. Set STREAM_API_KEY environment variable or call ApiKey() method."
                );
            }

            if (string.IsNullOrEmpty(apiSecret))
            {
                throw new InvalidOperationException(
                    "API secret not provided. Set STREAM_API_SECRET environment variable or call ApiSecret() method."
                );
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
