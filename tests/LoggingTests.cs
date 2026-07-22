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
    public class LoggingTests
    {
        private sealed class RecordingLogger : ILogger
        {
            public readonly List<(LogLevel Level, string Message)> Entries = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => Entries.Add((logLevel, formatter(state, exception)));

            public List<(LogLevel Level, string Message)> Named(string ev) => Entries.Where(e => e.Message.StartsWith(ev)).ToList();
        }

        private sealed class CannedHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _status;
            private readonly string _body;
            private readonly bool _throw;

            public CannedHandler(HttpStatusCode status, string body, bool throwTransport = false)
            {
                _status = status; _body = body; _throw = throwTransport;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                if (_throw) throw new HttpRequestException("reset");
                return Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") });
            }
        }

        private static (BaseClient, RecordingLogger) Build(HttpMessageHandler handler, bool logBodies = false)
        {
            var logger = new RecordingLogger();
            var client = new BaseClient(new StreamOptions
            {
                ApiKey = "key",
                // HMAC-SHA256 requires a >= 128-bit (16-byte) key; a bare "secret" throws
                // ArgumentOutOfRangeException at JWT-signing time (see GzipTests.cs for the
                // same constraint documented on the existing dummy secrets in this repo).
                ApiSecret = "secret-that-is-long-enough-for-hmac-sha256",
                HttpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") },
                Logger = logger,
                LogBodies = logBodies,
            });
            return (client, logger);
        }

        private static Task Get(BaseClient c) => c.MakeRequestAsync<object, Dictionary<string, object>>("GET", "/api/v2/app", null, null, null);

        [Test]
        public void ClientInitializedOnceWithSchema()
        {
            var (_, logger) = Build(new CannedHandler(HttpStatusCode.OK, "{}"));
            var inits = logger.Named("client.initialized");
            Assert.That(inits, Has.Count.EqualTo(1));
            Assert.That(inits[0].Message, Does.Contain("getstream-net"));
        }

        [Test]
        public async Task SentAndReceivedOnSuccess()
        {
            var (client, logger) = Build(new CannedHandler(HttpStatusCode.OK, "{}"));
            await Get(client);
            Assert.That(logger.Named("http.request.sent"), Has.Count.EqualTo(1));
            var received = logger.Named("http.response.received");
            Assert.That(received, Has.Count.EqualTo(1));
            Assert.That(received[0].Message, Does.Contain("200"));
        }

        [Test]
        public void ErrorStatusIsReceivedNotFailed()
        {
            var (client, logger) = Build(new CannedHandler(HttpStatusCode.InternalServerError, "{\"code\":1,\"message\":\"boom\"}"));
            Assert.ThrowsAsync<GetStreamApiException>(() => Get(client));
            Assert.That(logger.Named("http.response.received"), Has.Count.EqualTo(1));
            Assert.That(logger.Named("http.request.failed"), Is.Empty);
        }

        [Test]
        public void TransportFailureEmitsFailed()
        {
            var (client, logger) = Build(new CannedHandler(HttpStatusCode.OK, "{}", throwTransport: true));
            Assert.ThrowsAsync<GetStreamTransportException>(() => Get(client));
            var failed = logger.Named("http.request.failed");
            Assert.That(failed, Has.Count.EqualTo(1));
            Assert.That(failed[0].Level, Is.EqualTo(LogLevel.Error));
        }

        [Test]
        public async Task QueryRedaction()
        {
            var (client, logger) = Build(new CannedHandler(HttpStatusCode.OK, "{}"));
            await client.MakeRequestAsync<object, Dictionary<string, object>>("GET", "/api/v2/app", new Dictionary<string, string> { ["api_key"] = "sekret" }, null, null);
            foreach (var e in logger.Entries)
                Assert.That(e.Message, Does.Not.Contain("sekret"));
        }

        [Test]
        public async Task LogBodiesOptInWithRedactionAndWarn()
        {
            // The secret value must not be a substring of its own key name ("token" would make
            // "tok" unavoidably present in output even after the value is redacted).
            var (client, logger) = Build(new CannedHandler(HttpStatusCode.OK, "{\"token\":\"sekrit-val\",\"keep\":\"v\"}"), logBodies: true);
            Assert.That(logger.Entries.Count(e => e.Level == LogLevel.Warning && e.Message.Contains("bodies will be logged")), Is.EqualTo(1));
            await Get(client);
            var received = logger.Named("http.response.received")[0];
            Assert.That(received.Message, Does.Contain("keep"));
            Assert.That(received.Message, Does.Not.Contain("sekrit-val"));
        }

        [Test]
        public void RedactionHelpers()
        {
            Assert.That(LogRedaction.RedactQuery("api_key=sekret&x=1"), Is.EqualTo("api_key=<redacted>&x=1"));
            var body = LogRedaction.RedactJsonBody("{\"api_secret\":\"s\",\"password\":\"p\",\"keep\":\"v\"}");
            Assert.That(body, Does.Not.Contain("\"s\"").And.Contain("\"keep\":\"v\""));
            Assert.That(LogRedaction.RedactJsonBody("not json"), Is.EqualTo("not json"));
        }

        // -- CHA-2957 proactive secret-leak guard: error.message must never carry a raw secret --

        [Test]
        public void RedactMessage_RedactsSecretQueryValuesInFreeText()
        {
            var msg = "transport error (unknown): call to https://chat.stream-io-api.com/api/v2/app?api_key=SUPERSECRETKEY&foo=bar failed";
            var redacted = LogRedaction.RedactMessage(msg);
            Assert.That(redacted, Does.Contain("api_key=<redacted>"));
            Assert.That(redacted, Does.Not.Contain("SUPERSECRETKEY"));
        }

        [Test]
        public void RedactMessage_RedactsAllThreeKeys_PreservesNonSecretParam()
        {
            var msg = "api_key=a&api_secret=b&token=c&foo=bar";
            var redacted = LogRedaction.RedactMessage(msg);
            Assert.That(redacted, Is.EqualTo("api_key=<redacted>&api_secret=<redacted>&token=<redacted>&foo=bar"));
        }
    }
}
