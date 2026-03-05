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
    public class VideoIntegrationTests : ChatTestBase
    {
        [Test, Order(1)]
        public async Task CRUDCallTypeOperations()
        {
            var callTypeName = "calltype-" + Guid.NewGuid().ToString("N")[..16];

            // List call types; if > 10, delete custom ones to avoid hitting limits
            var listResp = await StreamClient.MakeRequestAsync<object, ListCallTypeResponse>(
                "GET", "/api/v2/video/calltypes", null, null, null);
            Assert.That(listResp.Data, Is.Not.Null);

            if (listResp.Data!.CallTypes.Count > 10)
            {
                var builtinTypes = new HashSet<string> { "default", "livestream", "audio_room", "development" };
                foreach (var ct in listResp.Data.CallTypes)
                {
                    if (!builtinTypes.Contains(ct.Key))
                    {
                        try
                        {
                            await StreamClient.MakeRequestAsync<object, Response>(
                                "DELETE", "/api/v2/video/calltypes/{name}", null, null,
                                new Dictionary<string, string> { ["name"] = ct.Key });
                        }
                        catch { /* ignore cleanup errors */ }
                    }
                }
            }

            try
            {
                // Create call type
                var createResp = await StreamClient.MakeRequestAsync<CreateCallTypeRequest, CreateCallTypeResponse>(
                    "POST",
                    "/api/v2/video/calltypes",
                    null,
                    new CreateCallTypeRequest
                    {
                        Name = callTypeName,
                        Settings = new CallSettingsRequest
                        {
                            Audio = new AudioSettingsRequest
                            {
                                DefaultDevice = "speaker",
                                MicDefaultOn = true
                            },
                            Screensharing = new ScreensharingSettingsRequest
                            {
                                AccessRequestEnabled = false,
                                Enabled = true
                            }
                        },
                        NotificationSettings = new NotificationSettingsRequest
                        {
                            Enabled = true,
                            CallNotification = new EventNotificationSettingsRequest
                            {
                                APNS = new APNSPayload { Title = "{{ user.display_name }} invites you to a call", Body = "" },
                                Enabled = true
                            },
                            SessionStarted = new EventNotificationSettingsRequest
                            {
                                APNS = new APNSPayload { Title = "{{ user.display_name }} invites you to a call", Body = "" },
                                Enabled = false
                            },
                            CallLiveStarted = new EventNotificationSettingsRequest
                            {
                                APNS = new APNSPayload { Title = "{{ user.display_name }} invites you to a call", Body = "" },
                                Enabled = false
                            },
                            CallRing = new EventNotificationSettingsRequest
                            {
                                APNS = new APNSPayload { Title = "{{ user.display_name }} invites you to a call", Body = "" },
                                Enabled = false
                            }
                        },
                        Grants = new Dictionary<string, List<string>>
                        {
                            ["admin"] = new List<string> { "send-audio", "send-video", "mute-users" },
                            ["user"] = new List<string> { "send-audio", "send-video" }
                        }
                    },
                    null);

                Assert.That(createResp.Data, Is.Not.Null);
                Assert.That(createResp.Data!.Name, Is.EqualTo(callTypeName));
                Assert.That(createResp.Data.Settings.Audio.MicDefaultOn, Is.True);
                Assert.That(createResp.Data.Settings.Audio.DefaultDevice, Is.EqualTo("speaker"));
                Assert.That(createResp.Data.Settings.Screensharing.AccessRequestEnabled, Is.False);

                // Video call types have eventual consistency; wait before update/read
                await Task.Delay(20000);

                // Update call type (with retry for eventual consistency)
                UpdateCallTypeResponse? updateData = null;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        var updateResp = await StreamClient.MakeRequestAsync<UpdateCallTypeRequest, UpdateCallTypeResponse>(
                            "PUT",
                            "/api/v2/video/calltypes/{name}",
                            null,
                            new UpdateCallTypeRequest
                            {
                                Settings = new CallSettingsRequest
                                {
                                    Audio = new AudioSettingsRequest
                                    {
                                        DefaultDevice = "earpiece",
                                        MicDefaultOn = false
                                    },
                                    Recording = new RecordSettingsRequest
                                    {
                                        Mode = "disabled"
                                    },
                                    Backstage = new BackstageSettingsRequest
                                    {
                                        Enabled = true
                                    }
                                },
                                Grants = new Dictionary<string, List<string>>
                                {
                                    ["host"] = new List<string> { "join-backstage" }
                                }
                            },
                            new Dictionary<string, string> { ["name"] = callTypeName });

                        updateData = updateResp.Data;
                        break;
                    }
                    catch
                    {
                        if (attempt < 2)
                            await Task.Delay(5000);
                    }
                }

                Assert.That(updateData, Is.Not.Null);
                Assert.That(updateData!.Settings.Audio.MicDefaultOn, Is.False);
                Assert.That(updateData.Settings.Audio.DefaultDevice, Is.EqualTo("earpiece"));
                Assert.That(updateData.Settings.Recording.Mode, Is.EqualTo("disabled"));
                Assert.That(updateData.Settings.Backstage.Enabled, Is.True);
                Assert.That(updateData.Grants.ContainsKey("host"), Is.True);
                Assert.That(updateData.Grants["host"], Does.Contain("join-backstage"));

                // Read call type (with retry)
                GetCallTypeResponse? getCallTypeData = null;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        var getResp = await StreamClient.MakeRequestAsync<object, GetCallTypeResponse>(
                            "GET",
                            "/api/v2/video/calltypes/{name}",
                            null,
                            null,
                            new Dictionary<string, string> { ["name"] = callTypeName });

                        getCallTypeData = getResp.Data;
                        break;
                    }
                    catch
                    {
                        if (attempt < 2)
                            await Task.Delay(5000);
                    }
                }

                Assert.That(getCallTypeData, Is.Not.Null);
                Assert.That(getCallTypeData!.Name, Is.EqualTo(callTypeName));
            }
            finally
            {
                // Wait before delete for eventual consistency, then retry delete
                await Task.Delay(6000);
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    try
                    {
                        await StreamClient.MakeRequestAsync<object, Response>(
                            "DELETE",
                            "/api/v2/video/calltypes/{name}",
                            null,
                            null,
                            new Dictionary<string, string> { ["name"] = callTypeName });
                        break;
                    }
                    catch
                    {
                        if (attempt < 4)
                            await Task.Delay(3000);
                    }
                }
            }
        }

        [Test, Order(3)]
        public async Task BlockUnblockUserFromCalls()
        {
            // Create a user to block
            var userIds = await CreateTestUsers(1);
            var badUserId = userIds[0];

            var callType = "default";
            var callId = "test-call-" + Guid.NewGuid().ToString("N")[..16];

            try
            {
                // Create call
                await StreamClient.MakeRequestAsync<GetOrCreateCallRequest, GetOrCreateCallResponse>(
                    "POST",
                    "/api/v2/video/call/{type}/{id}",
                    null,
                    new GetOrCreateCallRequest
                    {
                        Data = new CallRequest { CreatedByID = badUserId }
                    },
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

                // Block user from call
                var blockResp = await StreamClient.MakeRequestAsync<BlockUserRequest, BlockUserResponse>(
                    "POST",
                    "/api/v2/video/call/{type}/{id}/block",
                    null,
                    new BlockUserRequest { UserID = badUserId },
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

                Assert.That(blockResp.Data, Is.Not.Null);

                // Get call and verify user is blocked
                var getResp = await StreamClient.MakeRequestAsync<object, GetCallResponse>(
                    "GET",
                    "/api/v2/video/call/{type}/{id}",
                    null,
                    null,
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

                Assert.That(getResp.Data, Is.Not.Null);
                Assert.That(getResp.Data!.Call, Is.Not.Null);
                Assert.That(getResp.Data.Call.BlockedUserIds, Does.Contain(badUserId));

                // Unblock user from call
                var unblockResp = await StreamClient.MakeRequestAsync<UnblockUserRequest, UnblockUserResponse>(
                    "POST",
                    "/api/v2/video/call/{type}/{id}/unblock",
                    null,
                    new UnblockUserRequest { UserID = badUserId },
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

                Assert.That(unblockResp.Data, Is.Not.Null);

                // Get call and verify user is no longer blocked
                var getResp2 = await StreamClient.MakeRequestAsync<object, GetCallResponse>(
                    "GET",
                    "/api/v2/video/call/{type}/{id}",
                    null,
                    null,
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

                Assert.That(getResp2.Data, Is.Not.Null);
                Assert.That(getResp2.Data!.Call, Is.Not.Null);
                Assert.That(getResp2.Data.Call.BlockedUserIds, Does.Not.Contain(badUserId));
            }
            finally
            {
                // Clean up: delete the call
                try
                {
                    await StreamClient.MakeRequestAsync<object, object>(
                        "POST",
                        "/api/v2/video/call/{type}/{id}/delete",
                        null,
                        null,
                        new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });
                }
                catch { /* ignore cleanup errors */ }
            }
        }

        [Test, Order(5)]
        public async Task MuteAll()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];

            var callType = "default";
            var callId = "test-call-" + Guid.NewGuid().ToString("N")[..16];

            try
            {
                // Create call
                await StreamClient.MakeRequestAsync<GetOrCreateCallRequest, GetOrCreateCallResponse>(
                    "POST",
                    "/api/v2/video/call/{type}/{id}",
                    null,
                    new GetOrCreateCallRequest
                    {
                        Data = new CallRequest { CreatedByID = userId }
                    },
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

                // Mute all users in the call
                var muteResp = await StreamClient.MakeRequestAsync<MuteUsersRequest, MuteUsersResponse>(
                    "POST",
                    "/api/v2/video/call/{type}/{id}/mute_users",
                    null,
                    new MuteUsersRequest
                    {
                        MutedByID = userId,
                        MuteAllUsers = true,
                        Audio = true
                    },
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

                Assert.That(muteResp.Data, Is.Not.Null);
                Assert.That(muteResp.Data!.Duration, Is.Not.Null.And.Not.Empty);
            }
            finally
            {
                try
                {
                    await StreamClient.MakeRequestAsync<object, object>(
                        "POST",
                        "/api/v2/video/call/{type}/{id}/delete",
                        null,
                        null,
                        new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });
                }
                catch { /* ignore cleanup errors */ }
            }
        }

        [Test, Order(6)]
        public async Task MuteSomeUsers()
        {
            // Create 2 users: one to be the muter, one to be muted
            var userIds = await CreateTestUsers(2);
            var muterId = userIds[0];
            var targetId = userIds[1];

            var callType = "default";
            var callId = "test-call-" + Guid.NewGuid().ToString("N")[..16];

            try
            {
                // Create call with both users as members
                await StreamClient.MakeRequestAsync<GetOrCreateCallRequest, GetOrCreateCallResponse>(
                    "POST",
                    "/api/v2/video/call/{type}/{id}",
                    null,
                    new GetOrCreateCallRequest
                    {
                        Data = new CallRequest
                        {
                            CreatedByID = muterId,
                            Members = userIds.Select(id => new MemberRequest { UserID = id }).ToList()
                        }
                    },
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

                // Mute specific users (audio, video, screenshare)
                var muteResp = await StreamClient.MakeRequestAsync<MuteUsersRequest, MuteUsersResponse>(
                    "POST",
                    "/api/v2/video/call/{type}/{id}/mute_users",
                    null,
                    new MuteUsersRequest
                    {
                        MutedByID = muterId,
                        UserIds = new List<string> { targetId },
                        Audio = true,
                        Video = true,
                        Screenshare = true
                    },
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

                Assert.That(muteResp.Data, Is.Not.Null);
                Assert.That(muteResp.Data!.Duration, Is.Not.Null.And.Not.Empty);
            }
            finally
            {
                try
                {
                    await StreamClient.MakeRequestAsync<object, object>(
                        "POST",
                        "/api/v2/video/call/{type}/{id}/delete",
                        null,
                        null,
                        new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });
                }
                catch { /* ignore cleanup errors */ }
            }
        }

        [Test, Order(4)]
        public async Task SendCustomEvent()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];

            var callType = "default";
            var callId = "test-call-" + Guid.NewGuid().ToString("N")[..16];

            try
            {
                // Create call
                await StreamClient.MakeRequestAsync<GetOrCreateCallRequest, GetOrCreateCallResponse>(
                    "POST",
                    "/api/v2/video/call/{type}/{id}",
                    null,
                    new GetOrCreateCallRequest
                    {
                        Data = new CallRequest { CreatedByID = userId }
                    },
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

                // Send a custom event to the call
                var sendEventResp = await StreamClient.MakeRequestAsync<SendCallEventRequest, SendCallEventResponse>(
                    "POST",
                    "/api/v2/video/call/{type}/{id}/event",
                    null,
                    new SendCallEventRequest
                    {
                        UserID = userId,
                        Custom = new Dictionary<string, object> { ["bananas"] = "good" }
                    },
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

                Assert.That(sendEventResp.Data, Is.Not.Null);
            }
            finally
            {
                try
                {
                    await StreamClient.MakeRequestAsync<object, object>(
                        "POST",
                        "/api/v2/video/call/{type}/{id}/delete",
                        null,
                        null,
                        new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });
                }
                catch { /* ignore cleanup errors */ }
            }
        }

        [Test, Order(7)]
        public async Task UpdateUserPermissions()
        {
            // Create a user to grant/revoke permissions for
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];

            var callType = "default";
            var callId = "test-call-" + Guid.NewGuid().ToString("N")[..16];

            try
            {
                // Create call
                await StreamClient.MakeRequestAsync<GetOrCreateCallRequest, GetOrCreateCallResponse>(
                    "POST",
                    "/api/v2/video/call/{type}/{id}",
                    null,
                    new GetOrCreateCallRequest
                    {
                        Data = new CallRequest { CreatedByID = userId }
                    },
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

                // Revoke permissions
                var revokeResp = await StreamClient.MakeRequestAsync<UpdateUserPermissionsRequest, UpdateUserPermissionsResponse>(
                    "POST",
                    "/api/v2/video/call/{type}/{id}/user_permissions",
                    null,
                    new UpdateUserPermissionsRequest
                    {
                        UserID = userId,
                        RevokePermissions = new List<string> { "send-audio" }
                    },
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

                Assert.That(revokeResp.Data, Is.Not.Null);
                Assert.That(revokeResp.Data!.Duration, Is.Not.Null.And.Not.Empty);

                // Grant permissions back
                var grantResp = await StreamClient.MakeRequestAsync<UpdateUserPermissionsRequest, UpdateUserPermissionsResponse>(
                    "POST",
                    "/api/v2/video/call/{type}/{id}/user_permissions",
                    null,
                    new UpdateUserPermissionsRequest
                    {
                        UserID = userId,
                        GrantPermissions = new List<string> { "send-audio" }
                    },
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

                Assert.That(grantResp.Data, Is.Not.Null);
                Assert.That(grantResp.Data!.Duration, Is.Not.Null.And.Not.Empty);
            }
            finally
            {
                try
                {
                    await StreamClient.MakeRequestAsync<object, object>(
                        "POST",
                        "/api/v2/video/call/{type}/{id}/delete",
                        null,
                        null,
                        new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });
                }
                catch { /* ignore cleanup errors */ }
            }
        }

        [Test, Order(8)]
        public async Task DeactivateUser()
        {
            var userIds = await CreateTestUsers(2);
            var aliceId = userIds[0];
            var bobId = userIds[1];

            // Deactivate single user
            var deactivateResp = await StreamClient.DeactivateUserAsync(aliceId, new DeactivateUserRequest());
            Assert.That(deactivateResp.Data, Is.Not.Null);
            Assert.That(deactivateResp.Data!.Duration, Is.Not.Null.And.Not.Empty);

            // Reactivate single user
            var reactivateResp = await StreamClient.ReactivateUserAsync(aliceId, new ReactivateUserRequest());
            Assert.That(reactivateResp.Data, Is.Not.Null);
            Assert.That(reactivateResp.Data!.Duration, Is.Not.Null.And.Not.Empty);

            // Batch deactivate multiple users
            var batchResp = await StreamClient.DeactivateUsersAsync(new DeactivateUsersRequest
            {
                UserIds = new List<string> { aliceId, bobId }
            });
            Assert.That(batchResp.Data, Is.Not.Null);
            Assert.That(batchResp.Data!.TaskID, Is.Not.Null.And.Not.Empty);

            // Poll task until complete
            await WaitForTask(batchResp.Data.TaskID);
        }

        [Test, Order(10)]
        public async Task UserBlocking()
        {
            // Create 2 users: alice (blocker) and bob (to be blocked)
            var userIds = await CreateTestUsers(2);
            var aliceId = userIds[0];
            var bobId = userIds[1];

            // Block bob from alice's perspective
            var blockResp = await StreamClient.BlockUsersAsync(new BlockUsersRequest
            {
                BlockedUserID = bobId,
                UserID = aliceId
            });
            Assert.That(blockResp.Data, Is.Not.Null);

            // Verify bob is in alice's blocked list
            var getBlockedResp = await StreamClient.GetBlockedUsersAsync(new { user_id = aliceId });
            Assert.That(getBlockedResp.Data, Is.Not.Null);
            Assert.That(getBlockedResp.Data!.Blocks, Is.Not.Null);
            Assert.That(getBlockedResp.Data.Blocks.Count, Is.GreaterThanOrEqualTo(1));

            var bobBlock = getBlockedResp.Data.Blocks.Find(b => b.BlockedUserID == bobId);
            Assert.That(bobBlock, Is.Not.Null);
            Assert.That(bobBlock!.UserID, Is.EqualTo(aliceId));
            Assert.That(bobBlock.BlockedUserID, Is.EqualTo(bobId));

            // Unblock bob from alice's perspective
            var unblockResp = await StreamClient.UnblockUsersAsync(new UnblockUsersRequest
            {
                BlockedUserID = bobId,
                UserID = aliceId
            });
            Assert.That(unblockResp.Data, Is.Not.Null);

            // Verify bob is no longer in alice's blocked list
            var getBlockedResp2 = await StreamClient.GetBlockedUsersAsync(new { user_id = aliceId });
            Assert.That(getBlockedResp2.Data, Is.Not.Null);
            var bobBlockAfter = getBlockedResp2.Data!.Blocks?.Find(b => b.BlockedUserID == bobId);
            Assert.That(bobBlockAfter, Is.Null);
        }

        [Test, Order(9)]
        public async Task CreateCallWithSessionTimer()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];

            var callType = "default";
            var callId = "test-call-" + Guid.NewGuid().ToString("N")[..16];

            try
            {
                // Create call with max_duration_seconds = 3600
                var createResp = await StreamClient.MakeRequestAsync<GetOrCreateCallRequest, GetOrCreateCallResponse>(
                    "POST",
                    "/api/v2/video/call/{type}/{id}",
                    null,
                    new GetOrCreateCallRequest
                    {
                        Data = new CallRequest
                        {
                            CreatedByID = userId,
                            SettingsOverride = new CallSettingsRequest
                            {
                                Limits = new LimitsSettingsRequest { MaxDurationSeconds = 3600 }
                            }
                        }
                    },
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

                Assert.That(createResp.Data, Is.Not.Null);
                Assert.That(createResp.Data!.Call.Settings.Limits.MaxDurationSeconds, Is.EqualTo(3600));

                // Update call with max_duration_seconds = 7200
                var updateResp = await StreamClient.MakeRequestAsync<UpdateCallRequest, UpdateCallResponse>(
                    "PATCH",
                    "/api/v2/video/call/{type}/{id}",
                    null,
                    new UpdateCallRequest
                    {
                        SettingsOverride = new CallSettingsRequest
                        {
                            Limits = new LimitsSettingsRequest { MaxDurationSeconds = 7200 }
                        }
                    },
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

                Assert.That(updateResp.Data, Is.Not.Null);
                Assert.That(updateResp.Data!.Call.Settings.Limits.MaxDurationSeconds, Is.EqualTo(7200));

                // Update call with max_duration_seconds = 0 (disabled)
                var updateResp2 = await StreamClient.MakeRequestAsync<UpdateCallRequest, UpdateCallResponse>(
                    "PATCH",
                    "/api/v2/video/call/{type}/{id}",
                    null,
                    new UpdateCallRequest
                    {
                        SettingsOverride = new CallSettingsRequest
                        {
                            Limits = new LimitsSettingsRequest { MaxDurationSeconds = 0 }
                        }
                    },
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

                Assert.That(updateResp2.Data, Is.Not.Null);
                Assert.That(updateResp2.Data!.Call.Settings.Limits.MaxDurationSeconds, Is.EqualTo(0));
            }
            finally
            {
                try
                {
                    await StreamClient.MakeRequestAsync<object, object>(
                        "POST",
                        "/api/v2/video/call/{type}/{id}/delete",
                        null,
                        null,
                        new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });
                }
                catch { /* ignore cleanup errors */ }
            }
        }

        [Test, Order(11)]
        public async Task CreateCallWithBackstageAndJoinAhead()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];

            var callType = "default";
            var callId = "test-call-" + Guid.NewGuid().ToString("N")[..16];

            try
            {
                // Create call with backstage enabled + join_ahead_time_seconds = 300
                var createResp = await StreamClient.MakeRequestAsync<GetOrCreateCallRequest, GetOrCreateCallResponse>(
                    "POST",
                    "/api/v2/video/call/{type}/{id}",
                    null,
                    new GetOrCreateCallRequest
                    {
                        Data = new CallRequest
                        {
                            CreatedByID = userId,
                            SettingsOverride = new CallSettingsRequest
                            {
                                Backstage = new BackstageSettingsRequest
                                {
                                    Enabled = true,
                                    JoinAheadTimeSeconds = 300
                                }
                            }
                        }
                    },
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

                Assert.That(createResp.Data, Is.Not.Null);
                Assert.That(createResp.Data!.Call.JoinAheadTimeSeconds, Is.EqualTo(300));

                // Update join_ahead_time_seconds to 600
                var updateResp = await StreamClient.MakeRequestAsync<UpdateCallRequest, UpdateCallResponse>(
                    "PATCH",
                    "/api/v2/video/call/{type}/{id}",
                    null,
                    new UpdateCallRequest
                    {
                        SettingsOverride = new CallSettingsRequest
                        {
                            Backstage = new BackstageSettingsRequest
                            {
                                JoinAheadTimeSeconds = 600
                            }
                        }
                    },
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

                Assert.That(updateResp.Data, Is.Not.Null);
                Assert.That(updateResp.Data!.Call.JoinAheadTimeSeconds, Is.EqualTo(600));

                // Update join_ahead_time_seconds to 0 (disabled)
                var updateResp2 = await StreamClient.MakeRequestAsync<UpdateCallRequest, UpdateCallResponse>(
                    "PATCH",
                    "/api/v2/video/call/{type}/{id}",
                    null,
                    new UpdateCallRequest
                    {
                        SettingsOverride = new CallSettingsRequest
                        {
                            Backstage = new BackstageSettingsRequest
                            {
                                JoinAheadTimeSeconds = 0
                            }
                        }
                    },
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

                Assert.That(updateResp2.Data, Is.Not.Null);
                Assert.That(updateResp2.Data!.Call.JoinAheadTimeSeconds, Is.EqualTo(0));
            }
            finally
            {
                try
                {
                    await StreamClient.MakeRequestAsync<object, object>(
                        "POST",
                        "/api/v2/video/call/{type}/{id}/delete",
                        null,
                        null,
                        new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });
                }
                catch { /* ignore cleanup errors */ }
            }
        }

        [Test, Order(12)]
        public async Task DeleteCall()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];

            var callType = "default";
            var callId = "test-call-" + Guid.NewGuid().ToString("N")[..16];

            // Create call
            await StreamClient.MakeRequestAsync<GetOrCreateCallRequest, GetOrCreateCallResponse>(
                "POST",
                "/api/v2/video/call/{type}/{id}",
                null,
                new GetOrCreateCallRequest
                {
                    Data = new CallRequest { CreatedByID = userId }
                },
                new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

            // Soft delete the call
            var deleteResp = await StreamClient.MakeRequestAsync<DeleteCallRequest, DeleteCallResponse>(
                "POST",
                "/api/v2/video/call/{type}/{id}/delete",
                null,
                new DeleteCallRequest(),
                new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

            Assert.That(deleteResp.Data, Is.Not.Null);
            Assert.That(deleteResp.Data!.Call, Is.Not.Null);
            Assert.That(deleteResp.Data.TaskID, Is.Null);

            // Verify the call is no longer accessible
            try
            {
                await StreamClient.MakeRequestAsync<object, GetCallResponse>(
                    "GET",
                    "/api/v2/video/call/{type}/{id}",
                    null,
                    null,
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });
                Assert.Fail("Expected an error when getting deleted call, but none was thrown");
            }
            catch (Exception ex)
            {
                Assert.That(ex.Message, Does.Contain("Can't find call with id").Or.Contain("404").Or.Contain("not found"),
                    $"Expected error about call not found but got: {ex.Message}");
            }
        }

        [Test, Order(13)]
        public async Task HardDeleteCall()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];

            var callType = "default";
            var callId = "test-call-" + Guid.NewGuid().ToString("N")[..16];

            // Create call
            await StreamClient.MakeRequestAsync<GetOrCreateCallRequest, GetOrCreateCallResponse>(
                "POST",
                "/api/v2/video/call/{type}/{id}",
                null,
                new GetOrCreateCallRequest
                {
                    Data = new CallRequest { CreatedByID = userId }
                },
                new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

            // Hard delete the call
            var deleteResp = await StreamClient.MakeRequestAsync<DeleteCallRequest, DeleteCallResponse>(
                "POST",
                "/api/v2/video/call/{type}/{id}/delete",
                null,
                new DeleteCallRequest { Hard = true },
                new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

            Assert.That(deleteResp.Data, Is.Not.Null);
            Assert.That(deleteResp.Data!.TaskID, Is.Not.Null.And.Not.Empty);

            // Poll task until completed
            await WaitForTask(deleteResp.Data.TaskID!);
        }

        [Test, Order(14)]
        public async Task Teams()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];

            // Update user to have teams
            await StreamClient.UpdateUsersAsync(new UpdateUsersRequest
            {
                Users = new Dictionary<string, UserRequest>
                {
                    [userId] = new UserRequest { ID = userId, Teams = new List<string> { "red", "blue" } }
                }
            });

            var callType = "default";
            var callId = "test-call-" + Guid.NewGuid().ToString("N")[..16];

            try
            {
                // Create call with team="blue"
                var createResp = await StreamClient.MakeRequestAsync<GetOrCreateCallRequest, GetOrCreateCallResponse>(
                    "POST",
                    "/api/v2/video/call/{type}/{id}",
                    null,
                    new GetOrCreateCallRequest
                    {
                        Data = new CallRequest
                        {
                            CreatedByID = userId,
                            Team = "blue"
                        }
                    },
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

                Assert.That(createResp.Data, Is.Not.Null);
                Assert.That(createResp.Data!.Call, Is.Not.Null);
                Assert.That(createResp.Data.Call.Team, Is.EqualTo("blue"));

                // Query users with teams $in ["red", "blue"] filter, verify our user found
                var usersResp = await QueryUsers(new QueryUsersPayload
                {
                    FilterConditions = new Dictionary<string, object>
                    {
                        ["id"] = userId,
                        ["teams"] = new Dictionary<string, object>
                        {
                            ["$in"] = new List<string> { "red", "blue" }
                        }
                    }
                });
                Assert.That(usersResp.Data, Is.Not.Null);
                Assert.That(usersResp.Data!.Users, Is.Not.Null);
                var foundUserIds = new HashSet<string>(usersResp.Data.Users.Select(u => u.ID));
                Assert.That(foundUserIds, Does.Contain(userId));

                // Query users with teams=null (users without teams) - just verify no error
                var noTeamsResp = await QueryUsers(new QueryUsersPayload
                {
                    FilterConditions = new Dictionary<string, object>
                    {
                        ["teams"] = null!
                    }
                });
                Assert.That(noTeamsResp.Data, Is.Not.Null);

                // Query calls with team="blue" filter, verify our call found
                var callsResp = await StreamClient.MakeRequestAsync<QueryCallsRequest, QueryCallsResponse>(
                    "POST",
                    "/api/v2/video/calls",
                    null,
                    new QueryCallsRequest
                    {
                        FilterConditions = new Dictionary<string, object>
                        {
                            ["id"] = callId,
                            ["team"] = new Dictionary<string, object> { ["$eq"] = "blue" }
                        }
                    },
                    null);

                Assert.That(callsResp.Data, Is.Not.Null);
                Assert.That(callsResp.Data!.Calls, Is.Not.Null);
                Assert.That(callsResp.Data.Calls.Count, Is.GreaterThan(0));
            }
            finally
            {
                try
                {
                    await StreamClient.MakeRequestAsync<object, object>(
                        "POST",
                        "/api/v2/video/call/{type}/{id}/delete",
                        null,
                        null,
                        new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });
                }
                catch { /* ignore cleanup errors */ }
            }
        }

        [Test, Order(15)]
        public async Task ExternalStorageOperations()
        {
            var uniqueName = "test-storage-" + RandomString(10);

            // List existing external storages; if > 1, delete them to avoid accumulation
            var listResp = await StreamClient.ListExternalStorageAsync();
            Assert.That(listResp.Data, Is.Not.Null);

            if (listResp.Data!.ExternalStorages != null && listResp.Data.ExternalStorages.Count > 1)
            {
                foreach (var storage in listResp.Data.ExternalStorages.Values)
                {
                    try
                    {
                        await StreamClient.DeleteExternalStorageAsync(storage.Name);
                    }
                    catch { /* ignore cleanup errors */ }
                }
            }

            try
            {
                // Create external storage with S3 type (test credentials)
                var createResp = await StreamClient.CreateExternalStorageAsync(new CreateExternalStorageRequest
                {
                    Bucket = "test-bucket",
                    Name = uniqueName,
                    StorageType = "s3",
                    Path = "test-directory/",
                    AWSS3 = new S3Request
                    {
                        S3Region = "us-east-1",
                        S3APIKey = "test-access-key",
                        S3Secret = "test-secret"
                    }
                });
                Assert.That(createResp.Data, Is.Not.Null);

                // Wait for eventual consistency - list may return empty for up to 24s
                // Retry with 3s intervals, up to 8 attempts
                bool found = false;
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    await Task.Delay(3000);
                    var listResp2 = await StreamClient.ListExternalStorageAsync();
                    if (listResp2.Data?.ExternalStorages != null &&
                        listResp2.Data.ExternalStorages.ContainsKey(uniqueName))
                    {
                        found = true;
                        break;
                    }
                }

                Assert.That(found, Is.True, $"External storage '{uniqueName}' should appear in list after creation");
            }
            finally
            {
                // Delete the storage (with retry since delete may fail with "does not exist")
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    try
                    {
                        await StreamClient.DeleteExternalStorageAsync(uniqueName);
                        break;
                    }
                    catch
                    {
                        if (attempt < 4)
                            await Task.Delay(3000);
                    }
                }
            }
        }

        [Test, Order(16)]
        public async Task EnableCallRecordingAndBackstageMode()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];

            var callType = "default";
            var callId = "test-call-" + Guid.NewGuid().ToString("N")[..16];

            try
            {
                // Create call
                await StreamClient.MakeRequestAsync<GetOrCreateCallRequest, GetOrCreateCallResponse>(
                    "POST",
                    "/api/v2/video/call/{type}/{id}",
                    null,
                    new GetOrCreateCallRequest
                    {
                        Data = new CallRequest { CreatedByID = userId }
                    },
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

                // Enable recording with mode="available" and audio_only=true
                var recordingResp = await StreamClient.MakeRequestAsync<UpdateCallRequest, UpdateCallResponse>(
                    "PATCH",
                    "/api/v2/video/call/{type}/{id}",
                    null,
                    new UpdateCallRequest
                    {
                        SettingsOverride = new CallSettingsRequest
                        {
                            Recording = new RecordSettingsRequest
                            {
                                Mode = "available",
                                AudioOnly = true
                            }
                        }
                    },
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

                Assert.That(recordingResp.Data, Is.Not.Null);
                Assert.That(recordingResp.Data!.Call.Settings.Recording.Mode, Is.EqualTo("available"));

                // Enable backstage mode
                var backstageResp = await StreamClient.MakeRequestAsync<UpdateCallRequest, UpdateCallResponse>(
                    "PATCH",
                    "/api/v2/video/call/{type}/{id}",
                    null,
                    new UpdateCallRequest
                    {
                        SettingsOverride = new CallSettingsRequest
                        {
                            Backstage = new BackstageSettingsRequest
                            {
                                Enabled = true
                            }
                        }
                    },
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

                Assert.That(backstageResp.Data, Is.Not.Null);
                Assert.That(backstageResp.Data!.Call.Settings.Backstage.Enabled, Is.True);
            }
            finally
            {
                try
                {
                    await StreamClient.MakeRequestAsync<object, object>(
                        "POST",
                        "/api/v2/video/call/{type}/{id}/delete",
                        null,
                        null,
                        new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });
                }
                catch { /* ignore cleanup errors */ }
            }
        }

        [Test, Order(17)]
        public async Task DeleteRecordingsAndTranscriptions()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];

            var callType = "default";
            var callId = "test-call-" + Guid.NewGuid().ToString("N")[..16];

            try
            {
                // Create call
                await StreamClient.MakeRequestAsync<GetOrCreateCallRequest, GetOrCreateCallResponse>(
                    "POST",
                    "/api/v2/video/call/{type}/{id}",
                    null,
                    new GetOrCreateCallRequest
                    {
                        Data = new CallRequest { CreatedByID = userId }
                    },
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

                // Attempt to delete a non-existent recording - expect an error
                bool recordingErrorThrown = false;
                try
                {
                    await StreamClient.MakeRequestAsync<object, DeleteRecordingResponse>(
                        "DELETE",
                        "/api/v2/video/call/{type}/{id}/{session}/recordings/{filename}",
                        null,
                        null,
                        new Dictionary<string, string>
                        {
                            ["type"] = callType,
                            ["id"] = callId,
                            ["session"] = "non-existent-session",
                            ["filename"] = "non-existent-recording.mp4"
                        });
                }
                catch (Exception)
                {
                    recordingErrorThrown = true;
                }
                Assert.That(recordingErrorThrown, Is.True, "Expected error when deleting non-existent recording");

                // Attempt to delete a non-existent transcription - expect an error
                bool transcriptionErrorThrown = false;
                try
                {
                    await StreamClient.MakeRequestAsync<object, DeleteTranscriptionResponse>(
                        "DELETE",
                        "/api/v2/video/call/{type}/{id}/{session}/transcriptions/{filename}",
                        null,
                        null,
                        new Dictionary<string, string>
                        {
                            ["type"] = callType,
                            ["id"] = callId,
                            ["session"] = "non-existent-session",
                            ["filename"] = "non-existent-transcription.vtt"
                        });
                }
                catch (Exception)
                {
                    transcriptionErrorThrown = true;
                }
                Assert.That(transcriptionErrorThrown, Is.True, "Expected error when deleting non-existent transcription");
            }
            finally
            {
                try
                {
                    await StreamClient.MakeRequestAsync<object, object>(
                        "POST",
                        "/api/v2/video/call/{type}/{id}/delete",
                        null,
                        null,
                        new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });
                }
                catch { /* ignore cleanup errors */ }
            }
        }

        [Test, Order(2)]
        public async Task CreateCallWithMembers()
        {
            // Create 2 test users to add as members
            var userIds = await CreateTestUsers(2);

            var callType = "default";
            var callId = "test-call-" + Guid.NewGuid().ToString("N")[..16];

            try
            {
                // Create call with members
                var createResp = await StreamClient.MakeRequestAsync<GetOrCreateCallRequest, GetOrCreateCallResponse>(
                    "POST",
                    "/api/v2/video/call/{type}/{id}",
                    null,
                    new GetOrCreateCallRequest
                    {
                        Data = new CallRequest
                        {
                            CreatedByID = userIds[0],
                            Members = userIds.Select(id => new MemberRequest { UserID = id }).ToList()
                        }
                    },
                    new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });

                Assert.That(createResp.Data, Is.Not.Null);
                Assert.That(createResp.Data!.Call, Is.Not.Null);
                Assert.That(createResp.Data.Call.ID, Is.EqualTo(callId));
                Assert.That(createResp.Data.Members, Is.Not.Null);
                Assert.That(createResp.Data.Members.Count, Is.GreaterThanOrEqualTo(2));
            }
            finally
            {
                // Clean up: delete the call
                try
                {
                    await StreamClient.MakeRequestAsync<object, object>(
                        "POST",
                        "/api/v2/video/call/{type}/{id}/delete",
                        null,
                        null,
                        new Dictionary<string, string> { ["type"] = callType, ["id"] = callId });
                }
                catch { /* ignore cleanup errors */ }
            }
        }
    }
}
