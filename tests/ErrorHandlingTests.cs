using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GetStream;
using GetStream.Models;
using NUnit.Framework;

namespace GetStream.Tests
{
    [TestFixture]
    public class ErrorHandlingTests
    {
        private const string DummyApiKey = "dummy-api-key";
        private const string DummySecret = "dummy-secret-that-is-long-enough-for-hmac-sha256";

        // -- Hierarchy + field-population unit tests --------------------------------------

        [Test]
        public void GetStreamApiException_PopulatesAllFields()
        {
            var inner = new InvalidOperationException("inner");
            var fields = new Dictionary<string, string> { ["name"] = "required" };
            var ex = new GetStreamApiException(
                message: "bad request",
                statusCode: 400,
                code: 4,
                exceptionFields: fields,
                unrecoverable: true,
                rawResponseBody: "{\"code\":4}",
                moreInfo: "https://example/info",
                details: new List<int> { 1, 2 },
                innerException: inner);

            Assert.That(ex, Is.InstanceOf<GetStreamException>());
            Assert.That(ex.StatusCode, Is.EqualTo(400));
            Assert.That(ex.Code, Is.EqualTo(4));
            Assert.That(ex.ExceptionFields["name"], Is.EqualTo("required"));
            Assert.That(ex.Unrecoverable, Is.True);
            Assert.That(ex.RawResponseBody, Is.EqualTo("{\"code\":4}"));
            Assert.That(ex.MoreInfo, Is.EqualTo("https://example/info"));
            Assert.That(ex.Details, Is.Not.Null);
            Assert.That(ex.InnerException, Is.SameAs(inner));
        }

        [Test]
        public void GetStreamApiException_NullExceptionFields_BecomesEmptyMap()
        {
            var ex = new GetStreamApiException(
                message: "x", statusCode: 500, code: 0,
                exceptionFields: null!, unrecoverable: false,
                rawResponseBody: "");
            Assert.That(ex.ExceptionFields, Is.Not.Null);
            Assert.That(ex.ExceptionFields.Count, Is.EqualTo(0));
        }

        [Test]
        public void GetStreamRateLimitException_IsApiException_AndCarriesRetryAfter()
        {
            var ex = new GetStreamRateLimitException(
                message: "rate limited", statusCode: 429, code: 9,
                exceptionFields: new Dictionary<string, string>(),
                unrecoverable: false, rawResponseBody: "{}",
                retryAfter: TimeSpan.FromSeconds(30));

            Assert.That(ex, Is.InstanceOf<GetStreamApiException>());
            Assert.That(ex, Is.InstanceOf<GetStreamException>());
            Assert.That(ex.StatusCode, Is.EqualTo(429));
            Assert.That(ex.RetryAfter, Is.EqualTo(TimeSpan.FromSeconds(30)));
        }

        [Test]
        public void GetStreamTransportException_PreservesInnerException()
        {
            var inner = new SocketException((int)SocketError.ConnectionReset);
            var ex = new GetStreamTransportException("boom", "connection_reset", inner);
            Assert.That(ex, Is.InstanceOf<GetStreamException>());
            Assert.That(ex.ErrorType, Is.EqualTo("connection_reset"));
            Assert.That(ex.InnerException, Is.SameAs(inner));
        }

        [Test]
        public void GetStreamTaskException_HasAllFields_AndShadowedStackTrace()
        {
            var ex = new GetStreamTaskException(
                taskId: "t-1",
                errorType: "ImportError",
                description: "boom",
                stackTrace: "frame-1",
                version: "v1");
            Assert.That(ex.TaskId, Is.EqualTo("t-1"));
            Assert.That(ex.ErrorType, Is.EqualTo("ImportError"));
            Assert.That(ex.Description, Is.EqualTo("boom"));
            Assert.That(ex.StackTrace, Is.EqualTo("frame-1"));
            Assert.That(ex.Version, Is.EqualTo("v1"));
        }

        // -- Regression: deleted subclasses must not return -------------------------------

        [Test]
        public void DeletedSubclasses_NoLongerExistOnAssembly()
        {
            var asm = typeof(GetStreamException).Assembly;
            foreach (var name in new[]
            {
                "GetStream.GetStreamAuthenticationException",
                "GetStream.GetStreamValidationException",
                "GetStream.GetStreamFeedException",
            })
            {
                Assert.That(asm.GetType(name, throwOnError: false), Is.Null,
                    $"{name} was deleted in CHA-2958 and must not be reintroduced");
            }
        }

        // -- Retry-After parsing ----------------------------------------------------------

        [Test]
        public void ParseRetryAfter_IntegerSeconds_ReturnsExactDuration()
        {
            var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            resp.Headers.Add("Retry-After", "30");
            var result = InvokeParseRetryAfter(resp, DateTimeOffset.UtcNow);
            Assert.That(result, Is.EqualTo(TimeSpan.FromSeconds(30)));
        }

        [Test]
        public void ParseRetryAfter_NegativeInteger_ClampsToZero()
        {
            var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            // HttpHeaders.Add validates Retry-After; bypass to test the SDK's own parser.
            resp.Headers.TryAddWithoutValidation("Retry-After", "-5");
            var result = InvokeParseRetryAfter(resp, DateTimeOffset.UtcNow);
            Assert.That(result, Is.EqualTo(TimeSpan.Zero));
        }

        [Test]
        public void ParseRetryAfter_HttpDate_ReturnsCorrectDelta()
        {
            var now = new DateTimeOffset(2026, 5, 28, 12, 0, 0, TimeSpan.Zero);
            var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            resp.Headers.Add("Retry-After", "Thu, 28 May 2026 12:01:00 GMT");
            var result = InvokeParseRetryAfter(resp, now);
            Assert.That(result, Is.EqualTo(TimeSpan.FromMinutes(1)));
        }

        [Test]
        public void ParseRetryAfter_PastHttpDate_ReturnsZero()
        {
            var now = new DateTimeOffset(2026, 5, 28, 12, 0, 0, TimeSpan.Zero);
            var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            resp.Headers.Add("Retry-After", "Thu, 28 May 2026 11:59:00 GMT");
            var result = InvokeParseRetryAfter(resp, now);
            Assert.That(result, Is.EqualTo(TimeSpan.Zero));
        }

        [Test]
        public void ParseRetryAfter_HeaderMissing_ReturnsNull()
        {
            var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            var result = InvokeParseRetryAfter(resp, DateTimeOffset.UtcNow);
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ParseRetryAfter_Garbage_ReturnsNull()
        {
            var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            resp.Headers.TryAddWithoutValidation("Retry-After", "not-a-date");
            var result = InvokeParseRetryAfter(resp, DateTimeOffset.UtcNow);
            Assert.That(result, Is.Null);
        }

        // -- Transport-error wrapping (no live network) -----------------------------------

        [Test]
        public void MakeRequestAsync_HttpRequestExceptionWithSocketReset_BecomesTransportException()
        {
            var inner = new HttpRequestException("reset", new SocketException((int)SocketError.ConnectionReset));
            var client = BuildClientWith(new ThrowingHandler(inner));
            var ex = Assert.ThrowsAsync<GetStreamTransportException>(async () =>
                await client.MakeRequestAsync<object, object>(
                    "GET", "/anything", null, null, null));
            Assert.That(ex!.ErrorType, Is.EqualTo("connection_reset"));
            Assert.That(ex.InnerException, Is.SameAs(inner));
        }

        [Test]
        public void MakeRequestAsync_DnsFailure_ClassifiedAsDnsFailure()
        {
            var inner = new HttpRequestException("dns", new SocketException((int)SocketError.HostNotFound));
            var client = BuildClientWith(new ThrowingHandler(inner));
            var ex = Assert.ThrowsAsync<GetStreamTransportException>(async () =>
                await client.MakeRequestAsync<object, object>(
                    "GET", "/anything", null, null, null));
            Assert.That(ex!.ErrorType, Is.EqualTo("dns_failure"));
        }

        [Test]
        public void MakeRequestAsync_TimeoutException_ClassifiedAsTimeout()
        {
            var inner = new TimeoutException("slow");
            var client = BuildClientWith(new ThrowingHandler(inner));
            var ex = Assert.ThrowsAsync<GetStreamTransportException>(async () =>
                await client.MakeRequestAsync<object, object>(
                    "GET", "/anything", null, null, null));
            Assert.That(ex!.ErrorType, Is.EqualTo("timeout"));
        }

        [Test]
        public void MakeRequestAsync_CallerCancellationPropagatesNatively()
        {
            // Caller-supplied cancellation is NOT a transport error per the spec.
            var handler = new BlockingHandler();
            var client = BuildClientWith(handler);
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(50));
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await client.MakeRequestAsync<object, object>(
                    "GET", "/slow", null, null, null, cts.Token));
        }

        // -- HTTP API error parsing -------------------------------------------------------

        [Test]
        public void MakeRequestAsync_ApiError_PopulatesFromEnvelope()
        {
            var body = JsonSerializer.Serialize(new APIError
            {
                Code = 4,
                Message = "input invalid",
                Unrecoverable = true,
                ExceptionFields = new Dictionary<string, string> { ["name"] = "required" },
                MoreInfo = "https://x/info",
                StatusCode = 400,
            });
            var client = BuildClientWith(new CannedHandler(HttpStatusCode.BadRequest, body));
            var ex = Assert.ThrowsAsync<GetStreamApiException>(async () =>
                await client.MakeRequestAsync<object, object>(
                    "GET", "/anything", null, null, null));
            Assert.That(ex!.StatusCode, Is.EqualTo(400));
            Assert.That(ex.Code, Is.EqualTo(4));
            Assert.That(ex.Message, Is.EqualTo("input invalid"));
            Assert.That(ex.Unrecoverable, Is.True);
            Assert.That(ex.ExceptionFields["name"], Is.EqualTo("required"));
            Assert.That(ex.MoreInfo, Is.EqualTo("https://x/info"));
            Assert.That(ex.RawResponseBody, Is.EqualTo(body));
        }

        [Test]
        public void MakeRequestAsync_UnparseableBody_FallsThroughToUnparseablePath()
        {
            var client = BuildClientWith(new CannedHandler(HttpStatusCode.InternalServerError, "<html>oops</html>"));
            var ex = Assert.ThrowsAsync<GetStreamApiException>(async () =>
                await client.MakeRequestAsync<object, object>(
                    "GET", "/anything", null, null, null));
            Assert.That(ex!.StatusCode, Is.EqualTo(500));
            Assert.That(ex.Code, Is.EqualTo(0));
            Assert.That(ex.Message, Is.EqualTo("failed to parse error response"));
            Assert.That(ex.RawResponseBody, Is.EqualTo("<html>oops</html>"));
            Assert.That(ex.ExceptionFields.Count, Is.EqualTo(0));
        }

        [Test]
        public void MakeRequestAsync_429WithRetryAfterSeconds_ThrowsRateLimitException()
        {
            var body = JsonSerializer.Serialize(new APIError { Code = 9, Message = "slow down" });
            var headers = new Dictionary<string, string> { ["Retry-After"] = "42" };
            var client = BuildClientWith(new CannedHandler(HttpStatusCode.TooManyRequests, body, headers));
            var ex = Assert.ThrowsAsync<GetStreamRateLimitException>(async () =>
                await client.MakeRequestAsync<object, object>(
                    "GET", "/anything", null, null, null));
            Assert.That(ex!.RetryAfter, Is.EqualTo(TimeSpan.FromSeconds(42)));
            Assert.That(ex.Code, Is.EqualTo(9));
            Assert.That(ex, Is.InstanceOf<GetStreamApiException>(),
                "rate-limit exception must still satisfy catch (GetStreamApiException)");
        }

        [Test]
        public void MakeRequestAsync_429WithoutRetryAfter_ReturnsNullRetryAfter()
        {
            var body = JsonSerializer.Serialize(new APIError { Code = 9, Message = "slow" });
            var client = BuildClientWith(new CannedHandler(HttpStatusCode.TooManyRequests, body));
            var ex = Assert.ThrowsAsync<GetStreamRateLimitException>(async () =>
                await client.MakeRequestAsync<object, object>(
                    "GET", "/anything", null, null, null));
            Assert.That(ex!.RetryAfter, Is.Null);
        }

        // -- WaitForTaskAsync -------------------------------------------------------------

        [Test]
        public async Task WaitForTaskAsync_TaskCompleted_ReturnsTask()
        {
            var handler = new ScriptedTaskHandler(new[]
            {
                BuildTaskResponseJson("t-1", "pending"),
                BuildTaskResponseJson("t-1", "completed"),
            });
            var client = BuildClientWith(handler);
            var result = await client.WaitForTaskAsync(
                "t-1",
                pollInterval: TimeSpan.FromMilliseconds(10),
                timeout: TimeSpan.FromSeconds(5));
            Assert.That(result.Status, Is.EqualTo("completed"));
            Assert.That(handler.CallCount, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void WaitForTaskAsync_TaskFailed_ThrowsTaskException()
        {
            var errJson = "{\"type\":\"ImportError\",\"description\":\"row 7 invalid\",\"stacktrace\":\"frame-1\",\"version\":\"v1\"}";
            var handler = new ScriptedTaskHandler(new[] { BuildTaskResponseJson("t-2", "failed", errJson) });
            var client = BuildClientWith(handler);
            var ex = Assert.ThrowsAsync<GetStreamTaskException>(async () =>
                await client.WaitForTaskAsync(
                    "t-2",
                    pollInterval: TimeSpan.FromMilliseconds(10),
                    timeout: TimeSpan.FromSeconds(5)));
            Assert.That(ex!.TaskId, Is.EqualTo("t-2"));
            Assert.That(ex.ErrorType, Is.EqualTo("ImportError"));
            Assert.That(ex.Description, Is.EqualTo("row 7 invalid"));
            Assert.That(ex.StackTrace, Is.EqualTo("frame-1"));
            Assert.That(ex.Version, Is.EqualTo("v1"));
        }

        [Test]
        public void WaitForTaskAsync_Timeout_ThrowsTransportExceptionWithTimeoutType()
        {
            var pending = BuildTaskResponseJson("t-3", "running");
            var handler = new ScriptedTaskHandler(Enumerable.Repeat(pending, 100).ToArray());
            var client = BuildClientWith(handler);
            var ex = Assert.ThrowsAsync<GetStreamTransportException>(async () =>
                await client.WaitForTaskAsync(
                    "t-3",
                    pollInterval: TimeSpan.FromMilliseconds(30),
                    timeout: TimeSpan.FromMilliseconds(80)));
            Assert.That(ex!.ErrorType, Is.EqualTo("timeout"));
        }

        // -- helpers ----------------------------------------------------------------------

        private static BaseClient BuildClientWith(HttpMessageHandler handler)
        {
            var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            return new BaseClient(new StreamOptions
            {
                ApiKey = DummyApiKey,
                ApiSecret = DummySecret,
                HttpClient = http,
            });
        }

        // Hand-build the task-response JSON so unit tests don't trip the
        // NanosecondTimestampConverter on default DateTime values.
        private static string BuildTaskResponseJson(string taskId, string status, string? errorJson = null)
        {
            var sb = new StringBuilder("{");
            sb.Append("\"task_id\":\"").Append(taskId).Append("\",");
            sb.Append("\"status\":\"").Append(status).Append("\",");
            sb.Append("\"duration\":\"0ms\",");
            sb.Append("\"created_at\":\"2026-05-28T12:00:00Z\",");
            sb.Append("\"updated_at\":\"2026-05-28T12:00:00Z\"");
            if (errorJson != null)
            {
                sb.Append(",\"error\":").Append(errorJson);
            }
            sb.Append("}");
            return sb.ToString();
        }

        private static TimeSpan? InvokeParseRetryAfter(HttpResponseMessage resp, DateTimeOffset now)
        {
            var method = typeof(BaseClient).GetMethod(
                "ParseRetryAfter",
                BindingFlags.Static | BindingFlags.NonPublic)!;
            return (TimeSpan?)method.Invoke(null, new object[] { resp, now });
        }

        private sealed class ThrowingHandler : HttpMessageHandler
        {
            private readonly Exception _ex;
            public ThrowingHandler(Exception ex) { _ex = ex; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                throw _ex;
            }
        }

        private sealed class BlockingHandler : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        }

        private sealed class CannedHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _status;
            private readonly string _body;
            private readonly Dictionary<string, string>? _headers;
            public CannedHandler(HttpStatusCode status, string body, Dictionary<string, string>? headers = null)
            {
                _status = status;
                _body = body;
                _headers = headers;
            }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var resp = new HttpResponseMessage(_status)
                {
                    Content = new StringContent(_body, Encoding.UTF8, "application/json"),
                };
                if (_headers != null)
                {
                    foreach (var kv in _headers)
                    {
                        resp.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                    }
                }
                return Task.FromResult(resp);
            }
        }

        private sealed class ScriptedTaskHandler : HttpMessageHandler
        {
            private readonly string[] _bodies;
            public int CallCount { get; private set; }
            public ScriptedTaskHandler(string[] bodies) { _bodies = bodies; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var idx = Math.Min(CallCount, _bodies.Length - 1);
                CallCount++;
                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_bodies[idx], Encoding.UTF8, "application/json"),
                };
                return Task.FromResult(resp);
            }
        }
    }
}
