using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GetStream;
using NUnit.Framework;

namespace GetStream.Tests
{
    [TestFixture]
    public class ConnectionPoolTests
    {
        private const string DummyApiKey = "dummy-api-key";
        private const string DummySecret = "dummy-secret-that-is-long-enough-for-hmac-sha256";

        [Test]
        public void BaseClient_WithDefaultStreamOptions_HasSpecDefaults()
        {
            var client = new BaseClient(new StreamOptions
            {
                ApiKey = DummyApiKey,
                ApiSecret = DummySecret,
            });

            var (httpClient, handler) = UnwrapHandler(client);
            Assert.That(httpClient.Timeout, Is.EqualTo(TimeSpan.FromSeconds(30)),
                "default RequestTimeout = 30s");
            Assert.That(handler.MaxConnectionsPerServer, Is.EqualTo(5),
                "default MaxConnsPerHost = 5");
            Assert.That(handler.PooledConnectionIdleTimeout, Is.EqualTo(TimeSpan.FromSeconds(55)),
                "default IdleTimeout = 55s");
            Assert.That(handler.ConnectTimeout, Is.EqualTo(TimeSpan.FromSeconds(10)),
                "default ConnectTimeout = 10s");
            Assert.That(handler.AutomaticDecompression.HasFlag(DecompressionMethods.GZip), Is.True,
                "gzip wiring from CHA-2961 must be preserved on the default-built handler");
        }

        [Test]
        public void BaseClient_WithStreamOptions_AllKnobsOverridable()
        {
            var client = new BaseClient(new StreamOptions
            {
                ApiKey = DummyApiKey,
                ApiSecret = DummySecret,
                MaxConnsPerHost = 17,
                IdleTimeout = TimeSpan.FromSeconds(123),
                ConnectTimeout = TimeSpan.FromSeconds(7),
                RequestTimeout = TimeSpan.FromSeconds(42),
            });
            var (httpClient, handler) = UnwrapHandler(client);
            Assert.That(handler.MaxConnectionsPerServer, Is.EqualTo(17));
            Assert.That(handler.PooledConnectionIdleTimeout, Is.EqualTo(TimeSpan.FromSeconds(123)));
            Assert.That(handler.ConnectTimeout, Is.EqualTo(TimeSpan.FromSeconds(7)));
            Assert.That(httpClient.Timeout, Is.EqualTo(TimeSpan.FromSeconds(42)));
        }

        [Test]
        public void BaseClient_PositionalConstructor_StillWorks_WithSpecDefaults()
        {
            // The existing 3-arg constructor must still produce a client wired with the new defaults.
            var client = new BaseClient(DummyApiKey, DummySecret);
            var (httpClient, handler) = UnwrapHandler(client);
            Assert.That(httpClient.Timeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
            Assert.That(handler.MaxConnectionsPerServer, Is.EqualTo(5));
            Assert.That(handler.PooledConnectionIdleTimeout, Is.EqualTo(TimeSpan.FromSeconds(55)));
            Assert.That(handler.ConnectTimeout, Is.EqualTo(TimeSpan.FromSeconds(10)));
            Assert.That(handler.AutomaticDecompression.HasFlag(DecompressionMethods.GZip), Is.True);
        }

        [Test]
        public async Task BaseClient_PerCallCancellationToken_PreEmptsClientTimeout()
        {
            var port = GetFreePort();
            var prefix = $"http://127.0.0.1:{port}/";

            using var listener = new System.Net.HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();

            // Server holds the connection open well past the per-call deadline.
            var serverTask = Task.Run(async () =>
            {
                try
                {
                    var ctx = await listener.GetContextAsync();
                    await Task.Delay(TimeSpan.FromSeconds(3));
                    ctx.Response.StatusCode = 200;
                    ctx.Response.OutputStream.Close();
                }
                catch { /* listener stopped */ }
            });

            try
            {
                var client = new BaseClient(new StreamOptions
                {
                    ApiKey = DummyApiKey,
                    ApiSecret = DummySecret,
                    BaseUrl = prefix.TrimEnd('/'),
                    RequestTimeout = TimeSpan.FromSeconds(30),
                });

                using var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromMilliseconds(150));

                var sw = System.Diagnostics.Stopwatch.StartNew();
                Exception? caught = null;
                try
                {
                    await client.MakeRequestAsync<object, object>(
                        method: "GET", path: "/slow",
                        queryParams: null, requestBody: null, pathParams: null,
                        cancellationToken: cts.Token);
                }
                catch (Exception ex) { caught = ex; }
                sw.Stop();

                Assert.That(caught, Is.Not.Null, "expected cancellation to surface as an exception");
                Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(1)),
                    "per-call CancellationToken must pre-empt the 30s client timeout");
            }
            finally { listener.Stop(); }
        }

        private static int GetFreePort()
        {
            var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            l.Start();
            var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        [Test]
        public void BaseClient_EscapeHatch_UserSuppliedHttpClient_IgnoresKnobs()
        {
            var customHandler = new SocketsHttpHandler
            {
                MaxConnectionsPerServer = 99,
                ConnectTimeout = TimeSpan.FromSeconds(99),
            };
            var custom = new HttpClient(customHandler) { Timeout = TimeSpan.FromSeconds(99) };

            var client = new BaseClient(new StreamOptions
            {
                ApiKey = DummyApiKey,
                ApiSecret = DummySecret,
                HttpClient = custom,
                // These four MUST be ignored:
                MaxConnsPerHost = 3,
                IdleTimeout = TimeSpan.FromSeconds(3),
                ConnectTimeout = TimeSpan.FromSeconds(3),
                RequestTimeout = TimeSpan.FromSeconds(3),
            });
            var (httpClient, handler) = UnwrapHandler(client);
            Assert.That(httpClient, Is.SameAs(custom), "SDK must use the user-supplied HttpClient as-is");
            Assert.That(httpClient.Timeout, Is.EqualTo(TimeSpan.FromSeconds(99)), "Timeout preserved");
            Assert.That(handler, Is.SameAs(customHandler), "handler preserved");
            Assert.That(handler.MaxConnectionsPerServer, Is.EqualTo(99), "handler untouched");
            Assert.That(handler.ConnectTimeout, Is.EqualTo(TimeSpan.FromSeconds(99)), "handler untouched");
        }

        private sealed class CapturingLogger : Microsoft.Extensions.Logging.ILogger
        {
            public System.Collections.Generic.List<string> Infos { get; } = new();
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
            public void Log<TState>(
                Microsoft.Extensions.Logging.LogLevel logLevel,
                Microsoft.Extensions.Logging.EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (logLevel == Microsoft.Extensions.Logging.LogLevel.Information)
                    Infos.Add(formatter(state, exception));
            }
            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();
                public void Dispose() { }
            }
        }

        [Test]
        public void BaseClient_InfoLog_OnConstruction_WithDefaults()
        {
            var capture = new CapturingLogger();
            _ = new BaseClient(new StreamOptions
            {
                ApiKey = DummyApiKey,
                ApiSecret = DummySecret,
                Logger = capture,
            });
            Assert.That(capture.Infos.Count, Is.EqualTo(1), "exactly one INFO line on construction");
            var msg = capture.Infos[0];
            Assert.That(msg, Does.StartWith("client.initialized"));
            Assert.That(msg, Does.Contain("stream.client.max_conns_per_host=5"));
            Assert.That(msg, Does.Contain("stream.client.idle_timeout_seconds=55"));
            Assert.That(msg, Does.Contain("stream.client.connect_timeout_seconds=10"));
            Assert.That(msg, Does.Contain("stream.client.request_timeout_seconds=30"));
            Assert.That(msg, Does.Contain("stream.client.user_http_client=false"));
            Assert.That(msg, Does.Contain("stream.client.gzip_enabled=true"));
            Assert.That(msg, Does.Contain("stream.client.log_bodies=false"));
        }

        [Test]
        public void BaseClient_InfoLog_OnConstruction_WithUserHttpClient()
        {
            var capture = new CapturingLogger();
            _ = new BaseClient(new StreamOptions
            {
                ApiKey = DummyApiKey,
                ApiSecret = DummySecret,
                HttpClient = new HttpClient(),
                Logger = capture,
            });
            Assert.That(capture.Infos.Count, Is.EqualTo(1));
            Assert.That(capture.Infos[0], Does.Contain("stream.client.user_http_client=true"));
            Assert.That(capture.Infos[0], Does.Contain("stream.client.gzip_enabled=false"),
                "escape hatch: SDK doesn't wire gzip itself, so it can't claim gzip_enabled=true");
        }

        [Test]
        public void BaseClient_InfoLog_Silent_WhenNoLoggerProvided()
        {
            // No logger → no log. The SDK must NOT spam Console for users who didn't opt in.
            Assert.DoesNotThrow(() => new BaseClient(new StreamOptions
            {
                ApiKey = DummyApiKey,
                ApiSecret = DummySecret,
            }));
        }

        // ----- helpers (used across all tests in this fixture) -----

        internal static (HttpClient httpClient, SocketsHttpHandler handler) UnwrapHandler(BaseClient client)
        {
            var hcField = typeof(BaseClient).GetField("_httpClient",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var httpClient = (HttpClient)hcField.GetValue(client)!;
            var hField = typeof(HttpMessageInvoker).GetField("_handler",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var handler = (SocketsHttpHandler)hField.GetValue(httpClient)!;
            return (httpClient, handler);
        }

        /// <summary>
        /// Unwrap the <see cref="SocketsHttpHandler"/> from a generated wrapper client (ChatClient, VideoClient,
        /// FeedsV3Client, ModerationClient) by reaching its private <c>_client</c> field. CHA-2956 routes these
        /// builders through the hand-written <see cref="BaseClient"/> seam, so the inner IClient is a BaseClient.
        /// </summary>
        internal static (HttpClient httpClient, SocketsHttpHandler handler) UnwrapWrapperHandler(object wrapperClient)
        {
            var clientField = wrapperClient.GetType().GetField("_client",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var inner = (BaseClient)clientField.GetValue(wrapperClient)!;
            return UnwrapHandler(inner);
        }
    }
}
