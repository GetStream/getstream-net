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
        [Test, Order(2)]
        public async Task CastPollVote()
        {
            var userIds = await CreateTestUsers(2);
            var userId = userIds[0];
            var voterId = userIds[1];

            string pollId = null;
            try
            {
                // Create a poll with options
                var createResp = await StreamClient.CreatePollAsync(new CreatePollRequest
                {
                    Name = "Vote test poll " + RandomString(6),
                    EnforceUniqueVote = true,
                    UserID = userId,
                    Options = new List<PollOptionInput>
                    {
                        new PollOptionInput { Text = "Yes" },
                        new PollOptionInput { Text = "No" }
                    }
                });

                Assert.That(createResp.Data, Is.Not.Null);
                Assert.That(createResp.Data!.Poll, Is.Not.Null);
                pollId = createResp.Data!.Poll.ID;
                var optionId = createResp.Data!.Poll.Options[0].ID;

                // Create a channel with both users and send a message with the poll attached
                var channelId = await CreateTestChannelWithMembers(userId, new List<string> { userId, voterId });

                var sendResp = await StreamClient.MakeRequestAsync<SendMessageRequest, SendMessageResponse>(
                    "POST",
                    "/api/v2/chat/channels/{type}/{id}/message",
                    null,
                    new SendMessageRequest
                    {
                        Message = new MessageRequest
                        {
                            Text = "Please vote!",
                            UserID = userId,
                            PollID = pollId
                        }
                    },
                    new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

                Assert.That(sendResp.Data, Is.Not.Null);
                Assert.That(sendResp.Data!.Message, Is.Not.Null);
                var msgId = sendResp.Data!.Message.ID;

                // Cast a vote
                var voteResp = await StreamClient.MakeRequestAsync<CastPollVoteRequest, PollVoteResponse>(
                    "POST",
                    "/api/v2/chat/messages/{message_id}/polls/{poll_id}/vote",
                    null,
                    new CastPollVoteRequest
                    {
                        UserID = voterId,
                        Vote = new VoteData { OptionID = optionId }
                    },
                    new Dictionary<string, string> { ["message_id"] = msgId, ["poll_id"] = pollId });

                Assert.That(voteResp.Data, Is.Not.Null);
                Assert.That(voteResp.Data!.Vote, Is.Not.Null);
                Assert.That(voteResp.Data!.Vote!.OptionID, Is.EqualTo(optionId));

                // Verify poll has votes
                var getResp = await StreamClient.GetPollAsync(pollId);
                Assert.That(getResp.Data, Is.Not.Null);
                Assert.That(getResp.Data!.Poll, Is.Not.Null);
                Assert.That(getResp.Data!.Poll.VoteCount, Is.GreaterThanOrEqualTo(1));
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
