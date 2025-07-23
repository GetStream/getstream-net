using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GetStream
{
    public abstract class BaseClient
    {
        protected readonly Client _client;

        protected BaseClient(Client client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        protected async Task<StreamResponse<TResponse>> MakeRequestAsync<TRequest, TResponse>(
            string method,
            string path,
            Dictionary<string, string>? queryParams,
            TRequest? requestBody,
            Dictionary<string, string>? pathParams,
            CancellationToken cancellationToken = default)
        {
            Console.WriteLine(path);
            return await _client.MakeRequestAsync<TRequest, TResponse>(
                method, path, queryParams, requestBody, pathParams, cancellationToken);
        }

        protected Dictionary<string, string>? ExtractQueryParams(object? request)
        {
            return QueryParamsHelper.ExtractQueryParams(request);
        }
    }
} 