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
    }
}
