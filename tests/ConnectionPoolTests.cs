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
