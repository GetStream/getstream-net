using System;
using System.IO;
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
                // Try to find .env file in the solution root (going up from tests directory)
                var solutionRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
                var envFilePath = Path.Combine(solutionRoot, ".env");
                
                ClientBuilder builder;
                if (File.Exists(envFilePath))
                {
                    Console.WriteLine($"Loading configuration from: {envFilePath}");
                    builder = ClientBuilder.FromEnvFile(envFilePath);
                }
                else
                {
                    Console.WriteLine("No .env file found, using environment variables");
                    builder = ClientBuilder.FromEnv();
                }
                
                StreamClient = builder.Build();
                FeedsV3Client = builder.BuildFeedsClient();
                
                Console.WriteLine("Successfully loaded GetStream configuration");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"Configuration error: {ex.Message}");
                Console.WriteLine("Make sure to:");
                Console.WriteLine("1. Copy .env.example to .env in the solution root");
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