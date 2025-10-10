using System;
using System.Threading.Tasks;
using System.Linq;
using GetStream;
using GetStream.Models;
using System.Collections.Generic;
using NUnit.Framework;
using FluentAssertions;

namespace GetStream.Tests
{
    [TestFixture]
    public class FeedTests1 : TestBase
    {
        [Test]
        public async Task CreateAndGetFeed_ShouldSucceed()
        {
            var userRequest = new UpdateUsersRequest
            {
                Users = new Dictionary<string, UserRequest>
                {
                    {
                        "sara", new UserRequest
                        {
                            ID = "sara",
                            Name = "Sara Connor",
                        }
                    }
                }
            };
            
            var res=await StreamClient.UpdateUsersAsync(userRequest);
            
            // Create a test feed
            var feed = FeedsV3Client.Feed("user", "test-feed-1");
            var createResponse = await feed.GetOrCreateFeedAsync(
                request: new GetOrCreateFeedRequest
                {
                    UserID = "sara"
                }
            );

            // Verify creation was successful
            createResponse.Data.Should().NotBeNull();

            // Get the same feed
            var getResponse = await FeedsV3Client.GetOrCreateFeedAsync("user","test-feed-1",
                request: new GetOrCreateFeedRequest
                {
                    UserID = "sara"
                }
            );

            // Verify retrieval was successful
            getResponse.Data.Should().NotBeNull();

            // Verify both responses are the same (same feed)
            getResponse.Data.Should().NotBeNull();
            createResponse.Data.Should().NotBeNull();
            getResponse.Data!.ToString().Should().Be(createResponse.Data!.ToString());
        }

        [Test]
        public async Task AddActivityAndVerify_ShouldSucceed()
        {
            // Add an activity to the feed
            var addActivityResponse = await FeedsV3Client.AddActivityAsync(
                new AddActivityRequest
                {
                    Type = "post",
                    Feeds = new List<string> { "user:test-feed-1" },
                    Text = "This is a test activity for verification",
                    UserID = "sara"
                }
            );
        
            // Verify activity was added successfully
            addActivityResponse.Data.Should().NotBeNull();
            addActivityResponse.Data!.Activity.Should().NotBeNull();
            var activity = addActivityResponse.Data.Activity!;
            activity.Type.Should().Be("post");
            activity.Text.Should().Be("This is a test activity for verification");
            activity.User.Should().NotBeNull();
            activity.User!.ID.Should().Be("sara");
        }
        
        [Test]
        public async Task AddCommentAndVerify_ShouldSucceed()
        {
            // First, get the activity we just created
            var queryResponse = await FeedsV3Client.QueryActivitiesAsync(
                new QueryActivitiesRequest
                {
                    Limit = 1
                }
            );
        
            queryResponse.Data.Should().NotBeNull();
            queryResponse.Data!.Activities.Should().NotBeNull();
            queryResponse.Data!.Activities.Should().NotBeEmpty();
        
            queryResponse.Data.Should().NotBeNull();
            queryResponse.Data!.Activities.Should().NotBeNull();
            var activities = queryResponse.Data.Activities!;
            activities.Should().NotBeEmpty();
            var firstActivity = activities.First();
            firstActivity.Should().NotBeNull();
            var activityId = firstActivity!.ID;
        
            // Add a comment to the activity
            var addCommentResponse = await FeedsV3Client.AddActivityAsync(
                new AddActivityRequest
                {
                    Type = "comment",
                    Feeds = new List<string> { "user:test-feed-1" },
                    Text = "This is a test comment",
                    UserID = "sara"
                }
            );
        
            // Verify comment was added successfully
            addCommentResponse.Data.Should().NotBeNull();
            addCommentResponse.Data!.Activity.Should().NotBeNull();
        
            addCommentResponse.Data.Should().NotBeNull();
            addCommentResponse.Data!.Activity.Should().NotBeNull();
            var comment = addCommentResponse.Data.Activity!;
            comment.Type.Should().Be("comment");
            comment.Text.Should().Be("This is a test comment");
            comment.User.Should().NotBeNull();
            comment.User!.ID.Should().Be("sara");
        }
        
        [Test]
        public async Task GetFeedActivities_ShouldSucceed()
        {
            // Get all activities from the feed
            var queryResponse = await FeedsV3Client.QueryActivitiesAsync(
                new QueryActivitiesRequest
                {
                    Limit = 10
                }
            );
        
            // Verify we got activities
            queryResponse.Data.Should().NotBeNull();
            queryResponse.Data!.Activities.Should().NotBeNull();
        
            queryResponse.Data.Should().NotBeNull();
            queryResponse.Data!.Activities.Should().NotBeNull();
            var activities = queryResponse.Data.Activities!.ToList();
            activities.Should().NotBeEmpty();
        
            // Verify that we can find our test activities in the results
            var testActivities = activities.Where(a => 
                a?.Text == "This is a test activity for verification" || 
                a?.Text == "This is a test comment"
            ).ToList();
        
            testActivities.Should().HaveCountGreaterThanOrEqualTo(2);
        }
        
        [Test]
        public async Task MultipleFeeds_ShouldSucceed()
        {
            // Create multiple feeds
            var feedIds = new[] { "test-feed-2", "test-feed-3", "test-feed-4" };
            var createdFeeds = new List<string>();
        
            foreach (var feedId in feedIds)
            {
                var response = await FeedsV3Client.GetOrCreateFeedAsync("user", feedId,
                    request: new GetOrCreateFeedRequest
                    {
                        UserID = "sara"
                    }
                );
        
                response.Data.Should().NotBeNull();
                response.Data.Should().NotBeNull();
                var data = response.Data!;
                data.Should().NotBeNull();
                createdFeeds.Add(data.ToString() ?? "");
            }
        
            // Verify we can retrieve all feeds
            foreach (var feedId in feedIds)
            {
                var response = await FeedsV3Client.GetOrCreateFeedAsync("user", feedId,
                    request: new GetOrCreateFeedRequest
                    {
                        UserID = "sara"
                    }
                );
        
                response.Data.Should().NotBeNull();
            }
        }
    }
} 