using System.Collections.Generic;
using System.Threading.Tasks;
using GetStream;
using GetStream.Models;
using NUnit.Framework;

namespace GetStream.Tests
{
    [TestFixture]
    public class ChatMessageIntegrationTests : ChatTestBase
    {
        [Test, Order(1)]
        public async Task SendAndGetMessage()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];
            var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId });

            var msgText = "Hello from integration test " + RandomString(8);
            var sendResp = await StreamClient.MakeRequestAsync<SendMessageRequest, SendMessageResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/message",
                null,
                new SendMessageRequest
                {
                    Message = new MessageRequest { Text = msgText, UserID = userId }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(sendResp.Data, Is.Not.Null);
            Assert.That(sendResp.Data!.Message, Is.Not.Null);
            Assert.That(sendResp.Data!.Message.ID, Is.Not.Null.And.Not.Empty);
            Assert.That(sendResp.Data!.Message.Text, Is.EqualTo(msgText));

            var msgId = sendResp.Data!.Message.ID;

            // Get message by ID
            var getResp = await StreamClient.MakeRequestAsync<object, GetMessageResponse>(
                "GET",
                "/api/v2/chat/messages/{id}",
                null,
                null,
                new Dictionary<string, string> { ["id"] = msgId });

            Assert.That(getResp.Data, Is.Not.Null);
            Assert.That(getResp.Data!.Message, Is.Not.Null);
            Assert.That(getResp.Data!.Message.ID, Is.EqualTo(msgId));
            Assert.That(getResp.Data!.Message.Text, Is.EqualTo(msgText));
        }
    }
}
