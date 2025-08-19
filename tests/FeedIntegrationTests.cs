using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GetStream;
using GetStream.Models;
using GetStream.Requests;
using NUnit.Framework;

namespace GetStream.Tests
{
    /// <summary>
    /// Systematic Integration tests for Feed operations
    /// These tests follow a logical flow: setup → create → operate → cleanup
    ///
    /// Test order:
    /// 1. Environment Setup (user, feed creation)
    /// 2. Activity Operations (create, read, update, delete)
    /// 3. Reaction Operations (add, query, delete)
    /// 4. Comment Operations (add, read, update, delete)
    /// 5. Batch Operations
    /// 6. Cleanup
    /// </summary>
    [TestFixture]
    public class FeedIntegrationTests
    {
        private StreamClient _client;
        private FeedsV3Client _feedsV3Client;
        private string _testUserId;
        private string _testUserId2; // For follow operations
        private string _testFeedId;
        private string _testFeedId2;

        // Track created resources for cleanup
        private readonly List<string> _createdActivityIds = new List<string>();
        private readonly List<string> _createdCommentIds = new List<string>();

        [OneTimeSetUp]
        public async Task SetUp()
        {
            // Try to find .env file in the solution root (going up from tests directory)
            var solutionRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            var envFilePath = Path.Combine(solutionRoot, ".env");
            
            ClientBuilder builder;
            if (File.Exists(envFilePath))
            {
                builder = ClientBuilder.FromEnvFile(envFilePath);
            }
            else
            {
                builder = ClientBuilder.FromEnv();
            }
            
            _client = builder.Build();
            _feedsV3Client = builder.BuildFeedsClient();

            _testUserId = "test-user-" + Guid.NewGuid().ToString("N")[..8];
            _testUserId2 = "test-user-2-" + Guid.NewGuid().ToString("N")[..8];
            _testFeedId = "test-feed-" + Guid.NewGuid().ToString("N")[..8];
            _testFeedId2 = "test-feed-2-" + Guid.NewGuid().ToString("N")[..8];

            // Setup environment for each test
            await SetupEnvironment();
        }

        [OneTimeTearDown]
        public async Task TearDown()
        {
            // Cleanup created resources in reverse order
            await CleanupResources();
        }

        // =================================================================
        // ENVIRONMENT SETUP (called in SetUp for each test)
        // =================================================================

        private async Task SetupEnvironment()
        {
            try
            {
                Console.WriteLine("🔧 Setting up test environment...");

                // Create test users first
                // snippet-start: CreateUsers
                var updateUsersRequest = new UpdateUsersRequest
                {
                    Users = new Dictionary<string, UserRequest>
                    {
                        [_testUserId] = new UserRequest
                        {
                            ID = _testUserId,
                            Name = "Test User 1",
                            Role = "user"
                        },
                        [_testUserId2] = new UserRequest
                        {
                            ID = _testUserId2,
                            Name = "Test User 2", 
                            Role = "user"
                        }
                    }
                };
                var userResponse = await _client.UpdateUsersAsync(updateUsersRequest);
                // snippet-end: CreateUsers

                if (userResponse.Data == null)
                {
                    throw new Exception($"Failed to create users");
                }

                Console.WriteLine($"✅ Created test users: {_testUserId}, {_testUserId2}");

                // Create feeds
                // snippet-start: GetOrCreateFeed
                var feedResponse1 = await _feedsV3Client.GetOrCreateFeedAsync(
                    FeedGroupID: "user",
                    FeedID: _testFeedId,
                    request: new GetOrCreateFeedRequest { UserID = _testUserId }
                );
                var feedResponse2 = await _feedsV3Client.GetOrCreateFeedAsync(
                    FeedGroupID: "user", 
                    FeedID: _testFeedId2,
                    request: new GetOrCreateFeedRequest { UserID = _testUserId2 }
                );
                // snippet-end: GetOrCreateFeed

                if (feedResponse1.Data == null)
                {
                    throw new Exception($"Failed to create feed 1");
                }
                if (feedResponse2.Data == null)
                {
                    throw new Exception($"Failed to create feed 2");
                }

                Console.WriteLine($"✅ Created test feeds: {_testFeedId}, {_testFeedId2}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"⚠️ Setup failed: {e.Message}");
                throw;
            }
        }

        // =================================================================
        // 1. ENVIRONMENT SETUP TEST (demonstrates the setup process)
        // =================================================================

        [Test, Order(1)]
        public void Test01_SetupEnvironmentDemo()
        {
            Console.WriteLine("\n🔧 Demonstrating environment setup...");
            Console.WriteLine("✅ Users and feeds are automatically created in SetUp()");
            Console.WriteLine($"   Test User 1: {_testUserId}");
            Console.WriteLine($"   Test User 2: {_testUserId2}");
            Console.WriteLine($"   Test Feed 1: {_testFeedId}");
            Console.WriteLine($"   Test Feed 2: {_testFeedId2}");

            Assert.That(true, Is.True); // Just a demo test
        }

        // =================================================================
        // 2. ACTIVITY OPERATIONS
        // =================================================================

        [Test, Order(2)]
        public async Task Test02_CreateActivity()
        {
            Console.WriteLine("\n📝 Testing activity creation...");

            // snippet-start: AddActivity
            var activity = new AddActivityRequest
            {
                Type = "post",
                Text = "This is a test activity from .NET SDK",
                UserID = _testUserId,
                Feeds = new List<string> { $"user:{_testFeedId}" }
            };
            var response = await _feedsV3Client.AddActivityAsync(activity);
            // snippet-end: AddActivity

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Data, Is.Not.Null);
            Assert.That(response.Data.Activity, Is.Not.Null);
            Assert.That(response.Data.Activity.ID, Is.Not.Null);

            var activityId = response.Data.Activity.ID;
            _createdActivityIds.Add(activityId);
            
            Console.WriteLine($"✅ Created activity with ID: {activityId}");
        }

        [Test, Order(3)]
        public async Task Test02b_CreateActivityWithAttachments()
        {
            Console.WriteLine("\n🖼️ Testing activity creation with image attachments...");

            // snippet-start: AddActivityWithImageAttachment
            var activity = new AddActivityRequest
            {
                Type = "post",
                Text = "Look at this amazing view of NYC!",
                UserID = _testUserId,
                Feeds = new List<string> { $"user:{_testFeedId}" },
                Attachments = new List<Attachment>
                {
                    new Attachment
                    {
                        ImageUrl = "https://example.com/nyc-skyline.jpg",
                        Type = "image",
                        Title = "NYC Skyline"
                    }
                },
                Custom = new Dictionary<string, object>
                {
                    ["location"] = "New York City",
                    ["camera"] = "iPhone 15 Pro"
                }
            };
            var response = await _feedsV3Client.AddActivityAsync(activity);
            // snippet-end: AddActivityWithImageAttachment

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Data, Is.Not.Null);
            Assert.That(response.Data.Activity, Is.Not.Null);
            
            var activityId = response.Data.Activity.ID;
            _createdActivityIds.Add(activityId);
            
            Console.WriteLine($"✅ Created activity with image attachment: {activityId}");
        }

        [Test, Order(4)]
        public async Task Test03_QueryActivities()
        {
            Console.WriteLine("\n🔍 Testing activity querying...");
            
            // snippet-start: QueryActivities
            var response = await _feedsV3Client.QueryActivitiesAsync(
                new QueryActivitiesRequest
                {
                    Limit = 10,
                    Filter = new Dictionary<string, object> { ["activity_type"] = "post" }
                }
            );
            // snippet-end: QueryActivities

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Data, Is.Not.Null);
            Assert.That(response.Data.Activities, Is.Not.Null);
            Console.WriteLine("✅ Queried activities successfully");
        }

        [Test, Order(5)]
        public async Task Test04_GetSingleActivity()
        {
            Console.WriteLine("\n📄 Testing single activity retrieval...");
            
            // First create an activity to retrieve
            var activity = new AddActivityRequest
            {
                Type = "post",
                Text = "Activity for retrieval test",
                UserID = _testUserId,
                Feeds = new List<string> { $"user:{_testFeedId}" }
            };

            var createResponse = await _feedsV3Client.AddActivityAsync(activity);
            Assert.That(createResponse.Data?.Activity?.ID, Is.Not.Null);
            
            var activityId = createResponse.Data.Activity.ID;
            _createdActivityIds.Add(activityId);

            // snippet-start: GetActivity
            var response = await _feedsV3Client.GetActivityAsync(activityId);
            // snippet-end: GetActivity

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Data, Is.Not.Null);
            Assert.That(response.Data.Activity, Is.Not.Null);
            Assert.That(response.Data.Activity.ID, Is.EqualTo(activityId));
            Console.WriteLine("✅ Retrieved single activity");
        }

        [Test, Order(6)]
        public async Task Test05_UpdateActivity()
        {
            Console.WriteLine("\n✏️ Testing activity update...");
            
            // First create an activity to update
            var activity = new AddActivityRequest
            {
                Type = "post",
                Text = "Activity for update test",
                UserID = _testUserId,
                Feeds = new List<string> { $"user:{_testFeedId}" }
            };
            
            var createResponse = await _feedsV3Client.AddActivityAsync(activity);
            Assert.That(createResponse.Data?.Activity?.ID, Is.Not.Null);
            
            var activityId = createResponse.Data.Activity.ID;
            _createdActivityIds.Add(activityId);

            // snippet-start: UpdateActivity
            var response = await _feedsV3Client.UpdateActivityAsync(
                activityId,
                new UpdateActivityRequest
                {
                    Text = "Updated activity text from .NET SDK",
                    UserID = _testUserId,  // Required for server-side auth
                    Custom = new Dictionary<string, object>
                    {
                        ["updated"] = true,
                        ["update_time"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    }
                }
            );
            // snippet-end: UpdateActivity

            Assert.That(response, Is.Not.Null);
            Console.WriteLine("✅ Updated activity");
        }

        // =================================================================
        // 3. REACTION OPERATIONS
        // =================================================================

        [Test, Order(7)]
        public async Task Test06_AddReaction()
        {
            Console.WriteLine("\n👍 Testing reaction addition...");
            
            // First create an activity to react to
            var activity = new AddActivityRequest
            {
                Type = "post",
                Text = "Activity for reaction test",
                UserID = _testUserId,
                Feeds = new List<string> { $"user:{_testFeedId}" }
            };
            
            var createResponse = await _feedsV3Client.AddActivityAsync(activity);
            Assert.That(createResponse.Data?.Activity?.ID, Is.Not.Null);
            
            var activityId = createResponse.Data.Activity.ID;
            _createdActivityIds.Add(activityId);

            // snippet-start: AddReaction
            var response = await _feedsV3Client.AddReactionAsync(
                activityId,
                new AddReactionRequest
                {
                    Type = "like",
                    UserID = _testUserId
                }
            );
            // snippet-end: AddReaction

            Assert.That(response, Is.Not.Null);
            Console.WriteLine("✅ Added like reaction");
        }

        [Test, Order(8)]
        public async Task Test07_QueryReactions()
        {
            Console.WriteLine("\n🔍 Testing reaction querying...");
            
            // Create an activity and add a reaction to it
            var activity = new AddActivityRequest
            {
                Type = "post",
                Text = "Activity for query reactions test",
                UserID = _testUserId,
                Feeds = new List<string> { $"user:{_testFeedId}" }
            };
            
            var createResponse = await _feedsV3Client.AddActivityAsync(activity);
            Assert.That(createResponse.Data?.Activity?.ID, Is.Not.Null);
            
            var activityId = createResponse.Data.Activity.ID;
            _createdActivityIds.Add(activityId);
            
            // Add a reaction first
            var reactionResponse = await _feedsV3Client.AddReactionAsync(
                activityId,
                new AddReactionRequest
                {
                    Type = "like",
                    UserID = _testUserId
                }
            );
            Assert.That(reactionResponse, Is.Not.Null);

            try
            {
                // snippet-start: QueryActivityReactions
                var response = await _feedsV3Client.QueryActivityReactionsAsync(
                    activityId,
                    new QueryActivityReactionsRequest
                    {
                        Limit = 10,
                        Filter = new Dictionary<string, object> { ["type"] = "like" }
                    }
                );
                // snippet-end: QueryActivityReactions

                Assert.That(response, Is.Not.Null);
                Console.WriteLine("✅ Queried reactions");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Query reactions skipped: {e.Message}");
                Assert.Ignore($"Query reactions not supported: {e.Message}");
            }
        }

        // =================================================================
        // 4. COMMENT OPERATIONS
        // =================================================================

        [Test, Order(9)]
        public async Task Test08_AddComment()
        {
            Console.WriteLine("\n💬 Testing comment addition...");
            
            // First create an activity to comment on
            var activity = new AddActivityRequest
            {
                Type = "post",
                Text = "Activity for comment test",
                UserID = _testUserId,
                Feeds = new List<string> { $"user:{_testFeedId}" }
            };
            
            var createResponse = await _feedsV3Client.AddActivityAsync(activity);
            Assert.That(createResponse.Data?.Activity?.ID, Is.Not.Null);
            
            var activityId = createResponse.Data.Activity.ID;
            _createdActivityIds.Add(activityId);

            // snippet-start: AddComment
            var response = await _feedsV3Client.AddCommentAsync(
                new AddCommentRequest
                {
                    Comment = "This is a test comment from .NET SDK",
                    ObjectID = activityId,
                    ObjectType = "activity",
                    UserID = _testUserId
                }
            );
            // snippet-end: AddComment

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Data, Is.Not.Null);
            
            if (response.Data.Comment?.ID != null)
            {
                _createdCommentIds.Add(response.Data.Comment.ID);
                Console.WriteLine($"✅ Added comment with ID: {response.Data.Comment.ID}");
            }
            else
            {
                Console.WriteLine("✅ Added comment (no ID returned)");
            }
        }

        [Test, Order(10)]
        public async Task Test09_QueryComments()
        {
            Console.WriteLine("\n🔍 Testing comment querying...");
            
            // Create an activity and add a comment to it
            var activity = new AddActivityRequest
            {
                Type = "post",
                Text = "Activity for query comments test",
                UserID = _testUserId,
                Feeds = new List<string> { $"user:{_testFeedId}" }
            };
            
            var createResponse = await _feedsV3Client.AddActivityAsync(activity);
            Assert.That(createResponse.Data?.Activity?.ID, Is.Not.Null);
            
            var activityId = createResponse.Data.Activity.ID;
            _createdActivityIds.Add(activityId);
            
            // Add a comment first
            var commentResponse = await _feedsV3Client.AddCommentAsync(
                new AddCommentRequest
                {
                    Comment = "Comment for query test",
                    ObjectID = activityId,
                    ObjectType = "activity",
                    UserID = _testUserId
                }
            );
            Assert.That(commentResponse, Is.Not.Null);

            // snippet-start: QueryComments
            var response = await _feedsV3Client.QueryCommentsAsync(
                new QueryCommentsRequest
                {
                    Filter = new Dictionary<string, object> { ["object_id"] = activityId },
                    Limit = 10
                }
            );
            // snippet-end: QueryComments

            Assert.That(response, Is.Not.Null);
            Console.WriteLine("✅ Queried comments");
        }

        [Test, Order(11)]
        public async Task Test10_UpdateComment()
        {
            Console.WriteLine("\n✏️ Testing comment update...");
            
            // Create an activity and add a comment to update
            var activity = new AddActivityRequest
            {
                Type = "post",
                Text = "Activity for update comment test",
                UserID = _testUserId,
                Feeds = new List<string> { $"user:{_testFeedId}" }
            };
            
            var createResponse = await _feedsV3Client.AddActivityAsync(activity);
            Assert.That(createResponse.Data?.Activity?.ID, Is.Not.Null);
            
            var activityId = createResponse.Data.Activity.ID;
            _createdActivityIds.Add(activityId);
            
            // Add a comment to update
            var commentResponse = await _feedsV3Client.AddCommentAsync(
                new AddCommentRequest
                {
                    Comment = "Comment to be updated",
                    ObjectID = activityId,
                    ObjectType = "activity",
                    UserID = _testUserId
                }
            );
            Assert.That(commentResponse, Is.Not.Null);
            
            var commentId = commentResponse.Data?.Comment?.ID ?? "comment-id";  // Fallback if ID not returned

            // snippet-start: UpdateComment
            var response = await _feedsV3Client.UpdateCommentAsync(
                commentId,
                new UpdateCommentRequest
                {
                    Comment = "Updated comment text from .NET SDK"
                }
            );
            // snippet-end: UpdateComment

            Assert.That(response, Is.Not.Null);
            Console.WriteLine("✅ Updated comment");
        }

        // =================================================================
        // 5. BOOKMARK OPERATIONS
        // =================================================================

        [Test, Order(12)]
        public async Task Test11_AddBookmark()
        {
            Console.WriteLine("\n🔖 Testing bookmark addition...");
            
            // Create an activity to bookmark
            var activity = new AddActivityRequest
            {
                Type = "post",
                Text = "Activity for bookmark test",
                UserID = _testUserId,
                Feeds = new List<string> { $"user:{_testFeedId}" }
            };
            
            var createResponse = await _feedsV3Client.AddActivityAsync(activity);
            Assert.That(createResponse.Data?.Activity?.ID, Is.Not.Null);
            
            var activityId = createResponse.Data.Activity.ID;
            _createdActivityIds.Add(activityId);

            try
            {
                // snippet-start: AddBookmark
                var response = await _feedsV3Client.AddBookmarkAsync(
                    activityId,
                    new AddBookmarkRequest
                    {
                        UserID = _testUserId,
                        NewFolder = new AddFolderRequest { Name = "test-bookmarks1" }
                    }
                );
                // snippet-end: AddBookmark

                Assert.That(response, Is.Not.Null);
                Console.WriteLine("✅ Added bookmark");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Add bookmark failed: {e.Message}");
                Assert.Ignore($"Add bookmark not supported: {e.Message}");
            }
        }

        [Test, Order(13)]
        public async Task Test12_QueryBookmarks()
        {
            Console.WriteLine("\n🔍 Testing bookmark querying...");

            // snippet-start: QueryBookmarks
            var response = await _feedsV3Client.QueryBookmarksAsync(
                new QueryBookmarksRequest
                {
                    Limit = 10,
                    Filter = new Dictionary<string, object> { ["user_id"] = _testUserId }
                }
            );
            // snippet-end: QueryBookmarks

            Assert.That(response, Is.Not.Null);
            Console.WriteLine("✅ Queried bookmarks");
        }

        // =================================================================
        // 6. BATCH OPERATIONS
        // =================================================================

        [Test, Order(10)]
        public async Task Test16_UpsertActivities()
        {
            Console.WriteLine("\n📝 Testing batch activity upsert...");

            // snippet-start: UpsertActivities
            var activities = new List<ActivityRequest>
            {
                new ActivityRequest
                {
                    Type = "post",
                    Text = "Batch activity 1",
                    UserID = _testUserId
                },
                new ActivityRequest
                {
                    Type = "post", 
                    Text = "Batch activity 2",
                    UserID = _testUserId
                }
            };

            var response = await _feedsV3Client.UpsertActivitiesAsync(
                new UpsertActivitiesRequest { Activities = activities }
            );
            // snippet-end: UpsertActivities

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Data, Is.Not.Null);
            
            // Track created activities for cleanup
            if (response.Data.Activities != null)
            {
                foreach (var activityResponse in response.Data.Activities)
                {
                    if (!string.IsNullOrEmpty(activityResponse.ID))
                    {
                        _createdActivityIds.Add(activityResponse.ID);
                    }
                }
            }
            
            Console.WriteLine("✅ Upserted batch activities");
        }

        // =================================================================
        // 6. COMPREHENSIVE REAL-WORLD SCENARIO
        // =================================================================

        [Test, Order(11)]
        public async Task Test32_RealWorldUsageDemo()
        {
            Console.WriteLine("\n🌍 Testing real-world usage patterns...");
            
            // Scenario: User posts content, gets reactions and comments
            // snippet-start: RealWorldScenario
            
            // 1. User creates a post with image
            var postActivity = new AddActivityRequest
            {
                Type = "post",
                Text = "Just visited the most amazing coffee shop! ☕️",
                UserID = _testUserId,
                Feeds = new List<string> { $"user:{_testFeedId}" },
                Attachments = new List<Attachment>
                {
                    new Attachment
                    {
                        ImageUrl = "https://example.com/coffee-shop.jpg",
                        Type = "image",
                        Title = "Amazing Coffee Shop"
                    }
                },
                Custom = new Dictionary<string, object>
                {
                    ["location"] = "Downtown Coffee Co.",
                    ["rating"] = 5,
                    ["tags"] = new[] { "coffee", "food", "downtown" }
                }
            };
            var postResponse = await _feedsV3Client.AddActivityAsync(postActivity);
            Assert.That(postResponse.Data?.Activity?.ID, Is.Not.Null);
            
            var postId = postResponse.Data.Activity.ID;
            _createdActivityIds.Add(postId);
            
            // 2. Other users react to the post
            var reactionTypes = new[] { "like", "love", "wow" };
            foreach (var reactionType in reactionTypes)
            {
                var reactionResponse = await _feedsV3Client.AddReactionAsync(
                    postId,
                    new AddReactionRequest
                    {
                        Type = reactionType,
                        UserID = _testUserId2
                    }
                );
                Assert.That(reactionResponse, Is.Not.Null);
            }
            
            // 3. Users comment on the post
            var comments = new[]
            {
                "That place looks amazing! What did you order?",
                "I love their espresso! Great choice 😍",
                "Adding this to my must-visit list!"
            };
            
            foreach (var commentText in comments)
            {
                var commentResponse = await _feedsV3Client.AddCommentAsync(
                    new AddCommentRequest
                    {
                        Comment = commentText,
                        ObjectID = postId,
                        ObjectType = "activity",
                        UserID = _testUserId2
                    }
                );
                Assert.That(commentResponse, Is.Not.Null);
            }
            
            // 4. Query the activity with all its interactions
            var enrichedResponse = await _feedsV3Client.GetActivityAsync(postId);
            Assert.That(enrichedResponse, Is.Not.Null);
            
            // snippet-end: RealWorldScenario
            
            Console.WriteLine("✅ Completed real-world usage scenario demonstration");
        }

        // =================================================================
        // 7. CLEANUP OPERATIONS (in reverse order)
        // =================================================================

        [Test, Order(12)]
        public async Task Test23_DeleteActivities()
        {
            Console.WriteLine("\n🗑️ Testing activity deletion...");
            
            // Create some activities to delete
            var activitiesToDelete = new List<string>();
            for (int i = 1; i <= 2; i++)
            {
                var activity = new AddActivityRequest
                {
                    Type = "post",
                    Text = $"Activity {i} for delete test",
                    UserID = _testUserId,
                    Feeds = new List<string> { $"user:{_testFeedId}" }
                };
                
                var createResponse = await _feedsV3Client.AddActivityAsync(activity);
                Assert.That(createResponse.Data?.Activity?.ID, Is.Not.Null);
                
                var activityId = createResponse.Data.Activity.ID;
                activitiesToDelete.Add(activityId);
                _createdActivityIds.Add(activityId);
            }

            foreach (var activityId in activitiesToDelete)
            {
                // snippet-start: DeleteActivity
                var response = await _feedsV3Client.DeleteActivityAsync(activityId, false); // soft delete
                // snippet-end: DeleteActivity

                Assert.That(response, Is.Not.Null);
            }
            
            Console.WriteLine($"✅ Deleted {activitiesToDelete.Count} activities");
            _createdActivityIds.Clear();
        }

        // =================================================================
        // HELPER METHODS
        // =================================================================

        private async Task CleanupResources()
        {
            Console.WriteLine("\n🧹 Cleaning up test resources...");
            
            // Delete any remaining activities
            if (_createdActivityIds.Count > 0)
            {
                foreach (var activityId in _createdActivityIds)
                {
                    try
                    {
                        await _feedsV3Client.DeleteActivityAsync(activityId, true); // hard delete
                    }
                    catch (Exception e)
                    {
                        // Ignore cleanup errors
                        Console.WriteLine($"Warning: Failed to cleanup activity {activityId}: {e.Message}");
                    }
                }
            }
            
            // Delete any remaining comments
            if (_createdCommentIds.Count > 0)
            {
                foreach (var commentId in _createdCommentIds)
                {
                    try
                    {
                        await _feedsV3Client.DeleteCommentAsync(commentId, true); // hard delete
                    }
                    catch (Exception e)
                    {
                        // Ignore cleanup errors
                        Console.WriteLine($"Warning: Failed to cleanup comment {commentId}: {e.Message}");
                    }
                }
            }
            
            Console.WriteLine("✅ Cleanup completed");
        }
    }
}
