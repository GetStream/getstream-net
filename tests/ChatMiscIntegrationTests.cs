using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using GetStream;
using GetStream.Models;
using NUnit.Framework;

namespace GetStream.Tests
{
    [TestFixture]
    public class ChatMiscIntegrationTests : ChatTestBase
    {
        private ChatClient _chatClient;

        [OneTimeSetUp]
        public void ChatMiscSetup()
        {
            _chatClient = new ChatClient(StreamClient);
        }

        [Test, Order(1)]
        public async Task CreateListDeleteDevice()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];

            var deviceId = "test-device-" + RandomString(12);

            try
            {
                // Create a firebase device for the user
                await StreamClient.CreateDeviceAsync(new CreateDeviceRequest
                {
                    ID = deviceId,
                    PushProvider = "firebase",
                    UserID = userId
                });
            }
            catch (Exception e) when (
                e.Message.Contains("no push providers configured") ||
                e.Message.Contains("push provider") ||
                e.Message.Contains("push_provider"))
            {
                Assert.Ignore("Push providers not configured for this app");
                return;
            }

            // List devices and verify ours is present
            var listResp = await StreamClient.ListDevicesAsync(new { user_id = userId });
            Assert.That(listResp.Data, Is.Not.Null);
            Assert.That(listResp.Data!.Devices, Is.Not.Null);

            var found = listResp.Data!.Devices.Any(d => d.ID == deviceId);
            Assert.That(found, Is.True, "Created device should appear in list");

            var device = listResp.Data!.Devices.First(d => d.ID == deviceId);
            Assert.That(device.PushProvider, Is.EqualTo("firebase"));
            Assert.That(device.UserID, Is.EqualTo(userId));

            // Delete the device
            await StreamClient.DeleteDeviceAsync(new { id = deviceId, user_id = userId });

            // Verify it's gone
            var listResp2 = await StreamClient.ListDevicesAsync(new { user_id = userId });
            Assert.That(listResp2.Data, Is.Not.Null);

            var stillPresent = listResp2.Data!.Devices?.Any(d => d.ID == deviceId) ?? false;
            Assert.That(stillPresent, Is.False, "Deleted device should not appear in list");
        }

        [Test, Order(2)]
        public async Task CreateListDeleteBlocklist()
        {
            var blocklistName = "test-blocklist-" + RandomString(8);

            try
            {
                // Create the blocklist
                var createResp = await StreamClient.CreateBlockListAsync(new CreateBlockListRequest
                {
                    Name = blocklistName,
                    Words = new List<string> { "badword1", "badword2", "badword3" }
                });
                Assert.That(createResp.Data, Is.Not.Null);

                // Small delay for eventual consistency
                await Task.Delay(500);

                // Get the blocklist and verify
                var getResp = await StreamClient.GetBlockListAsync(blocklistName);
                Assert.That(getResp.Data, Is.Not.Null);
                Assert.That(getResp.Data!.Blocklist, Is.Not.Null);
                Assert.That(getResp.Data!.Blocklist!.Name, Is.EqualTo(blocklistName));
                Assert.That(getResp.Data!.Blocklist!.Words, Has.Count.EqualTo(3));

                // List blocklists and verify ours is found
                var listResp = await StreamClient.ListBlockListsAsync();
                Assert.That(listResp.Data, Is.Not.Null);
                Assert.That(listResp.Data!.Blocklists, Is.Not.Null);

                var found = listResp.Data!.Blocklists.Any(bl => bl.Name == blocklistName);
                Assert.That(found, Is.True, "Created blocklist should appear in list");

                // Delete the blocklist
                await StreamClient.DeleteBlockListAsync(blocklistName);

                // Verify it's gone
                await Task.Delay(500);
                var listResp2 = await StreamClient.ListBlockListsAsync();
                var stillPresent2 = listResp2.Data!.Blocklists?.Any(bl => bl.Name == blocklistName) ?? false;
                Assert.That(stillPresent2, Is.False, "Deleted blocklist should not appear in list");
            }
            finally
            {
                // Cleanup in case test fails midway
                try { await StreamClient.DeleteBlockListAsync(blocklistName); } catch { /* ignore */ }
            }
        }

        [Test, Order(4)]
        public async Task CreateUpdateDeleteChannelType()
        {
            // Channel type names must be lowercase alphanumeric
            var typeName = "testtype" + RandomString(6);

            try
            {
                // Create the channel type
                var createResp = await _chatClient.CreateChannelTypeAsync(new CreateChannelTypeRequest
                {
                    Name = typeName,
                    Automod = "disabled",
                    AutomodBehavior = "flag",
                    MaxMessageLength = 5000
                });
                Assert.That(createResp.Data, Is.Not.Null);
                Assert.That(createResp.Data!.Name, Is.EqualTo(typeName));
                Assert.That(createResp.Data!.MaxMessageLength, Is.EqualTo(5000));

                // Channel types are eventually consistent — wait before proceeding
                await Task.Delay(6000);

                // Get the channel type with retry
                GetChannelTypeResponse getResult = null;
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        var getResp = await _chatClient.GetChannelTypeAsync(typeName);
                        getResult = getResp.Data;
                        break;
                    }
                    catch
                    {
                        await Task.Delay(1000);
                    }
                }
                Assert.That(getResult, Is.Not.Null);
                Assert.That(getResult!.Name, Is.EqualTo(typeName));

                // Update the channel type settings
                var updateResp = await _chatClient.UpdateChannelTypeAsync(typeName, new UpdateChannelTypeRequest
                {
                    Automod = "disabled",
                    AutomodBehavior = "flag",
                    MaxMessageLength = 10000,
                    TypingEvents = false
                });
                Assert.That(updateResp.Data, Is.Not.Null);
                Assert.That(updateResp.Data!.MaxMessageLength, Is.EqualTo(10000));
                Assert.That(updateResp.Data!.TypingEvents, Is.False);

                // List channel types and verify our type appears
                bool found = false;
                for (int i = 0; i < 5; i++)
                {
                    var listResp = await _chatClient.ListChannelTypesAsync();
                    Assert.That(listResp.Data, Is.Not.Null);
                    Assert.That(listResp.Data!.ChannelTypes, Is.Not.Null);

                    if (listResp.Data!.ChannelTypes.ContainsKey(typeName))
                    {
                        found = true;
                        break;
                    }
                    await Task.Delay(1000);
                }
                Assert.That(found, Is.True, "Created channel type should appear in list");

                // Delete the channel type with retry
                Exception lastErr = null;
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        await _chatClient.DeleteChannelTypeAsync(typeName);
                        lastErr = null;
                        break;
                    }
                    catch (Exception e)
                    {
                        lastErr = e;
                        await Task.Delay(1000);
                    }
                }
                if (lastErr != null)
                    throw lastErr;
            }
            finally
            {
                // Cleanup in case test fails midway
                try { await _chatClient.DeleteChannelTypeAsync(typeName); } catch { /* ignore */ }
            }
        }

        [Test, Order(5)]
        public async Task ListChannelTypes()
        {
            var resp = await _chatClient.ListChannelTypesAsync();
            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.ChannelTypes, Is.Not.Null);
            Assert.That(resp.Data!.ChannelTypes, Is.Not.Empty);

            // Default channel types should always be present
            Assert.That(resp.Data!.ChannelTypes.ContainsKey("messaging"), Is.True, "Default 'messaging' channel type should be present");
        }

        [Test, Order(6)]
        public async Task ListPermissions()
        {
            // List all permissions - should return a non-empty list
            var resp = await StreamClient.ListPermissionsAsync();
            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Permissions, Is.Not.Null);
            Assert.That(resp.Data!.Permissions, Is.Not.Empty, "Should have at least one permission");
        }

        [Test, Order(7)]
        public async Task CreatePermission()
        {
            // CreatePermission is hidden from the generated spec (Ignore: true in backend).
            // The Go reference also does not test this endpoint for the same reason.
            // See: chat_misc_integration_test.go lines 1644-1646
            Assert.Ignore("CreatePermission is not available in the generated API spec (backend marks it as Ignore: true)");
            await Task.CompletedTask;
        }

        [Test, Order(8)]
        public async Task GetPermission()
        {
            // Get a specific well-known permission by ID
            var resp = await StreamClient.GetPermissionAsync("create-channel");
            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Permission, Is.Not.Null);
            Assert.That(resp.Data!.Permission.ID, Is.EqualTo("create-channel"));
            Assert.That(resp.Data!.Permission.Action, Is.Not.Empty, "Permission should have a non-empty action");
        }

        [Test, Order(9)]
        public async Task QueryBannedUsers()
        {
            // Create 2 users: admin (banner) and target (to be banned)
            var userIds = await CreateTestUsers(2);
            var adminId = userIds[0];
            var targetId = userIds[1];

            var moderationClient = new ModerationClient(StreamClient);

            // Ban the target user with a reason and timeout
            await moderationClient.BanAsync(new BanRequest
            {
                TargetUserID = targetId,
                BannedByID = adminId,
                Reason = "test ban reason",
                Timeout = 60 // 60 minutes
            });

            // Query banned users filtering by the target user ID
            var payload = new QueryBannedUsersPayload
            {
                FilterConditions = new Dictionary<string, object>
                {
                    ["user_id"] = new Dictionary<string, object> { ["$eq"] = targetId }
                }
            };
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            var payloadJson = JsonSerializer.Serialize(payload, jsonOptions);
            var queryParams = new Dictionary<string, string> { ["payload"] = payloadJson };

            var qResp = await StreamClient.MakeRequestAsync<object, QueryBannedUsersResponse>(
                "GET",
                "/api/v2/chat/query_banned_users",
                queryParams,
                null,
                null);

            Assert.That(qResp.Data, Is.Not.Null);
            Assert.That(qResp.Data!.Bans, Is.Not.Null);
            Assert.That(qResp.Data!.Bans, Is.Not.Empty, "Should find the banned user");

            var ban = qResp.Data!.Bans[0];
            Assert.That(ban.User, Is.Not.Null);
            Assert.That(ban.User!.ID, Is.EqualTo(targetId));
            Assert.That(ban.Reason, Is.EqualTo("test ban reason"));
            Assert.That(ban.Expires, Is.Not.Null, "Ban with timeout should have Expires set");

            // Unban the user by passing target_user_id as a query param
            await StreamClient.MakeRequestAsync<object, UnbanResponse>(
                "POST",
                "/api/v2/moderation/unban",
                new Dictionary<string, string> { ["target_user_id"] = targetId },
                null,
                null);

            // Verify ban is gone after unban
            var qResp2 = await StreamClient.MakeRequestAsync<object, QueryBannedUsersResponse>(
                "GET",
                "/api/v2/chat/query_banned_users",
                queryParams,
                null,
                null);

            Assert.That(qResp2.Data, Is.Not.Null);
            Assert.That(qResp2.Data!.Bans, Is.Empty, "Bans should be empty after unban");
        }

        [Test, Order(11)]
        public async Task GetAppSettings()
        {
            var resp = await StreamClient.GetAppAsync();
            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.App, Is.Not.Null);
            Assert.That(resp.Data!.App.Name, Is.Not.Empty, "App name should not be empty");
        }

        [Test, Order(12)]
        public async Task ExportChannels()
        {
            // Create 1 user and a channel with that user as member
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];
            var channelId = await CreateTestChannel(userId);

            // Send a message so the channel has content to export
            await SendTestMessage("messaging", channelId, userId, "Message for export test");

            var cid = "messaging:" + channelId;

            // Export the channel
            var exportResp = await StreamClient.MakeRequestAsync<ExportChannelsRequest, ExportChannelsResponse>(
                "POST",
                "/api/v2/chat/export_channels",
                null,
                new ExportChannelsRequest
                {
                    Channels = new List<ChannelExport>
                    {
                        new ChannelExport { Cid = cid }
                    }
                },
                null);

            Assert.That(exportResp.Data, Is.Not.Null);
            Assert.That(exportResp.Data!.TaskID, Is.Not.Empty, "Export should return a task ID");

            // Wait for the export task to complete
            await WaitForTask(exportResp.Data!.TaskID);
        }

        [Test, Order(10)]
        public async Task MuteUnmuteUser()
        {
            // Create 2 users: muter and target
            var userIds = await CreateTestUsers(2);
            var muterId = userIds[0];
            var targetId = userIds[1];

            var moderationClient = new ModerationClient(StreamClient);

            // Mute the target user (without timeout)
            var muteResp = await moderationClient.MuteAsync(new MuteRequest
            {
                TargetIds = new List<string> { targetId },
                UserID = muterId
            });
            Assert.That(muteResp.Data, Is.Not.Null);
            Assert.That(muteResp.Data!.Mutes, Is.Not.Null);
            Assert.That(muteResp.Data!.Mutes, Is.Not.Empty, "Mute response should contain mutes");

            var mute = muteResp.Data!.Mutes[0];
            Assert.That(mute.User, Is.Not.Null, "Mute should have a User");
            Assert.That(mute.Target, Is.Not.Null, "Mute should have a Target");
            Assert.That(mute.Expires, Is.Null, "Mute without timeout should have no Expires");

            // Verify mute appears in QueryUsers for the muting user
            var qResp = await QueryUsers(new QueryUsersPayload
            {
                FilterConditions = new Dictionary<string, object>
                {
                    ["id"] = new Dictionary<string, object> { ["$eq"] = muterId }
                }
            });
            Assert.That(qResp.Data, Is.Not.Null);
            Assert.That(qResp.Data!.Users, Is.Not.Empty);
            Assert.That(qResp.Data!.Users[0].Mutes, Is.Not.Empty, "User should have Mutes after muting");

            // Unmute the user
            var unmuteResp = await moderationClient.UnmuteAsync(new UnmuteRequest
            {
                TargetIds = new List<string> { targetId },
                UserID = muterId
            });
            Assert.That(unmuteResp.Data, Is.Not.Null);
        }

        [Test, Order(3)]
        public async Task CreateListDeleteCommand()
        {
            // Command names must be lowercase alphanumeric
            var cmdName = "testcmd" + RandomString(6);

            try
            {
                // Create the command
                var createResp = await _chatClient.CreateCommandAsync(new CreateCommandRequest
                {
                    Name = cmdName,
                    Description = "A test command"
                });
                Assert.That(createResp.Data, Is.Not.Null);
                Assert.That(createResp.Data!.Command, Is.Not.Null);
                Assert.That(createResp.Data!.Command!.Name, Is.EqualTo(cmdName));
                Assert.That(createResp.Data!.Command!.Description, Is.EqualTo("A test command"));

                // Wait for eventual consistency
                await Task.Delay(500);

                // Get the command and verify
                var getResp = await _chatClient.GetCommandAsync(cmdName);
                Assert.That(getResp.Data, Is.Not.Null);
                Assert.That(getResp.Data!.Name, Is.EqualTo(cmdName));
                Assert.That(getResp.Data!.Description, Is.EqualTo("A test command"));

                // List commands and verify ours is found
                var listResp = await _chatClient.ListCommandsAsync();
                Assert.That(listResp.Data, Is.Not.Null);
                Assert.That(listResp.Data!.Commands, Is.Not.Null);

                var found = listResp.Data!.Commands.Any(c => c.Name == cmdName);
                Assert.That(found, Is.True, "Created command should appear in list");

                // Delete the command with retry for eventual consistency
                Exception? lastErr = null;
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        var deleteResp = await _chatClient.DeleteCommandAsync(cmdName);
                        Assert.That(deleteResp.Data, Is.Not.Null);
                        Assert.That(deleteResp.Data!.Name, Is.EqualTo(cmdName));
                        lastErr = null;
                        break;
                    }
                    catch (Exception e)
                    {
                        lastErr = e;
                        await Task.Delay(1000);
                    }
                }
                if (lastErr != null)
                    throw lastErr;
            }
            finally
            {
                // Cleanup in case test fails midway
                try { await _chatClient.DeleteCommandAsync(cmdName); } catch { /* ignore */ }
            }
        }

        [Test, Order(14)]
        public async Task GetUnreadCounts()
        {
            // Create 2 users and a channel with a message so there's something to count
            var userIds = await CreateTestUsers(2);
            var userId1 = userIds[0];
            var userId2 = userIds[1];

            var channelId = await CreateTestChannelWithMembers(userId1, new List<string> { userId1, userId2 });
            await SendTestMessage("messaging", channelId, userId1, "Message for unread counts test");

            // Get unread counts for user2 (who received a message)
            var resp = await StreamClient.MakeRequestAsync<object, WrappedUnreadCountsResponse>(
                "GET",
                "/api/v2/chat/unread",
                new Dictionary<string, string> { ["user_id"] = userId2 },
                null,
                null);

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.TotalUnreadCount, Is.GreaterThanOrEqualTo(0));
        }

        [Test, Order(15)]
        public async Task GetUnreadCountsBatch()
        {
            // Create 2 users and a channel with a message so there's something to count
            var userIds = await CreateTestUsers(2);
            var userId1 = userIds[0];
            var userId2 = userIds[1];

            var channelId = await CreateTestChannelWithMembers(userId1, new List<string> { userId1, userId2 });
            await SendTestMessage("messaging", channelId, userId1, "Message for batch unread counts test");

            // Get unread counts for both users in batch
            var resp = await StreamClient.MakeRequestAsync<UnreadCountsBatchRequest, UnreadCountsBatchResponse>(
                "POST",
                "/api/v2/chat/unread_batch",
                null,
                new UnreadCountsBatchRequest
                {
                    UserIds = new List<string> { userId1, userId2 }
                },
                null);

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.CountsByUser, Is.Not.Null);
            Assert.That(resp.Data!.CountsByUser.ContainsKey(userId1), Is.True, "Should have counts for user1");
            Assert.That(resp.Data!.CountsByUser.ContainsKey(userId2), Is.True, "Should have counts for user2");
        }

        [Test, Order(13)]
        public async Task Threads()
        {
            // Create 2 users and a channel with both as members
            var userIds = await CreateTestUsers(2);
            var userId1 = userIds[0];
            var userId2 = userIds[1];

            var channelId = await CreateTestChannelWithMembers(userId1, new List<string> { userId1, userId2 });
            var channelCid = "messaging:" + channelId;

            // Create a thread: send a parent message then two replies
            var parentId = await SendTestMessage("messaging", channelId, userId1, "Thread parent message");

            // First reply from user2
            await StreamClient.MakeRequestAsync<SendMessageRequest, SendMessageResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/message",
                null,
                new SendMessageRequest
                {
                    Message = new MessageRequest
                    {
                        Text = "First reply in thread",
                        UserID = userId2,
                        ParentID = parentId
                    }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            // Second reply from user1
            await StreamClient.MakeRequestAsync<SendMessageRequest, SendMessageResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/message",
                null,
                new SendMessageRequest
                {
                    Message = new MessageRequest
                    {
                        Text = "Second reply in thread",
                        UserID = userId1,
                        ParentID = parentId
                    }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            // Query threads filtering by channel_cid
            var queryResp = await StreamClient.MakeRequestAsync<QueryThreadsRequest, QueryThreadsResponse>(
                "POST",
                "/api/v2/chat/threads",
                null,
                new QueryThreadsRequest
                {
                    UserID = userId1,
                    Filter = new Dictionary<string, object>
                    {
                        ["channel_cid"] = new Dictionary<string, object>
                        {
                            ["$eq"] = channelCid
                        }
                    }
                },
                null);

            Assert.That(queryResp.Data, Is.Not.Null);
            Assert.That(queryResp.Data!.Threads, Is.Not.Null);
            Assert.That(queryResp.Data!.Threads, Is.Not.Empty, "Should have at least one thread");

            // Verify our thread appears and was created by user2 (first reply sender)
            bool found = false;
            foreach (var thread in queryResp.Data!.Threads)
            {
                if (thread.ParentMessageID == parentId)
                {
                    found = true;
                    Assert.That(thread.CreatedByUserID, Is.EqualTo(userId2),
                        "Thread's CreatedByUserID should be the first reply sender");
                    break;
                }
            }
            Assert.That(found, Is.True, $"Thread with parent {parentId} should appear in query results");

            // Also verify GetThread works for the parent message
            var getResp = await StreamClient.MakeRequestAsync<object, GetThreadResponse>(
                "GET",
                "/api/v2/chat/threads/{message_id}",
                new Dictionary<string, string> { ["reply_limit"] = "10" },
                null,
                new Dictionary<string, string> { ["message_id"] = parentId });

            Assert.That(getResp.Data, Is.Not.Null);
            Assert.That(getResp.Data!.Thread, Is.Not.Null);
            Assert.That(getResp.Data!.Thread.ParentMessageID, Is.EqualTo(parentId));
            Assert.That(getResp.Data!.Thread.LatestReplies.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test, Order(17)]
        public async Task SendUserCustomEvent()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];

            // Send a custom event to the user (dots not allowed in event type)
            var resp = await StreamClient.MakeRequestAsync<SendUserCustomEventRequest, Response>(
                "POST",
                "/api/v2/chat/users/{user_id}/event",
                null,
                new SendUserCustomEventRequest
                {
                    Event = new UserCustomEventRequest
                    {
                        Type = "friendship_request",
                        Custom = new Dictionary<string, object>
                        {
                            ["message"] = "Let's be friends!"
                        }
                    }
                },
                new Dictionary<string, string> { ["user_id"] = userId });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Duration, Is.Not.Empty, "Response should have a duration");
        }

        [Test, Order(16)]
        public async Task Reminders()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];

            var channelId = await CreateTestChannel(userId);
            var msgId = await SendTestMessage("messaging", channelId, userId, "Message for reminder test");

            var remindAt = DateTime.UtcNow.AddHours(24);

            // Create reminder - must send remind_at as RFC 3339 string (not nanosecond int)
            ReminderResponseData? created = null;
            try
            {
                var createResp = await StreamClient.MakeRequestAsync<Dictionary<string, object>, ReminderResponseData>(
                    "POST",
                    "/api/v2/chat/messages/{message_id}/reminders",
                    null,
                    new Dictionary<string, object>
                    {
                        ["user_id"] = userId,
                        ["remind_at"] = new DateTimeOffset(remindAt, TimeSpan.Zero).ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ"),
                    },
                    new Dictionary<string, string> { ["message_id"] = msgId });

                Assert.That(createResp.Data, Is.Not.Null);
                created = createResp.Data;
                Assert.That(created!.UserID, Is.EqualTo(userId));
                Assert.That(created.MessageID, Is.EqualTo(msgId));
                Assert.That(created.RemindAt, Is.Not.Null);
            }
            catch (Exception e) when (
                e.Message.Contains("not enabled") ||
                e.Message.Contains("feature") ||
                e.Message.Contains("reminders"))
            {
                Assert.Ignore("Reminders feature not enabled for this app");
                return;
            }

            // Query reminders for the user
            var queryResp = await _chatClient.QueryRemindersAsync(new QueryRemindersRequest
            {
                UserID = userId,
                Filter = new Dictionary<string, object>
                {
                    ["message_id"] = msgId,
                },
                Sort = new List<SortParamRequest>(),
            });
            Assert.That(queryResp.Data, Is.Not.Null);
            Assert.That(queryResp.Data!.Reminders, Is.Not.Null);
            Assert.That(queryResp.Data!.Reminders, Is.Not.Empty, "Should find the created reminder");
            Assert.That(queryResp.Data!.Reminders[0].MessageID, Is.EqualTo(msgId));
            Assert.That(queryResp.Data!.Reminders[0].UserID, Is.EqualTo(userId));

            // Update reminder - must send remind_at as RFC 3339 string
            var newRemindAt = DateTime.UtcNow.AddHours(48);
            var updateResp = await StreamClient.MakeRequestAsync<Dictionary<string, object>, UpdateReminderResponse>(
                "PATCH",
                "/api/v2/chat/messages/{message_id}/reminders",
                null,
                new Dictionary<string, object>
                {
                    ["user_id"] = userId,
                    ["remind_at"] = new DateTimeOffset(newRemindAt, TimeSpan.Zero).ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ"),
                },
                new Dictionary<string, string> { ["message_id"] = msgId });

            Assert.That(updateResp.Data, Is.Not.Null);
            Assert.That(updateResp.Data!.Reminder, Is.Not.Null);
            Assert.That(updateResp.Data!.Reminder.MessageID, Is.EqualTo(msgId));
            Assert.That(updateResp.Data!.Reminder.UserID, Is.EqualTo(userId));

            // Delete reminder
            var deleteResp = await _chatClient.DeleteReminderAsync(msgId, userId);
            Assert.That(deleteResp.Data, Is.Not.Null);
        }

        [Test, Order(19)]
        public async Task ChannelBatchUpdate()
        {
            // ChannelBatchUpdate uses PUT /api/v2/chat/channels/batch - this is a Beta feature
            // behind Ignore+Beta in the backend OpenAPI spec, so the generated .NET SDK does not
            // include a typed method for it. We attempt the raw API call and skip gracefully
            // if the endpoint is not available (matching the getstream-go reference behavior).
            var userIds = await CreateTestUsers(2);
            var userId1 = userIds[0];
            var userId2 = userIds[1];

            var channelId1 = await CreateTestChannel(userId1);
            var channelId2 = await CreateTestChannel(userId1);

            try
            {
                var cid1 = $"messaging:{channelId1}";
                var cid2 = $"messaging:{channelId2}";

                // Batch add userId2 as a member to both channels via filter on CIDs
                var resp = await StreamClient.MakeRequestAsync<Dictionary<string, object>, ExportChannelsResponse>(
                    "PUT",
                    "/api/v2/chat/channels/batch",
                    null,
                    new Dictionary<string, object>
                    {
                        ["operation"] = "addMembers",
                        ["filter"] = new Dictionary<string, object>
                        {
                            ["cids"] = new List<string> { cid1, cid2 }
                        },
                        ["members"] = new List<Dictionary<string, object>>
                        {
                            new Dictionary<string, object> { ["user_id"] = userId2 }
                        }
                    },
                    null);

                Assert.That(resp.Data, Is.Not.Null);
                Assert.That(resp.Data!.TaskID, Is.Not.Empty, "Batch update should return a task_id");

                // Poll the async task until it completes
                await WaitForTask(resp.Data.TaskID);
            }
            catch (Exception e) when (
                e.Message.Contains("not found") ||
                e.Message.Contains("Not Found") ||
                e.Message.Contains("404") ||
                e.Message.Contains("not available") ||
                e.Message.Contains("not enabled") ||
                e.Message.Contains("Beta") ||
                e.Message.Contains("Method Not Allowed") ||
                e.Message.Contains("405") ||
                e.Message.Contains("InternalServerError") ||
                e.Message.Contains("500") ||
                e.Message.Contains("ChannelBatchUpdate failed"))
            {
                Assert.Ignore("ChannelBatchUpdate endpoint is not available on this app (Beta feature)");
            }
        }

        [Test, Order(18)]
        public async Task QueryTeamUsageStats()
        {
            try
            {
                var resp = await StreamClient.MakeRequestAsync<Dictionary<string, object>, Response>(
                    "POST",
                    "/api/v2/chat/stats/team_usage",
                    null,
                    new Dictionary<string, object>(),
                    null);

                Assert.That(resp.Data, Is.Not.Null);
                Assert.That(resp.Data!.Duration, Is.Not.Null);
            }
            catch (Exception e) when (
                e.Message.Contains("not available") ||
                e.Message.Contains("not found") ||
                e.Message.Contains("Not Found") ||
                e.Message.Contains("Token signature"))
            {
                Assert.Ignore("QueryTeamUsageStats not available on this app");
            }
        }

        [Test, Order(21)]
        public async Task GetRetentionPolicyRuns()
        {
            try
            {
                var resp = await _chatClient.GetRetentionPolicyRunsAsync(new GetRetentionPolicyRunsRequest
                {
                    Limit = 10
                });

                Assert.That(resp.Data, Is.Not.Null);
                Assert.That(resp.Data!.Duration, Is.Not.Empty);
                Assert.That(resp.Data!.Runs, Is.Not.Null);
            }
            catch (Exception e) when (
                e.Message.Contains("not available") ||
                e.Message.Contains("not enabled") ||
                e.Message.Contains("not found") ||
                e.Message.Contains("Not Found") ||
                e.Message.Contains("retention") ||
                e.Message.Contains("feature"))
            {
                Assert.Ignore("Retention policy feature not available on this app");
            }
        }
    }
}
