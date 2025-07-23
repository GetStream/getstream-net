using System;
using System.Threading.Tasks;

namespace GetStream.Tests
{
    public class TestRunner
    {
        public static async Task RunTests(string apiKey, string apiSecret, string appId)
        {
            Console.WriteLine("🚀 Starting GetStream.NET Tests");
            Console.WriteLine("================================\n");

            try
            {
                // Create test instance with provided credentials
                var tests = new FeedTests();
                
                // Update the client with real credentials
                tests.UpdateCredentials(apiKey, apiSecret, appId);
                
                // Run all tests
                await tests.RunAllTests();
                
                Console.WriteLine("\n🎉 All tests completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n💥 Test execution failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }
    }
} 