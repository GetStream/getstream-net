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
                var createResp = await StreamClient.CreateChannelTypeAsync(new CreateChannelTypeRequest
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
                        var getResp = await StreamClient.GetChannelTypeAsync(typeName);
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
                var updateResp = await StreamClient.UpdateChannelTypeAsync(typeName, new UpdateChannelTypeRequest
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
                    var listResp = await StreamClient.ListChannelTypesAsync();
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
                        await StreamClient.DeleteChannelTypeAsync(typeName);
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
                try { await StreamClient.DeleteChannelTypeAsync(typeName); } catch { /* ignore */ }
            }
        }

        [Test, Order(5)]
        public async Task ListChannelTypes()
        {
            var resp = await StreamClient.ListChannelTypesAsync();
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

        [Test, Order(3)]
        public async Task CreateListDeleteCommand()
        {
            // Command names must be lowercase alphanumeric
            var cmdName = "testcmd" + RandomString(6);

            try
            {
                // Create the command
                var createResp = await StreamClient.CreateCommandAsync(new CreateCommandRequest
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
                var getResp = await StreamClient.GetCommandAsync(cmdName);
                Assert.That(getResp.Data, Is.Not.Null);
                Assert.That(getResp.Data!.Name, Is.EqualTo(cmdName));
                Assert.That(getResp.Data!.Description, Is.EqualTo("A test command"));

                // List commands and verify ours is found
                var listResp = await StreamClient.ListCommandsAsync();
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
                        var deleteResp = await StreamClient.DeleteCommandAsync(cmdName);
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
                try { await StreamClient.DeleteCommandAsync(cmdName); } catch { /* ignore */ }
            }
        }
    }
}
