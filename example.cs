using GetStream;
using GetStream.Models;

/// <summary>
/// Stream .NET SDK Test using getstream-net v1.5.0
/// Tests basic functionality and activity creation
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🔧 Setting up test environment...");

        // API credentials
        const string apiKey = "XXXXXX";
        const string apiSecret = "XXXXXX";

        try
        {
            // Initialize Stream client using getstream-net v1.5.0
            var builder = new ClientBuilder()
                .ApiKey(apiKey)
                .ApiSecret(apiSecret);

            var client = builder.Build();
            var feedsClient = builder.BuildFeedsClient();
            Console.WriteLine("✅ Initialized Stream .NET client");

            // Create a test user
            var testUserId = $"test-user-{Guid.NewGuid().ToString("N")[..8]}";
            Console.WriteLine($"✅ Test user ID: {testUserId}");

            // Create test user
            await CreateUser(client, testUserId);

            // Create user feed
            var testFeedId = await CreateFeed(feedsClient, testUserId);

            // Add an activity
            var activityId = await AddActivity(feedsClient, testUserId, testFeedId);

            // Query activities
            await QueryActivities(feedsClient, testUserId);

            // Get single activity
            await GetSingleActivity(feedsClient, activityId);

            Console.WriteLine("✅ Test completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner Exception: {ex.InnerException.Message}");
            }
            Environment.Exit(1);
        }
    }

    static async Task CreateUser(StreamClient client, string userId)
    {
        var updateUsersRequest = new UpdateUsersRequest
        {
            Users = new Dictionary<string, UserRequest>
            {
                [userId] = new UserRequest
                {
                    ID = userId,
                    Name = "Test User",
                    Role = "user"
                }
            }
        };

        await client.UpdateUsersAsync(updateUsersRequest);
        Console.WriteLine("✅ Created test user");
    }

    static async Task<string> CreateFeed(FeedsV3Client feedsClient, string userId)
    {
        var feedId = $"user-{userId}";
        var feedRequest = new GetOrCreateFeedRequest 
        { 
            UserID = userId 
        };

        var feedResponse = await feedsClient.GetOrCreateFeedAsync(
            "user",
            feedId,
            feedRequest
        );

        var createdFeedId = feedResponse.Data?.Feed?.Feed ?? feedId;
        Console.WriteLine($"✅ Created user feed: {createdFeedId}");
        return createdFeedId;
    }

    static async Task<string> AddActivity(FeedsV3Client feedsClient, string userId, string feedId)
    {
        var activity = new AddActivityRequest
        {
            Type = "post",
            Text = "This is a test activity from .NET SDK v1.5.0",
            UserID = userId,
            Feeds = new List<string> { feedId }
        };

        var activityResponse = await feedsClient.AddActivityAsync(activity);
        var activityId = activityResponse.Data?.Activity?.ID ?? "unknown";
        Console.WriteLine($"✅ Created activity with ID: {activityId}");
        return activityId;
    }

    static async Task QueryActivities(FeedsV3Client feedsClient, string userId)
    {
        var filter = new Dictionary<string, object>
        {
            ["user_id"] = userId
        };

        var queryRequest = new QueryActivitiesRequest
        {
            Filter = filter,
            Limit = 10
        };

        var queryResponse = await feedsClient.QueryActivitiesAsync(queryRequest);
        var activities = queryResponse.Data?.Activities ?? new List<ActivityResponse>();
        
        Console.WriteLine($"✅ Retrieved {activities.Count} activities:");
        foreach (var activity in activities)
        {
            Console.WriteLine($"   - ID: {activity.ID}, Type: {activity.Type}, Text: {activity.Text}");
        }
    }

    static async Task GetSingleActivity(FeedsV3Client feedsClient, string activityId)
    {
        try
        {
            var getActivityResponse = await feedsClient.GetActivityAsync(activityId);
            var activity = getActivityResponse.Data?.Activity;
            
            if (activity != null)
            {
                Console.WriteLine($"✅ Retrieved single activity: {activity.Text}");
            }
            else
            {
                Console.WriteLine("⚠️ Activity not found or empty response");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Failed to retrieve single activity: {ex.Message}");
        }
    }
}
