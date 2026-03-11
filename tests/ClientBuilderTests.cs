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
    }
}
