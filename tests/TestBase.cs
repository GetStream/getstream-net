using System;
using System.Threading.Tasks;
using GetStream;
using NUnit.Framework;

namespace GetStream.Tests
{
    public class TestBase
    {
        protected StreamClient StreamClient { get; private set; } = null!;
        protected FeedsV3Client FeedsV3Client { get; private set; } = null!;

        [OneTimeSetUp]
        public void Setup()
        {
            try
            {
                // Clients automatically load configuration from .env file or environment variables
                StreamClient = new StreamClient();
                FeedsV3Client = new FeedsV3Client();
                
                Console.WriteLine("Successfully loaded GetStream configuration from environment");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"Configuration error: {ex.Message}");
                Console.WriteLine("Make sure to:");
                Console.WriteLine("1. Copy .env.example to .env");
                Console.WriteLine("2. Fill in your STREAM_API_KEY and STREAM_API_SECRET in the .env file");
                Console.WriteLine("3. Or set these as environment variables");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error during setup: {ex.Message}");
                throw;
            }
        }
    }
} 