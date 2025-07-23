using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Serialization;
using GetStream.Models;

namespace GetStream
{
    public class StreamResponse<T>
    {
        public T? Data { get; set; }
        public string? Duration { get; set; }
        public string? Error { get; set; }
    }

    public class Client
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _apiSecret;
        private readonly string _baseUrl;
        private readonly JsonSerializerOptions _jsonOptions;

        public Client(string apiKey, string apiSecret, string baseUrl = "https://chat-edge-ohio-ce1.stream-io-api.com")
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _apiSecret = apiSecret ?? throw new ArgumentNullException(nameof(apiSecret));
            _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
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

            // Add request body if provided
            if (requestBody != null)
            {
                var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"HTTP {response.StatusCode}: {responseContent}");
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

            // Add request body if provided
            if (requestBody != null)
            {
                var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"HTTP {response.StatusCode}: {responseContent}");
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
            var url = _baseUrl + path;

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
            allQueryParams["api_key"] = _apiKey;

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
            var key = Encoding.ASCII.GetBytes(_apiSecret);
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
    }

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
                    queryParams[property.Name.ToLowerInvariant()] = value.ToString() ?? "";
                }
            }
            
            return queryParams.Count > 0 ? queryParams : null;
        }
    }
} 