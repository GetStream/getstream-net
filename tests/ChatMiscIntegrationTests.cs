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
