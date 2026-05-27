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
            var originalKey = Environment.GetEnvironmentVariable("STREAM_API_KEY");
            var originalSecret = Environment.GetEnvironmentVariable("STREAM_API_SECRET");
            Environment.SetEnvironmentVariable("STREAM_API_KEY", "env-key");
            Environment.SetEnvironmentVariable("STREAM_API_SECRET", "env-secret");
            try
            {
                var builder = new ClientBuilder().SkipEnvLoad();
                Assert.Throws<InvalidOperationException>(() => builder.Build());
            }
            finally
            {
                Environment.SetEnvironmentVariable("STREAM_API_KEY", originalKey);
                Environment.SetEnvironmentVariable("STREAM_API_SECRET", originalSecret);
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
            var originalBaseUrl = Environment.GetEnvironmentVariable("STREAM_BASE_URL");
            Environment.SetEnvironmentVariable("STREAM_BASE_URL", "https://custom.example.com");
            try
            {
                var defaultBaseUrl = new ClientBuilder().BaseUrlValue;
                var builder = new ClientBuilder()
                    .ApiKey(TestApiKey)
                    .ApiSecret(TestApiSecret)
                    .SkipEnvLoad();
                builder.LoadCredentials();
                Assert.That(builder.BaseUrlValue, Is.EqualTo(defaultBaseUrl));
            }
            finally
            {
                Environment.SetEnvironmentVariable("STREAM_BASE_URL", originalBaseUrl);
            }
        }

        [Test]
        public void ClientBuilder_PoolKnobs_AppliedToBuiltClient()
        {
            // CHA-2956: knobs flow through the IClient-wrapping builders (Chat/Video/Feeds/Moderation),
            // which are constructed over the hand-written BaseClient seam. This test asserts on BuildChatClient;
            // the StreamClient-typed Build() is covered separately by Build_StreamClient_AppliesCustomPoolKnobs.
            var chatClient = new ClientBuilder()
                .ApiKey(TestApiKey)
                .ApiSecret(TestApiSecret)
                .MaxConnsPerHost(13)
                .IdleTimeout(TimeSpan.FromSeconds(77))
                .ConnectTimeout(TimeSpan.FromSeconds(8))
                .RequestTimeout(TimeSpan.FromSeconds(22))
                .SkipEnvLoad()
                .BuildChatClient();

            var (httpClient, handler) = GetStream.Tests.ConnectionPoolTests.UnwrapWrapperHandler(chatClient);
            Assert.That(handler.MaxConnectionsPerServer, Is.EqualTo(13));
            Assert.That(handler.PooledConnectionIdleTimeout, Is.EqualTo(TimeSpan.FromSeconds(77)));
            Assert.That(handler.ConnectTimeout, Is.EqualTo(TimeSpan.FromSeconds(8)));
            Assert.That(httpClient.Timeout, Is.EqualTo(TimeSpan.FromSeconds(22)));
        }

        [Test]
        public void ClientBuilder_DefaultsWhenNoPoolKnobsSet()
        {
            var chatClient = new ClientBuilder()
                .ApiKey(TestApiKey)
                .ApiSecret(TestApiSecret)
                .SkipEnvLoad()
                .BuildChatClient();
            var (httpClient, handler) = GetStream.Tests.ConnectionPoolTests.UnwrapWrapperHandler(chatClient);
            Assert.That(handler.MaxConnectionsPerServer, Is.EqualTo(5));
            Assert.That(handler.PooledConnectionIdleTimeout, Is.EqualTo(TimeSpan.FromSeconds(55)));
            Assert.That(handler.ConnectTimeout, Is.EqualTo(TimeSpan.FromSeconds(10)));
            Assert.That(httpClient.Timeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
        }

        [Test]
        public void Build_StreamClient_AppliesCustomPoolKnobs()
        {
            // CHA-2956: the StreamClient-typed Build() now routes through the generated
            // StreamClient(StreamOptions) ctor (-> BaseClient(StreamOptions)), so custom knobs
            // set on the builder flow through, the same as the IClient-wrapping builders.
            var client = new ClientBuilder()
                .ApiKey(TestApiKey)
                .ApiSecret(TestApiSecret)
                .MaxConnsPerHost(13)
                .IdleTimeout(TimeSpan.FromSeconds(77))
                .ConnectTimeout(TimeSpan.FromSeconds(8))
                .RequestTimeout(TimeSpan.FromSeconds(22))
                .SkipEnvLoad()
                .Build();
            var (httpClient, handler) = GetStream.Tests.ConnectionPoolTests.UnwrapHandler(client);
            Assert.That(handler.MaxConnectionsPerServer, Is.EqualTo(13), "Build() now carries the custom MaxConnsPerHost");
            Assert.That(handler.PooledConnectionIdleTimeout, Is.EqualTo(TimeSpan.FromSeconds(77)));
            Assert.That(handler.ConnectTimeout, Is.EqualTo(TimeSpan.FromSeconds(8)));
            Assert.That(httpClient.Timeout, Is.EqualTo(TimeSpan.FromSeconds(22)));
        }

        [Test]
        public void Build_StreamClient_DefaultsWhenNoPoolKnobsSet()
        {
            // Sanity check that Build() still yields spec defaults when no knobs are customized.
            var client = new ClientBuilder()
                .ApiKey(TestApiKey)
                .ApiSecret(TestApiSecret)
                .SkipEnvLoad()
                .Build();
            var (httpClient, handler) = GetStream.Tests.ConnectionPoolTests.UnwrapHandler(client);
            Assert.That(handler.MaxConnectionsPerServer, Is.EqualTo(5));
            Assert.That(handler.PooledConnectionIdleTimeout, Is.EqualTo(TimeSpan.FromSeconds(55)));
            Assert.That(handler.ConnectTimeout, Is.EqualTo(TimeSpan.FromSeconds(10)));
            Assert.That(httpClient.Timeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
        }
    }
}
