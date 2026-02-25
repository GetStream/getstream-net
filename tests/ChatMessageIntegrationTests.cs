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

        [Test, Order(2)]
        public async Task GetManyMessages()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];
            var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId });

            var id1 = await SendTestMessage("messaging", channelId, userId, "Msg 1 " + RandomString(6));
            var id2 = await SendTestMessage("messaging", channelId, userId, "Msg 2 " + RandomString(6));
            var id3 = await SendTestMessage("messaging", channelId, userId, "Msg 3 " + RandomString(6));

            // GET /api/v2/chat/channels/{type}/{id}/messages?ids=id1,id2,id3
            var resp = await StreamClient.MakeRequestAsync<object, GetManyMessagesResponse>(
                "GET",
                "/api/v2/chat/channels/{type}/{id}/messages",
                new Dictionary<string, string> { ["ids"] = string.Join(",", id1, id2, id3) },
                null,
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Messages, Is.Not.Null);
            Assert.That(resp.Data!.Messages.Count, Is.EqualTo(3));
        }

        [Test, Order(3)]
        public async Task UpdateMessage()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];
            var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId });

            var msgId = await SendTestMessage("messaging", channelId, userId, "Original text " + RandomString(6));

            var updatedText = "Updated text " + RandomString(8);
            var resp = await StreamClient.MakeRequestAsync<UpdateMessageRequest, UpdateMessageResponse>(
                "POST",
                "/api/v2/chat/messages/{id}",
                null,
                new UpdateMessageRequest
                {
                    Message = new MessageRequest { Text = updatedText, UserID = userId }
                },
                new Dictionary<string, string> { ["id"] = msgId });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Message, Is.Not.Null);
            Assert.That(resp.Data!.Message.Text, Is.EqualTo(updatedText));
        }

        [Test, Order(4)]
        public async Task PartialUpdateMessage()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];
            var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId });

            var msgId = await SendTestMessage("messaging", channelId, userId, "Partial update test " + RandomString(6));

            // Set custom fields
            var setResp = await StreamClient.MakeRequestAsync<UpdateMessagePartialRequest, UpdateMessagePartialResponse>(
                "PUT",
                "/api/v2/chat/messages/{id}",
                null,
                new UpdateMessagePartialRequest
                {
                    UserID = userId,
                    Set = new Dictionary<string, object>
                    {
                        ["priority"] = "high",
                        ["status"] = "reviewed"
                    }
                },
                new Dictionary<string, string> { ["id"] = msgId });

            Assert.That(setResp.Data, Is.Not.Null);
            Assert.That(setResp.Data!.Message, Is.Not.Null);

            // Unset one custom field
            var unsetResp = await StreamClient.MakeRequestAsync<UpdateMessagePartialRequest, UpdateMessagePartialResponse>(
                "PUT",
                "/api/v2/chat/messages/{id}",
                null,
                new UpdateMessagePartialRequest
                {
                    UserID = userId,
                    Unset = new List<string> { "status" }
                },
                new Dictionary<string, string> { ["id"] = msgId });

            Assert.That(unsetResp.Data, Is.Not.Null);
            Assert.That(unsetResp.Data!.Message, Is.Not.Null);
        }

        [Test, Order(5)]
        public async Task DeleteMessage()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];
            var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId });

            var msgId = await SendTestMessage("messaging", channelId, userId, "Message to soft delete " + RandomString(6));

            // Soft delete: DELETE /api/v2/chat/messages/{id} with no query params
            var resp = await StreamClient.MakeRequestAsync<object, DeleteMessageResponse>(
                "DELETE",
                "/api/v2/chat/messages/{id}",
                null,
                null,
                new Dictionary<string, string> { ["id"] = msgId });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Message, Is.Not.Null);
            Assert.That(resp.Data!.Message.Type, Is.EqualTo("deleted"));
        }

        [Test, Order(6)]
        public async Task HardDeleteMessage()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];
            var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId });

            var msgId = await SendTestMessage("messaging", channelId, userId, "Message to hard delete " + RandomString(6));

            // Hard delete: DELETE /api/v2/chat/messages/{id}?hard=true
            var resp = await StreamClient.MakeRequestAsync<object, DeleteMessageResponse>(
                "DELETE",
                "/api/v2/chat/messages/{id}",
                new Dictionary<string, string> { ["hard"] = "true" },
                null,
                new Dictionary<string, string> { ["id"] = msgId });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Message, Is.Not.Null);
            Assert.That(resp.Data!.Message.Type, Is.EqualTo("deleted"));
        }
    }
}
