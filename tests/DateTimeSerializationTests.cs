using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GetStream;
using GetStream.Models;
using NUnit.Framework;

namespace GetStream.Tests
{
    /// <summary>
    /// Request date-times must go over the wire as RFC 3339 strings: the API spec types every
    /// request date-time as a string, and the server rejects a JSON number for them.
    /// </summary>
    [TestFixture]
    public class DateTimeSerializationTests
    {
        private const string DummyApiKey = "dummy-api-key";
        private const string DummySecret = "dummy-secret-that-is-long-enough-for-hmac-sha256";

        [Test]
        public async Task CreateReminder_SendsDateTimesAsRfc3339Strings()
        {
            var handler = new CapturingHandler();
            var chat = new ChatClient(BuildClientWith(handler));
            var remindAt = new DateTime(2026, 10, 15, 12, 34, 56, DateTimeKind.Utc).AddTicks(1234567);

            await chat.CreateReminderAsync("message-id", new CreateReminderRequest
            {
                RemindAt = remindAt,
                ExpiresAt = new DateTime(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc),
                UserID = "user-id",
            });

            using var body = JsonDocument.Parse(handler.LastBody!);
            var sentRemindAt = body.RootElement.GetProperty("remind_at");
            var sentExpiresAt = body.RootElement.GetProperty("expires_at");
            Assert.That(sentRemindAt.ValueKind, Is.EqualTo(JsonValueKind.String));
            Assert.That(sentRemindAt.GetString(), Is.EqualTo("2026-10-15T12:34:56.1234567Z"));
            Assert.That(sentExpiresAt.GetString(), Is.EqualTo("2026-10-16T00:00:00.0000000Z"));
        }

        [Test]
        public async Task MarkUnread_SendsMessageTimestampAsRfc3339String()
        {
            var handler = new CapturingHandler();
            var chat = new ChatClient(BuildClientWith(handler));

            await chat.MarkUnreadAsync("messaging", "channel-id", new MarkUnreadRequest
            {
                MessageTimestamp = new DateTime(2026, 1, 2, 3, 4, 5, 678, DateTimeKind.Utc),
                UserID = "user-id",
            });

            using var body = JsonDocument.Parse(handler.LastBody!);
            Assert.That(body.RootElement.GetProperty("message_timestamp").GetString(),
                Is.EqualTo("2026-01-02T03:04:05.6780000Z"));
        }

        [Test]
        public async Task UpdateUsers_SendsDateTimeInCustomDataAsRfc3339String()
        {
            // Custom is typed object, so System.Text.Json picks the converter by runtime type and
            // DateTime values inside custom data go out as RFC 3339 strings too.
            var handler = new CapturingHandler();
            var client = new StreamClient(new StreamOptions
            {
                ApiKey = DummyApiKey,
                ApiSecret = DummySecret,
                HttpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) },
            });

            await client.UpdateUsersAsync(new UpdateUsersRequest
            {
                Users = new Dictionary<string, UserRequest>
                {
                    ["user-id"] = new UserRequest
                    {
                        ID = "user-id",
                        Custom = new Dictionary<string, object>
                        {
                            ["birthday"] = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Unspecified),
                        },
                    },
                },
            });

            using var body = JsonDocument.Parse(handler.LastBody!);
            var birthday = body.RootElement.GetProperty("users").GetProperty("user-id")
                .GetProperty("custom").GetProperty("birthday");
            Assert.That(birthday.ValueKind, Is.EqualTo(JsonValueKind.String));
            Assert.That(birthday.GetString(), Is.EqualTo("2026-01-01T09:00:00.0000000Z"));
        }

        [Test]
        public void Write_LocalKind_IsConvertedToUtc()
        {
            var local = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Local);

            var json = JsonSerializer.Serialize(local, Options());

            var expected = local.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(json, Is.EqualTo($"\"{expected}\""));
        }

        [Test]
        public void Write_UnspecifiedKind_IsTreatedAsUtc()
        {
            var unspecified = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Unspecified);

            var json = JsonSerializer.Serialize(unspecified, Options());

            Assert.That(json, Is.EqualTo("\"2026-10-15T12:00:00.0000000Z\""));
        }

        [Test]
        public void ResponseNanosecondTimestamp_RoundTripsToSameInstant()
        {
            // 2025-10-15T12:34:56.789Z in nanoseconds, as the API returns response date-times.
            const string responseJson = "1760531696789000000";

            var read = JsonSerializer.Deserialize<DateTime>(responseJson, Options());
            var written = JsonSerializer.Serialize(read, Options());

            Assert.That(written, Is.EqualTo("\"2025-10-15T12:34:56.7890000Z\""));
            Assert.That(DateTimeOffset.Parse(JsonSerializer.Deserialize<string>(written)!).ToUnixTimeMilliseconds(),
                Is.EqualTo(1760531696789L));
        }

        private static JsonSerializerOptions Options() =>
            new JsonSerializerOptions { Converters = { new NanosecondTimestampConverter() } };

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

        private sealed class CapturingHandler : HttpMessageHandler
        {
            public string? LastBody { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"duration\":\"1ms\"}", Encoding.UTF8, "application/json"),
                };
            }
        }
    }
}
