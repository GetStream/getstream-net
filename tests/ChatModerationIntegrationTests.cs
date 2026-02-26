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
    public class ChatModerationIntegrationTests : ChatTestBase
    {
        private ModerationClient _moderationClient = null!;

        [OneTimeSetUp]
        public void ModerationSetup()
        {
            _moderationClient = new ModerationClient(StreamClient);
        }

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        [Test, Order(2)]
        public async Task MuteUnmuteUser()
        {
            // Create 2 users: muter and target
            var userIds = await CreateTestUsers(2);
            var muterId = userIds[0];
            var targetId = userIds[1];

            // Mute the target user (without timeout)
            var muteResp = await _moderationClient.MuteAsync(new MuteRequest
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
            var unmuteResp = await _moderationClient.UnmuteAsync(new UnmuteRequest
            {
                TargetIds = new List<string> { targetId },
                UserID = muterId
            });
            Assert.That(unmuteResp.Data, Is.Not.Null);
        }

        [Test, Order(1)]
        public async Task BanUnbanUser()
        {
            // Create 2 users: admin (banner) and target (to be banned)
            var userIds = await CreateTestUsers(2);
            var adminId = userIds[0];
            var targetId = userIds[1];

            // Ban target user at app level (no ChannelCid) using moderation API
            var banResp = await _moderationClient.BanAsync(new BanRequest
            {
                TargetUserID = targetId,
                BannedByID = adminId,
                Reason = "test ban",
                Timeout = 60 // 60 minutes
            });
            Assert.That(banResp.Data, Is.Not.Null);

            // Query banned users to verify the ban is in effect
            var payload = new QueryBannedUsersPayload
            {
                FilterConditions = new Dictionary<string, object>
                {
                    ["user_id"] = new Dictionary<string, object> { ["$eq"] = targetId }
                }
            };
            var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
            var queryParams = new Dictionary<string, string> { ["payload"] = payloadJson };

            var qResp = await StreamClient.MakeRequestAsync<object, QueryBannedUsersResponse>(
                "GET",
                "/api/v2/chat/query_banned_users",
                queryParams,
                null,
                null);

            Assert.That(qResp.Data, Is.Not.Null);
            Assert.That(qResp.Data!.Bans, Is.Not.Null.And.Not.Empty, "Banned user should appear in query");

            var ban = qResp.Data!.Bans.FirstOrDefault(b => b.User?.ID == targetId);
            Assert.That(ban, Is.Not.Null, "Target user should be in banned list");
            Assert.That(ban!.Reason, Is.EqualTo("test ban"));
            Assert.That(ban.Expires, Is.Not.Null, "Ban with timeout should have Expires set");

            // Unban the user via moderation API (target_user_id passed as query param)
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
            var stillBanned = qResp2.Data!.Bans?.Any(b => b.User?.ID == targetId) ?? false;
            Assert.That(stillBanned, Is.False, "User should no longer be banned after unban");
        }
    }
}
