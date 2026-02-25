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
    }
}
