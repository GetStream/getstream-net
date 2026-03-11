using System;
using GetStream;
using NUnit.Framework;

namespace GetStream.Tests
{
    [TestFixture]
    public class ClientBuilderTests
    {
        private const string TestApiKey = "test-api-key";
        private const string TestApiSecret = "test-api-secret-that-is-long-enough-for-hmac256";

        [Test]
        public void BuildChatClient_ReturnsInstance()
        {
            var builder = new ClientBuilder()
                .ApiKey(TestApiKey)
                .ApiSecret(TestApiSecret)
                .SkipEnvLoad();

            var chatClient = builder.BuildChatClient();

            Assert.That(chatClient, Is.Not.Null);
            Assert.That(chatClient, Is.InstanceOf<ChatClient>());
        }

        [Test]
        public void BuildVideoClient_ReturnsInstance()
        {
            var builder = new ClientBuilder()
                .ApiKey(TestApiKey)
                .ApiSecret(TestApiSecret)
                .SkipEnvLoad();

            var videoClient = builder.BuildVideoClient();

            Assert.That(videoClient, Is.Not.Null);
            Assert.That(videoClient, Is.InstanceOf<VideoClient>());
        }

        [Test]
        public void BuildModerationClient_ReturnsInstance()
        {
            var builder = new ClientBuilder()
                .ApiKey(TestApiKey)
                .ApiSecret(TestApiSecret)
                .SkipEnvLoad();

            var moderationClient = builder.BuildModerationClient();

            Assert.That(moderationClient, Is.Not.Null);
            Assert.That(moderationClient, Is.InstanceOf<ModerationClient>());
        }

        [Test]
        public void BuildFeedsClient_ReturnsInstance()
        {
            var builder = new ClientBuilder()
                .ApiKey(TestApiKey)
                .ApiSecret(TestApiSecret)
                .SkipEnvLoad();

            var feedsClient = builder.BuildFeedsClient();

            Assert.That(feedsClient, Is.Not.Null);
            Assert.That(feedsClient, Is.InstanceOf<FeedsV3Client>());
        }

        [Test]
        public void AllBuilders_WithoutCredentials_ThrowsInvalidOperationException()
        {
            var builder = new ClientBuilder().SkipEnvLoad();

            Assert.Throws<InvalidOperationException>(() => builder.BuildChatClient());
            Assert.Throws<InvalidOperationException>(() => builder.BuildVideoClient());
            Assert.Throws<InvalidOperationException>(() => builder.BuildModerationClient());
            Assert.Throws<InvalidOperationException>(() => builder.BuildFeedsClient());
        }

        [Test]
        public void SkipEnvLoad_IgnoresEnvironmentVariables()
        {
            Environment.SetEnvironmentVariable("STREAM_API_KEY", "env-key");
            Environment.SetEnvironmentVariable("STREAM_API_SECRET", "env-secret");
            try
            {
                var builder = new ClientBuilder().SkipEnvLoad();
                Assert.Throws<InvalidOperationException>(() => builder.Build());
            }
            finally
            {
                Environment.SetEnvironmentVariable("STREAM_API_KEY", null);
                Environment.SetEnvironmentVariable("STREAM_API_SECRET", null);
            }
        }

        [Test]
        public void SkipEnvLoad_ErrorMessage_OmitsEnvVarHint()
        {
            var builder = new ClientBuilder().SkipEnvLoad();
            var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
            Assert.That(ex.Message, Does.Contain("Call ApiKey()"));
            Assert.That(ex.Message, Does.Not.Contain("Set STREAM_API_KEY"));
        }

        [Test]
        public void SkipEnvLoad_IgnoresBaseUrlEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable("STREAM_BASE_URL", "https://custom.example.com");
            try
            {
                var builder = new ClientBuilder()
                    .ApiKey(TestApiKey)
                    .ApiSecret(TestApiSecret)
                    .SkipEnvLoad();
                builder.LoadCredentials();
                Assert.That(builder.BaseUrlValue, Is.EqualTo("https://chat.stream-io-api.com"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("STREAM_BASE_URL", null);
            }
        }
    }
}
