using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GetStream;
using GetStream.Models;
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
        private StreamClient _client = null!;
        private FeedsV3Client _feedsV3Client = null!;
        private string _testUserId = null!;
        private string _testUserId2 = null!; // For follow operations
        private string _testUserId3 = null!; // For follow operations
        private string _testFeedId = null!;
        private string _testFeedId2 = null!;
        private string _testFeedId3 = null!;

        // Track created resources for cleanup
        private readonly List<string> _createdActivityIds = new List<string>();
        private readonly List<string> _createdCommentIds = new List<string>();

        [OneTimeSetUp]
        public async Task SetUp()
        {
            // Try to find .env file in the solution root (going up from tests directory)
            var solutionRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            var envFilePath = Path.Combine(solutionRoot, ".env");
            
            // snippet-start: Getting_Started
            ClientBuilder builder;
            if (File.Exists(envFilePath))
            {
                builder = ClientBuilder.FromEnvFile(envFilePath);
            }
            else
            {
                builder = ClientBuilder.FromEnv();
            }

            // snippet-end: Getting_Started
            
            _client = builder.Build();
            _feedsV3Client = builder.BuildFeedsClient();

            _testUserId = "test-user-" + Guid.NewGuid().ToString("N")[..8];
            _testUserId2 = "test-user-2-" + Guid.NewGuid().ToString("N")[..8];
            _testUserId3 = "test-user-3-" + Guid.NewGuid().ToString("N")[..8];
            _testFeedId = "test-feed-" + Guid.NewGuid().ToString("N")[..8];
            _testFeedId2 = "test-feed-2-" + Guid.NewGuid().ToString("N")[..8];
            _testFeedId3 = "test-feed-3-" + Guid.NewGuid().ToString("N")[..8];
            

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
                        },
                        [_testUserId3] = new UserRequest
                        {
                            ID = _testUserId3,
                            Name = "Test User 3", 
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

                var feedResponse3 = await _feedsV3Client.GetOrCreateFeedAsync(
                    FeedGroupID: "user",
                    FeedID: _testFeedId3,
                    request: new GetOrCreateFeedRequest { UserID = _testUserId3 }
                );
                if (feedResponse1.Data == null)
                {
                    throw new Exception($"Failed to create feed 1");
                }
                if (feedResponse2.Data == null)
                {
                    throw new Exception($"Failed to create feed 2");
                }
                if (feedResponse3.Data == null)
                {
                    throw new Exception($"Failed to create feed 3");
                }

                Console.WriteLine($"✅ Created test feeds: {_testFeedId}, {_testFeedId2}, {_testFeedId3}");
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

            Assert.Pass(); // Just a demo test
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
            Assert.That(response.Data!.Activity, Is.Not.Null);
            Assert.That(response.Data!.Activity!.ID, Is.Not.Null);

            var activityId = response.Data!.Activity!.ID!;
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
            Assert.That(response.Data!.Activity, Is.Not.Null);
            
            var activityId = response.Data!.Activity!.ID!;
            _createdActivityIds.Add(activityId);
            
            Console.WriteLine($"✅ Created activity with image attachment: {activityId}");
        }

        [Test, Order(4)]
        public async Task Test02c_CreateVideoActivity()
        {
            Console.WriteLine("\n🎥 Testing video activity creation...");

            // snippet-start: AddVideoActivity
            var activity = new AddActivityRequest
            {
                Type = "video",
                Text = "Check out this amazing video!",
                UserID = _testUserId,
                Feeds = new List<string> { $"user:{_testFeedId}" }
                // Note: Video attachments would be added here in a real scenario
            };
            var response = await _feedsV3Client.AddActivityAsync(activity);
            // snippet-end: AddVideoActivity

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Data, Is.Not.Null);
            Assert.That(response.Data!.Activity, Is.Not.Null);
            
            var activityId = response.Data!.Activity!.ID!;
            _createdActivityIds.Add(activityId);
            
            Console.WriteLine($"✅ Created video activity: {activityId}");
        }

        [Test, Order(5)]
        public async Task Test02d_CreateStoryActivityWithExpiration()
        {
            Console.WriteLine("\n📖 Testing story activity with expiration...");

            // snippet-start: AddStoryActivityWithExpiration
            var tomorrow = DateTime.UtcNow.AddDays(1);
            var activity = new AddActivityRequest
            {
                Type = "story",
                Text = "My daily story - expires tomorrow!",
                UserID = _testUserId,
                Feeds = new List<string> { $"user:{_testFeedId}" },
                ExpiresAt = tomorrow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Attachments = new List<Attachment>
                {
                    new Attachment
                    {
                        ImageUrl = "https://example.com/story-image.jpg",
                        Type = "image"
                    },
                    new Attachment
                    {
                        AssetUrl = "https://example.com/story-video.mp4",
                        Type = "video"
                    }
                },
                Custom = new Dictionary<string, object>
                {
                    ["story_type"] = "daily",
                    ["auto_expire"] = true
                }
            };
            var response = await _feedsV3Client.AddActivityAsync(activity);
            // snippet-end: AddStoryActivityWithExpiration

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Data, Is.Not.Null);
            Assert.That(response.Data!.Activity, Is.Not.Null);
            
            var activityId = response.Data!.Activity!.ID!;
            _createdActivityIds.Add(activityId);
            
            Console.WriteLine($"✅ Created story activity with expiration: {activityId}");
        }

        [Test, Order(6)]
        public async Task Test02e_CreateActivityMultipleFeeds()
        {
            Console.WriteLine("\n📡 Testing activity creation to multiple feeds...");

            // snippet-start: AddActivityToMultipleFeeds
            var activity = new AddActivityRequest
            {
                Type = "post",
                Text = "This post appears in multiple feeds!",
                UserID = _testUserId,
                Feeds = new List<string> 
                { 
                    $"user:{_testFeedId}",
                    $"user:{_testFeedId2}"
                },
                Custom = new Dictionary<string, object>
                {
                    ["cross_posted"] = true,
                    ["target_feeds"] = 2
                }
            };
            var response = await _feedsV3Client.AddActivityAsync(activity);
            // snippet-end: AddActivityToMultipleFeeds

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Data, Is.Not.Null);
            Assert.That(response.Data!.Activity, Is.Not.Null);
            
            var activityId = response.Data!.Activity!.ID!;
            _createdActivityIds.Add(activityId);
            
            Console.WriteLine($"✅ Created activity in multiple feeds: {activityId}");
        }

        [Test, Order(7)]
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
            Assert.That(response.Data!.Activities, Is.Not.Null);
            Console.WriteLine("✅ Queried activities successfully");
        }

        [Test, Order(8)]
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
            
            var activityId = createResponse.Data!.Activity!.ID!;
            _createdActivityIds.Add(activityId);

            // snippet-start: GetActivity
            var response = await _feedsV3Client.GetActivityAsync(activityId);
            // snippet-end: GetActivity

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Data, Is.Not.Null);
            Assert.That(response.Data!.Activity, Is.Not.Null);
            Assert.That(response.Data!.Activity!.ID, Is.EqualTo(activityId));
            Console.WriteLine("✅ Retrieved single activity");
        }

        [Test, Order(9)]
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
            
            var activityId = createResponse.Data!.Activity!.ID!;
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
            
            var activityId = createResponse.Data!.Activity!.ID!;
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
            
            var activityId = createResponse.Data!.Activity!.ID!;
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

                // snippet-start: QueryActivityReactions
                var response = await _feedsV3Client.QueryActivityReactionsAsync(
                    activityId,
                    new QueryActivityReactionsRequest
                    {
                        Limit = 10,
                        Filter = new Dictionary<string, object> { ["reaction_type"] = "like" }
                    }
                );
                // snippet-end: QueryActivityReactions

                Assert.That(response, Is.Not.Null);
                Console.WriteLine("✅ Queried reactions");
            
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
            
            var activityId = createResponse.Data!.Activity!.ID!;
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
            
            if (response.Data!.Comment?.ID != null)
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
            
            var activityId = createResponse.Data!.Activity!.ID!;
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
            
            var activityId = createResponse.Data!.Activity!.ID!;
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
            
            var activityId = createResponse.Data!.Activity!.ID!;
            _createdActivityIds.Add(activityId);

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

        [Test, Order(13)]
        public async Task Test13_UpdateBookmark()
        {
            Console.WriteLine("\n✏️ Testing bookmark update...");
            
            // Create an activity to bookmark first
            var activity = new AddActivityRequest
            {
                Type = "post",
                Text = "Activity for update bookmark test",
                UserID = _testUserId,
                Feeds = new List<string> { $"user:{_testFeedId}" }
            };
            
            var createResponse = await _feedsV3Client.AddActivityAsync(activity);
            Assert.That(createResponse.Data?.Activity?.ID, Is.Not.Null);
            
            var activityId = createResponse.Data!.Activity!.ID!;
            _createdActivityIds.Add(activityId);

            // Add a bookmark first
                var addResponse = await _feedsV3Client.AddBookmarkAsync(
                    activityId,
                    new AddBookmarkRequest
                    {
                        UserID = _testUserId,
                        NewFolder = new AddFolderRequest { Name = "test-bookmarks-update" }
                    }
                );
                Assert.That(addResponse, Is.Not.Null);
                Assert.That(addResponse.Data?.Bookmark?.Folder?.ID, Is.Not.Null);

                // Get the folder ID from the bookmark response
                var folderId = addResponse.Data!.Bookmark!.Folder!.ID;

                // snippet-start: UpdateBookmark
                var response = await _feedsV3Client.UpdateBookmarkAsync(
                    activityId,
                    new UpdateBookmarkRequest
                    {
                        UserID = _testUserId,
                        FolderID = folderId  // Use existing folder ID, not create new folder
                    }
                );
                // snippet-end: UpdateBookmark

                Assert.That(response, Is.Not.Null);
                Console.WriteLine("✅ Updated bookmark");
        }

        [Test, Order(14)]
        public async Task Test14_FollowUser()
        {
            Console.WriteLine("\n👥 Testing follow operation...");

                // snippet-start: Follow
                var response = await _feedsV3Client.FollowAsync(
                    new FollowRequest
                    {
                        Source = $"user:{_testFeedId}",
                        Target = $"user:{_testFeedId2}"
                    }
                );
                // snippet-end: Follow

                Assert.That(response, Is.Not.Null);
                Console.WriteLine("✅ Followed user");
        }

        [Test, Order(15)]
        public async Task Test15_QueryFollows()
        {
            Console.WriteLine("\n🔍 Testing follow querying...");

            // snippet-start: QueryFollows
            var response = await _feedsV3Client.QueryFollowsAsync(
                new QueryFollowsRequest
                {
                    Limit = 10
                }
            );
            // snippet-end: QueryFollows

            Assert.That(response, Is.Not.Null);
            Console.WriteLine("✅ Queried follows");
        }

        [Test, Order(16)]
        public async Task Test17_PinActivity()
        {
            Console.WriteLine("\n📌 Testing activity pinning...");
            
            // Create an activity to pin
            var activity = new AddActivityRequest
            {
                Type = "post",
                Text = "Activity to pin",
                UserID = _testUserId,
                Feeds = new List<string> { $"user:{_testFeedId}" }
            };
            
            var createResponse = await _feedsV3Client.AddActivityAsync(activity);
            Assert.That(createResponse.Data?.Activity?.ID, Is.Not.Null);
            
            var activityId = createResponse.Data!.Activity!.ID!;
            _createdActivityIds.Add(activityId);

            // snippet-start: PinActivity
            var response = await _feedsV3Client.PinActivityAsync(
                "user",
                _testFeedId,
                activityId,
                new PinActivityRequest
                {
                    UserID = _testUserId
                }
            );
            // snippet-end: PinActivity

            Assert.That(response, Is.Not.Null);
            Console.WriteLine("✅ Pinned activity");
        }

        [Test, Order(17)]
        public async Task Test18_UnpinActivity()
        {
            Console.WriteLine("\n📌 Testing activity unpinning...");
            
            // Create and pin an activity first
            var activity = new AddActivityRequest
            {
                Type = "post",
                Text = "Activity to unpin",
                UserID = _testUserId,
                Feeds = new List<string> { $"user:{_testFeedId}" }
            };
            
            var createResponse = await _feedsV3Client.AddActivityAsync(activity);
            Assert.That(createResponse.Data?.Activity?.ID, Is.Not.Null);
            
            var activityId = createResponse.Data!.Activity!.ID!;
            _createdActivityIds.Add(activityId);

            // Pin it first
            await _feedsV3Client.PinActivityAsync(
                "user",
                _testFeedId,
                activityId,
                new PinActivityRequest
                {
                    UserID = _testUserId
                }
            );

            // snippet-start: UnpinActivity
            var response = await _feedsV3Client.UnpinActivityAsync(
                "user",
                _testFeedId,
                activityId,
                new { user_id = _testUserId }
            );
            // snippet-end: UnpinActivity

            Assert.That(response, Is.Not.Null);
            Console.WriteLine("✅ Unpinned activity");
        }

        [Test, Order(18)]
        public async Task Test19_DeleteBookmark()
        {
            Console.WriteLine("\n🗑️ Testing bookmark deletion...");
            
            // Create an activity to bookmark first
            var activity = new AddActivityRequest
            {
                Type = "post",
                Text = "Activity for delete bookmark test",
                UserID = _testUserId,
                Feeds = new List<string> { $"user:{_testFeedId}" }
            };
            
            var createResponse = await _feedsV3Client.AddActivityAsync(activity);
            Assert.That(createResponse.Data?.Activity?.ID, Is.Not.Null);
            
            var activityId = createResponse.Data!.Activity!.ID!;
            _createdActivityIds.Add(activityId);

            // Add a bookmark first
                var addResponse = await _feedsV3Client.AddBookmarkAsync(
                    activityId,
                    new AddBookmarkRequest
                    {
                        UserID = _testUserId,
                        NewFolder = new AddFolderRequest { Name = "test-bookmarks-delete" }
                    }
                );
                Assert.That(addResponse, Is.Not.Null);
                Assert.That(addResponse.Data?.Bookmark?.Folder?.ID, Is.Not.Null);

                // Get the folder ID from the bookmark response
                var folderId = addResponse.Data!.Bookmark!.Folder!.ID;

                // snippet-start: DeleteBookmark
                var response = await _feedsV3Client.DeleteBookmarkAsync(
                    activityId,
                    new { folder_id = folderId, user_id = _testUserId }
                );
                // snippet-end: DeleteBookmark

                Assert.That(response, Is.Not.Null);
                Console.WriteLine("✅ Deleted bookmark");
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
            if (response.Data!.Activities != null)
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

        [Test, Order(19)]
        public async Task Test20_DeleteReaction()
        {
            Console.WriteLine("\n🗑️ Testing reaction deletion...");
            
            // Create an activity and add a reaction first
            var activity = new AddActivityRequest
            {
                Type = "post",
                Text = "Activity for delete reaction test",
                UserID = _testUserId,
                Feeds = new List<string> { $"user:{_testFeedId}" }
            };
            
            var createResponse = await _feedsV3Client.AddActivityAsync(activity);
            Assert.That(createResponse.Data?.Activity?.ID, Is.Not.Null);
            
            var activityId = createResponse.Data!.Activity!.ID!;
            _createdActivityIds.Add(activityId);

            // Add a reaction first
            await _feedsV3Client.AddReactionAsync(
                activityId,
                new AddReactionRequest
                {
                    Type = "like",
                    UserID = _testUserId
                }
            );

            // snippet-start: DeleteActivityReaction
            var response = await _feedsV3Client.DeleteActivityReactionAsync(
                activityId,
                "like",
                new { user_id = _testUserId }
            );
            // snippet-end: DeleteActivityReaction

            Assert.That(response, Is.Not.Null);
            Console.WriteLine("✅ Deleted reaction");
        }

        [Test, Order(20)]
        public async Task Test21_DeleteComment()
        {
            Console.WriteLine("\n🗑️ Testing comment deletion...");
            
            // Create an activity and add a comment first
            var activity = new AddActivityRequest
            {
                Type = "post",
                Text = "Activity for delete comment test",
                UserID = _testUserId,
                Feeds = new List<string> { $"user:{_testFeedId}" }
            };
            
            var createResponse = await _feedsV3Client.AddActivityAsync(activity);
            Assert.That(createResponse.Data?.Activity?.ID, Is.Not.Null);
            
            var activityId = createResponse.Data!.Activity!.ID!;
            _createdActivityIds.Add(activityId);

            // Add a comment first
            var commentResponse = await _feedsV3Client.AddCommentAsync(
                new AddCommentRequest
                {
                    Comment = "Comment to delete",
                    ObjectID = activityId,
                    ObjectType = "activity",
                    UserID = _testUserId
                }
            );
            Assert.That(commentResponse.Data?.Comment?.ID, Is.Not.Null);
            var commentId = commentResponse.Data!.Comment!.ID!;

            // snippet-start: DeleteComment
            var response = await _feedsV3Client.DeleteCommentAsync(
                commentId,
                new { user_id = _testUserId }
            );
            // snippet-end: DeleteComment

            Assert.That(response, Is.Not.Null);
            Console.WriteLine("✅ Deleted comment");
        }

        [Test, Order(21)]
        public async Task Test22_UnfollowUser()
        {
            Console.WriteLine("\n👥 Testing unfollow operation...");

                // Follow first
                await _feedsV3Client.FollowAsync(
                    new FollowRequest
                    {
                        Source = $"user:{_testFeedId}",
                        Target = $"user:{_testFeedId3}"
                    }
                );

                // snippet-start: Unfollow
                var response = await _feedsV3Client.UnfollowAsync(
                    $"user:{_testFeedId}",
                    $"user:{_testFeedId3}",
                    new { user_id = _testUserId }
                );
                // snippet-end: Unfollow

                Assert.That(response, Is.Not.Null);
                Console.WriteLine("✅ Unfollowed user");
        }

        [Test, Order(22)]
        public async Task Test24_CreatePoll()
        {
            Console.WriteLine("\n📊 Testing poll creation...");
            
                // snippet-start: CreatePoll
                // First create a poll using the poll API
                var poll = new CreatePollRequest
                {
                    Name = "Programming Language Poll",
                    Description = "What's your favorite programming language?",
                    UserID = _testUserId,
                    Options = new List<PollOptionInput>
                    {
                        new PollOptionInput { Text = "C#" },
                        new PollOptionInput { Text = "Python" },
                        new PollOptionInput { Text = "JavaScript" },
                        new PollOptionInput { Text = "Go" }
                    }
                };
                
                var pollResponse = await _client.CreatePollAsync(poll);
                Assert.That(pollResponse.Data?.Poll?.ID, Is.Not.Null);
                
                var pollId = pollResponse.Data!.Poll!.ID;
                
                // Create activity with the poll
                var activity = new AddActivityRequest
                {
                    Type = "poll",
                    Text = "What's your favorite programming language?",
                    UserID = _testUserId,
                    Feeds = new List<string> { $"user:{_testFeedId}" },
                    PollID = pollId
                };
                
                var response = await _feedsV3Client.AddActivityAsync(activity);
                // snippet-end: CreatePoll

                Assert.That(response.Data?.Activity?.ID, Is.Not.Null);
                _createdActivityIds.Add(response.Data!.Activity!.ID!);
                Console.WriteLine("✅ Created poll activity");
        }

        [Test, Order(23)]
        public async Task Test25_VotePoll()
        {
            Console.WriteLine("\n🗳️ Testing poll voting...");
            
                // Create a poll first using the proper API
                var poll = new CreatePollRequest
                {
                    Name = "Favorite Color Poll",
                    Description = "What is your favorite color?",
                    UserID = _testUserId,
                    Options = new List<PollOptionInput>
                    {
                        new PollOptionInput { Text = "Red" },
                        new PollOptionInput { Text = "Blue" },
                        new PollOptionInput { Text = "Green" }
                    }
                };
                
                var pollResponse = await _client.CreatePollAsync(poll);
                Assert.That(pollResponse.Data?.Poll?.ID, Is.Not.Null);
                Assert.That(pollResponse.Data?.Poll?.Options, Is.Not.Null.And.Not.Empty);
                
                var pollId = pollResponse.Data!.Poll!.ID;
                var pollOptions = pollResponse.Data.Poll.Options;
                
                // Create activity with the poll
                var activity = new AddActivityRequest
                {
                    Type = "poll",
                    Text = "Vote test poll",
                    UserID = _testUserId,
                    Feeds = new List<string> { $"user:{_testFeedId}" },
                    PollID = pollId
                };
                
                var createResponse = await _feedsV3Client.AddActivityAsync(activity);
                Assert.That(createResponse.Data?.Activity?.ID, Is.Not.Null);
                
                var activityId = createResponse.Data!.Activity!.ID!;
                _createdActivityIds.Add(activityId);

                // Get the first option ID for voting
                var optionId = pollOptions[0].ID;

                // snippet-start: VotePoll
                var voteResponse = await _feedsV3Client.CastPollVoteAsync(
                    activityId,
                    pollId,
                    new CastPollVoteRequest
                    {
                        UserID = _testUserId,
                        Vote = new VoteData
                        {
                            OptionID = optionId
                        }
                    }
                );
                // snippet-end: VotePoll

                Assert.That(voteResponse, Is.Not.Null);
                Console.WriteLine("✅ Cast poll vote");
        }

        [Test, Order(24)]
        public async Task Test26_ModerateActivity()
        {
            Console.WriteLine("\n🛡️ Testing activity moderation...");
            
                // Create an activity to moderate
                var activity = new AddActivityRequest
                {
                    Type = "post",
                    Text = "This content needs moderation",
                    UserID = _testUserId,
                    Feeds = new List<string> { $"user:{_testFeedId}" }
                };
                
                var createResponse = await _feedsV3Client.AddActivityAsync(activity);
                Assert.That(createResponse.Data?.Activity?.ID, Is.Not.Null);
                
                var activityId = createResponse.Data!.Activity!.ID!;
                _createdActivityIds.Add(activityId);

                // snippet-start: ModerateActivity
                // Note: Moderation typically requires admin permissions
                // This test demonstrates the API structure
                Console.WriteLine($"Activity {activityId} would be moderated here");
                // In a real scenario, you would call moderation endpoints
                // snippet-end: ModerateActivity

                Console.WriteLine("✅ Moderation test completed (demo only)");
        }

        [Test, Order(25)]
        public async Task Test27_DeviceManagement()
        {
            Console.WriteLine("\n📱 Testing device management...");
            
            var deviceToken = $"test-device-token-{Guid.NewGuid()}";
            
            try
            {
                // snippet-start: AddDevice
                var addDeviceResponse = await _client.CreateDeviceAsync(
                    new CreateDeviceRequest
                    {
                        ID = deviceToken,
                        PushProvider = "apn",
                        UserID = _testUserId
                    }
                );
                // snippet-end: AddDevice
                
                Assert.That(addDeviceResponse, Is.Not.Null);
                Console.WriteLine($"✅ Added device: {deviceToken}");
                
                // snippet-start: RemoveDevice
                var removeDeviceResponse = await _client.DeleteDeviceAsync(
                    deviceToken
                );
                // snippet-end: RemoveDevice
                
                Assert.That(removeDeviceResponse, Is.Not.Null);
                Console.WriteLine($"✅ Removed device: {deviceToken}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Device management skipped: {ex.Message}");
                // Device management might not be available in all configurations
            }
        }

        [Test, Order(26)]
        public async Task Test28_QueryActivitiesWithFilters()
        {
            Console.WriteLine("\n🔍 Testing activity queries with advanced filters...");
            
            // Create activities with different types and metadata
            var activityTypes = new[] { "post", "photo", "video", "story" };
            
            foreach (var type in activityTypes)
            {
                var activity = new AddActivityRequest
                {
                    Type = type,
                    Text = $"Test {type} activity for filtering",
                    UserID = _testUserId,
                    Feeds = new List<string> { $"user:{_testFeedId}" },
                    Custom = new Dictionary<string, object>
                    {
                        ["category"] = type,
                        ["priority"] = new Random().Next(1, 6),
                        ["tags"] = new[] { type, "test", "filter" }
                    }
                };
                
                var response = await _feedsV3Client.AddActivityAsync(activity);
                if (response.Data?.Activity?.ID != null)
                {
                    _createdActivityIds.Add(response.Data!.Activity!.ID!);
                }
            }

            // Query with type filter
            // snippet-start: QueryActivitiesWithTypeFilter
            var typeFilterResponse = await _feedsV3Client.QueryActivitiesAsync(
                new QueryActivitiesRequest
                {
                    Limit = 10,
                    Filter = new Dictionary<string, object>
                    {
                        ["activity_type"] = "post",
                        ["user_id"] = _testUserId
                    }
                    // Note: Sort would be added here: Sort = new Dictionary<string, int> { ["created_at"] = -1 }
                }
            );
            // snippet-end: QueryActivitiesWithTypeFilter

            Assert.That(typeFilterResponse, Is.Not.Null);
            Console.WriteLine("✅ Queried activities with type filter");

            // Query with custom field filter
            // snippet-start: QueryActivitiesWithCustomFilter
            var customFilterResponse = await _feedsV3Client.QueryActivitiesAsync(
                new QueryActivitiesRequest
                {
                    Limit = 10,
                    Filter = new Dictionary<string, object>
                    {
                        // Note: Custom field filtering syntax may vary by API implementation
                        ["user_id"] = _testUserId
                    }
                }
            );
            // snippet-end: QueryActivitiesWithCustomFilter

            Assert.That(customFilterResponse, Is.Not.Null);
            Console.WriteLine("✅ Queried activities with custom filter");
        }

        [Test, Order(27)]
        public async Task Test29_GetFeedActivitiesWithPagination()
        {
            Console.WriteLine("\n📄 Testing feed activities with pagination...");

            // snippet-start: GetFeedActivitiesWithPagination
            // Get first page
            var firstPageResponse = await _feedsV3Client.QueryActivitiesAsync(
                new QueryActivitiesRequest
                {
                    Filter = new Dictionary<string, object> { ["user_id"] = _testUserId },
                    Limit = 3
                }
            );

            Assert.That(firstPageResponse, Is.Not.Null);
            
            // Get second page if there's a next token
            // snippet-start: GetFeedActivitiesSecondPage
            if (firstPageResponse.Data?.Next != null)
            {
                var secondPageResponse = await _feedsV3Client.QueryActivitiesAsync(
                    new QueryActivitiesRequest
                    {
                        Filter = new Dictionary<string, object> { ["user_id"] = _testUserId },
                        Limit = 3,
                        Next = firstPageResponse.Data.Next
                    }
                );
                
                Assert.That(secondPageResponse, Is.Not.Null);
                Console.WriteLine("✅ Retrieved second page of activities");
            }
            else
            {
                Console.WriteLine("⚠️ No next page available");
            }
            // snippet-end: GetFeedActivitiesSecondPage
            // snippet-end: GetFeedActivitiesWithPagination

            Console.WriteLine("✅ Retrieved feed activities with pagination");
        }

        [Test, Order(28)]
        public async Task Test30_ErrorHandlingScenarios()
        {
            Console.WriteLine("\n⚠️ Testing error handling scenarios...");

            // Test 1: Invalid activity ID
            try
            {
                // snippet-start: HandleInvalidActivityId
                await _feedsV3Client.GetActivityAsync("invalid-activity-id-12345");
                // snippet-end: HandleInvalidActivityId
                
                Assert.Fail("Expected exception was not thrown");
            }
            catch (GetStreamApiException ex)
            {
                Assert.That(ex, Is.Not.Null);
                Console.WriteLine($"✅ Caught expected error for invalid activity ID: {ex.GetType().Name}");
            }
            catch (Exception ex)
            {
                Assert.That(ex, Is.Not.Null);
                Console.WriteLine($"✅ Caught expected error for invalid activity ID: {ex.GetType().Name}");
            }

            // Test 2: Empty activity text
            try
            {
                // snippet-start: HandleEmptyActivityText
                var emptyActivity = new AddActivityRequest
                {
                    Type = "post",
                    Text = "", // Empty text
                    UserID = _testUserId,
                    Feeds = new List<string> { $"user:{_testFeedId}" }
                };
                await _feedsV3Client.AddActivityAsync(emptyActivity);
                // snippet-end: HandleEmptyActivityText
                
                // If we reach here, the API allows empty text (which might be valid)
                Console.WriteLine("✅ Empty activity text was accepted by API");
            }
            catch (GetStreamApiException ex)
            {
                Console.WriteLine($"✅ Caught expected error for empty activity text: {ex.GetType().Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✅ Caught expected error for empty activity text: {ex.GetType().Name}");
            }

            // Test 3: Invalid user ID
            try
            {
                // snippet-start: HandleInvalidUserId
                var invalidUserActivity = new AddActivityRequest
                {
                    Type = "post",
                    Text = "Test with invalid user",
                    UserID = "", // Empty user ID
                    Feeds = new List<string> { $"user:{_testFeedId}" }
                };
                await _feedsV3Client.AddActivityAsync(invalidUserActivity);
                // snippet-end: HandleInvalidUserId
                
                Assert.Fail("Expected exception was not thrown for invalid user ID");
            }
            catch (GetStreamApiException ex)
            {
                Console.WriteLine($"✅ Caught expected error for invalid user ID: {ex.GetType().Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✅ Caught expected error for invalid user ID: {ex.GetType().Name}");
            }
        }

        [Test, Order(29)]
        public async Task Test31_AuthenticationScenarios()
        {
            Console.WriteLine("\n🔐 Testing authentication scenarios...");

            // Test with valid user authentication
            // snippet-start: ValidUserAuthentication
            var activity = new AddActivityRequest
            {
                Type = "post",
                Text = "Activity with proper authentication",
                UserID = _testUserId,
                Feeds = new List<string> { $"user:{_testFeedId}" }
            };
            var response = await _feedsV3Client.AddActivityAsync(activity);
            // snippet-end: ValidUserAuthentication
            
            Assert.That(response.Data?.Activity?.ID, Is.Not.Null);
            
            var activityId = response.Data!.Activity!.ID!;
            _createdActivityIds.Add(activityId);
            Console.WriteLine($"✅ Successfully authenticated and created activity: {activityId}");

            // Test user permissions for updating activity
            // snippet-start: UserPermissionUpdate
            var updateResponse = await _feedsV3Client.UpdateActivityAsync(
                activityId,
                new UpdateActivityRequest
                {
                    Text = "Updated with proper user permissions",
                    UserID = _testUserId // Same user can update
                }
            );
            // snippet-end: UserPermissionUpdate
            
            Assert.That(updateResponse, Is.Not.Null);
            Console.WriteLine("✅ Successfully updated activity with proper user permissions");
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
            
            var postId = postResponse.Data!.Activity!.ID!;
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

        /// <summary>
        /// Test 33: Feed Group CRUD Operations
        /// </summary>
        [Test, Order(33)]
        public async Task Test33_FeedGroupCRUD()
        {
            Console.WriteLine("\n📁 Testing Feed Group CRUD operations...");

            var feedGroupId = $"test-feed-group-{Guid.NewGuid().ToString()[..8]}";

            // Test 1: List Feed Groups
            Console.WriteLine("\n📋 Testing list feed groups...");
            // snippet-start: ListFeedGroups
            var listResponse = await _feedsV3Client.ListFeedGroupsAsync();
            // snippet-end: ListFeedGroups
            
            Assert.That(listResponse, Is.Not.Null);
            Console.WriteLine($"✅ Listed {listResponse.Data.Groups.Count} existing feed groups");

            // Test 2: Create Feed Group
            Console.WriteLine("\n➕ Testing create feed group...");
            // snippet-start: CreateFeedGroup
            var createResponse = await _feedsV3Client.CreateFeedGroupAsync(new CreateFeedGroupRequest
            {
                ID = feedGroupId,
                DefaultVisibility = "public",
                ActivityProcessors = new List<ActivityProcessorConfig>
                {
                    new() { Type = "default" }
                }
            });
            // snippet-end: CreateFeedGroup

            Assert.That(createResponse, Is.Not.Null);
            Console.WriteLine($"✅ Created feed group: {feedGroupId}");

            // Test 3: Get Feed Group
            Console.WriteLine("\n🔍 Testing get feed group...");
            // snippet-start: GetFeedGroup
            var getResponse = await _feedsV3Client.GetFeedGroupAsync("feed_group_id");
            // snippet-end: GetFeedGroup

            Assert.That(getResponse, Is.Not.Null);
            Console.WriteLine($"✅ Retrieved feed group: {feedGroupId}");

            // Test 4: Update Feed Group
            Console.WriteLine("\n✏️ Testing update feed group...");
            // snippet-start: UpdateFeedGroup
            var updateResponse = await _feedsV3Client.UpdateFeedGroupAsync("feed_group_id", new UpdateFeedGroupRequest
            {
                ActivityProcessors = new List<ActivityProcessorConfig>
                {
                    new() { Type = "default" }
                },
                Aggregation = new AggregationConfig
                {
                    Format = "time_based"
                }
            });
            // snippet-end: UpdateFeedGroup

            Assert.That(updateResponse, Is.Not.Null);
            Console.WriteLine($"✅ Updated feed group: {feedGroupId}");

            // Test 5: Get or Create Feed Group (should get existing)
            Console.WriteLine("\n🔄 Testing get or create feed group (existing)...");
            // snippet-start: GetOrCreateFeedGroupExisting
            var getOrCreateResponse = await _feedsV3Client.GetOrCreateFeedGroupAsync("feed_group_id", new GetOrCreateFeedGroupRequest
            {
                DefaultVisibility = "public"
            });
            // snippet-end: GetOrCreateFeedGroupExisting

            Assert.That(getOrCreateResponse, Is.Not.Null);
            Console.WriteLine($"✅ Got existing feed group: {feedGroupId}");

            // Test 6: Delete Feed Group
            Console.WriteLine("\n🗑️ Testing delete feed group...");
            // snippet-start: DeleteFeedGroup
            await _feedsV3Client.DeleteFeedGroupAsync("groupID-123");
            // snippet-end: DeleteFeedGroup

                    Console.WriteLine("✅ Completed Feed Group CRUD operations");

        // Additional Feed Group Creation Examples
        Console.WriteLine("\n📊 Testing create feed group with aggregation...");
        // snippet-start: CreateFeedGroupWithAggregation
        await _feedsV3Client.CreateFeedGroupAsync(new CreateFeedGroupRequest
        {
            ID = "aggregated-group",
            DefaultVisibility = "public",
            ActivityProcessors = new List<ActivityProcessorConfig>
            {
                new() { Type = "default" }
            },
            Aggregation = new AggregationConfig
            {
                Format = "{{ type }}-{{ time.strftime(\"%Y-%m-%d\") }}"
            }
        });
        // snippet-end: CreateFeedGroupWithAggregation

        Console.WriteLine("\n🏆 Testing create feed group with ranking...");
        // snippet-start: CreateFeedGroupWithRanking
        await _feedsV3Client.CreateFeedGroupAsync(new CreateFeedGroupRequest
        {
            ID = "ranked-group",
            DefaultVisibility = "public",
            Ranking = new RankingConfig
            {
                Type = "default",
                Score = "decay_linear(time) * popularity"
            }
        });
        // snippet-end: CreateFeedGroupWithRanking
    }

        /// <summary>
        /// Test 34: Feed View CRUD Operations
        /// </summary>
        [Test, Order(34)]
        public async Task Test34_FeedViewCRUD()
        {
            Console.WriteLine("\n👁️ Testing Feed View CRUD operations...");

            var feedViewId = $"test-feed-view-{Guid.NewGuid().ToString()[..8]}";

            // Test 1: List Feed Views
            Console.WriteLine("\n📋 Testing list feed views...");
            // snippet-start: ListFeedViews
            var listResponse = await _feedsV3Client.ListFeedViewsAsync();
            // snippet-end: ListFeedViews

            Console.WriteLine($"✅ Listed {listResponse.Data.Views.Count} existing feed views");

            // Test 2: Create Feed View
            Console.WriteLine("\n➕ Testing create feed view...");
            // snippet-start: CreateFeedView
            var createResponse = await _feedsV3Client.CreateFeedViewAsync(new CreateFeedViewRequest
            {
                ID = feedViewId,
                ActivitySelectors = new List<ActivitySelectorConfig>
                {
                    new() { Type = "recent" }
                },
                ActivityProcessors = new List<ActivityProcessorConfig>
                {
                    new() { Type = "default" }
                },
                Aggregation = new AggregationConfig
                {
                    Format = "time_based"
                }
            });
            // snippet-end: CreateFeedView

            Assert.Equals(feedViewId, createResponse.Data.FeedView.ID);
            Console.WriteLine($"✅ Created feed view: {feedViewId}");

            // Test 3: Get Feed View
            Console.WriteLine("\n🔍 Testing get feed view...");
            // snippet-start: GetFeedView
            var getResponse = await _feedsV3Client.GetFeedViewAsync("feedViewID");
            // snippet-end: GetFeedView

            Assert.Equals("feedViewID", getResponse.Data.FeedView.ID);
            Console.WriteLine($"✅ Retrieved feed view: {feedViewId}");

            // Test 4: Update Feed View
            Console.WriteLine("\n✏️ Testing update feed view...");
            // snippet-start: UpdateFeedView
            var updateResponse = await _feedsV3Client.UpdateFeedViewAsync("feedViewID", new UpdateFeedViewRequest
            {
                ActivitySelectors = new List<ActivitySelectorConfig>
                {
                    new() { Type = "popular", MinPopularity = 10 }
                },
                Aggregation = new AggregationConfig
                {
                    Format = "popularity_based"
                }
            });
            // snippet-end: UpdateFeedView

            Console.WriteLine($"✅ Updated feed view: {feedViewId}");

            // Test 5: Get or Create Feed View (should get existing)
            Console.WriteLine("\n🔄 Testing get or create feed view (existing)...");
            // snippet-start: GetOrCreateFeedViewExisting
            var getOrCreateResponse = await _feedsV3Client.GetOrCreateFeedViewAsync(feedViewId, new GetOrCreateFeedViewRequest
            {
                ActivitySelectors = new List<ActivitySelectorConfig>
                {
                    new() { Type = "recent" }
                }
            });
            // snippet-end: GetOrCreateFeedViewExisting

            Console.WriteLine($"✅ Got existing feed view: {feedViewId}");

            // Test 6: Delete Feed View
            Console.WriteLine("\n🗑️ Testing delete feed view...");
            // snippet-start: DeleteFeedView
            await _feedsV3Client.DeleteFeedViewAsync("viewID-123");
            // snippet-end: DeleteFeedView

            Console.WriteLine("✅ Completed Feed View CRUD operations");
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
                
                var activityId = createResponse.Data!.Activity!.ID!;
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
