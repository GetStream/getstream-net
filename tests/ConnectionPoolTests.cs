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
    }
}
