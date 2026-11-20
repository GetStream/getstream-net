using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GetStream.Models;
using Moq;
using NUnit.Framework;

namespace GetStream.Tests
{
    /// <summary>
    /// A call that sends a JSON body must not reflect that body onto the query string.
    /// Object.ToString() is the CLR type name, which is how GetOrCreateFeed leaked
    /// enrichmentoptions=GetStream.Models.EnrichmentOptions.
    /// </summary>
    [TestFixture]
    public class BodyQueryParamTests
    {
        [Test]
        public async Task GetOrCreateFeed_DoesNotCopyBodyFieldsOntoTheQueryString()
        {
            Dictionary<string, string>? query = null;
            GetOrCreateFeedRequest? body = null;
            var sawCall = false;
            var mock = new Mock<IClient>();
            mock.Setup(x => x.MakeRequestAsync<GetOrCreateFeedRequest, GetOrCreateFeedResponse>(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.Is<Dictionary<string, string>?>(_ => true),
                    It.IsAny<GetOrCreateFeedRequest>(),
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, string, Dictionary<string, string>?, GetOrCreateFeedRequest, Dictionary<string, string>?, CancellationToken>(
                    (_, _, capturedQuery, capturedBody, _, _) =>
                    {
                        sawCall = true;
                        query = capturedQuery;
                        body = capturedBody;
                    })
                .ReturnsAsync(new StreamResponse<GetOrCreateFeedResponse>
                {
                    Data = new GetOrCreateFeedResponse(),
                });

            var client = new FeedsV3Client(mock.Object);
            var request = new GetOrCreateFeedRequest
            {
                Limit = 10,
                UserID = "user-1",
                View = "news_default",
                EnrichmentOptions = new EnrichmentOptions(),
                ExternalRanking = new Dictionary<string, object> { ["popularity"] = 1 },
            };

            await client.GetOrCreateFeedAsync("news", "feed-1", request);

            Assert.That(sawCall, Is.True);
            Assert.That(body, Is.SameAs(request));
            Assert.That(query, Is.Null.Or.Empty);
            if (query == null)
            {
                return;
            }

            Assert.That(query.Keys, Does.Not.Contain("enrichmentoptions"));
            Assert.That(query.Keys, Does.Not.Contain("externalranking"));
            Assert.That(query.Keys, Does.Not.Contain("userid"));
            foreach (var pair in query)
            {
                Assert.That(pair.Value, Does.Not.Contain("GetStream.Models"));
                Assert.That(pair.Value, Does.Not.Contain("System.Collections"));
            }
        }
    }
}
