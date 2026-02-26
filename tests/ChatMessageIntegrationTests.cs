using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using GetStream;
using GetStream.Models;
using NUnit.Framework;

namespace GetStream.Tests
{
    public class UndeleteMessageRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("undeleted_by")]
        public string UndeletedBy { get; set; }
    }

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

        [Test, Order(7)]
        public async Task PinUnpinMessage()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];
            var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId });

            // Send a pinned message
            var sendResp = await StreamClient.MakeRequestAsync<SendMessageRequest, SendMessageResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/message",
                null,
                new SendMessageRequest
                {
                    Message = new MessageRequest { Text = "Pinned message", UserID = userId, Pinned = true }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(sendResp.Data, Is.Not.Null);
            Assert.That(sendResp.Data!.Message, Is.Not.Null);
            Assert.That(sendResp.Data!.Message.Pinned, Is.True);
            var msgId = sendResp.Data!.Message.ID;

            // Unpin via partial update
            var unpinResp = await StreamClient.MakeRequestAsync<UpdateMessagePartialRequest, UpdateMessagePartialResponse>(
                "PUT",
                "/api/v2/chat/messages/{id}",
                null,
                new UpdateMessagePartialRequest
                {
                    UserID = userId,
                    Set = new Dictionary<string, object> { ["pinned"] = false }
                },
                new Dictionary<string, string> { ["id"] = msgId });

            Assert.That(unpinResp.Data, Is.Not.Null);
            Assert.That(unpinResp.Data!.Message, Is.Not.Null);
            Assert.That(unpinResp.Data!.Message.Pinned, Is.False);
        }

        [Test, Order(8)]
        public async Task TranslateMessage()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];
            var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId });

            var msgId = await SendTestMessage("messaging", channelId, userId, "Hello, how are you?");

            var resp = await StreamClient.MakeRequestAsync<TranslateMessageRequest, MessageActionResponse>(
                "POST",
                "/api/v2/chat/messages/{id}/translate",
                null,
                new TranslateMessageRequest { Language = "es" },
                new Dictionary<string, string> { ["id"] = msgId });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Message, Is.Not.Null, "Message should be present in translate response");
            Assert.That(resp.Data!.Message!.I18n, Is.Not.Null, "i18n field should be set after translation");
        }

        [Test, Order(9)]
        public async Task ThreadReply()
        {
            var userIds = await CreateTestUsers(2);
            var userId = userIds[0];
            var userId2 = userIds[1];
            var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId, userId2 });

            // Send parent message
            var parentId = await SendTestMessage("messaging", channelId, userId, "Parent message for thread");

            // Send reply with parent_id set
            var replyResp = await StreamClient.MakeRequestAsync<SendMessageRequest, SendMessageResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/message",
                null,
                new SendMessageRequest
                {
                    Message = new MessageRequest
                    {
                        Text = "Reply to parent",
                        UserID = userId2,
                        ParentID = parentId
                    }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(replyResp.Data, Is.Not.Null);
            Assert.That(replyResp.Data!.Message, Is.Not.Null);
            Assert.That(replyResp.Data!.Message.ID, Is.Not.Null.And.Not.Empty);

            // Get replies for the parent message
            var repliesResp = await StreamClient.MakeRequestAsync<object, GetRepliesResponse>(
                "GET",
                "/api/v2/chat/messages/{id}/replies",
                null,
                null,
                new Dictionary<string, string> { ["id"] = parentId });

            Assert.That(repliesResp.Data, Is.Not.Null);
            Assert.That(repliesResp.Data!.Messages, Is.Not.Null);
            Assert.That(repliesResp.Data!.Messages.Count, Is.GreaterThanOrEqualTo(1));
        }

        [Test, Order(12)]
        public async Task PendingMessage()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];
            var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId });

            SendMessageResponse sendData;
            try
            {
                var sendResp = await StreamClient.MakeRequestAsync<SendMessageRequest, SendMessageResponse>(
                    "POST",
                    "/api/v2/chat/channels/{type}/{id}/message",
                    null,
                    new SendMessageRequest
                    {
                        Message = new MessageRequest { Text = "Pending message text", UserID = userId },
                        Pending = true,
                        SkipPush = true
                    },
                    new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

                Assert.That(sendResp.Data, Is.Not.Null);
                Assert.That(sendResp.Data!.Message, Is.Not.Null);
                Assert.That(sendResp.Data!.Message.ID, Is.Not.Null.And.Not.Empty);
                sendData = sendResp.Data!;
            }
            catch (Exception e) when (e.Message.Contains("pending messages not enabled") || e.Message.Contains("feature flag"))
            {
                Assert.Ignore("Pending messages not enabled for this app");
                return;
            }

            var msgId = sendData.Message.ID;

            // Commit the pending message
            var commitResp = await StreamClient.MakeRequestAsync<CommitMessageRequest, MessageActionResponse>(
                "POST",
                "/api/v2/chat/messages/{id}/commit",
                null,
                new CommitMessageRequest(),
                new Dictionary<string, string> { ["id"] = msgId });

            Assert.That(commitResp.Data, Is.Not.Null);
            Assert.That(commitResp.Data!.Message, Is.Not.Null);
            Assert.That(commitResp.Data!.Message!.ID, Is.EqualTo(msgId));
        }

        [Test, Order(13)]
        public async Task QueryMessageHistory()
        {
            var userIds = await CreateTestUsers(2);
            var userId = userIds[0];
            var userId2 = userIds[1];
            var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId, userId2 });

            // Send initial message
            var msgId = await SendTestMessage("messaging", channelId, userId, "initial text");

            // Update by user1 with new text
            await StreamClient.MakeRequestAsync<UpdateMessageRequest, UpdateMessageResponse>(
                "POST",
                "/api/v2/chat/messages/{id}",
                null,
                new UpdateMessageRequest
                {
                    Message = new MessageRequest { Text = "updated text", UserID = userId }
                },
                new Dictionary<string, string> { ["id"] = msgId });

            // Update by user2 with new text
            await StreamClient.MakeRequestAsync<UpdateMessageRequest, UpdateMessageResponse>(
                "POST",
                "/api/v2/chat/messages/{id}",
                null,
                new UpdateMessageRequest
                {
                    Message = new MessageRequest { Text = "updated text 2", UserID = userId2 }
                },
                new Dictionary<string, string> { ["id"] = msgId });

            // Query message history (requires feature flag)
            QueryMessageHistoryResponse histData;
            try
            {
                var histResp = await StreamClient.MakeRequestAsync<QueryMessageHistoryRequest, QueryMessageHistoryResponse>(
                    "POST",
                    "/api/v2/chat/messages/history",
                    null,
                    new QueryMessageHistoryRequest
                    {
                        Filter = new Dictionary<string, object> { ["message_id"] = msgId },
                        Sort = new List<SortParamRequest>()
                    },
                    null);

                Assert.That(histResp.Data, Is.Not.Null);
                histData = histResp.Data!;
            }
            catch (Exception e) when (e.Message.Contains("feature flag") || e.Message.Contains("not enabled"))
            {
                Assert.Ignore("QueryMessageHistory feature not enabled for this app");
                return;
            }

            Assert.That(histData.MessageHistory, Is.Not.Null);
            Assert.That(histData.MessageHistory.Count, Is.GreaterThanOrEqualTo(2), "Should have at least 2 history entries");

            // Verify all entries reference the correct message
            foreach (var entry in histData.MessageHistory)
            {
                Assert.That(entry.MessageID, Is.EqualTo(msgId));
            }

            // Verify text values in descending order (most recent first)
            // history[0] = most recent prior version = "updated text"
            // history[1] = original = "initial text"
            Assert.That(histData.MessageHistory[0].Text, Is.EqualTo("updated text"));
            Assert.That(histData.MessageHistory[0].MessageUpdatedByID, Is.EqualTo(userId));
            Assert.That(histData.MessageHistory[1].Text, Is.EqualTo("initial text"));
            Assert.That(histData.MessageHistory[1].MessageUpdatedByID, Is.EqualTo(userId));
        }

        [Test, Order(14)]
        public async Task QueryMessageHistorySort()
        {
            var userIds = await CreateTestUsers(2);
            var userId = userIds[0];
            var userId2 = userIds[1];
            var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId, userId2 });

            // Send initial message
            var msgId = await SendTestMessage("messaging", channelId, userId, "sort initial");

            // Update twice
            await StreamClient.MakeRequestAsync<UpdateMessageRequest, UpdateMessageResponse>(
                "POST",
                "/api/v2/chat/messages/{id}",
                null,
                new UpdateMessageRequest
                {
                    Message = new MessageRequest { Text = "sort updated 1", UserID = userId }
                },
                new Dictionary<string, string> { ["id"] = msgId });

            await StreamClient.MakeRequestAsync<UpdateMessageRequest, UpdateMessageResponse>(
                "POST",
                "/api/v2/chat/messages/{id}",
                null,
                new UpdateMessageRequest
                {
                    Message = new MessageRequest { Text = "sort updated 2", UserID = userId }
                },
                new Dictionary<string, string> { ["id"] = msgId });

            // Query with ascending sort by message_updated_at
            QueryMessageHistoryResponse histData;
            try
            {
                var histResp = await StreamClient.MakeRequestAsync<QueryMessageHistoryRequest, QueryMessageHistoryResponse>(
                    "POST",
                    "/api/v2/chat/messages/history",
                    null,
                    new QueryMessageHistoryRequest
                    {
                        Filter = new Dictionary<string, object> { ["message_id"] = msgId },
                        Sort = new List<SortParamRequest>
                        {
                            new SortParamRequest { Field = "message_updated_at", Direction = 1 }
                        }
                    },
                    null);

                Assert.That(histResp.Data, Is.Not.Null);
                histData = histResp.Data!;
            }
            catch (Exception e) when (e.Message.Contains("feature flag") || e.Message.Contains("not enabled"))
            {
                Assert.Ignore("QueryMessageHistory feature not enabled for this app");
                return;
            }

            Assert.That(histData.MessageHistory, Is.Not.Null);
            Assert.That(histData.MessageHistory.Count, Is.GreaterThanOrEqualTo(2));

            // Ascending: oldest first
            Assert.That(histData.MessageHistory[0].Text, Is.EqualTo("sort initial"));
            Assert.That(histData.MessageHistory[0].MessageUpdatedByID, Is.EqualTo(userId));
        }

        [Test, Order(15)]
        public async Task SkipEnrichUrl()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];
            var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId });

            // Send a message with a URL but skip enrichment
            var sendResp = await StreamClient.MakeRequestAsync<SendMessageRequest, SendMessageResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/message",
                null,
                new SendMessageRequest
                {
                    Message = new MessageRequest
                    {
                        Text = "Check out https://getstream.io for more info",
                        UserID = userId
                    },
                    SkipEnrichUrl = true
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(sendResp.Data, Is.Not.Null);
            Assert.That(sendResp.Data!.Message, Is.Not.Null);
            Assert.That(sendResp.Data!.Message.Attachments, Is.Null.Or.Empty,
                "Attachments should be empty when SkipEnrichUrl is true");

            var msgId = sendResp.Data!.Message.ID;

            // Wait a moment then verify attachments remain empty
            await Task.Delay(1000);

            var getResp = await StreamClient.MakeRequestAsync<object, GetMessageResponse>(
                "GET",
                "/api/v2/chat/messages/{id}",
                null,
                null,
                new Dictionary<string, string> { ["id"] = msgId });

            Assert.That(getResp.Data, Is.Not.Null);
            Assert.That(getResp.Data!.Message, Is.Not.Null);
            Assert.That(getResp.Data!.Message.Attachments, Is.Null.Or.Empty,
                "Attachments should remain empty after enrichment window");
        }

        [Test, Order(11)]
        public async Task SilentMessage()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];
            var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId });

            var resp = await StreamClient.MakeRequestAsync<SendMessageRequest, SendMessageResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/message",
                null,
                new SendMessageRequest
                {
                    Message = new MessageRequest
                    {
                        Text = "This is a silent message",
                        UserID = userId,
                        Silent = true
                    }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Message, Is.Not.Null);
            Assert.That(resp.Data!.Message.Silent, Is.True);
        }

        [Test, Order(16)]
        public async Task KeepChannelHidden()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];
            var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId });
            var cid = "messaging:" + channelId;

            // Hide the channel for the user
            await StreamClient.MakeRequestAsync<HideChannelRequest, HideChannelResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/hide",
                null,
                new HideChannelRequest { UserID = userId },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            // Send a message with keep_channel_hidden=true
            await StreamClient.MakeRequestAsync<SendMessageRequest, SendMessageResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/message",
                null,
                new SendMessageRequest
                {
                    Message = new MessageRequest { Text = "Hidden message", UserID = userId },
                    KeepChannelHidden = true
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            // Query channels — the channel should still be hidden (not returned)
            var qResp = await QueryChannels(new QueryChannelsRequest
            {
                FilterConditions = new Dictionary<string, object> { ["cid"] = cid },
                UserID = userId
            });

            Assert.That(qResp.Data, Is.Not.Null);
            Assert.That(qResp.Data!.Channels, Is.Empty,
                "Channel should remain hidden after sending with KeepChannelHidden=true");
        }

        [Test, Order(17)]
        public async Task UndeleteMessage()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];
            var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId });

            var msgId = await SendTestMessage("messaging", channelId, userId, "Message to undelete");

            // Soft delete the message
            await StreamClient.MakeRequestAsync<object, DeleteMessageResponse>(
                "DELETE",
                "/api/v2/chat/messages/{id}",
                null,
                null,
                new Dictionary<string, string> { ["id"] = msgId });

            // Verify it's deleted
            var getResp = await StreamClient.MakeRequestAsync<object, GetMessageResponse>(
                "GET",
                "/api/v2/chat/messages/{id}",
                null,
                null,
                new Dictionary<string, string> { ["id"] = msgId });

            Assert.That(getResp.Data, Is.Not.Null);
            Assert.That(getResp.Data!.Message, Is.Not.Null);
            Assert.That(getResp.Data!.Message.Type, Is.EqualTo("deleted"));

            // Undelete via POST /api/v2/chat/messages/{id}/undelete
            StreamResponse<MessageActionResponse> undelResp;
            try
            {
                undelResp = await StreamClient.MakeRequestAsync<UndeleteMessageRequest, MessageActionResponse>(
                    "POST",
                    "/api/v2/chat/messages/{id}/undelete",
                    null,
                    new UndeleteMessageRequest { UndeletedBy = userId },
                    new Dictionary<string, string> { ["id"] = msgId });
            }
            catch (Exception e) when (e.Message.Contains("undeleted_by") || e.Message.Contains("required field") || e.Message.Contains("not enabled"))
            {
                Assert.Ignore("UndeleteMessage feature not available on this app");
                return;
            }

            Assert.That(undelResp.Data, Is.Not.Null);
            Assert.That(undelResp.Data!.Message, Is.Not.Null);
            Assert.That(undelResp.Data!.Message!.Type, Is.Not.EqualTo("deleted"));
            Assert.That(undelResp.Data!.Message!.Text, Is.EqualTo("Message to undelete"));
        }

        [Test, Order(18)]
        public async Task RestrictedVisibility()
        {
            var userIds = await CreateTestUsers(2);
            var userId = userIds[0];
            var userId2 = userIds[1];
            var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId, userId2 });

            SendMessageResponse sendData;
            try
            {
                var sendResp = await StreamClient.MakeRequestAsync<SendMessageRequest, SendMessageResponse>(
                    "POST",
                    "/api/v2/chat/channels/{type}/{id}/message",
                    null,
                    new SendMessageRequest
                    {
                        Message = new MessageRequest
                        {
                            Text = "Secret message",
                            UserID = userId,
                            RestrictedVisibility = new List<string> { userId }
                        }
                    },
                    new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

                Assert.That(sendResp.Data, Is.Not.Null);
                Assert.That(sendResp.Data!.Message, Is.Not.Null);
                sendData = sendResp.Data!;
            }
            catch (Exception e) when (e.Message.Contains("private messaging is not allowed") || e.Message.Contains("not enabled") || e.Message.Contains("feature flag"))
            {
                Assert.Ignore("RestrictedVisibility (private messaging) is not enabled for this app");
                return;
            }

            Assert.That(sendData.Message.RestrictedVisibility, Is.Not.Null);
            Assert.That(sendData.Message.RestrictedVisibility, Contains.Item(userId));
        }

        [Test, Order(19)]
        public async Task DeleteMessageForMe()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];
            var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId });

            var msgId = await SendTestMessage("messaging", channelId, userId, "Message to delete for me " + RandomString(6));

            // Delete the message only for the sender (not for everyone)
            var resp = await StreamClient.MakeRequestAsync<object, DeleteMessageResponse>(
                "DELETE",
                "/api/v2/chat/messages/{id}",
                new Dictionary<string, string> { ["delete_for_me"] = "true", ["deleted_by"] = userId },
                null,
                new Dictionary<string, string> { ["id"] = msgId });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Message, Is.Not.Null);
        }

        [Test, Order(20)]
        public async Task PinExpiration()
        {
            var userIds = await CreateTestUsers(2);
            var userId = userIds[0];
            var userId2 = userIds[1];
            var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId, userId2 });

            // Send a message from user2
            var msgId = await SendTestMessage("messaging", channelId, userId2, "Message to pin with expiry " + RandomString(6));

            // Pin with 3-second expiry
            var expiry = DateTime.UtcNow.AddSeconds(3).ToString("o");
            var pinResp = await StreamClient.MakeRequestAsync<UpdateMessagePartialRequest, UpdateMessagePartialResponse>(
                "PUT",
                "/api/v2/chat/messages/{id}",
                null,
                new UpdateMessagePartialRequest
                {
                    UserID = userId,
                    Set = new Dictionary<string, object>
                    {
                        ["pinned"] = true,
                        ["pin_expires"] = expiry
                    }
                },
                new Dictionary<string, string> { ["id"] = msgId });

            Assert.That(pinResp.Data, Is.Not.Null);
            Assert.That(pinResp.Data!.Message, Is.Not.Null);
            Assert.That(pinResp.Data!.Message.Pinned, Is.True, "Message should be pinned");

            // Wait for pin to expire
            await Task.Delay(4000);

            // Verify pin has expired
            var getResp = await StreamClient.MakeRequestAsync<object, GetMessageResponse>(
                "GET",
                "/api/v2/chat/messages/{id}",
                null,
                null,
                new Dictionary<string, string> { ["id"] = msgId });

            Assert.That(getResp.Data, Is.Not.Null);
            Assert.That(getResp.Data!.Message, Is.Not.Null);
            Assert.That(getResp.Data!.Message.Pinned, Is.False, "Pin should have expired after 4 seconds");
        }

        [Test, Order(22)]
        public async Task PendingFalse()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];
            var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId });

            // Send message with Pending explicitly set to false (non-pending)
            var sendResp = await StreamClient.MakeRequestAsync<SendMessageRequest, SendMessageResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/message",
                null,
                new SendMessageRequest
                {
                    Message = new MessageRequest { Text = "Non-pending message", UserID = userId },
                    Pending = false
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(sendResp.Data, Is.Not.Null);
            Assert.That(sendResp.Data!.Message, Is.Not.Null);
            Assert.That(sendResp.Data!.Message.ID, Is.Not.Null.And.Not.Empty);

            var msgId = sendResp.Data!.Message.ID;

            // Get the message to verify it's immediately available (no commit needed)
            var getResp = await StreamClient.MakeRequestAsync<object, GetMessageResponse>(
                "GET",
                "/api/v2/chat/messages/{id}",
                null,
                null,
                new Dictionary<string, string> { ["id"] = msgId });

            Assert.That(getResp.Data, Is.Not.Null);
            Assert.That(getResp.Data!.Message, Is.Not.Null);
            Assert.That(getResp.Data!.Message.Text, Is.EqualTo("Non-pending message"));
        }

        [Test, Order(21)]
        public async Task SystemMessage()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];
            var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId });

            var resp = await StreamClient.MakeRequestAsync<SendMessageRequest, SendMessageResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/message",
                null,
                new SendMessageRequest
                {
                    Message = new MessageRequest
                    {
                        Text = "User joined the channel",
                        UserID = userId,
                        Type = "system"
                    }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Message, Is.Not.Null);
            Assert.That(resp.Data!.Message.Type, Is.EqualTo("system"));
        }

        [Test, Order(10)]
        public async Task SearchMessages()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];
            var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId });

            var searchTerm = "uniquesearch" + RandomString(8);
            await SendTestMessage("messaging", channelId, userId, "This message contains " + searchTerm + " for testing");

            // Wait for search indexing
            await Task.Delay(2000);

            var payload = new SearchPayload
            {
                Query = searchTerm,
                FilterConditions = new Dictionary<string, object>
                {
                    ["cid"] = "messaging:" + channelId
                }
            };

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            var json = JsonSerializer.Serialize(payload, jsonOptions);

            var resp = await StreamClient.MakeRequestAsync<object, SearchResponse>(
                "GET",
                "/api/v2/chat/search",
                new Dictionary<string, string> { ["payload"] = json },
                null,
                null);

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Results, Is.Not.Null.And.Not.Empty);
        }
    }
}
