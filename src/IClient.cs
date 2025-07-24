using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using GetStream.Models;

namespace GetStream
{
    public interface IClient
    {
        Task<StreamResponse<TResponse>> MakeRequestAsync<TRequest, TResponse>(
            string method,
            string path,
            Dictionary<string, string>? queryParams,
            TRequest? requestBody,
            Dictionary<string, string>? pathParams,
            CancellationToken cancellationToken = default);
    }
} 