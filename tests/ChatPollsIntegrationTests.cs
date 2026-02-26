using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using GetStream;
using GetStream.Models;
using NUnit.Framework;

namespace GetStream.Tests
{
    // Local request class to preserve snake_case JSON key names (PropertyNamingPolicy CamelCase
    // would otherwise convert user_id -> userId in Dictionary keys on .NET 8)
    internal class QueryPollsWithUserRequest
    {
        [JsonPropertyName("user_id")]
        public string? UserID { get; set; }

        [JsonPropertyName("filter")]
        public object? Filter { get; set; }
    }

    [TestFixture]
    public class ChatPollsIntegrationTests : ChatTestBase
    {
        [Test, Order(1)]
        public async Task CreateAndQueryPoll()
        {
            var userIds = await CreateTestUsers(1);
            var userId = userIds[0];

            string pollId = null;
            try
            {
                // Create a poll with options
                var createResp = await StreamClient.CreatePollAsync(new CreatePollRequest
                {
                    Name = "Favorite color? " + RandomString(6),
                    EnforceUniqueVote = true,
                    UserID = userId,
                    Options = new List<PollOptionInput>
                    {
                        new PollOptionInput { Text = "Red" },
                        new PollOptionInput { Text = "Blue" },
                        new PollOptionInput { Text = "Green" }
                    }
                });

                Assert.That(createResp.Data, Is.Not.Null);
                Assert.That(createResp.Data!.Poll, Is.Not.Null);
                Assert.That(createResp.Data!.Poll.ID, Is.Not.Null.And.Not.Empty);
                Assert.That(createResp.Data!.Poll.EnforceUniqueVote, Is.True);
                Assert.That(createResp.Data!.Poll.Options, Has.Count.EqualTo(3));
                pollId = createResp.Data!.Poll.ID;

                // Query polls filtered by poll ID
                // user_id is required for server-side auth; pass as URL query param
                var queryResp = await StreamClient.MakeRequestAsync<QueryPollsWithUserRequest, QueryPollsResponse>(
                    "POST",
                    "/api/v2/polls/query",
                    new Dictionary<string, string> { ["user_id"] = userId },
                    new QueryPollsWithUserRequest
                    {
                        UserID = userId,
                        Filter = new Dictionary<string, object> { ["id"] = pollId }
                    },
                    null);

                Assert.That(queryResp.Data, Is.Not.Null);
                Assert.That(queryResp.Data!.Polls, Is.Not.Null.And.Not.Empty);
                Assert.That(queryResp.Data!.Polls[0].ID, Is.EqualTo(pollId));
            }
            catch (Exception e) when (
                e.Message.Contains("polls not enabled") ||
                e.Message.Contains("feature flag") ||
                e.Message.Contains("not enabled"))
            {
                Assert.Ignore("Polls feature not enabled for this app");
            }
            finally
            {
                if (pollId != null)
                {
                    try
                    {
                        await StreamClient.DeletePollAsync(pollId, new { user_id = userId });
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }
    }
}
