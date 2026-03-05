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

        [Test, Order(13)]
        public async Task TruncateChannel()
        {
            var userIds = await CreateTestUsers(2);
            var creatorId = userIds[0];
            var memberId = userIds[1];

            var channelId = await CreateTestChannelWithMembers(creatorId, new List<string> { creatorId, memberId });

            // Send 3 messages
            await SendTestMessage("messaging", channelId, creatorId, "Message 1");
            await SendTestMessage("messaging", channelId, creatorId, "Message 2");
            await SendTestMessage("messaging", channelId, creatorId, "Message 3");

            // Truncate
            var truncResp = await StreamClient.MakeRequestAsync<TruncateChannelRequest, TruncateChannelResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/truncate",
                null,
                new TruncateChannelRequest(),
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(truncResp.Data, Is.Not.Null);

            // Verify messages are gone by re-querying the channel
            var resp = await StreamClient.MakeRequestAsync<ChannelGetOrCreateRequest, ChannelStateResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/query",
                null,
                new ChannelGetOrCreateRequest(),
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Messages, Is.Not.Null);
            Assert.That(resp.Data!.Messages.Count, Is.EqualTo(0), "Messages should be empty after truncation");
        }

        [Test, Order(14)]
        public async Task FreezeUnfreezeChannel()
        {
            var userIds = await CreateTestUsers(1);
            var creatorId = userIds[0];

            var channelId = await CreateTestChannel(creatorId);

            // Freeze the channel
            var freezeResp = await StreamClient.MakeRequestAsync<UpdateChannelPartialRequest, UpdateChannelPartialResponse>(
                "PATCH",
                "/api/v2/chat/channels/{type}/{id}",
                null,
                new UpdateChannelPartialRequest
                {
                    Set = new Dictionary<string, object>
                    {
                        ["frozen"] = true
                    }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(freezeResp.Data, Is.Not.Null);
            Assert.That(freezeResp.Data!.Channel, Is.Not.Null);
            Assert.That(freezeResp.Data!.Channel!.Frozen, Is.True);

            // Unfreeze the channel
            var unfreezeResp = await StreamClient.MakeRequestAsync<UpdateChannelPartialRequest, UpdateChannelPartialResponse>(
                "PATCH",
                "/api/v2/chat/channels/{type}/{id}",
                null,
                new UpdateChannelPartialRequest
                {
                    Set = new Dictionary<string, object>
                    {
                        ["frozen"] = false
                    }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(unfreezeResp.Data, Is.Not.Null);
            Assert.That(unfreezeResp.Data!.Channel, Is.Not.Null);
            Assert.That(unfreezeResp.Data!.Channel!.Frozen, Is.False);
        }

        [Test, Order(15)]
        public async Task MarkReadUnread()
        {
            var userIds = await CreateTestUsers(2);
            var creatorId = userIds[0];
            var memberId = userIds[1];

            var channelId = await CreateTestChannelWithMembers(creatorId, new List<string> { creatorId, memberId });

            // Send a message
            var msgId = await SendTestMessage("messaging", channelId, creatorId, "Message to mark read");

            // Mark read for memberId
            var readResp = await StreamClient.MakeRequestAsync<MarkReadRequest, MarkReadResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/read",
                null,
                new MarkReadRequest
                {
                    UserID = memberId
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(readResp.Data, Is.Not.Null);

            // Mark unread from this message
            var unreadResp = await StreamClient.MakeRequestAsync<MarkUnreadRequest, Response>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/unread",
                null,
                new MarkUnreadRequest
                {
                    UserID = memberId,
                    MessageID = msgId
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(unreadResp.Data, Is.Not.Null);
        }

        [Test, Order(16)]
        public async Task MuteUnmuteChannel()
        {
            var userIds = await CreateTestUsers(2);
            var creatorId = userIds[0];
            var memberId = userIds[1];

            var channelId = await CreateTestChannelWithMembers(creatorId, new List<string> { creatorId, memberId });
            var cid = $"messaging:{channelId}";

            // Mute the channel for memberId
            var muteResp = await StreamClient.MakeRequestAsync<MuteChannelRequest, MuteChannelResponse>(
                "POST",
                "/api/v2/chat/moderation/mute/channel",
                null,
                new MuteChannelRequest
                {
                    ChannelCids = new List<string> { cid },
                    UserID = memberId
                },
                null);

            Assert.That(muteResp.Data, Is.Not.Null);
            Assert.That(muteResp.Data!.ChannelMute, Is.Not.Null, "Mute response should contain ChannelMute");
            Assert.That(muteResp.Data!.ChannelMute!.Channel, Is.Not.Null, "ChannelMute should have Channel");
            Assert.That(muteResp.Data!.ChannelMute!.Channel!.Cid, Is.EqualTo(cid));

            // Verify via QueryChannels with muted=true
            var qResp = await QueryChannels(new QueryChannelsRequest
            {
                FilterConditions = new Dictionary<string, object>
                {
                    ["muted"] = true,
                    ["cid"] = cid
                },
                UserID = memberId
            });

            Assert.That(qResp.Data, Is.Not.Null);
            Assert.That(qResp.Data!.Channels, Is.Not.Null);
            Assert.That(qResp.Data!.Channels.Count, Is.EqualTo(1), "Should find exactly 1 muted channel");
            Assert.That(qResp.Data!.Channels[0].Channel!.Cid, Is.EqualTo(cid));

            // Unmute the channel
            var unmuteResp = await StreamClient.MakeRequestAsync<UnmuteChannelRequest, UnmuteResponse>(
                "POST",
                "/api/v2/chat/moderation/unmute/channel",
                null,
                new UnmuteChannelRequest
                {
                    ChannelCids = new List<string> { cid },
                    UserID = memberId
                },
                null);

            Assert.That(unmuteResp.Data, Is.Not.Null);

            // Verify unmute via query with muted=false
            var qResp2 = await QueryChannels(new QueryChannelsRequest
            {
                FilterConditions = new Dictionary<string, object>
                {
                    ["muted"] = false,
                    ["cid"] = cid
                },
                UserID = memberId
            });

            Assert.That(qResp2.Data, Is.Not.Null);
            Assert.That(qResp2.Data!.Channels, Is.Not.Null);
            Assert.That(qResp2.Data!.Channels.Count, Is.EqualTo(1), "Unmuted channel should appear in muted=false query");
        }

        [Test, Order(17)]
        public async Task MemberPartialUpdate()
        {
            var userIds = await CreateTestUsers(2);
            var creatorId = userIds[0];
            var memberId = userIds[1];

            var channelId = await CreateTestChannelWithMembers(creatorId, new List<string> { creatorId, memberId });

            // Set custom fields on member
            var setResp = await StreamClient.MakeRequestAsync<UpdateMemberPartialRequest, UpdateMemberPartialResponse>(
                "PATCH",
                "/api/v2/chat/channels/{type}/{id}/member",
                new Dictionary<string, string> { ["user_id"] = memberId },
                new UpdateMemberPartialRequest
                {
                    Set = new Dictionary<string, object>
                    {
                        ["role_label"] = "moderator",
                        ["score"] = 42
                    }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(setResp.Data, Is.Not.Null);
            Assert.That(setResp.Data!.ChannelMember, Is.Not.Null);
            var custom = (System.Text.Json.JsonElement)setResp.Data!.ChannelMember!.Custom;
            Assert.That(custom.GetProperty("role_label").GetString(), Is.EqualTo("moderator"));

            // Unset score field
            var unsetResp = await StreamClient.MakeRequestAsync<UpdateMemberPartialRequest, UpdateMemberPartialResponse>(
                "PATCH",
                "/api/v2/chat/channels/{type}/{id}/member",
                new Dictionary<string, string> { ["user_id"] = memberId },
                new UpdateMemberPartialRequest
                {
                    Unset = new List<string> { "score" }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(unsetResp.Data, Is.Not.Null);
            Assert.That(unsetResp.Data!.ChannelMember, Is.Not.Null);
            var custom2 = (System.Text.Json.JsonElement)unsetResp.Data!.ChannelMember!.Custom;
            Assert.That(custom2.TryGetProperty("score", out _), Is.False, "score should be unset");
        }

        [Test, Order(18)]
        public async Task AssignRoles()
        {
            var userIds = await CreateTestUsers(2);
            var creatorId = userIds[0];
            var memberId = userIds[1];

            var channelId = await CreateTestChannelWithMembers(creatorId, new List<string> { creatorId, memberId });

            // Assign channel_moderator role to memberId
            var resp = await StreamClient.MakeRequestAsync<UpdateChannelRequest, UpdateChannelResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}",
                null,
                new UpdateChannelRequest
                {
                    AssignRoles = new List<ChannelMemberRequest>
                    {
                        new ChannelMemberRequest { UserID = memberId, ChannelRole = "channel_moderator" }
                    }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(resp.Data, Is.Not.Null);

            // Verify via QueryMembers that the role is set
            var qResp = await QueryMembers(new QueryMembersPayload
            {
                Type = "messaging",
                ID = channelId,
                FilterConditions = new Dictionary<string, object>
                {
                    ["id"] = memberId
                }
            });

            Assert.That(qResp.Data, Is.Not.Null);
            Assert.That(qResp.Data!.Members, Is.Not.Null.And.Not.Empty);
            Assert.That(qResp.Data!.Members[0].ChannelRole, Is.EqualTo("channel_moderator"));
        }

        [Test, Order(19)]
        public async Task AddDemoteModerators()
        {
            var userIds = await CreateTestUsers(2);
            var creatorId = userIds[0];
            var memberId = userIds[1];

            var channelId = await CreateTestChannelWithMembers(creatorId, new List<string> { creatorId, memberId });

            // Add member as moderator via UpdateChannelRequest.AddModerators
            var addResp = await StreamClient.MakeRequestAsync<UpdateChannelRequest, UpdateChannelResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}",
                null,
                new UpdateChannelRequest
                {
                    AddModerators = new List<string> { memberId }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(addResp.Data, Is.Not.Null);

            // Verify via QueryMembers that role is channel_moderator
            var qResp = await QueryMembers(new QueryMembersPayload
            {
                Type = "messaging",
                ID = channelId,
                FilterConditions = new Dictionary<string, object>
                {
                    ["id"] = memberId
                }
            });

            Assert.That(qResp.Data, Is.Not.Null);
            Assert.That(qResp.Data!.Members, Is.Not.Null.And.Not.Empty);
            Assert.That(qResp.Data!.Members[0].ChannelRole, Is.EqualTo("channel_moderator"));

            // Demote moderator back to member
            var demoteResp = await StreamClient.MakeRequestAsync<UpdateChannelRequest, UpdateChannelResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}",
                null,
                new UpdateChannelRequest
                {
                    DemoteModerators = new List<string> { memberId }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(demoteResp.Data, Is.Not.Null);

            // Verify via QueryMembers that role is back to channel_member
            var qResp2 = await QueryMembers(new QueryMembersPayload
            {
                Type = "messaging",
                ID = channelId,
                FilterConditions = new Dictionary<string, object>
                {
                    ["id"] = memberId
                }
            });

            Assert.That(qResp2.Data, Is.Not.Null);
            Assert.That(qResp2.Data!.Members, Is.Not.Null.And.Not.Empty);
            Assert.That(qResp2.Data!.Members[0].ChannelRole, Is.EqualTo("channel_member"));
        }

        [Test, Order(20)]
        public async Task MarkUnreadWithThread()
        {
            var userIds = await CreateTestUsers(2);
            var creatorId = userIds[0];
            var memberId = userIds[1];

            var channelId = await CreateTestChannelWithMembers(creatorId, new List<string> { creatorId, memberId });

            // Send parent message
            var parentMsgId = await SendTestMessage("messaging", channelId, creatorId, "Parent for mark unread thread");

            // Send a reply to create a thread
            var replyResp = await StreamClient.MakeRequestAsync<SendMessageRequest, SendMessageResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/message",
                null,
                new SendMessageRequest
                {
                    Message = new MessageRequest
                    {
                        Text = "Reply in thread",
                        UserID = creatorId,
                        ParentID = parentMsgId
                    }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(replyResp.Data, Is.Not.Null);
            Assert.That(replyResp.Data!.Message, Is.Not.Null);

            // Mark unread from thread (using thread_id = parent message ID)
            var unreadResp = await StreamClient.MakeRequestAsync<MarkUnreadRequest, Response>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/unread",
                null,
                new MarkUnreadRequest
                {
                    UserID = memberId,
                    ThreadID = parentMsgId
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(unreadResp.Data, Is.Not.Null);
        }

        [Test, Order(21)]
        public async Task TruncateWithOptions()
        {
            var userIds = await CreateTestUsers(2);
            var creatorId = userIds[0];
            var memberId = userIds[1];

            var channelId = await CreateTestChannelWithMembers(creatorId, new List<string> { creatorId, memberId });

            // Send 2 messages
            await SendTestMessage("messaging", channelId, creatorId, "Truncate msg 1");
            await SendTestMessage("messaging", channelId, creatorId, "Truncate msg 2");

            // Truncate with message, skip_push=true, hard_delete=true
            var truncResp = await StreamClient.MakeRequestAsync<TruncateChannelRequest, TruncateChannelResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/truncate",
                null,
                new TruncateChannelRequest
                {
                    Message = new MessageRequest
                    {
                        Text = "Channel was truncated",
                        UserID = creatorId
                    },
                    SkipPush = true,
                    HardDelete = true
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(truncResp.Data, Is.Not.Null);
        }

        [Test, Order(22)]
        public async Task PinUnpinChannel()
        {
            var userIds = await CreateTestUsers(2);
            var creatorId = userIds[0];
            var memberId = userIds[1];

            var channelId = await CreateTestChannelWithMembers(creatorId, new List<string> { creatorId, memberId });
            var cid = $"messaging:{channelId}";

            // Pin channel for memberId via UpdateMemberPartial
            var pinResp = await StreamClient.MakeRequestAsync<UpdateMemberPartialRequest, UpdateMemberPartialResponse>(
                "PATCH",
                "/api/v2/chat/channels/{type}/{id}/member",
                new Dictionary<string, string> { ["user_id"] = memberId },
                new UpdateMemberPartialRequest
                {
                    Set = new Dictionary<string, object>
                    {
                        ["pinned"] = true
                    }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(pinResp.Data, Is.Not.Null);

            // Verify via QueryChannels with pinned=true
            var qResp = await QueryChannels(new QueryChannelsRequest
            {
                FilterConditions = new Dictionary<string, object>
                {
                    ["pinned"] = true,
                    ["cid"] = cid
                },
                UserID = memberId
            });

            Assert.That(qResp.Data, Is.Not.Null);
            Assert.That(qResp.Data!.Channels, Is.Not.Null);
            Assert.That(qResp.Data!.Channels.Count, Is.EqualTo(1), "Should find 1 pinned channel");
            Assert.That(qResp.Data!.Channels[0].Channel!.Cid, Is.EqualTo(cid));

            // Unpin channel
            var unpinResp = await StreamClient.MakeRequestAsync<UpdateMemberPartialRequest, UpdateMemberPartialResponse>(
                "PATCH",
                "/api/v2/chat/channels/{type}/{id}/member",
                new Dictionary<string, string> { ["user_id"] = memberId },
                new UpdateMemberPartialRequest
                {
                    Set = new Dictionary<string, object>
                    {
                        ["pinned"] = false
                    }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(unpinResp.Data, Is.Not.Null);

            // Verify unpinned via QueryChannels with pinned=false
            var qResp2 = await QueryChannels(new QueryChannelsRequest
            {
                FilterConditions = new Dictionary<string, object>
                {
                    ["pinned"] = false,
                    ["cid"] = cid
                },
                UserID = memberId
            });

            Assert.That(qResp2.Data, Is.Not.Null);
            Assert.That(qResp2.Data!.Channels, Is.Not.Null);
            Assert.That(qResp2.Data!.Channels.Count, Is.EqualTo(1), "Should find channel with pinned=false");
        }

        [Test, Order(23)]
        public async Task ArchiveUnarchiveChannel()
        {
            var userIds = await CreateTestUsers(2);
            var creatorId = userIds[0];
            var memberId = userIds[1];

            var channelId = await CreateTestChannelWithMembers(creatorId, new List<string> { creatorId, memberId });
            var cid = $"messaging:{channelId}";

            // Archive channel for memberId via UpdateMemberPartial
            var archiveResp = await StreamClient.MakeRequestAsync<UpdateMemberPartialRequest, UpdateMemberPartialResponse>(
                "PATCH",
                "/api/v2/chat/channels/{type}/{id}/member",
                new Dictionary<string, string> { ["user_id"] = memberId },
                new UpdateMemberPartialRequest
                {
                    Set = new Dictionary<string, object>
                    {
                        ["archived"] = true
                    }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(archiveResp.Data, Is.Not.Null);

            // Verify via QueryChannels with archived=true
            var qResp = await QueryChannels(new QueryChannelsRequest
            {
                FilterConditions = new Dictionary<string, object>
                {
                    ["archived"] = true,
                    ["cid"] = cid
                },
                UserID = memberId
            });

            Assert.That(qResp.Data, Is.Not.Null);
            Assert.That(qResp.Data!.Channels, Is.Not.Null);
            Assert.That(qResp.Data!.Channels.Count, Is.EqualTo(1), "Should find 1 archived channel");
            Assert.That(qResp.Data!.Channels[0].Channel!.Cid, Is.EqualTo(cid));

            // Unarchive channel
            var unarchiveResp = await StreamClient.MakeRequestAsync<UpdateMemberPartialRequest, UpdateMemberPartialResponse>(
                "PATCH",
                "/api/v2/chat/channels/{type}/{id}/member",
                new Dictionary<string, string> { ["user_id"] = memberId },
                new UpdateMemberPartialRequest
                {
                    Set = new Dictionary<string, object>
                    {
                        ["archived"] = false
                    }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(unarchiveResp.Data, Is.Not.Null);

            // Verify unarchived via QueryChannels with archived=false
            var qResp2 = await QueryChannels(new QueryChannelsRequest
            {
                FilterConditions = new Dictionary<string, object>
                {
                    ["archived"] = false,
                    ["cid"] = cid
                },
                UserID = memberId
            });

            Assert.That(qResp2.Data, Is.Not.Null);
            Assert.That(qResp2.Data!.Channels, Is.Not.Null);
            Assert.That(qResp2.Data!.Channels.Count, Is.EqualTo(1), "Should find channel with archived=false");
        }

        [Test, Order(24)]
        public async Task AddMembersWithRoles()
        {
            var userIds = await CreateTestUsers(1);
            var creatorId = userIds[0];

            var channelId = await CreateTestChannel(creatorId);

            // Create 2 new users with specific roles to add
            var newUserIds = await CreateTestUsers(2);
            var modUserId = newUserIds[0];
            var memberUserId = newUserIds[1];

            // Add members with specific channel roles
            var addResp = await StreamClient.MakeRequestAsync<UpdateChannelRequest, UpdateChannelResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}",
                null,
                new UpdateChannelRequest
                {
                    AddMembers = new List<ChannelMemberRequest>
                    {
                        new ChannelMemberRequest { UserID = modUserId, ChannelRole = "channel_moderator" },
                        new ChannelMemberRequest { UserID = memberUserId, ChannelRole = "channel_member" }
                    }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(addResp.Data, Is.Not.Null);

            // Query members to verify roles
            var qResp = await QueryMembers(new QueryMembersPayload
            {
                Type = "messaging",
                ID = channelId,
                FilterConditions = new Dictionary<string, object>
                {
                    ["id"] = new Dictionary<string, object> { ["$in"] = newUserIds }
                }
            });

            Assert.That(qResp.Data, Is.Not.Null);
            Assert.That(qResp.Data!.Members, Is.Not.Null.And.Not.Empty);

            // Build a role map keyed by user ID
            var roleMap = qResp.Data!.Members
                .Where(m => m.UserID != null)
                .ToDictionary(m => m.UserID!, m => m.ChannelRole);

            Assert.That(roleMap.ContainsKey(modUserId), Is.True, "modUser should be in members");
            Assert.That(roleMap[modUserId], Is.EqualTo("channel_moderator"), "First user should be channel_moderator");
            Assert.That(roleMap.ContainsKey(memberUserId), Is.True, "memberUser should be in members");
            Assert.That(roleMap[memberUserId], Is.EqualTo("channel_member"), "Second user should be channel_member");
        }

        [Test, Order(25)]
        public async Task MessageCount()
        {
            var userIds = await CreateTestUsers(1);
            var creatorId = userIds[0];

            var channelId = await CreateTestChannel(creatorId);

            // Send a message
            await SendTestMessage("messaging", channelId, creatorId, "Hello message count!");

            // Query the channel and verify message_count >= 1
            var resp = await QueryChannels(new QueryChannelsRequest
            {
                FilterConditions = new Dictionary<string, object>
                {
                    ["id"] = channelId
                }
            });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Channels, Is.Not.Null.And.Not.Empty);
            var channel = resp.Data!.Channels[0].Channel;
            Assert.That(channel, Is.Not.Null);
            // MessageCount may be null if count_messages is disabled on the channel type
            if (channel!.MessageCount.HasValue)
            {
                Assert.That(channel!.MessageCount.Value, Is.GreaterThanOrEqualTo(1));
            }
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

        [Test, Order(26)]
        public async Task SendChannelEvent()
        {
            var userIds = await CreateTestUsers(2);
            var creatorId = userIds[0];
            var memberId = userIds[1];

            var channelId = await CreateTestChannelWithMembers(creatorId, new List<string> { creatorId, memberId });

            var resp = await StreamClient.MakeRequestAsync<SendEventRequest, EventResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/event",
                null,
                new SendEventRequest
                {
                    Event = new EventRequest
                    {
                        Type = "typing.start",
                        UserID = creatorId
                    }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Duration, Is.Not.Null.And.Not.Empty);
        }

        [Test, Order(27)]
        public async Task FilterTags()
        {
            var userIds = await CreateTestUsers(1);
            var creatorId = userIds[0];

            var channelId = await CreateTestChannel(creatorId);

            // Add filter tags
            var addResp = await StreamClient.MakeRequestAsync<UpdateChannelRequest, UpdateChannelResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}",
                null,
                new UpdateChannelRequest
                {
                    AddFilterTags = new List<string> { "sports", "news" }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(addResp.Data, Is.Not.Null);
            Assert.That(addResp.Data!.Channel, Is.Not.Null);

            // Remove a filter tag
            var removeResp = await StreamClient.MakeRequestAsync<UpdateChannelRequest, UpdateChannelResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}",
                null,
                new UpdateChannelRequest
                {
                    RemoveFilterTags = new List<string> { "sports" }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(removeResp.Data, Is.Not.Null);
            Assert.That(removeResp.Data!.Channel, Is.Not.Null);
        }

        [Test, Order(28)]
        public async Task MessageCountDisabled()
        {
            var userIds = await CreateTestUsers(2);
            var creatorId = userIds[0];
            var memberId = userIds[1];

            var channelId = await CreateTestChannelWithMembers(creatorId, new List<string> { creatorId, memberId });

            // Disable count_messages via config_overrides partial update
            await StreamClient.MakeRequestAsync<UpdateChannelPartialRequest, UpdateChannelPartialResponse>(
                "PATCH",
                "/api/v2/chat/channels/{type}/{id}",
                null,
                new UpdateChannelPartialRequest
                {
                    Set = new Dictionary<string, object>
                    {
                        ["config_overrides"] = new Dictionary<string, object>
                        {
                            ["count_messages"] = false
                        }
                    }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            // Send a message
            await SendTestMessage("messaging", channelId, creatorId, "hello world disabled count");

            // Query the channel — MessageCount should be null when count_messages is disabled
            var resp = await QueryChannels(new QueryChannelsRequest
            {
                FilterConditions = new Dictionary<string, object>
                {
                    ["cid"] = "messaging:" + channelId
                },
                UserID = creatorId
            });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Channels, Is.Not.Null.And.Not.Empty);
            var channel = resp.Data!.Channels[0].Channel;
            Assert.That(channel, Is.Not.Null);
            Assert.That(channel!.MessageCount, Is.Null, "MessageCount should be null when count_messages is disabled");
        }

        [Test, Order(30)]
        public async Task HideForCreator()
        {
            var userIds = await CreateTestUsers(2);
            var creatorId = userIds[0];
            var memberId = userIds[1];

            var channelId = $"test-hide-{RandomString(12)}";

            // Create channel with hide_for_creator=true
            await StreamClient.MakeRequestAsync<ChannelGetOrCreateRequest, ChannelStateResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/query",
                null,
                new ChannelGetOrCreateRequest
                {
                    HideForCreator = true,
                    Data = new ChannelInput
                    {
                        CreatedByID = creatorId,
                        Members = new List<ChannelMemberRequest>
                        {
                            new ChannelMemberRequest { UserID = creatorId },
                            new ChannelMemberRequest { UserID = memberId }
                        }
                    }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            CreatedChannels.Add(("messaging", channelId));

            // Channel should be hidden for creator — query without show_hidden should not find it
            var resp = await QueryChannels(new QueryChannelsRequest
            {
                FilterConditions = new Dictionary<string, object>
                {
                    ["cid"] = "messaging:" + channelId
                },
                UserID = creatorId
            });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Channels, Is.Empty, "Channel should be hidden for creator");
        }

        [Test, Order(31)]
        public async Task UploadAndDeleteFile()
        {
            var userIds = await CreateTestUsers(1);
            var creatorId = userIds[0];
            var channelId = await CreateTestChannelWithMembers(creatorId, new List<string> { creatorId });

            // Create a temp file to upload
            var tmpPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"chat-test-{Guid.NewGuid():N}.txt");
            await System.IO.File.WriteAllTextAsync(tmpPath, "hello world test file content");

            try
            {
                // Upload file
                var uploadResp = await StreamClient.MakeRequestAsync<FileUploadRequest, FileUploadResponse>(
                    "POST",
                    "/api/v2/chat/channels/{type}/{id}/file",
                    null,
                    new FileUploadRequest
                    {
                        File = tmpPath,
                        User = new OnlyUserID { ID = creatorId }
                    },
                    new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

                Assert.That(uploadResp.Data, Is.Not.Null);
                Assert.That(uploadResp.Data!.File, Is.Not.Null.And.Not.Empty);
                var fileUrl = uploadResp.Data!.File!;
                Assert.That(fileUrl, Does.Contain("http"));

                // Delete file
                var deleteResp = await StreamClient.MakeRequestAsync<object, Response>(
                    "DELETE",
                    "/api/v2/chat/channels/{type}/{id}/file",
                    new Dictionary<string, string> { ["url"] = fileUrl },
                    null,
                    new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

                Assert.That(deleteResp.Data, Is.Not.Null);
            }
            finally
            {
                if (System.IO.File.Exists(tmpPath))
                    System.IO.File.Delete(tmpPath);
            }
        }

        [Test, Order(32)]
        public async Task UploadAndDeleteImage()
        {
            var userIds = await CreateTestUsers(1);
            var creatorId = userIds[0];
            var channelId = await CreateTestChannelWithMembers(creatorId, new List<string> { creatorId });

            // Create a minimal valid PNG file for upload
            var imagePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"chat-test-{Guid.NewGuid():N}.png");
            // Minimal 1x1 pixel PNG bytes
            byte[] pngBytes = new byte[]
            {
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
                0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
                0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
                0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE,
                0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, 0x54,
                0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00, 0x00,
                0x00, 0x02, 0x00, 0x01, 0xE2, 0x21, 0xBC, 0x33,
                0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
            };
            await System.IO.File.WriteAllBytesAsync(imagePath, pngBytes);

            try
            {
                // Upload image
                var uploadResp = await StreamClient.MakeRequestAsync<ImageUploadRequest, ImageUploadResponse>(
                    "POST",
                    "/api/v2/chat/channels/{type}/{id}/image",
                    null,
                    new ImageUploadRequest
                    {
                        File = imagePath,
                        User = new OnlyUserID { ID = creatorId }
                    },
                    new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

                Assert.That(uploadResp.Data, Is.Not.Null);
                Assert.That(uploadResp.Data!.File, Is.Not.Null.And.Not.Empty);
                var imageUrl = uploadResp.Data!.File!;
                Assert.That(imageUrl, Does.Contain("http"));

                // Delete image
                var deleteResp = await StreamClient.MakeRequestAsync<object, Response>(
                    "DELETE",
                    "/api/v2/chat/channels/{type}/{id}/image",
                    new Dictionary<string, string> { ["url"] = imageUrl },
                    null,
                    new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

                Assert.That(deleteResp.Data, Is.Not.Null);
            }
            finally
            {
                if (System.IO.File.Exists(imagePath))
                    System.IO.File.Delete(imagePath);
            }
        }

        [Test, Order(29)]
        public async Task MarkUnreadWithTimestamp()
        {
            var userIds = await CreateTestUsers(2);
            var creatorId = userIds[0];
            var memberId = userIds[1];

            var channelId = await CreateTestChannelWithMembers(creatorId, new List<string> { creatorId, memberId });

            // Send a message to get a valid timestamp
            var sendResp = await StreamClient.MakeRequestAsync<SendMessageRequest, SendMessageResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/message",
                null,
                new SendMessageRequest
                {
                    Message = new MessageRequest { Text = "test message for timestamp unread", UserID = creatorId }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(sendResp.Data, Is.Not.Null);
            Assert.That(sendResp.Data!.Message, Is.Not.Null);
            var msgTimestamp = sendResp.Data!.Message.CreatedAt;

            // The NanosecondTimestampConverter writes DateTime as a nanosecond integer,
            // but the API's message_timestamp field expects an RFC 3339 string.
            // Pass the timestamp as a pre-formatted string via a raw dictionary to bypass
            // the converter.
            var timestampStr = new DateTimeOffset(msgTimestamp, TimeSpan.Zero)
                .ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ");

            // Mark unread using message timestamp instead of message ID
            var unreadResp = await StreamClient.MakeRequestAsync<Dictionary<string, object>, Response>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/unread",
                null,
                new Dictionary<string, object>
                {
                    ["user_id"] = memberId,
                    ["message_timestamp"] = timestampStr
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            Assert.That(unreadResp.Data, Is.Not.Null);
        }
    }
}
