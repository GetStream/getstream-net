using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GetStream;
using GetStream.Models;
using NUnit.Framework;

namespace GetStream.Tests
{
    [TestFixture]
    public class ChatScheduledMessagesIntegrationTests : ChatTestBase
    {
        // Error code the API returns when scheduled messages are off for the app.
        private const int ScheduledMessagesDisabled = 114;

        private ChatClient _chatClient;

        [OneTimeSetUp]
        public void ChatScheduledMessagesSetup()
        {
            _chatClient = new ChatClient(StreamClient);
        }

        // Millisecond precision, so it compares equal after the round trip.
        private static DateTime InOneHour()
        {
            var t = DateTime.UtcNow.AddHours(1);
            return new DateTime(t.Ticks - t.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
        }

        private static string Rfc3339(DateTime t) =>
            new DateTimeOffset(t, TimeSpan.Zero).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        /// <summary>
        /// Schedules a message, or ignores the test when the app cannot: the feature is
        /// opt-in per app, and backends without it ignore scheduled_at and send right away.
        /// </summary>
        private async Task<ScheduledMessageResponse> ScheduleMessage(string channelId, string userId, string text)
        {
            SendMessageResponse data;
            try
            {
                // scheduled_at must be sent as an RFC 3339 string: the client writes DateTime
                // properties as nanosecond numbers, which chat's time fields reject.
                var resp = await StreamClient.MakeRequestAsync<Dictionary<string, object>, SendMessageResponse>(
                    "POST",
                    "/api/v2/chat/channels/{type}/{id}/message",
                    null,
                    new Dictionary<string, object>
                    {
                        ["message"] = new MessageRequest { Text = text, UserID = userId },
                        ["scheduled_at"] = Rfc3339(InOneHour()),
                    },
                    new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });
                data = resp.Data!;
            }
            catch (GetStreamApiException e) when (e.Code == ScheduledMessagesDisabled)
            {
                Assert.Ignore("Scheduled messages not enabled for this app");
                throw;
            }

            if (data.ScheduledMessage == null)
            {
                Assert.Ignore("Backend does not support scheduled messages: the message was sent immediately");
            }

            return data.ScheduledMessage!;
        }

        [Test]
        public async Task ScheduleQueryUpdateAndSendNow()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];
            var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId });
            var cid = $"messaging:{channelId}";

            var scheduled = await ScheduleMessage(channelId, userId, "Scheduled from getstream-net");
            Assert.That(scheduled.ID, Is.Not.Null.And.Not.Empty);
            Assert.That(scheduled.Status, Is.EqualTo("scheduled"));
            Assert.That(scheduled.Cid, Is.EqualTo(cid));

            var queryResp = await _chatClient.QueryScheduledMessagesAsync(new QueryScheduledMessagesRequest
            {
                UserID = userId,
                Filter = new ScheduledMessagesFilter { Cid = cid }
            });
            Assert.That(queryResp.Data!.ScheduledMessages, Has.Count.EqualTo(1));
            var queried = queryResp.Data.ScheduledMessages[0];
            Assert.That(queried.ID, Is.EqualTo(scheduled.ID));
            Assert.That(queried.Message.Text, Is.EqualTo("Scheduled from getstream-net"));

            var newTime = InOneHour();
            var updateResp = await StreamClient.MakeRequestAsync<Dictionary<string, object>, UpdateScheduledMessageResponse>(
                "PATCH",
                "/api/v2/chat/scheduled_messages/{id}",
                null,
                new Dictionary<string, object>
                {
                    ["scheduled_at"] = Rfc3339(newTime),
                    ["message"] = new MessageRequest { Text = "Rescheduled from getstream-net" },
                },
                new Dictionary<string, string> { ["id"] = scheduled.ID });
            Assert.That(updateResp.Data!.ScheduledMessage.Message.Text, Is.EqualTo("Rescheduled from getstream-net"));
            Assert.That(updateResp.Data.ScheduledMessage.ScheduledAt,
                Is.EqualTo(newTime).Within(TimeSpan.FromMilliseconds(1)));

            var sendNowResp = await _chatClient.SendScheduledMessageNowAsync(scheduled.ID, new SendScheduledMessageNowRequest());
            Assert.That(sendNowResp.Data!.ScheduledMessage.ID, Is.EqualTo(scheduled.ID));
        }

        [Test]
        public async Task DeleteScheduledMessage()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];
            var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId });
            var cid = $"messaging:{channelId}";

            var scheduled = await ScheduleMessage(channelId, userId, "Deleted before sending");

            var deleteResp = await _chatClient.DeleteScheduledMessageAsync(scheduled.ID);
            Assert.That(deleteResp.Data!.ScheduledMessage.ID, Is.EqualTo(scheduled.ID));
            Assert.That(deleteResp.Data.ScheduledMessage.Status, Is.EqualTo("canceled"));

            var queryResp = await _chatClient.QueryScheduledMessagesAsync(new QueryScheduledMessagesRequest
            {
                UserID = userId,
                Filter = new ScheduledMessagesFilter { Cid = cid }
            });
            Assert.That(queryResp.Data!.ScheduledMessages.Select(m => m.ID), Does.Not.Contain(scheduled.ID));
        }
    }
}
