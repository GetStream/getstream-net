using System.Text.Json;
using GetStream.Models;
using NUnit.Framework;

namespace GetStream.Tests
{
    [TestFixture]
    public class ActivityMarksRankingTests
    {
        [Test]
        public void CreateFeedGroupRequest_SerializesActivityMarksAndIsSeenRanking()
        {
            var request = new CreateFeedGroupRequest
            {
                ID = "timeline",
                ActivityMarks = new ActivityMarksConfig
                {
                    TrackSeen = true,
                    TrackRead = true,
                },
                Ranking = new RankingConfig
                {
                    Type = "expression",
                    Score = "is_seen ? 0 : 100",
                },
            };

            var json = JsonSerializer.Serialize(request);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.That(root.GetProperty("activity_marks").GetProperty("track_seen").GetBoolean(), Is.True);
            Assert.That(root.GetProperty("activity_marks").GetProperty("track_read").GetBoolean(), Is.True);
            Assert.That(root.GetProperty("ranking").GetProperty("type").GetString(), Is.EqualTo("expression"));
            Assert.That(root.GetProperty("ranking").GetProperty("score").GetString(), Is.EqualTo("is_seen ? 0 : 100"));
        }
    }
}
