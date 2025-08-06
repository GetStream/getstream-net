using System;
using System.Threading.Tasks;
using System.Linq;
using GetStream;
using GetStream.Requests;
using GetStream.Models;
using System.Collections.Generic;

namespace GetStream.Tests
{
    public class FeedTests2
    {
        private Client _client;
        private FeedsV3Client _feeds;

        public FeedTests2()
        {
            // Initialize with placeholder credentials - will be updated by UpdateCredentials
            _client = new Client(
                apiKey: "placeholder",
                apiSecret: "placeholder"
            );
            _feeds = new FeedsV3Client(_client);
        }

        public void UpdateCredentials(string apiKey, string apiSecret, string appId)
        {
            _client = new Client(apiKey, apiSecret);
            _feeds = new FeedsV3Client(_client);
        }

        public async Task RunAllTests()
        {
            Console.WriteLine("🧪 Starting Feed Tests...\n");

            try
            {
                await TestCreateAndGetFeed();
                await TestAddActivityAndVerify();
                await TestAddCommentAndVerify();
                await TestGetFeedActivities();
                await TestMultipleFeeds();
                
                Console.WriteLine("✅ All tests passed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test failed: {ex.Message}");
                throw;
            }
        }

        private async Task TestCreateAndGetFeed()
        {
            Console.WriteLine("📝 Test 1: Create and Get Feed");
            
            // Create a test feed using existing user
            var createResponse = await _feeds.GetOrCreateFeedAsync(
                FeedGroupID: "user",
                FeedID: "test-feed-1",
                request: new GetOrCreateFeedRequest
                {
                    UserID = "sara"
                }
            );

            // Verify creation was successful
            if (createResponse.Data == null)
                throw new Exception("Feed creation failed - no data returned");

            Console.WriteLine($"✅ Feed created: {createResponse.Data}");

            // Get the same feed
            var getResponse = await _feeds.GetOrCreateFeedAsync(
                FeedGroupID: "user",
                FeedID: "test-feed-1",
                request: new GetOrCreateFeedRequest
                {
                    UserID = "sara"
                }
            );

            // Verify retrieval was successful
            if (getResponse.Data == null)
                throw new Exception("Feed retrieval failed - no data returned");

            Console.WriteLine($"✅ Feed retrieved: {getResponse.Data}");

            // Verify both responses are the same (same feed)
            if (createResponse.Data.ToString() != getResponse.Data.ToString())
                throw new Exception("Created and retrieved feeds don't match");

            Console.WriteLine("✅ Feed creation and retrieval test passed\n");
        }

        private async Task TestAddActivityAndVerify()
        {
            Console.WriteLine("📝 Test 2: Add Activity and Verify");

            // Add an activity to the feed
            var addActivityResponse = await _feeds.AddActivityAsync(
                new AddActivityRequest
                {
                    Type = "post",
                    Feeds = new List<string> { "user:test-feed-1" },
                    Text = "This is a test activity for verification",
                    UserID = "sara"
                }
            );

            // Verify activity was added successfully
            if (addActivityResponse.Data?.Activity == null)
                throw new Exception("Activity addition failed - no activity data returned");

            var activityId = addActivityResponse.Data.Activity.ID;
            Console.WriteLine($"✅ Activity added with ID: {activityId}");

            // Verify activity properties
            var activity = addActivityResponse.Data.Activity;
            if (activity.Type != "post")
                throw new Exception($"Activity type mismatch. Expected: post, Got: {activity.Type}");

            if (activity.Text != "This is a test activity for verification")
                throw new Exception($"Activity text mismatch. Expected: 'This is a test activity for verification', Got: '{activity.Text}'");

            if (activity.User?.ID != "sara")
                throw new Exception($"Activity user mismatch. Expected: sara, Got: {activity.User?.ID}");

            Console.WriteLine("✅ Activity verification test passed\n");
        }

        private async Task TestAddCommentAndVerify()
        {
            Console.WriteLine("📝 Test 3: Add Comment and Verify");

            // First, get the activity we just created
            var queryResponse = await _feeds.QueryActivitiesAsync(
                new QueryActivitiesRequest
                {
                    Limit = 1
                }
            );

            if (queryResponse.Data?.Activities == null || !queryResponse.Data.Activities.Any())
                throw new Exception("No activities found to add comment to");

            var activityId = queryResponse.Data.Activities.First().ID;

            // Add a comment to the activity
            var addCommentResponse = await _feeds.AddActivityAsync(
                new AddActivityRequest
                {
                    Type = "comment",
                    Feeds = new List<string> { "user:test-feed-1" },
                    Text = "This is a test comment",
                    UserID = "sara"
                }
            );

            // Verify comment was added successfully
            if (addCommentResponse.Data?.Activity == null)
                throw new Exception("Comment addition failed - no activity data returned");

            var commentId = addCommentResponse.Data.Activity.ID;
            Console.WriteLine($"✅ Comment added with ID: {commentId}");

            // Verify comment properties
            var comment = addCommentResponse.Data.Activity;
            if (comment.Type != "comment")
                throw new Exception($"Comment type mismatch. Expected: comment, Got: {comment.Type}");

            if (comment.Text != "This is a test comment")
                throw new Exception($"Comment text mismatch. Expected: 'This is a test comment', Got: '{comment.Text}'");

            if (comment.User?.ID != "sara")
                throw new Exception($"Comment user mismatch. Expected: sara, Got: {comment.User?.ID}");

            Console.WriteLine("✅ Comment verification test passed\n");
        }

        private async Task TestGetFeedActivities()
        {
            Console.WriteLine("📝 Test 4: Get Feed Activities and Verify");

            // Get all activities from the feed
            var queryResponse = await _feeds.QueryActivitiesAsync(
                new QueryActivitiesRequest
                {
                    Limit = 10
                }
            );

            // Verify we got activities
            if (queryResponse.Data?.Activities == null)
                throw new Exception("No activities data returned");

            var activities = queryResponse.Data.Activities.ToList();
            Console.WriteLine($"✅ Retrieved {activities.Count} activities from feed");

            // Verify we have at least some activities
            if (activities.Count == 0)
                throw new Exception($"Expected at least 1 activity, but got {activities.Count}");

            // Verify that we can find our test activities in the results
            var testActivities = activities.Where(a => 
                a.Text == "This is a test activity for verification" || 
                a.Text == "This is a test comment"
            ).ToList();

            if (testActivities.Count < 2)
                throw new Exception($"Expected to find our test activities, but only found {testActivities.Count}");

            Console.WriteLine($"✅ Found {testActivities.Count} test activities in feed");
            Console.WriteLine("✅ Feed activities verification test passed\n");
        }

        private async Task TestMultipleFeeds()
        {
            Console.WriteLine("📝 Test 5: Multiple Feeds Test");

            // Create multiple feeds
            var feedIds = new[] { "test-feed-2", "test-feed-3", "test-feed-4" };
            var createdFeeds = new List<string>();

            foreach (var feedId in feedIds)
            {
                var response = await _feeds.GetOrCreateFeedAsync(
                    FeedGroupID: "user",
                    FeedID: feedId,
                    request: new GetOrCreateFeedRequest
                    {
                        UserID = "sara"
                    }
                );

                if (response.Data == null)
                    throw new Exception($"Failed to create feed {feedId}");

                createdFeeds.Add(response.Data.ToString());
                Console.WriteLine($"✅ Created feed: {feedId}");
            }

            // Verify we can retrieve all feeds
            foreach (var feedId in feedIds)
            {
                var response = await _feeds.GetOrCreateFeedAsync(
                    FeedGroupID: "user",
                    FeedID: feedId,
                    request: new GetOrCreateFeedRequest
                    {
                        UserID = "sara"
                    }
                );

                if (response.Data == null)
                    throw new Exception($"Failed to retrieve feed {feedId}");

                Console.WriteLine($"✅ Retrieved feed: {feedId}");
            }

            Console.WriteLine("✅ Multiple feeds test passed\n");
        }
    }
} 