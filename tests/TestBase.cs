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
            var apiKey = Environment.GetEnvironmentVariable("STREAM_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                apiKey = "zta48ppyvwet";
            }
            var apiSecret = Environment.GetEnvironmentVariable("STREAM_API_SECRET");

            if (string.IsNullOrEmpty(apiKey))
                throw new Exception("STREAM_API_KEY environment variable is required");
            if (string.IsNullOrEmpty(apiSecret))
                throw new Exception("STREAM_API_SECRET environment variable is required");

            StreamClient = new StreamClient(apiKey, apiSecret);
            FeedsV3Client = new FeedsV3Client(StreamClient);
        }
    }
} 