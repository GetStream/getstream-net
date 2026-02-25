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
        public async Task CreateDistinctChannel()
        {
            var userIds = await CreateTestUsers(2);
            var creatorId = userIds[0];
            var memberId = userIds[1];

            var members = new List<ChannelMemberRequest>
            {
                new ChannelMemberRequest { UserID = creatorId },
                new ChannelMemberRequest { UserID = memberId }
            };

            // Create distinct channel (no channel ID - uses /api/v2/chat/channels/{type}/query)
            var resp = await StreamClient.MakeRequestAsync<ChannelGetOrCreateRequest, ChannelStateResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/query",
                null,
                new ChannelGetOrCreateRequest
                {
                    Data = new ChannelInput
                    {
                        CreatedByID = creatorId,
                        Members = members
                    }
                },
                new Dictionary<string, string> { ["type"] = "messaging" });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Channel, Is.Not.Null);
            var cid1 = resp.Data!.Channel!.Cid;
            Assert.That(cid1, Is.Not.Null.And.Not.Empty);

            // Track for cleanup
            var channelId1 = resp.Data!.Channel!.ID;
            CreatedChannels.Add(("messaging", channelId1));

            // Calling again with same members should return same channel
            var resp2 = await StreamClient.MakeRequestAsync<ChannelGetOrCreateRequest, ChannelStateResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/query",
                null,
                new ChannelGetOrCreateRequest
                {
                    Data = new ChannelInput
                    {
                        CreatedByID = creatorId,
                        Members = members
                    }
                },
                new Dictionary<string, string> { ["type"] = "messaging" });

            Assert.That(resp2.Data, Is.Not.Null);
            Assert.That(resp2.Data!.Channel, Is.Not.Null);
            Assert.That(resp2.Data!.Channel!.Cid, Is.EqualTo(cid1));
        }

        [Test, Order(4)]
        public async Task QueryChannelsTest()
        {
            var userIds = await CreateTestUsers(1);
            var creatorId = userIds[0];

            var channelId = await CreateTestChannel(creatorId);

            // Query by both type and id
            var resp = await QueryChannels(new QueryChannelsRequest
            {
                FilterConditions = new Dictionary<string, object>
                {
                    ["type"] = "messaging",
                    ["id"] = channelId
                }
            });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Channels, Is.Not.Null.And.Not.Empty);
            Assert.That(resp.Data!.Channels[0].Channel, Is.Not.Null);
            Assert.That(resp.Data!.Channels[0].Channel!.ID, Is.EqualTo(channelId));
        }

        [Test, Order(3)]
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
