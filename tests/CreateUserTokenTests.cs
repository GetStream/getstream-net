using System;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using GetStream;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;

namespace GetStream.Tests
{
    [TestFixture]
    public class CreateUserTokenTests
    {
        private const string TestApiKey = "test-api-key";
        private const string TestApiSecret = "test-api-secret-that-is-long-enough-for-hmac256";

        private StreamClient _client = null!;
        private JwtSecurityTokenHandler _tokenHandler = null!;

        [SetUp]
        public void SetUp()
        {
            _client = new StreamClient(TestApiKey, TestApiSecret);
            _tokenHandler = new JwtSecurityTokenHandler();
        }

        [Test]
        public void CreateUserToken_ContainsCorrectUserId()
        {
            var token = _client.CreateUserToken("john");
            var jwt = _tokenHandler.ReadJwtToken(token);

            Assert.That(jwt.Payload["user_id"], Is.EqualTo("john"));
        }

        [Test]
        public void CreateUserToken_DefaultExpiration_IsOneHour()
        {
            var before = DateTime.UtcNow;
            var token = _client.CreateUserToken("john");
            var jwt = _tokenHandler.ReadJwtToken(token);

            var exp = jwt.ValidTo;
            Assert.That(exp, Is.GreaterThan(before.AddMinutes(59)));
            Assert.That(exp, Is.LessThan(before.AddMinutes(61)));
        }

        [Test]
        public void CreateUserToken_CustomExpiration()
        {
            var before = DateTime.UtcNow;
            var token = _client.CreateUserToken("john", expiration: TimeSpan.FromHours(24));
            var jwt = _tokenHandler.ReadJwtToken(token);

            var exp = jwt.ValidTo;
            Assert.That(exp, Is.GreaterThan(before.AddHours(23)));
            Assert.That(exp, Is.LessThan(before.AddHours(25)));
        }

        [Test]
        public void CreateUserToken_IsValidSignature()
        {
            var token = _client.CreateUserToken("john");
            var key = Encoding.UTF8.GetBytes(TestApiSecret);

            var validationParams = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                IssuerSigningKey = new SymmetricSecurityKey(key),
            };

            _tokenHandler.ValidateToken(token, validationParams, out _);
        }

        [Test]
        public void CreateUserToken_DifferentUsers_ProduceDifferentTokens()
        {
            var token1 = _client.CreateUserToken("alice");
            var token2 = _client.CreateUserToken("bob");

            Assert.That(token1, Is.Not.EqualTo(token2));

            var jwt1 = _tokenHandler.ReadJwtToken(token1);
            var jwt2 = _tokenHandler.ReadJwtToken(token2);

            Assert.That(jwt1.Payload["user_id"], Is.EqualTo("alice"));
            Assert.That(jwt2.Payload["user_id"], Is.EqualTo("bob"));
        }
    }
}
