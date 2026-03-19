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
    public class ChatUserIntegrationTests : ChatTestBase
    {
        [Test, Order(1)]
        public async Task UpsertUsers()
        {
            var userIds = await CreateTestUsers(2);

            // Verify both users exist by querying with $in filter
            var resp = await QueryUsers(new QueryUsersPayload
            {
                FilterConditions = InFilter("id", userIds)
            });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Users, Is.Not.Null);
            Assert.That(resp.Data!.Users.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test, Order(2)]
        public async Task QueryUsersTest()
        {
            var userIds = await CreateTestUsers(2);

            var resp = await QueryUsers(new QueryUsersPayload
            {
                FilterConditions = InFilter("id", userIds)
            });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Users, Is.Not.Null);
            Assert.That(resp.Data!.Users.Count, Is.GreaterThanOrEqualTo(2));

            // Verify both specific user IDs are in the results
            var foundIds = resp.Data!.Users.Select(u => u.ID).ToHashSet();
            foreach (var id in userIds)
            {
                Assert.That(foundIds, Does.Contain(id), $"User {id} should be found in query results");
            }
        }

        [Test, Order(3)]
        public async Task QueryUsersWithOffsetLimit()
        {
            var userIds = await CreateTestUsers(3);

            var resp = await QueryUsers(new QueryUsersPayload
            {
                FilterConditions = InFilter("id", userIds),
                Offset = 1,
                Limit = 2
            });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Users, Is.Not.Null);
            Assert.That(resp.Data!.Users.Count, Is.EqualTo(2), "Should return exactly 2 users with offset=1 limit=2");
        }

        [Test, Order(4)]
        public async Task PartialUpdateUser()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];

            // Set custom fields (country, role)
            var setResp = await StreamClient.UpdateUsersPartialAsync(new UpdateUsersPartialRequest
            {
                Users = new List<UpdateUserPartialRequest>
                {
                    new UpdateUserPartialRequest
                    {
                        ID = userId,
                        Set = new Dictionary<string, object>
                        {
                            ["country"] = "NL",
                            ["role"] = "admin"
                        }
                    }
                }
            });

            Assert.That(setResp.Data, Is.Not.Null);
            Assert.That(setResp.Data!.Users, Is.Not.Null);
            Assert.That(setResp.Data!.Users.ContainsKey(userId), Is.True);
            Assert.That(setResp.Data!.Users[userId].Role, Is.EqualTo("admin"));

            // Verify via query
            var queryResp = await QueryUsers(new QueryUsersPayload
            {
                FilterConditions = new Dictionary<string, object>
                {
                    ["id"] = userId
                }
            });

            Assert.That(queryResp.Data, Is.Not.Null);
            Assert.That(queryResp.Data!.Users.Count, Is.EqualTo(1));
            Assert.That(queryResp.Data!.Users[0].Role, Is.EqualTo("admin"));

            // Unset country
            var unsetResp = await StreamClient.UpdateUsersPartialAsync(new UpdateUsersPartialRequest
            {
                Users = new List<UpdateUserPartialRequest>
                {
                    new UpdateUserPartialRequest
                    {
                        ID = userId,
                        Unset = new List<string> { "country" }
                    }
                }
            });

            Assert.That(unsetResp.Data, Is.Not.Null);
            Assert.That(unsetResp.Data!.Users, Is.Not.Null);
            Assert.That(unsetResp.Data!.Users.ContainsKey(userId), Is.True);
        }

        [Test, Order(5)]
        public async Task BlockUnblockUser()
        {
            var userIds = await CreateTestUsers(2);
            var alice = userIds[0];
            var bob = userIds[1];

            // Block bob from alice's perspective
            await StreamClient.BlockUsersAsync(new BlockUsersRequest
            {
                BlockedUserID = bob,
                UserID = alice
            });

            // Verify bob is in alice's blocked list
            var blockedResp = await StreamClient.GetBlockedUsersAsync(new { user_id = alice });
            Assert.That(blockedResp.Data, Is.Not.Null);
            Assert.That(blockedResp.Data!.Blocks, Is.Not.Empty, "Should have at least one block");
            var blockedIds = blockedResp.Data!.Blocks.Select(b => b.BlockedUserID).ToList();
            Assert.That(blockedIds, Does.Contain(bob), "Bob should be in Alice's blocked list");

            // Unblock bob
            await StreamClient.UnblockUsersAsync(new UnblockUsersRequest
            {
                BlockedUserID = bob,
                UserID = alice
            });

            // Verify bob is no longer in alice's blocked list
            var unblockedResp = await StreamClient.GetBlockedUsersAsync(new { user_id = alice });
            Assert.That(unblockedResp.Data, Is.Not.Null);
            if (unblockedResp.Data!.Blocks != null)
            {
                var stillBlockedIds = unblockedResp.Data!.Blocks.Select(b => b.BlockedUserID).ToList();
                Assert.That(stillBlockedIds, Does.Not.Contain(bob), "Bob should no longer be blocked");
            }
        }

        [Test, Order(6)]
        public async Task DeactivateReactivateUser()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];

            // Deactivate
            var deactivateResp = await StreamClient.DeactivateUserAsync(userId, new DeactivateUserRequest());
            Assert.That(deactivateResp.Data, Is.Not.Null);

            // Reactivate
            var reactivateResp = await StreamClient.ReactivateUserAsync(userId, new ReactivateUserRequest());
            Assert.That(reactivateResp.Data, Is.Not.Null);

            // Verify user is active again by querying
            var queryResp = await QueryUsers(new QueryUsersPayload
            {
                FilterConditions = new Dictionary<string, object>
                {
                    ["id"] = userId
                }
            });

            Assert.That(queryResp.Data, Is.Not.Null);
            Assert.That(queryResp.Data!.Users.Count, Is.EqualTo(1));
            Assert.That(queryResp.Data!.Users[0].ID, Is.EqualTo(userId));
        }

        [Test, Order(8)]
        public async Task ExportUser()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];

            var resp = await StreamClient.ExportUserAsync(userId);
            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.User, Is.Not.Null);
            Assert.That(resp.Data!.User!.ID, Is.EqualTo(userId));
        }

        [Test, Order(9)]
        public async Task CreateGuest()
        {
            var guestId = $"guest-{Guid.NewGuid():N}";
            CreatedUserIds.Add(guestId);

            try
            {
                var resp = await StreamClient.CreateGuestAsync(new CreateGuestRequest
                {
                    User = new UserRequest
                    {
                        ID = guestId,
                        Name = $"Guest User {guestId}"
                    }
                });

                Assert.That(resp.Data, Is.Not.Null);
                Assert.That(resp.Data!.AccessToken, Is.Not.Null.And.Not.Empty, "Access token should not be empty");
                Assert.That(resp.Data!.User, Is.Not.Null);
                // Server may prefix the guest ID, so check with Contains
                Assert.That(resp.Data!.User.ID, Does.Contain(guestId), "Guest user ID should contain the requested ID");

                // Also clean up the actual server-assigned ID (may differ from requested)
                if (resp.Data!.User.ID != guestId)
                {
                    CreatedUserIds.Add(resp.Data!.User.ID);
                }
            }
            catch (Exception e)
            {
                // Guest access may be disabled at the app level
                Assert.Ignore($"Guest creation not available: {e.Message}");
            }
        }

        [Test, Order(10)]
        public async Task UpsertUsersWithRoleAndTeamsRole()
        {
            var userId = $"teams-{Guid.NewGuid():N}";
            CreatedUserIds.Add(userId);

            var resp = await StreamClient.UpdateUsersAsync(new UpdateUsersRequest
            {
                Users = new Dictionary<string, UserRequest>
                {
                    [userId] = new UserRequest
                    {
                        ID = userId,
                        Name = $"Teams User {userId}",
                        Role = "admin",
                        Teams = new List<string> { "blue" },
                        TeamsRole = new Dictionary<string, string> { ["blue"] = "admin" }
                    }
                }
            });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Users, Is.Not.Null);
            Assert.That(resp.Data!.Users.ContainsKey(userId), Is.True, "User should be in response");

            var u = resp.Data!.Users[userId];
            Assert.That(u.Role, Is.EqualTo("admin"));
            Assert.That(u.Teams, Is.Not.Null.And.Contains("blue"));
            Assert.That(u.TeamsRole, Is.Not.Null);
            Assert.That(u.TeamsRole["blue"], Is.EqualTo("admin"));
        }

        [Test, Order(11)]
        public async Task PartialUpdateUserWithTeam()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];

            // Partial update to add teams and teams_role
            var resp = await StreamClient.UpdateUsersPartialAsync(new UpdateUsersPartialRequest
            {
                Users = new List<UpdateUserPartialRequest>
                {
                    new UpdateUserPartialRequest
                    {
                        ID = userId,
                        Set = new Dictionary<string, object>
                        {
                            ["teams"] = new List<string> { "blue" },
                            ["teams_role"] = new Dictionary<string, string> { ["blue"] = "admin" }
                        }
                    }
                }
            });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Users, Is.Not.Null);
            Assert.That(resp.Data!.Users.ContainsKey(userId), Is.True);

            var u = resp.Data!.Users[userId];
            Assert.That(u.Teams, Is.Not.Null.And.Contains("blue"));
            Assert.That(u.TeamsRole, Is.Not.Null);
            Assert.That(u.TeamsRole["blue"], Is.EqualTo("admin"));
        }

        [Test, Order(12)]
        public async Task UpdatePrivacySettings()
        {
            var userId = $"privacy-{Guid.NewGuid():N}";
            CreatedUserIds.Add(userId);

            // Create user without privacy settings
            var createResp = await StreamClient.UpdateUsersAsync(new UpdateUsersRequest
            {
                Users = new Dictionary<string, UserRequest>
                {
                    [userId] = new UserRequest { ID = userId, Name = $"Privacy User {userId}" }
                }
            });

            Assert.That(createResp.Data, Is.Not.Null);
            var u = createResp.Data!.Users[userId];
            Assert.That(u.PrivacySettings, Is.Null, "PrivacySettings should be nil initially");

            // Update with TypingIndicators disabled
            var resp1 = await StreamClient.UpdateUsersAsync(new UpdateUsersRequest
            {
                Users = new Dictionary<string, UserRequest>
                {
                    [userId] = new UserRequest
                    {
                        ID = userId,
                        PrivacySettings = new PrivacySettingsResponse
                        {
                            TypingIndicators = new TypingIndicatorsResponse { Enabled = false }
                        }
                    }
                }
            });

            Assert.That(resp1.Data, Is.Not.Null);
            u = resp1.Data!.Users[userId];
            Assert.That(u.PrivacySettings, Is.Not.Null);
            Assert.That(u.PrivacySettings!.TypingIndicators, Is.Not.Null);
            Assert.That(u.PrivacySettings!.TypingIndicators!.Enabled, Is.EqualTo(false));
            Assert.That(u.PrivacySettings!.ReadReceipts, Is.Null, "ReadReceipts should still be nil");

            // Update with both TypingIndicators=true and ReadReceipts=false
            var resp2 = await StreamClient.UpdateUsersAsync(new UpdateUsersRequest
            {
                Users = new Dictionary<string, UserRequest>
                {
                    [userId] = new UserRequest
                    {
                        ID = userId,
                        PrivacySettings = new PrivacySettingsResponse
                        {
                            TypingIndicators = new TypingIndicatorsResponse { Enabled = true },
                            ReadReceipts = new ReadReceiptsResponse { Enabled = false }
                        }
                    }
                }
            });

            Assert.That(resp2.Data, Is.Not.Null);
            u = resp2.Data!.Users[userId];
            Assert.That(u.PrivacySettings, Is.Not.Null);
            Assert.That(u.PrivacySettings!.TypingIndicators, Is.Not.Null);
            Assert.That(u.PrivacySettings!.TypingIndicators!.Enabled, Is.EqualTo(true));
            Assert.That(u.PrivacySettings!.ReadReceipts, Is.Not.Null);
            Assert.That(u.PrivacySettings!.ReadReceipts!.Enabled, Is.EqualTo(false));
        }

        [Test, Order(13)]
        public async Task PartialUpdatePrivacySettings()
        {
            var userId = $"privacy-partial-{Guid.NewGuid():N}";
            CreatedUserIds.Add(userId);

            // Create user without privacy settings
            var createResp = await StreamClient.UpdateUsersAsync(new UpdateUsersRequest
            {
                Users = new Dictionary<string, UserRequest>
                {
                    [userId] = new UserRequest { ID = userId, Name = $"Privacy Partial {userId}" }
                }
            });

            Assert.That(createResp.Data, Is.Not.Null);
            var u = createResp.Data!.Users[userId];
            Assert.That(u.PrivacySettings, Is.Null, "PrivacySettings should be nil initially");

            // Partial update: set typing_indicators.enabled = true
            var partialResp = await StreamClient.UpdateUsersPartialAsync(new UpdateUsersPartialRequest
            {
                Users = new List<UpdateUserPartialRequest>
                {
                    new UpdateUserPartialRequest
                    {
                        ID = userId,
                        Set = new Dictionary<string, object>
                        {
                            ["privacy_settings"] = new Dictionary<string, object>
                            {
                                ["typing_indicators"] = new Dictionary<string, object>
                                {
                                    ["enabled"] = true
                                }
                            }
                        }
                    }
                }
            });

            Assert.That(partialResp.Data, Is.Not.Null);
            var u2 = partialResp.Data!.Users[userId];
            Assert.That(u2.PrivacySettings, Is.Not.Null);
            Assert.That(u2.PrivacySettings!.TypingIndicators, Is.Not.Null);
            Assert.That(u2.PrivacySettings!.TypingIndicators!.Enabled, Is.EqualTo(true));
            Assert.That(u2.PrivacySettings!.ReadReceipts, Is.Null, "ReadReceipts should still be nil");

            // Partial update: set read_receipts.enabled = false (typing_indicators should be preserved)
            var partialResp2 = await StreamClient.UpdateUsersPartialAsync(new UpdateUsersPartialRequest
            {
                Users = new List<UpdateUserPartialRequest>
                {
                    new UpdateUserPartialRequest
                    {
                        ID = userId,
                        Set = new Dictionary<string, object>
                        {
                            ["privacy_settings"] = new Dictionary<string, object>
                            {
                                ["read_receipts"] = new Dictionary<string, object>
                                {
                                    ["enabled"] = false
                                }
                            }
                        }
                    }
                }
            });

            Assert.That(partialResp2.Data, Is.Not.Null);
            var u3 = partialResp2.Data!.Users[userId];
            Assert.That(u3.PrivacySettings, Is.Not.Null);
            Assert.That(u3.PrivacySettings!.TypingIndicators, Is.Not.Null);
            Assert.That(u3.PrivacySettings!.TypingIndicators!.Enabled, Is.EqualTo(true), "TypingIndicators should still be true");
            Assert.That(u3.PrivacySettings!.ReadReceipts, Is.Not.Null);
            Assert.That(u3.PrivacySettings!.ReadReceipts!.Enabled, Is.EqualTo(false));
        }

        [Test, Order(14)]
        public async Task QueryUsersWithDeactivated()
        {
            var userIds = await CreateTestUsers(3);
            var deactivatedUserId = userIds[2];

            // Deactivate one user
            await StreamClient.DeactivateUserAsync(deactivatedUserId, new DeactivateUserRequest());

            try
            {
                // Query WITHOUT including deactivated — should get 2
                var resp = await QueryUsers(new QueryUsersPayload
                {
                    FilterConditions = InFilter("id", userIds)
                });

                Assert.That(resp.Data, Is.Not.Null);
                Assert.That(resp.Data!.Users.Count, Is.EqualTo(2), "Should exclude deactivated user by default");

                // Query WITH including deactivated — should get all 3
                var resp2 = await QueryUsers(new QueryUsersPayload
                {
                    FilterConditions = InFilter("id", userIds),
                    IncludeDeactivatedUsers = true
                });

                Assert.That(resp2.Data, Is.Not.Null);
                Assert.That(resp2.Data!.Users.Count, Is.EqualTo(3), "Should include deactivated user");
            }
            finally
            {
                // Reactivate so cleanup can delete the user
                await StreamClient.ReactivateUserAsync(deactivatedUserId, new ReactivateUserRequest());
            }
        }

        [Test, Order(15)]
        public async Task DeactivateUsersPlural()
        {
            var userIds = await CreateTestUsers(2);

            try
            {
                // Deactivate multiple users at once (async task)
                var resp = await StreamClient.DeactivateUsersAsync(new DeactivateUsersRequest
                {
                    UserIds = userIds
                });

                Assert.That(resp.Data, Is.Not.Null);
                Assert.That(resp.Data!.TaskID, Is.Not.Null.And.Not.Empty, "Task ID should not be empty");

                // Wait for deactivation task to complete
                await WaitForTask(resp.Data!.TaskID);

                // Verify deactivated users don't appear in default query
                var queryResp = await QueryUsers(new QueryUsersPayload
                {
                    FilterConditions = InFilter("id", userIds)
                });

                Assert.That(queryResp.Data, Is.Not.Null);
                Assert.That(queryResp.Data!.Users.Count, Is.EqualTo(0), "Deactivated users should not appear in default query");
            }
            finally
            {
                // Reactivate users so cleanup can hard-delete them
                foreach (var userId in userIds)
                {
                    try { await StreamClient.ReactivateUserAsync(userId, new ReactivateUserRequest()); }
                    catch { /* ignore */ }
                }
            }
        }

        [Test, Order(16)]
        public async Task UserCustomData()
        {
            var userId = $"custom-{Guid.NewGuid():N}";
            CreatedUserIds.Add(userId);

            var custom = new Dictionary<string, object>
            {
                ["favorite_color"] = "blue",
                ["age"] = 30,
                ["tags"] = new List<string> { "vip", "early_adopter" }
            };

            // Create user with custom fields
            var resp = await StreamClient.UpdateUsersAsync(new UpdateUsersRequest
            {
                Users = new Dictionary<string, UserRequest>
                {
                    [userId] = new UserRequest
                    {
                        ID = userId,
                        Name = $"Custom User {userId}",
                        Custom = custom
                    }
                }
            });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Users.ContainsKey(userId), Is.True);

            var u = resp.Data!.Users[userId];
            Assert.That(u.Custom, Is.Not.Null);

            // Custom comes back as JsonElement, cast to access values
            var customJson = (System.Text.Json.JsonElement)u.Custom;
            Assert.That(customJson.GetProperty("favorite_color").GetString(), Is.EqualTo("blue"));
            Assert.That(customJson.GetProperty("age").GetInt32(), Is.EqualTo(30));

            // Verify persistence by querying back
            var queryResp = await QueryUsers(new QueryUsersPayload
            {
                FilterConditions = new Dictionary<string, object>
                {
                    ["id"] = userId
                }
            });

            Assert.That(queryResp.Data, Is.Not.Null);
            Assert.That(queryResp.Data!.Users.Count, Is.EqualTo(1));

            var queried = queryResp.Data!.Users[0];
            Assert.That(queried.Custom, Is.Not.Null);
            var queriedJson = (System.Text.Json.JsonElement)queried.Custom;
            Assert.That(queriedJson.GetProperty("favorite_color").GetString(), Is.EqualTo("blue"));
        }

        [Test, Order(17)]
        public async Task DeleteUsers()
        {
            // Create 2 users specifically for deletion (don't track in CreatedUserIds since we delete them here)
            var ids = Enumerable.Range(0, 2)
                .Select(_ => $"test-user-{Guid.NewGuid():N}")
                .ToList();

            var users = ids.ToDictionary(
                id => id,
                id => new UserRequest { ID = id, Name = $"Test User {id[..8]}", Role = "user" }
            );
            await StreamClient.UpdateUsersAsync(new UpdateUsersRequest { Users = users });

            // Delete users with retry for rate limiting
            StreamResponse<DeleteUsersResponse>? deleteResp = null;
            Exception? deleteErr = null;
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    deleteResp = await StreamClient.DeleteUsersAsync(new DeleteUsersRequest
                    {
                        UserIds = ids
                    });
                    deleteErr = null;
                    break;
                }
                catch (Exception e)
                {
                    deleteErr = e;
                    if (!e.Message.Contains("Too many requests")) break;
                    await Task.Delay((i + 1) * 5000);
                }
            }

            Assert.That(deleteErr, Is.Null, $"DeleteUsers failed: {deleteErr?.Message}");
            Assert.That(deleteResp, Is.Not.Null);
            Assert.That(deleteResp!.Data, Is.Not.Null);
            Assert.That(deleteResp!.Data!.TaskID, Is.Not.Null.And.Not.Empty, "Task ID should not be empty");

            // Poll task until completed
            await WaitForTask(deleteResp!.Data!.TaskID);
        }
    }
}
