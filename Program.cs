using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GetStream;
using GetStream.Requests;

namespace GetStreamExample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Initialize the client with your API credentials
            var client = new Client(
                apiKey: "yxe38defzb54",
                apiSecret: "qe3x56pv86egwk6ku55spf3rq6dd9f5uhazsj7rcqtk2kkff2fd6v26573jcsvh6"
            );

            // Create the FeedClient
            var feeds = new FeedClient(client);

            try
            {
                Console.WriteLine("=== GetStream .NET SDK Complete Example ===");
                
                // Debug breakpoint - uncomment to pause execution here
                System.Diagnostics.Debugger.Break();
                
                // Step 1: Create a feed
                Console.WriteLine("\n1. Creating feed...");
                var createFeedRequest = new GetOrCreateFeedRequest
                {
                    Data = new { 
                        visibility = "public",
                        custom = new { 
                            benchmark = true,
                            description = "Benchmark test feed for .NET SDK"
                        }
                    },
                    Watch = true
                };
                
                Console.WriteLine("Creating feed...");
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
                }

                // Step 2: Add an activity to the feed
                Console.WriteLine("\n2. Adding activity to feed...");
                var addActivityRequest = new AddActivityRequest
                {
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
                    Console.WriteLine($"   Activity ID: {addActivityResponse.Data.Activity.Id}");
                    Console.WriteLine($"   Activity Type: {addActivityResponse.Data.Activity.Type}");
                    Console.WriteLine($"   Activity Text: {addActivityResponse.Data.Activity.Text}");
                    
                    var activityId = addActivityResponse.Data.Activity.Id;
                    
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
                        }
                    };
                    
                    var addCommentResponse = await feeds.AddCommentAsync(addCommentRequest);
                    
                    if (addCommentResponse.Data?.Comment != null)
                    {
                        Console.WriteLine($"✅ Comment added successfully!");
                        Console.WriteLine($"   Comment ID: {addCommentResponse.Data.Comment.Id}");
                        Console.WriteLine($"   Comment Text: {addCommentResponse.Data.Comment.Text}");
                        
                        var commentId = addCommentResponse.Data.Comment.Id;
                        
                        // Step 4: Fetch the feed to see all activities
                        Console.WriteLine("\n4. Fetching feed activities...");
                        var queryActivitiesRequest = new QueryActivitiesRequest
                        {
                            Limit = 10,
                            Filter = "fid = 'user:john'"
                        };
                        
                        var queryActivitiesResponse = await feeds.QueryActivitiesAsync(queryActivitiesRequest);
                        
                        if (queryActivitiesResponse.Data?.Results != null)
                        {
                            Console.WriteLine($"✅ Feed fetched successfully!");
                            Console.WriteLine($"   Total activities: {queryActivitiesResponse.Data.Results.Count}");
                            
                            foreach (var activity in queryActivitiesResponse.Data.Results)
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