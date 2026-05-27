using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace GetStream
{
    /// <summary>
    /// Configuration object for <see cref="BaseClient"/> covering credentials, base URL, HTTP connection-pool tuning (CHA-2956), and optional dependencies.
    ///
    /// Defaults: MaxConnsPerHost=5, IdleTimeout=55s, ConnectTimeout=10s, RequestTimeout=30s.
    /// KeepAlive is always-on; the SDK never emits <c>Connection: close</c>.
    /// </summary>
    public class StreamOptions
    {
        /// <summary>API key (required).</summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>API secret (required).</summary>
        public string ApiSecret { get; set; } = string.Empty;

        /// <summary>Base URL. Default <c>https://chat.stream-io-api.com</c>.</summary>
        public string BaseUrl { get; set; } = "https://chat.stream-io-api.com";

        /// <summary>Max concurrent TCP connections per host. Default <c>5</c>.</summary>
        public int MaxConnsPerHost { get; set; } = 5;

        /// <summary>How long an idle pooled connection lingers. Default <c>55s</c> (5s below the typical 60s LB idle timeout).</summary>
        public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromSeconds(55);

        /// <summary>TCP+TLS handshake cap. Default <c>10s</c>.</summary>
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>Per-request timeout applied to <see cref="HttpClient.Timeout"/>. Default <c>30s</c>. Per-call override via <see cref="System.Threading.CancellationTokenSource.CancelAfter(TimeSpan)"/>.</summary>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>Escape hatch. When set, the SDK uses this instance as-is and applies NONE of the 5 knobs. The caller owns all handler configuration (including gzip).</summary>
        public HttpClient? HttpClient { get; set; }

        /// <summary>When set, the SDK emits one INFO line on construction.</summary>
        public ILogger? Logger { get; set; }
    }
}
