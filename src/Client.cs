using System.Text;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.IO;
using System.Net.Http;
using GetStream.Models;

namespace GetStream
{
    public class BaseClient :IClient
    {
        private const string VersionName = "4.0.0";
        private static readonly string VersionHeader = $"getstream-net-{VersionName}";
        
        private readonly HttpClient _httpClient;
        protected string ApiKey;
        protected string ApiSecret;
        protected string BaseUrl;
        private readonly JsonSerializerOptions _jsonOptions;

        //getters 
        // public string ApiKey => _apiKey;
        // public string ApiSecret => _apiSecret;
        // public string BaseUrl => _baseUrl;
        
        
        
        public BaseClient(string apiKey, string apiSecret, string baseUrl = "https://chat-edge-ohio-ce1.stream-io-api.com")
        {
            ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            ApiSecret = apiSecret ?? throw new ArgumentNullException(nameof(apiSecret));
            BaseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
            _httpClient = new HttpClient();
            
            // Configure JSON options once
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                Converters = { new NanosecondTimestampConverter(), new FeedOwnCapabilityConverter() }
            };
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

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new GetStreamApiException($"HTTP {response.StatusCode}: {responseContent}", (int)response.StatusCode, responseContent);
            }

            // Try to deserialize as the actual response type first
            var directResult = JsonSerializer.Deserialize<TResponse>(responseContent, _jsonOptions);

            if (directResult != null)
            {
                return new StreamResponse<TResponse> { Data = directResult };
            }
            
            return new StreamResponse<TResponse>();
        }

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

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new GetStreamApiException($"HTTP {response.StatusCode}: {responseContent}", (int)response.StatusCode, responseContent);
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

        private string GenerateServerSideToken()
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(ApiSecret);
            var claims = new List<System.Security.Claims.Claim>
            {
                // new System.Security.Claims.Claim("user_id", "*"),
                // new System.Security.Claims.Claim("user", "*")
            };
            
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new System.Security.Claims.ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
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
    }
} 