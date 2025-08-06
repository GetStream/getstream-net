using System;
using System.Threading.Tasks;
using GetStream;
using NUnit.Framework;

namespace GetStream.Tests
{
    public class TestBase
    {
        protected Client Client { get; private set; } = null!;
        protected FeedsV3Client Feeds { get; private set; } = null!;

        [OneTimeSetUp]
        public void Setup()
        {
            var apiKey = Environment.GetEnvironmentVariable("STREAM_API_KEY");
            var apiSecret = Environment.GetEnvironmentVariable("STREAM_API_SECRET");

            if (string.IsNullOrEmpty(apiKey))
                throw new Exception("STREAM_API_KEY environment variable is required");
            if (string.IsNullOrEmpty(apiSecret))
                throw new Exception("STREAM_API_SECRET environment variable is required");

            Client = new Client(apiKey, apiSecret);
            Feeds = new FeedsV3Client(Client);
        }
    }
} 