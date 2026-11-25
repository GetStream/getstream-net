using System.Text.Json.Nodes;
using GetStream;
using NUnit.Framework;

namespace GetStream.Tests
{
    [TestFixture]
    public class LogRedactionRecursiveTests
    {
        [Test]
        public void RedactJsonBody_RedactsNestedAndPatternMatchedSecretKeys()
        {
            var payload =
                "{\"access_token\":\"tok-123\",\"keep\":\"visible\","
                + "\"storage\":{\"s3_secret\":\"aws-secret-value\","
                + "\"firebase_credentials\":\"pem\",\"bucket\":\"my-bucket\"},"
                + "\"items\":[{\"apn_auth_key\":\"p8-key\",\"name\":\"ios\"}]}";

            var redacted = LogRedaction.RedactJsonBody(payload);

            Assert.That(redacted, Does.Not.Contain("tok-123"));
            Assert.That(redacted, Does.Not.Contain("aws-secret-value"));
            Assert.That(redacted, Does.Not.Contain("p8-key"));

            var root = JsonNode.Parse(redacted)!.AsObject();
            Assert.That(root["access_token"]!.GetValue<string>(), Is.EqualTo("<redacted>"));
            Assert.That(root["keep"]!.GetValue<string>(), Is.EqualTo("visible"));
            Assert.That(root["storage"]!["s3_secret"]!.GetValue<string>(), Is.EqualTo("<redacted>"));
            Assert.That(root["storage"]!["firebase_credentials"]!.GetValue<string>(), Is.EqualTo("<redacted>"));
            Assert.That(root["storage"]!["bucket"]!.GetValue<string>(), Is.EqualTo("my-bucket"));
            Assert.That(root["items"]![0]!["apn_auth_key"]!.GetValue<string>(), Is.EqualTo("<redacted>"));
            Assert.That(root["items"]![0]!["name"]!.GetValue<string>(), Is.EqualTo("ios"));
        }
    }
}
