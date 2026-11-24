using System;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;

namespace GetStream.Tests
{
    [TestFixture]
    public class WebhookEmptySecretTests
    {
        [Test]
        public void VerifySignature_RejectsEmptySecretEvenWithMatchingHmac()
        {
            var body = "{\"type\":\"message.new\"}";
            using var hmac = new HMACSHA256(Array.Empty<byte>());
            var forged = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

            Assert.That(Webhook.VerifySignature(body, forged, ""), Is.False);
            Assert.That(Webhook.VerifySignature(Encoding.UTF8.GetBytes(body), forged, ""), Is.False);
            Assert.That(Webhook.VerifySignature(body, forged, null!), Is.False);
        }

        [Test]
        public void VerifySignature_RejectsNullOrEmptySignatureWithoutThrowing()
        {
            var body = "{\"type\":\"message.new\"}";

            Assert.That(Webhook.VerifySignature(body, "", "real-secret"), Is.False);
            Assert.That(Webhook.VerifySignature(body, null!, "real-secret"), Is.False);
            Assert.That(Webhook.VerifySignature((byte[])null!, "sig", "real-secret"), Is.False);
            Assert.That(Webhook.VerifySignature((string)null!, "sig", "real-secret"), Is.False);
        }
    }
}
