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
    }
}
