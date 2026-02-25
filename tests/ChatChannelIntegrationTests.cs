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
    public class ChatChannelIntegrationTests : ChatTestBase
    {
        [Test, Order(1)]
        public async Task CreateChannelWithID()
        {
            var userIds = await CreateTestUsers(1);
            var creatorId = userIds[0];

            var channelId = await CreateTestChannel(creatorId);

            // Verify channel exists by querying
            var resp = await QueryChannels(new QueryChannelsRequest
            {
                FilterConditions = new Dictionary<string, object>
                {
                    ["id"] = channelId
                }
            });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Channels, Is.Not.Null.And.Not.Empty);
            Assert.That(resp.Data!.Channels[0].Channel, Is.Not.Null);
            Assert.That(resp.Data!.Channels[0].Channel!.ID, Is.EqualTo(channelId));
            Assert.That(resp.Data!.Channels[0].Channel!.Type, Is.EqualTo("messaging"));
        }

        [Test, Order(2)]
        public async Task CreateChannelWithMembers()
        {
            var userIds = await CreateTestUsers(3);
            var creatorId = userIds[0];

            var channelId = await CreateTestChannelWithMembers(creatorId, userIds);

            // Query the channel again to verify members
            var resp = await StreamClient.MakeRequestAsync<ChannelGetOrCreateRequest, ChannelStateResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/query",
                null,
                new ChannelGetOrCreateRequest(),
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Members, Is.Not.Null);
            Assert.That(resp.Data!.Members.Count, Is.GreaterThanOrEqualTo(3));
        }
    }
}
