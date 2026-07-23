using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GetStream;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace GetStream.Tests
{
    [TestFixture]
    public class RetryTests
    {
        private sealed class ScriptedHandler : HttpMessageHandler
        {
            private readonly Queue<Func<HttpResponseMessage>> _steps;
            public int Calls { get; private set; }

            public ScriptedHandler(params Func<HttpResponseMessage>[] steps) => _steps = new Queue<Func<HttpResponseMessage>>(steps);

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                Calls++;
                return Task.FromResult(_steps.Dequeue()());
            }
        }

        private sealed class ThrowOnceHandler : HttpMessageHandler
        {
            public int Calls { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                Calls++;
                if (Calls == 1) throw new HttpRequestException("reset");
                return Task.FromResult(Json(HttpStatusCode.OK, "{}"));
            }
        }

        // Captures the raw message template (via the "{OriginalFormat}" state entry) alongside the
        // named field values, so tests can assert which placeholders a log line does/doesn't carry --
        // not just substring-match the rendered text.
        private sealed class RecordingLogger : ILogger
        {
            public readonly List<(LogLevel Level, string Template, Dictionary<string, object?> Fields)> Entries = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var template = string.Empty;
                var fields = new Dictionary<string, object?>();
                if (state is IEnumerable<KeyValuePair<string, object>> kvps)
                {
                    foreach (var kv in kvps)
                    {
                        if (kv.Key == "{OriginalFormat}") template = kv.Value?.ToString() ?? string.Empty;
                        else fields[kv.Key] = kv.Value;
                    }
                }
                Entries.Add((logLevel, template, fields));
            }

            public List<(LogLevel Level, string Template, Dictionary<string, object?> Fields)> Named(string ev) =>
                Entries.Where(e => e.Template.StartsWith(ev)).ToList();
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string body, string? retryAfter = null)
        {
            var resp = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            if (retryAfter != null) resp.Headers.TryAddWithoutValidation("Retry-After", retryAfter);
            return resp;
        }

        private static BaseClient Client(HttpMessageHandler handler, RetryConfig? retry = null, ILogger? logger = null) =>
            new BaseClient(new StreamOptions
            {
                ApiKey = "key",
                ApiSecret = "secret-that-is-long-enough-for-hmac-sha256",
                HttpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") },
                Retry = retry,
                Logger = logger,
            });

        private static RetryConfig Enabled(int maxAttempts = 3, double maxBackoffMs = 1) =>
            new RetryConfig { Enabled = true, MaxAttempts = maxAttempts, MaxBackoff = TimeSpan.FromMilliseconds(maxBackoffMs) };

        private static Task<StreamResponse<Dictionary<string, object>>> Get(BaseClient c) =>
            c.MakeRequestAsync<object, Dictionary<string, object>>("GET", "/api/v2/app", null, null, null);

        private static Task<StreamResponse<Dictionary<string, object>>> Head(BaseClient c) =>
            c.MakeRequestAsync<object, Dictionary<string, object>>("HEAD", "/api/v2/app", null, null, null);

        private static Task<StreamResponse<Dictionary<string, object>>> Post(BaseClient c) =>
            c.MakeRequestAsync<object, Dictionary<string, object>>("POST", "/api/v2/x", null, null, null);

        [Test]
        public void DisabledByDefault_SingleAttempt()
        {
            var handler = new ScriptedHandler(() => Json(HttpStatusCode.TooManyRequests, "{}", "1"));
            var client = Client(handler);
            Assert.ThrowsAsync<GetStreamRateLimitException>(() => Get(client));
            Assert.That(handler.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task EnabledGet_RetriesThenSucceeds()
        {
            var handler = new ScriptedHandler(
                () => Json(HttpStatusCode.TooManyRequests, "{}"),
                () => Json(HttpStatusCode.OK, "{}"));
            var client = Client(handler, Enabled());
            await Get(client);
            Assert.That(handler.Calls, Is.EqualTo(2));
        }

        [Test]
        public async Task EnabledHead_RetriesThenSucceeds()
        {
            var handler = new ScriptedHandler(
                () => Json(HttpStatusCode.TooManyRequests, "{}"),
                () => Json(HttpStatusCode.OK, "{}"));
            var client = Client(handler, Enabled());
            await Head(client);
            Assert.That(handler.Calls, Is.EqualTo(2));
        }

        [Test]
        public void EnabledPost_NeverRetried()
        {
            var handler = new ScriptedHandler(() => Json(HttpStatusCode.TooManyRequests, "{}"));
            var client = Client(handler, Enabled());
            Assert.ThrowsAsync<GetStreamRateLimitException>(() => Post(client));
            Assert.That(handler.Calls, Is.EqualTo(1));
        }

        [Test]
        public void ServerError_NeverRetried()
        {
            var handler = new ScriptedHandler(() => Json(HttpStatusCode.InternalServerError, "{\"code\":1,\"message\":\"boom\"}"));
            var client = Client(handler, Enabled());
            Assert.ThrowsAsync<GetStreamApiException>(() => Get(client));
            Assert.That(handler.Calls, Is.EqualTo(1));
        }

        [Test]
        public void Unrecoverable429_NeverRetried()
        {
            var handler = new ScriptedHandler(() => Json(HttpStatusCode.TooManyRequests, "{\"code\":9,\"message\":\"nope\",\"unrecoverable\":true}"));
            var client = Client(handler, Enabled());
            Assert.ThrowsAsync<GetStreamRateLimitException>(() => Get(client));
            Assert.That(handler.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task TransportError_Retried()
        {
            var handler = new ThrowOnceHandler();
            var client = Client(handler, Enabled());
            await Get(client);
            Assert.That(handler.Calls, Is.EqualTo(2));
        }

        [Test]
        public void Exhaustion_SurfacesLastError()
        {
            var handler = new ScriptedHandler(
                () => Json(HttpStatusCode.TooManyRequests, "{}"),
                () => Json(HttpStatusCode.TooManyRequests, "{}"),
                () => Json(HttpStatusCode.TooManyRequests, "{}"));
            var client = Client(handler, Enabled(maxAttempts: 3));
            Assert.ThrowsAsync<GetStreamRateLimitException>(() => Get(client));
            Assert.That(handler.Calls, Is.EqualTo(3));
        }

        [Test]
        public void RetryDelay_ClampsAndJitters()
        {
            var client = Client(new ScriptedHandler(), new RetryConfig { Enabled = true, MaxAttempts = 3, MaxBackoff = TimeSpan.FromSeconds(30) });
            var rateLimited = new GetStreamRateLimitException("rl", 429, 9, new Dictionary<string, string>(), false, "{}", TimeSpan.FromSeconds(600));
            Assert.That(client.RetryDelay(rateLimited, 0), Is.EqualTo(TimeSpan.FromSeconds(30)));

            var transport = new GetStreamTransportException("timeout", "timeout");
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var ceil = TimeSpan.FromTicks(Math.Min(TimeSpan.FromSeconds(30).Ticks, TimeSpan.TicksPerSecond << attempt));
                for (var i = 0; i < 50; i++)
                {
                    var d = client.RetryDelay(transport, attempt);
                    Assert.That(d, Is.InRange(TimeSpan.Zero, ceil));
                }
            }
        }

        // -- Logging integration (CHA-2957/CHA-2959 reconciliation) --------------------------

        [Test]
        public async Task TransportRetry_DebugLogCarriesErrorTypeAndRetryAttempt()
        {
            var logger = new RecordingLogger();
            var client = Client(new ThrowOnceHandler(), Enabled(), logger);
            await Get(client);

            var failed = logger.Named("http.request.failed");
            Assert.That(failed, Has.Count.EqualTo(1));
            var (level, template, fields) = failed[0];
            Assert.That(level, Is.EqualTo(LogLevel.Debug));
            Assert.That(template, Does.Contain("{ErrorType}"));
            Assert.That(template, Does.Contain("{RetryAttempt}"));
            Assert.That(fields["RetryAttempt"], Is.EqualTo(1));
            Assert.That(fields["ErrorType"], Is.EqualTo("unknown"));
        }

        [Test]
        public async Task RateLimitRetry_DebugLogCarriesRetryAttemptButNoErrorType()
        {
            var handler = new ScriptedHandler(
                () => Json(HttpStatusCode.TooManyRequests, "{}"),
                () => Json(HttpStatusCode.OK, "{}"));
            var logger = new RecordingLogger();
            var client = Client(handler, Enabled(), logger);
            await Get(client);

            var failed = logger.Named("http.request.failed");
            Assert.That(failed, Has.Count.EqualTo(1));
            var (level, template, fields) = failed[0];
            Assert.That(level, Is.EqualTo(LogLevel.Debug));
            Assert.That(template, Does.Not.Contain("{ErrorType}"));
            Assert.That(template, Does.Not.Contain("rate_limited"));
            Assert.That(fields.ContainsKey("RetryAttempt"), Is.True);
            Assert.That(fields["RetryAttempt"], Is.EqualTo(1));
            Assert.That(fields.ContainsKey("ErrorType"), Is.False);
        }

        [Test]
        public void Disabled_TransportFailure_LogsSingleErrorWithAllFields()
        {
            var logger = new RecordingLogger();
            var client = Client(new ThrowOnceHandler(), retry: null, logger: logger);
            Assert.ThrowsAsync<GetStreamTransportException>(() => Get(client));

            var failed = logger.Named("http.request.failed");
            Assert.That(failed, Has.Count.EqualTo(1));
            var (level, template, fields) = failed[0];
            Assert.That(level, Is.EqualTo(LogLevel.Error));
            Assert.That(template, Does.Contain("{ErrorType}"));
            Assert.That(fields.ContainsKey("RetryAttempt"), Is.False);
        }

        [Test]
        public void Disabled_RateLimit_LogsNoFailed()
        {
            var handler = new ScriptedHandler(() => Json(HttpStatusCode.TooManyRequests, "{}"));
            var logger = new RecordingLogger();
            var client = Client(handler, retry: null, logger: logger);
            Assert.ThrowsAsync<GetStreamRateLimitException>(() => Get(client));

            Assert.That(logger.Named("http.request.failed"), Is.Empty);
            Assert.That(logger.Named("http.response.received"), Has.Count.EqualTo(1));
        }
    }
}
