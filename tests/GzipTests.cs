using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GetStream;
using GetStream.Models;
using NUnit.Framework;

namespace GetStream.Tests
{
    /// <summary>
    /// Verifies the SDK's HttpClient advertises gzip and transparently decodes
    /// gzip-encoded responses via SocketsHttpHandler.AutomaticDecompression.
    /// </summary>
    /// </summary>
    [TestFixture]
    public class GzipTests
    {
        /// <summary>
        /// Config sanity: a fresh BaseClient's internal HttpClient is wired through
        /// a SocketsHttpHandler with AutomaticDecompression = GZip.
        /// </summary>
        [Test]
        public void BaseClient_DefaultHttpClient_HasGzipAutomaticDecompression()
        {
            // The HMAC-SHA256 path is not exercised here, so any secret would do for
            // construction — keep it consistent with the e2e test for clarity.
            var client = new BaseClient(
                "dummy-api-key",
                "dummy-secret-that-is-long-enough-for-hmac-sha256");
            var handler = GetUnderlyingHandler(client);

            Assert.That(handler, Is.Not.Null, "expected default HttpClient to wrap an HttpMessageHandler");
            Assert.That(handler, Is.InstanceOf<SocketsHttpHandler>(),
                "expected default HttpClient to use SocketsHttpHandler");

            var socketsHandler = (SocketsHttpHandler)handler!;
            Assert.That(socketsHandler.AutomaticDecompression.HasFlag(DecompressionMethods.GZip), Is.True,
                "expected SocketsHttpHandler.AutomaticDecompression to include GZip");
        }

        /// <summary>
        /// End-to-end behavior: stand up an HttpListener that returns a gzip-encoded
        /// JSON body, point the SDK at it, and verify:
        ///   - The server sees Accept-Encoding: gzip on the incoming request.
        ///   - The SDK transparently decodes the gzipped response and deserializes it.
        /// </summary>
        [Test]
        public async Task MakeRequestAsync_SendsAcceptGzip_AndDecodesGzippedResponse()
        {
            // Pick an ephemeral port via the OS by binding to :0 on a TcpListener,
            // then reuse the port for HttpListener.
            var port = GetFreePort();
            var prefix = $"http://127.0.0.1:{port}/";

            using var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();

            string? observedAcceptEncoding = null;
            var responseJson = "{\"duration\":\"42ms\"}";

            // Serve exactly one request.
            var serverTask = Task.Run(async () =>
            {
                var ctx = await listener.GetContextAsync();
                observedAcceptEncoding = ctx.Request.Headers["Accept-Encoding"];

                var gzipped = GzipEncode(Encoding.UTF8.GetBytes(responseJson));
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json";
                ctx.Response.Headers["Content-Encoding"] = "gzip";
                ctx.Response.ContentLength64 = gzipped.Length;
                await ctx.Response.OutputStream.WriteAsync(gzipped, 0, gzipped.Length);
                ctx.Response.OutputStream.Close();
            });

            try
            {
                // Build a BaseClient pointed at the test server. HttpClient inside it
                // is constructed by the BaseClient ctor with the gzip handler.
                // The secret must be >= 16 bytes (128 bits) for HMAC-SHA256 token signing.
                var client = new BaseClient(
                    "dummy-api-key",
                    "dummy-secret-that-is-long-enough-for-hmac-sha256",
                    prefix.TrimEnd('/'));

                var result = await client.MakeRequestAsync<object, TestResponse>(
                    method: "GET",
                    path: "/test",
                    queryParams: null,
                    requestBody: null,
                    pathParams: null);

                // Wait for the server task to finish so observedAcceptEncoding is set.
                await serverTask.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.That(observedAcceptEncoding, Is.Not.Null,
                    "server did not see any Accept-Encoding header");
                Assert.That(
                    observedAcceptEncoding!.IndexOf("gzip", StringComparison.OrdinalIgnoreCase) >= 0,
                    Is.True,
                    $"expected Accept-Encoding to contain 'gzip', got: '{observedAcceptEncoding}'");

                Assert.That(result, Is.Not.Null);
                Assert.That(result.Data, Is.Not.Null);
                Assert.That(result.Data!.Duration, Is.EqualTo("42ms"));
            }
            finally
            {
                listener.Stop();
            }
        }

        // ----- helpers -----

        /// <summary>
        /// Reach into BaseClient via reflection and retrieve the underlying
        /// HttpMessageHandler used by the private _httpClient field. This is the
        /// least-invasive way to assert the SDK's default construction without
        /// adding a test-only accessor to public surface.
        /// </summary>
        private static HttpMessageHandler? GetUnderlyingHandler(BaseClient client)
        {
            var httpClientField = typeof(BaseClient).GetField(
                "_httpClient",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(httpClientField, Is.Not.Null, "BaseClient._httpClient field not found");
            var httpClient = (HttpClient)httpClientField!.GetValue(client)!;

            // HttpMessageInvoker (HttpClient's base) holds the handler in a private
            // field named "_handler" in current .NET runtime versions.
            var handlerField = typeof(HttpMessageInvoker).GetField(
                "_handler",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(handlerField, Is.Not.Null, "HttpMessageInvoker._handler field not found");
            return (HttpMessageHandler?)handlerField!.GetValue(httpClient);
        }

        private static byte[] GzipEncode(byte[] input)
        {
            using var output = new MemoryStream();
            using (var gz = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
            {
                gz.Write(input, 0, input.Length);
            }
            return output.ToArray();
        }

        private static int GetFreePort()
        {
            var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            l.Start();
            var port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        /// <summary>Test-only response shape for deserializing the canned gzip body.</summary>
        private sealed class TestResponse
        {
            public string? Duration { get; set; }
        }
    }
}
