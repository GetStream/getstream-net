using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GetStream;
using GetStream.Requests;
using GetStream.Models;
using GetStream.Tests;

namespace GetStreamExample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Read API credentials from environment variables
            var apiKey = Environment.GetEnvironmentVariable("STREAM_API_KEY");
            var apiSecret = Environment.GetEnvironmentVariable("STREAM_API_SECRET");
            var appId = Environment.GetEnvironmentVariable("GETSTREAM_APP_ID");

            // Validate that required environment variables are set
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new GetStreamAuthenticationException("STREAM_API_KEY environment variable is not set. Please set it with: export STREAM_API_KEY=your-api-key");
            }

            if (string.IsNullOrEmpty(apiSecret))
            {
                throw new GetStreamAuthenticationException("STREAM_API_SECRET environment variable is not set. Please set it with: export STREAM_API_SECRET=your-api-secret");
            }

            // Check if user wants to run tests
            if (args.Length > 0 && args[0].ToLower() == "test")
            {
                await TestRunner.RunTests(apiKey, apiSecret, appId ?? "default-app-id");
                return;
            }

            // Initialize the client with API credentials from environment
            var client = new Client(apiKey, apiSecret);

            // Create the FeedClient
            var feeds = new FeedClient(client);

            try
            {
                Console.WriteLine("=== GetStream .NET SDK Complete Example ===");
                
                
                // Step 1: Create a feed
                Console.WriteLine("\n1. Creating feed...");
                var createFeedRequest = new GetOrCreateFeedRequest
                {
                    UserID = "sara",
                    Data = new FeedInput
                    { 
                        Visibility = "public",
                        Custom = new { 
                            benchmark = true,
                            description = "Benchmark test feed for .NET SDK"
                        }
                    },
                    // Watch = true
                };
                
                var createFeedResponse = await feeds.GetOrCreateFeedAsync(
                    FeedGroupID: "user", 
                    FeedID: "john", 
                    request: createFeedRequest
                );
                
                if (createFeedResponse.Data != null)
                {
                    Console.WriteLine($"✅ Feed created successfully: {createFeedResponse.Data}");
                }
                else
                {
                    Console.WriteLine("⚠️ Feed creation response was null");
                    Console.WriteLine(createFeedResponse.Error);
                }

                // Step 2: Add an activity to the feed
                Console.WriteLine("\n2. Adding activity to feed...");
                var addActivityRequest = new AddActivityRequest
                {
                    UserID = "sara",
                    Type = "post",
                    Fids = new List<string> { "user:john" },
                    Text = "Hello from .NET SDK! This is my first activity.",
                    Custom = new { 
                        message = "This is a custom message",
                        timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    }
                };
                
                var addActivityResponse = await feeds.AddActivityAsync(addActivityRequest);
                
                if (addActivityResponse.Data?.Activity != null)
                {
                    Console.WriteLine($"✅ Activity added successfully!");
                    Console.WriteLine($"   Activity ID: {addActivityResponse.Data.Activity.ID}");
                    Console.WriteLine($"   Activity Type: {addActivityResponse.Data.Activity.Type}");
                    Console.WriteLine($"   Activity Text: {addActivityResponse.Data.Activity.Text}");
                    
                    var activityId = addActivityResponse.Data.Activity.ID;
                    
                    // Step 3: Add a comment to the activity
                    Console.WriteLine("\n3. Adding comment to activity...");
                    var addCommentRequest = new AddCommentRequest
                    {
                        Comment = "This is a great post!",
                        ObjectID = activityId,
                        ObjectType = "activity",
                        Custom = new { 
                            commenter = "john",
                            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                        },
                        UserID = "sara",
                    };
                    
                    var addCommentResponse = await feeds.AddCommentAsync(addCommentRequest);
                    
                    if (addCommentResponse.Data?.Comment != null)
                    {
                        Console.WriteLine($"✅ Comment added successfully!");
                        Console.WriteLine($"   Comment ID: {addCommentResponse.Data.Comment.ID}");
                        Console.WriteLine($"   Comment Text: {addCommentResponse.Data.Comment.Text}");
                        
                        var commentId = addCommentResponse.Data.Comment.ID;
                        
                        // Step 4: Fetch the feed to see all activities
                        Console.WriteLine("\n4. Fetching feed activities...");
                        var queryActivitiesRequest = new QueryActivitiesRequest
                        {
                            Limit = 10
                            // Filter removed for now to test basic functionality
                        };
                        
                        var queryActivitiesResponse = await feeds.QueryActivitiesAsync(queryActivitiesRequest);
                        
                        if (queryActivitiesResponse.Data?.Activities != null)
                        {
                            Console.WriteLine($"✅ Feed fetched successfully!");
                            Console.WriteLine($"   Total activities: {queryActivitiesResponse.Data.Activities.Count}");
                            
                            foreach (var activity in queryActivitiesResponse.Data.Activities)
                            {
                                Console.WriteLine($"   - Activity: {activity.Type} - {activity.Text}");
                            }
                        }
                        else
                        {
                            Console.WriteLine("⚠️ No activities found in feed");
                        }
                    }
                    else
                    {
                        Console.WriteLine("❌ Failed to add comment");
                    }
                }
                else
                {
                    Console.WriteLine("❌ Failed to add activity");
                    Console.WriteLine(addActivityResponse.Error);
                }

                // Step 5: Get the feed we created earlier
                Console.WriteLine("\n5. Getting the feed we created...");
                var getFeedResponse = await feeds.GetOrCreateFeedAsync(
                    FeedGroupID: "user", 
                    FeedID: "john",
                    request: new GetOrCreateFeedRequest
                    {
                        UserID = "sara"
                    }
                );
                
                if (getFeedResponse.Data != null)
                {
                    Console.WriteLine($"✅ Feed retrieved successfully!");
                    Console.WriteLine($"   Feed Data: {getFeedResponse.Data}");
                }
                else
                {
                    Console.WriteLine("❌ Failed to get feed");
                    Console.WriteLine(getFeedResponse.Error);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner error: {ex.InnerException.Message}");
                }
            }
            
            Console.WriteLine("\n=== Example completed ===");
        }
    }
} 