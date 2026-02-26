using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GetStream;
using GetStream.Models;
using NUnit.Framework;

namespace GetStream.Tests
{
    [TestFixture]
    public class ChatReactionIntegrationTests : ChatTestBase
    {
        [Test, Order(1)]
        public async Task SendAndGetReactions()
        {
            var userIds = await CreateTestUsers(2);
            var userId = userIds[0];
            var userId2 = userIds[1];
            var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId, userId2 });

            var msgId = await SendTestMessage("messaging", channelId, userId, "React to this message " + RandomString(8));

            // Send a "like" reaction from user2
            var sendResp = await StreamClient.MakeRequestAsync<SendReactionRequest, SendReactionResponse>(
                "POST",
                "/api/v2/chat/messages/{id}/reaction",
                null,
                new SendReactionRequest
                {
                    Reaction = new ReactionRequest { Type = "like", UserID = userId2 }
                },
                new Dictionary<string, string> { ["id"] = msgId });

            Assert.That(sendResp.Data, Is.Not.Null);
            Assert.That(sendResp.Data!.Reaction, Is.Not.Null);
            Assert.That(sendResp.Data!.Reaction.Type, Is.EqualTo("like"));
            Assert.That(sendResp.Data!.Reaction.UserID, Is.EqualTo(userId2));

            // Send a "love" reaction from user1
            await StreamClient.MakeRequestAsync<SendReactionRequest, SendReactionResponse>(
                "POST",
                "/api/v2/chat/messages/{id}/reaction",
                null,
                new SendReactionRequest
                {
                    Reaction = new ReactionRequest { Type = "love", UserID = userId }
                },
                new Dictionary<string, string> { ["id"] = msgId });

            // Get all reactions for the message
            var getResp = await StreamClient.MakeRequestAsync<object, GetReactionsResponse>(
                "GET",
                "/api/v2/chat/messages/{id}/reactions",
                null,
                null,
                new Dictionary<string, string> { ["id"] = msgId });

            Assert.That(getResp.Data, Is.Not.Null);
            Assert.That(getResp.Data!.Reactions, Is.Not.Null);
            Assert.That(getResp.Data!.Reactions.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test, Order(2)]
        public async Task DeleteReaction()
        {
            var userIds = await CreateTestUsers(2);
            var userId = userIds[0];
            var userId2 = userIds[1];
            var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId, userId2 });

            var msgId = await SendTestMessage("messaging", channelId, userId, "Delete reaction test " + RandomString(8));

            // Send a "like" reaction from user2
            await StreamClient.MakeRequestAsync<SendReactionRequest, SendReactionResponse>(
                "POST",
                "/api/v2/chat/messages/{id}/reaction",
                null,
                new SendReactionRequest
                {
                    Reaction = new ReactionRequest { Type = "like", UserID = userId2 }
                },
                new Dictionary<string, string> { ["id"] = msgId });

            // Delete the reaction
            await StreamClient.MakeRequestAsync<object, SendReactionResponse>(
                "DELETE",
                "/api/v2/chat/messages/{id}/reaction/{type}",
                new Dictionary<string, string> { ["user_id"] = userId2 },
                null,
                new Dictionary<string, string> { ["id"] = msgId, ["type"] = "like" });

            // Get reactions and verify the "like" reaction is removed
            var getResp = await StreamClient.MakeRequestAsync<object, GetReactionsResponse>(
                "GET",
                "/api/v2/chat/messages/{id}/reactions",
                null,
                null,
                new Dictionary<string, string> { ["id"] = msgId });

            Assert.That(getResp.Data, Is.Not.Null);
            Assert.That(getResp.Data!.Reactions, Is.Not.Null);
            // Verify no "like" reaction from user2 remains
            var likeFromUser2 = getResp.Data!.Reactions.FindAll(r => r.Type == "like" && r.UserID == userId2);
            Assert.That(likeFromUser2.Count, Is.EqualTo(0));
        }
    }
}
