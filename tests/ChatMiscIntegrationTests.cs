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
    }
}
