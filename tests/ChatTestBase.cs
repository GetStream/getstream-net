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
    /// <summary>
    /// Base class for chat integration tests. Provides helpers for creating and
    /// cleaning up users, channels, and messages following the patterns from getstream-go.
    /// </summary>
    public class ChatTestBase : TestBase
    {
        protected List<string> CreatedUserIds { get; } = new();
        protected List<(string Type, string Id)> CreatedChannels { get; } = new();

        private static readonly Random Rng = new();

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        [OneTimeTearDown]
        public async Task ChatCleanup()
        {
            // Hard-delete channels first (they reference users)
            foreach (var (type, id) in CreatedChannels)
            {
                try
                {
                    await StreamClient.MakeRequestAsync<object, DeleteChannelResponse>(
                        "DELETE",
                        "/api/v2/chat/channels/{type}/{id}",
                        new Dictionary<string, string> { ["hard_delete"] = "true" },
                        null,
                        new Dictionary<string, string> { ["type"] = type, ["id"] = id });
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            // Hard-delete users
            if (CreatedUserIds.Count > 0)
            {
                await DeleteUsersWithRetry(CreatedUserIds);
            }
        }

        /// <summary>
        /// Creates n test users with unique IDs. Tracks them for cleanup.
        /// </summary>
        protected async Task<List<string>> CreateTestUsers(int n)
        {
            var ids = Enumerable.Range(0, n)
                .Select(_ => $"test-user-{Guid.NewGuid():N}")
                .ToList();

            var users = ids.ToDictionary(
                id => id,
                id => new UserRequest { ID = id, Name = $"Test User {id}", Role = "user" }
            );

            await StreamClient.UpdateUsersAsync(new UpdateUsersRequest { Users = users });
            CreatedUserIds.AddRange(ids);
            return ids;
        }

        /// <summary>
        /// Creates a messaging channel with the given creator. Tracks it for cleanup.
        /// Returns the channel ID.
        /// </summary>
        protected async Task<string> CreateTestChannel(string creatorId)
        {
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

            CreatedChannels.Add(("messaging", channelId));
            return channelId;
        }

        /// <summary>
        /// Creates a messaging channel with the given creator and members. Tracks it for cleanup.
        /// Returns the channel ID.
        /// </summary>
        protected async Task<string> CreateTestChannelWithMembers(string creatorId, List<string> memberIds)
        {
            var channelId = $"test-ch-{RandomString(12)}";
            var members = memberIds.Select(id => new ChannelMemberRequest { UserID = id }).ToList();

            await StreamClient.MakeRequestAsync<ChannelGetOrCreateRequest, ChannelStateResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/query",
                null,
                new ChannelGetOrCreateRequest
                {
                    Data = new ChannelInput
                    {
                        CreatedByID = creatorId,
                        Members = members
                    }
                },
                new Dictionary<string, string> { ["type"] = "messaging", ["id"] = channelId });

            CreatedChannels.Add(("messaging", channelId));
            return channelId;
        }

        /// <summary>
        /// Sends a message to a channel and returns the message ID.
        /// </summary>
        protected async Task<string> SendTestMessage(string channelType, string channelId, string userId, string text)
        {
            var resp = await StreamClient.MakeRequestAsync<SendMessageRequest, SendMessageResponse>(
                "POST",
                "/api/v2/chat/channels/{type}/{id}/message",
                null,
                new SendMessageRequest
                {
                    Message = new MessageRequest { Text = text, UserID = userId }
                },
                new Dictionary<string, string> { ["type"] = channelType, ["id"] = channelId });

            Assert.That(resp.Data, Is.Not.Null);
            Assert.That(resp.Data!.Message, Is.Not.Null);
            Assert.That(resp.Data!.Message.ID, Is.Not.Null.And.Not.Empty);
            return resp.Data!.Message.ID;
        }

        /// <summary>
        /// Deletes users with retry logic to handle rate limiting.
        /// Retries up to 10 times with exponential backoff (3s, 6s, 9s...).
        /// </summary>
        protected async Task DeleteUsersWithRetry(List<string> userIds)
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    await StreamClient.DeleteUsersAsync(new DeleteUsersRequest
                    {
                        UserIds = userIds,
                        User = "hard",
                        Messages = "hard",
                        Conversations = "hard"
                    });
                    return;
                }
                catch (Exception e)
                {
                    if (!e.Message.Contains("Too many requests")) return;
                    await Task.Delay((i + 1) * 3000);
                }
            }
        }

        /// <summary>
        /// Polls an async task until it completes or fails (up to 30 seconds).
        /// </summary>
        protected async Task WaitForTask(string taskId)
        {
            for (int i = 0; i < 30; i++)
            {
                var result = await StreamClient.GetTaskAsync(taskId);
                if (result.Data?.Status == "completed" || result.Data?.Status == "failed")
                    return;
                await Task.Delay(1000);
            }
            Assert.Fail($"Task {taskId} did not complete after 30 attempts");
        }

        /// <summary>
        /// Generates a random alphanumeric string of length n.
        /// </summary>
        protected static string RandomString(int n)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Range(0, n).Select(_ => chars[Rng.Next(chars.Length)]).ToArray());
        }

        /// <summary>
        /// Query channels with the given filter conditions.
        /// </summary>
        protected async Task<StreamResponse<QueryChannelsResponse>> QueryChannels(QueryChannelsRequest request)
        {
            return await StreamClient.MakeRequestAsync<QueryChannelsRequest, QueryChannelsResponse>(
                "POST",
                "/api/v2/chat/channels",
                null,
                request,
                null);
        }

        /// <summary>
        /// Query users with the given filter. Encodes payload as JSON query param for GET endpoint.
        /// </summary>
        protected async Task<StreamResponse<QueryUsersResponse>> QueryUsers(QueryUsersPayload payload)
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            var queryParams = new Dictionary<string, string> { ["payload"] = json };
            return await StreamClient.MakeRequestAsync<object, QueryUsersResponse>(
                "GET",
                "/api/v2/users",
                queryParams,
                null,
                null);
        }

        /// <summary>
        /// Query channel members. Encodes payload as JSON query param for GET endpoint.
        /// </summary>
        protected async Task<StreamResponse<MembersResponse>> QueryMembers(QueryMembersPayload payload)
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            var queryParams = new Dictionary<string, string> { ["payload"] = json };
            return await StreamClient.MakeRequestAsync<object, MembersResponse>(
                "GET",
                "/api/v2/chat/members",
                queryParams,
                null,
                null);
        }

        /// <summary>
        /// Helper to build $in filter condition for IDs.
        /// </summary>
        protected static Dictionary<string, object> InFilter(string field, List<string> values)
        {
            return new Dictionary<string, object>
            {
                [field] = new Dictionary<string, object> { ["$in"] = values }
            };
        }
    }
}
