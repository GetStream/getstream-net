using System;
using System.Threading.Tasks;
using GetStream;
using GetStream.Models;
using GetStream.Requests;
using System.Collections.Generic;

namespace GetStream.Example
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== GetStream .NET SDK Complete Example ===\n");

            Console.WriteLine("🔧 Initializing clients...");

            // Clients automatically load credentials from .env file or environment variables
            var feedsClient = new FeedsV3Client();
            var client = new StreamClient();
            
            Console.WriteLine("✅ Successfully initialized clients with automatic credential loading!");
            var feed = feedsClient.Feed("user", "example-feed-2");
            try
            {
                // 0. Create a user
                Console.WriteLine("0. Creating user...");
                var userRes = await client.UpdateUsersAsync(
                    new UpdateUsersRequest
                    {
                        Users = new Dictionary<string, UserRequest> 
                        { 
                            { 
                                "okabe", // Changed from "sara" to match the user ID
                                new UserRequest 
                                { 
                                    ID = "okabe",
                                    Name = "Okabe",
                                    Custom = new Dictionary<string, object>
                                    {
                                        { "occupation", "Scientist" }
                                    }
                                } 
                            } 
                        }
                    }
                );

                
                // 1. Create a feed
                Console.WriteLine("1. Creating feed...");
                var feedRes = await feed.GetOrCreateFeedAsync(
                    request: new GetOrCreateFeedRequest
                    {
                        UserID = userRes.Data?.Users.FirstOrDefault().Value.ID
                    }
                );
                Console.WriteLine($"✅ Feed created successfully: {feedRes.Data.Feed.Feed}\n");

                // 2. Add an activity to the feed
                Console.WriteLine("2. Adding activity to feed...");
                var addActivityResponse = await feedsClient.AddActivityAsync(
                    new AddActivityRequest
                    {
                        Type = "post",
                        Feeds = new List<string> { feedRes.Data.Feed.Feed },
                        Text = "Hello from .NET SDK! This is my first activity.",
                        UserID = "okabe"
                    }
                );
                Console.WriteLine("✅ Activity added successfully!");
                Console.WriteLine($"   Activity ID: {addActivityResponse.Data?.Activity?.ID}");
                Console.WriteLine($"   Activity Type: {addActivityResponse.Data?.Activity?.Type}");
                Console.WriteLine($"   Activity Text: {addActivityResponse.Data?.Activity?.Text}\n");

                // 3. Add a comment to the activity
                Console.WriteLine("3. Adding comment to activity...");
                var addCommentResponse = await feedsClient.AddActivityAsync(
                    new AddActivityRequest
                    {
                        Type = "comment",
                        Feeds = new List<string> { feedRes.Data.Feed.Feed },
                        Text = "This is a great post!",
                        UserID = "okabe"
                    }
                );
                Console.WriteLine("✅ Comment added successfully!");
                Console.WriteLine($"   Comment ID: {addCommentResponse.Data?.Activity?.ID}");
                Console.WriteLine($"   Comment Text: {addCommentResponse.Data?.Activity?.Text}\n");

                // 4. Fetch feed activities
                Console.WriteLine("4. Fetching feed activities...");
                var queryResponse = await feedsClient.QueryActivitiesAsync(
                    new QueryActivitiesRequest
                    {
                        Limit = 10
                    }
                );
                Console.WriteLine("✅ Feed fetched successfully!");
                Console.WriteLine($"   Total activities: {queryResponse.Data?.Activities?.Count}");
                foreach (var activity in queryResponse.Data?.Activities ?? new List<ActivityResponse>())
                {
                    Console.WriteLine($"   - Activity: {activity.Type} - {activity.Text}");
                }
                Console.WriteLine();

                // 5. Get the feed we created
                Console.WriteLine("5. Getting the feed we created...");
                var getResponse = await feed.GetOrCreateFeedAsync(
                    request: new GetOrCreateFeedRequest
                    {
                        UserID = "okabe"
                    }
                );
                Console.WriteLine("✅ Feed retrieved successfully!");
                Console.WriteLine($"   Feed Data: {getResponse.Data}\n");

                Console.WriteLine("=== Example completed ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                throw;
            }
        }
    }
} 