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

        [Test, Order(5)]
        public async Task UpdateChannel()
        {
            var userIds = await CreateTestUsers(1);
            var creatorId = userIds[0];

            var channelId = await CreateTestChannel(creatorId);

            // Update channel with custom data + message
            var resp = await StreamClient.MakeRequestAsync<UpdateChannelRequest, UpdateChannelResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}",
                null,
                new UpdateChannelRequest
                {
                    Data = new ChannelInputRequest
                    {
                        Custom = new Dictionary<string, object>
                        {
                            ["color"] = "blue"
                        }
                    },
                    Message = new MessageRequest
                    {
                        Text = "Channel updated!",
                        UserID = creatorId
                    }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Channel, Is.Not.Null);

            // Verify custom field
            var custom = resp.Data!.Channel!.Custom;
            Assert.That(custom, Is.Not.Null);
            var customElement = (System.Text.Json.JsonElement)custom;
            Assert.That(customElement.GetProperty("color").GetString(), Is.EqualTo("blue"));
        }

        [Test, Order(6)]
        public async Task PartialUpdateChannel()
        {
            var userIds = await CreateTestUsers(1);
            var creatorId = userIds[0];

            var channelId = await CreateTestChannel(creatorId);

            // Set fields (color + description)
            var setResp = await StreamClient.MakeRequestAsync<UpdateChannelPartialRequest, UpdateChannelPartialResponse>(
                "PATCH",
                "/api/v2/chat/channels/{type}/{id}",
                null,
                new UpdateChannelPartialRequest
                {
                    Set = new Dictionary<string, object>
                    {
                        ["color"] = "red",
                        ["description"] = "A test channel"
                    }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(setResp.Data, Is.Not.Null);
            Assert.That(setResp.Data!.Channel, Is.Not.Null);
            var custom = (System.Text.Json.JsonElement)setResp.Data!.Channel!.Custom;
            Assert.That(custom.GetProperty("color").GetString(), Is.EqualTo("red"));

            // Unset color
            var unsetResp = await StreamClient.MakeRequestAsync<UpdateChannelPartialRequest, UpdateChannelPartialResponse>(
                "PATCH",
                "/api/v2/chat/channels/{type}/{id}",
                null,
                new UpdateChannelPartialRequest
                {
                    Unset = new List<string> { "color" }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(unsetResp.Data, Is.Not.Null);
            Assert.That(unsetResp.Data!.Channel, Is.Not.Null);
            var custom2 = (System.Text.Json.JsonElement)unsetResp.Data!.Channel!.Custom;
            // color should be unset
            Assert.That(custom2.TryGetProperty("color", out _), Is.False);
        }

        [Test, Order(8)]
        public async Task HardDeleteChannels()
        {
            var userIds = await CreateTestUsers(1);
            var creatorId = userIds[0];

            // Create 2 channels specifically for hard deletion (don't track - we're deleting them)
            var channelId1 = $"test-ch-{RandomString(12)}";
            await StreamClient.MakeRequestAsync<ChannelGetOrCreateRequest, ChannelStateResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/query",
                null,
                new ChannelGetOrCreateRequest
                {
                    Data = new ChannelInput { CreatedByID = creatorId }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId1 });

            var channelId2 = $"test-ch-{RandomString(12)}";
            await StreamClient.MakeRequestAsync<ChannelGetOrCreateRequest, ChannelStateResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/query",
                null,
                new ChannelGetOrCreateRequest
                {
                    Data = new ChannelInput { CreatedByID = creatorId }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId2 });

            var cid1 = $"messaging:{channelId1}";
            var cid2 = $"messaging:{channelId2}";

            // Hard delete both channels via batch endpoint
            StreamResponse<DeleteChannelsResponse> resp = null;
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    resp = await StreamClient.MakeRequestAsync<DeleteChannelsRequest, DeleteChannelsResponse>(
                        "POST",
                        "/api/v2/chat/channels/delete",
                        null,
                        new DeleteChannelsRequest
                        {
                            Cids = new List<string> { cid1, cid2 },
                            HardDelete = true
                        },
                        null);
                    break;
                }
                catch (Exception e)
                {
                    if (!e.Message.Contains("Too many requests")) throw;
                    await Task.Delay((i + 1) * 3000);
                }
            }

            Assert.That(resp, Is.Not.Null);
            Assert.That(resp!.Data, Is.Not.Null);
            Assert.That(resp!.Data!.TaskID, Is.Not.Null.And.Not.Empty);

            // Poll task until completed
            await WaitForTask(resp.Data!.TaskID);
        }

        [Test, Order(7)]
        public async Task DeleteChannel()
        {
            var userIds = await CreateTestUsers(1);
            var creatorId = userIds[0];

            // Create a channel specifically for deletion (don't track in CreatedChannels - we're deleting it)
            var channelId = $"test-ch-{RandomString(12)}";
            await StreamClient.MakeRequestAsync<ChannelGetOrCreateRequest, ChannelStateResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/query",
                null,
                new ChannelGetOrCreateRequest
                {
                    Data = new ChannelInput { CreatedByID = creatorId }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            // Soft delete
            var resp = await StreamClient.MakeRequestAsync<object, DeleteChannelResponse>(
                "DELETE",
                "/api/v2/chat/channels/{type}/{id}",
                null,
                null,
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Channel, Is.Not.Null);
        }

        [Test, Order(9)]
        public async Task AddRemoveMembers()
        {
            var userIds = await CreateTestUsers(4);
            var creatorId = userIds[0];
            var memberId1 = userIds[1];
            var memberId2 = userIds[2];
            var memberId3 = userIds[3];

            // Create channel with creator + member1
            var channelId = await CreateTestChannelWithMembers(creatorId, new List<string> { creatorId, memberId1 });

            // Add member2 and member3
            var addResp = await StreamClient.MakeRequestAsync<UpdateChannelRequest, UpdateChannelResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}",
                null,
                new UpdateChannelRequest
                {
                    AddMembers = new List<ChannelMemberRequest>
                    {
                        new ChannelMemberRequest { UserID = memberId2 },
                        new ChannelMemberRequest { UserID = memberId3 }
                    }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(addResp.Data, Is.Not.Null);
            Assert.That(addResp.Data!.Members, Is.Not.Null);
            Assert.That(addResp.Data!.Members.Count, Is.GreaterThanOrEqualTo(4));

            // Remove member3
            var removeResp = await StreamClient.MakeRequestAsync<UpdateChannelRequest, UpdateChannelResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}",
                null,
                new UpdateChannelRequest
                {
                    RemoveMembers = new List<string> { memberId3 }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(removeResp.Data, Is.Not.Null);
            Assert.That(removeResp.Data!.Members, Is.Not.Null);

            // Verify member3 is no longer in the list
            var memberIds = removeResp.Data!.Members
                .Where(m => m.UserID != null)
                .Select(m => m.UserID)
                .ToHashSet();
            Assert.That(memberIds, Does.Not.Contain(memberId3));
        }

        [Test, Order(10)]
        public async Task QueryMembers()
        {
            var userIds = await CreateTestUsers(3);
            var creatorId = userIds[0];

            var channelId = await CreateTestChannelWithMembers(creatorId, userIds);

            // Query members of the channel
            var resp = await QueryMembers(new QueryMembersPayload
            {
                Type = "messaging",
                ID = channelId,
                FilterConditions = new Dictionary<string, object>()
            });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Members, Is.Not.Null);
            Assert.That(resp.Data!.Members.Count, Is.GreaterThanOrEqualTo(3));
        }

        [Test, Order(11)]
        public async Task InviteAcceptReject()
        {
            var userIds = await CreateTestUsers(3);
            var creatorId = userIds[0];
            var invitee1 = userIds[1];
            var invitee2 = userIds[2];

            // Create channel with creator as member and 2 invited users
            var channelId = $"test-ch-{RandomString(12)}";
            await StreamClient.MakeRequestAsync<ChannelGetOrCreateRequest, ChannelStateResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/query",
                null,
                new ChannelGetOrCreateRequest
                {
                    Data = new ChannelInput
                    {
                        CreatedByID = creatorId,
                        Members = new List<ChannelMemberRequest>
                        {
                            new ChannelMemberRequest { UserID = creatorId }
                        },
                        Invites = new List<ChannelMemberRequest>
                        {
                            new ChannelMemberRequest { UserID = invitee1 },
                            new ChannelMemberRequest { UserID = invitee2 }
                        }
                    }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            CreatedChannels.Add(("messaging", channelId));

            // Accept invite for invitee1
            var acceptResp = await StreamClient.MakeRequestAsync<UpdateChannelRequest, UpdateChannelResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}",
                null,
                new UpdateChannelRequest
                {
                    AcceptInvite = true,
                    UserID = invitee1
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(acceptResp.Data, Is.Not.Null);

            // Reject invite for invitee2
            var rejectResp = await StreamClient.MakeRequestAsync<UpdateChannelRequest, UpdateChannelResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}",
                null,
                new UpdateChannelRequest
                {
                    RejectInvite = true,
                    UserID = invitee2
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(rejectResp.Data, Is.Not.Null);
        }

        [Test, Order(12)]
        public async Task HideShowChannel()
        {
            var userIds = await CreateTestUsers(2);
            var creatorId = userIds[0];
            var memberId = userIds[1];

            var channelId = await CreateTestChannelWithMembers(creatorId, new List<string> { creatorId, memberId });

            // Hide the channel for memberId
            var hideResp = await StreamClient.MakeRequestAsync<HideChannelRequest, HideChannelResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/hide",
                null,
                new HideChannelRequest
                {
                    UserID = memberId
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(hideResp.Data, Is.Not.Null);

            // Show the channel for memberId
            var showResp = await StreamClient.MakeRequestAsync<ShowChannelRequest, ShowChannelResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/show",
                null,
                new ShowChannelRequest
                {
                    UserID = memberId
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(showResp.Data, Is.Not.Null);
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
