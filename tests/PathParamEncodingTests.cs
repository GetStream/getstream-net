using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace GetStream.Tests
{
    [TestFixture]
    public class PathParamEncodingTests
    {
        private sealed class CapturingHandler : HttpMessageHandler
        {
            public Uri? LastRequestUri { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                LastRequestUri = request.RequestUri;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                });
            }
        }

        [Test]
        public async Task PathParametersArePercentEncodedAsSingleSegments()
        {
            var handler = new CapturingHandler();
            using var httpClient = new HttpClient(handler);
            var client = new Client(
                "test-key",
                "0123456789abcdef0123456789abcdef",
                httpClient,
                "https://chat.stream-io-api.com");

            await client.MakeRequestAsync<object, Dictionary<string, object>>(
                "GET",
                "/api/v2/video/call/{type}/{id}",
                null,
                null,
                new Dictionary<string, string>
                {
                    ["type"] = "default",
                    ["id"] = "../other/path?hard_delete=true#frag",
                });

            Assert.That(handler.LastRequestUri, Is.Not.Null);
            Assert.That(
                handler.LastRequestUri!.AbsolutePath,
                Is.EqualTo("/api/v2/video/call/default/..%2Fother%2Fpath%3Fhard_delete%3Dtrue%23frag"));
            Assert.That(handler.LastRequestUri.Query, Is.EqualTo("?api_key=test-key"));
            Assert.That(handler.LastRequestUri.Fragment, Is.Empty);
        }

        [TestCase("")]
        [TestCase(".")]
        [TestCase("..")]
        public void EmptyAndDotSegmentPathParametersAreRejected(string badValue)
        {
            var handler = new CapturingHandler();
            using var httpClient = new HttpClient(handler);
            var client = new Client(
                "test-key",
                "0123456789abcdef0123456789abcdef",
                httpClient,
                "https://chat.stream-io-api.com");

            Assert.ThrowsAsync<ArgumentException>(async () =>
                await client.MakeRequestAsync<object, Dictionary<string, object>>(
                    "GET",
                    "/api/v2/tasks/{id}",
                    null,
                    null,
                    new Dictionary<string, string> { ["id"] = badValue }));
        }
    }
}
