using System;
using System.IO;
using GetStream;
using NUnit.Framework;

namespace GetStream.Tests
{
    [TestFixture]
    public class ClientBuilderEnvWalkTests
    {
        [Test]
        public void FromEnv_DoesNotWalkAncestorDirectoriesImplicitly()
        {
            var root = Path.Combine(Path.GetTempPath(), "stream-env-walk-" + Guid.NewGuid().ToString("N"));
            var nested = Path.Combine(root, "child", "grandchild");
            Directory.CreateDirectory(nested);
            File.WriteAllText(
                Path.Combine(root, ".env"),
                "STREAM_API_KEY=ancestor-key\nSTREAM_API_SECRET=ancestor-secret\nSTREAM_BASE_URL=https://evil.invalid\n"
            );

            var origDir = Directory.GetCurrentDirectory();
            var origKey = Environment.GetEnvironmentVariable("STREAM_API_KEY");
            var origSecret = Environment.GetEnvironmentVariable("STREAM_API_SECRET");
            var origBase = Environment.GetEnvironmentVariable("STREAM_BASE_URL");
            try
            {
                Environment.SetEnvironmentVariable("STREAM_API_KEY", null);
                Environment.SetEnvironmentVariable("STREAM_API_SECRET", null);
                Environment.SetEnvironmentVariable("STREAM_BASE_URL", null);
                Directory.SetCurrentDirectory(nested);

                var builder = ClientBuilder.FromEnv();
                Assert.Throws<InvalidOperationException>(() => builder.LoadCredentials());
            }
            finally
            {
                Directory.SetCurrentDirectory(origDir);
                Environment.SetEnvironmentVariable("STREAM_API_KEY", origKey);
                Environment.SetEnvironmentVariable("STREAM_API_SECRET", origSecret);
                Environment.SetEnvironmentVariable("STREAM_BASE_URL", origBase);
                Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void ExplicitEnvPath_DoesNotClobberExistingProcessEnvVars()
        {
            var dir = Path.Combine(Path.GetTempPath(), "stream-env-noclobber-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            File.WriteAllText(
                Path.Combine(dir, ".env"),
                "STREAM_API_KEY=file-key\nSTREAM_API_SECRET=file-secret\nSTREAM_BASE_URL=https://file.invalid\n"
            );

            var origKey = Environment.GetEnvironmentVariable("STREAM_API_KEY");
            var origSecret = Environment.GetEnvironmentVariable("STREAM_API_SECRET");
            var origBase = Environment.GetEnvironmentVariable("STREAM_BASE_URL");
            try
            {
                Environment.SetEnvironmentVariable("STREAM_API_KEY", "proc-key");
                Environment.SetEnvironmentVariable("STREAM_API_SECRET", "proc-secret");
                Environment.SetEnvironmentVariable("STREAM_BASE_URL", "https://proc.example.com");

                var builder = new ClientBuilder().EnvPath(dir);
                builder.LoadCredentials();

                Assert.That(builder.ApiKeyValue, Is.EqualTo("proc-key"));
                Assert.That(builder.ApiSecretValue, Is.EqualTo("proc-secret"));
                Assert.That(builder.BaseUrlValue, Is.EqualTo("https://proc.example.com"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("STREAM_API_KEY", origKey);
                Environment.SetEnvironmentVariable("STREAM_API_SECRET", origSecret);
                Environment.SetEnvironmentVariable("STREAM_BASE_URL", origBase);
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
