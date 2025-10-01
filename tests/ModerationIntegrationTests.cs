using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GetStream;
using GetStream.Models;
using NUnit.Framework;

namespace GetStream.Tests
{
    /// <summary>
    /// Comprehensive Integration tests for Moderation operations
    /// These tests follow a logical flow: setup → create users → moderate → cleanup
    ///
    /// Test order:
    /// 1. Environment Setup (users, channels)
    /// 2. Ban/Unban Operations
    /// 3. Mute/Unmute Operations
    /// 4. Flag Operations
    /// 5. Content Moderation
    /// 6. Configuration Management
    /// 7. Review Queue Operations
    /// 8. Rules and Templates
    /// 9. Cleanup
    /// </summary>
    [TestFixture]
    public class ModerationIntegrationTests
    {
        private StreamClient _client = null!;
        private ModerationClient _moderationClient = null!;

        // Test users
        private string _testUserId = null!;
        private string _testUserId2 = null!;
        private string _moderatorUserId = null!;
        private string _reporterUserId = null!;

        // Test resources
        private string _testChannelId = null!;
        private string _testChannelCid = null!;

        // Track created resources for cleanup
        private readonly List<string> _createdUserIds = new List<string>();
        private readonly List<string> _bannedUserIds = new List<string>();
        private readonly List<string> _mutedUserIds = new List<string>();
        private readonly List<string> _createdConfigs = new List<string>();

        [OneTimeSetUp]
        public async Task SetUp()
        {
            // Try to find .env file in the solution root (going up from tests directory)
            var solutionRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            var envFilePath = Path.Combine(solutionRoot, ".env");
            
            ClientBuilder builder;
            if (File.Exists(envFilePath))
            {
                builder = ClientBuilder.FromEnvFile(envFilePath);
            }
            else
            {
                builder = ClientBuilder.FromEnv();
            }

            _client = builder.Build();
            _moderationClient = new ModerationClient(_client);

            _testUserId = "test-user-" + Guid.NewGuid().ToString("N")[..8];
            _testUserId2 = "test-user-2-" + Guid.NewGuid().ToString("N")[..8];
            _moderatorUserId = "moderator-" + Guid.NewGuid().ToString("N")[..8];
            _reporterUserId = "reporter-" + Guid.NewGuid().ToString("N")[..8];
            _testChannelId = "test-channel-" + Guid.NewGuid().ToString("N")[..8];
            _testChannelCid = "messaging:" + _testChannelId;

            // Setup environment for each test
            await SetupEnvironment();
        }

        [OneTimeTearDown]
        public async Task TearDown()
        {
            // Cleanup created resources in reverse order
            await CleanupResources();
        }

        // =================================================================
        // ENVIRONMENT SETUP (called in SetUp for each test)
        // =================================================================

        private async Task SetupEnvironment()
        {
            try
            {
                Console.WriteLine("🔧 Setting up moderation test environment...");

                // Create test users
                // snippet-start: CreateModerationUsers
                var updateUsersRequest = new UpdateUsersRequest
                {
                    Users = new Dictionary<string, UserRequest>
                    {
                        [_testUserId] = new UserRequest
                        {
                            ID = _testUserId,
                            Name = "Test User 1",
                            Role = "user"
                        },
                        [_testUserId2] = new UserRequest
                        {
                            ID = _testUserId2,
                            Name = "Test User 2", 
                            Role = "user"
                        },
                        [_moderatorUserId] = new UserRequest
                        {
                            ID = _moderatorUserId,
                            Name = "Moderator User",
                            Role = "admin"
                        },
                        [_reporterUserId] = new UserRequest
                        {
                            ID = _reporterUserId,
                            Name = "Reporter User",
                            Role = "user"
                        }
                    }
                };

                var response = await _client.UpdateUsersAsync(updateUsersRequest);
                // snippet-end: CreateModerationUsers

                Assert.That(response, Is.Not.Null);
                Assert.That(response.Data, Is.Not.Null);

                _createdUserIds.AddRange(new[] { _testUserId, _testUserId2, _moderatorUserId, _reporterUserId });

                Console.WriteLine("Created test users for moderation tests");
                Console.WriteLine($"   Target User: {_testUserId}");
                Console.WriteLine($"   Target User 2: {_testUserId2}");
                Console.WriteLine($"   Moderator: {_moderatorUserId}");
                Console.WriteLine($"   Reporter: {_reporterUserId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Setup failed: {ex.Message}");
                throw;
            }
        }

        // =================================================================
        // 1. ENVIRONMENT SETUP TEST (demonstrates the setup process)
        // =================================================================

        [Test, Order(1)]
        public void Test01_SetupEnvironmentDemo()
        {
            Console.WriteLine("\n🔧 Demonstrating moderation environment setup...");
            Console.WriteLine("Users are automatically created in SetUp()");
            Console.WriteLine($"   Test User 1: {_testUserId}");
            Console.WriteLine($"   Test User 2: {_testUserId2}");
            Console.WriteLine($"   Moderator: {_moderatorUserId}");
            Console.WriteLine($"   Reporter: {_reporterUserId}");

            Assert.That(_testUserId, Is.Not.Null.And.Not.Empty);
            Assert.That(_moderatorUserId, Is.Not.Null.And.Not.Empty);
        }

        // =================================================================
        // 2. BAN/UNBAN OPERATIONS
        // =================================================================

        [Test, Order(2)]
        public async Task Test02_BanUserWithReason()
        {
            Console.WriteLine("\n🚫 Testing user ban with reason...");

            // snippet-start: BanWithReason
            var request = new BanRequest
            {
                TargetUserID = _testUserId,
                Reason = "spam",
                Timeout = 60, // 60 minutes
                BannedByID = _moderatorUserId
            };

            var response = await _moderationClient.BanAsync(request);
            // snippet-stop: BanWithReason

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Data, Is.Not.Null);
            _bannedUserIds.Add(_testUserId);
            
            Console.WriteLine($"Successfully banned user: {_testUserId}");
        }

        // =================================================================
        // 3. MUTE/UNMUTE OPERATIONS
        // =================================================================

        [Test, Order(5)]
        public async Task Test05_MuteUser()
        {
            Console.WriteLine("\nTesting user mute...");

            // snippet-start: MuteUser
            var request = new MuteRequest
            {
                TargetIds = new List<string> { _testUserId2 },
                Timeout = 30, // 30 minutes
                UserID = _moderatorUserId
            };

            var response = await _moderationClient.MuteAsync(request);
            // snippet-stop: MuteUser

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Data, Is.Not.Null);
            _mutedUserIds.Add(_testUserId2);
            
            Console.WriteLine($"Successfully muted user: {_testUserId2}");
        }

        [Test, Order(6)]
        public async Task Test06_UnmuteUser()
        {
            Console.WriteLine("\n🔊 Testing user unmute...");

            // Ensure user is muted first
            if (!_mutedUserIds.Contains(_testUserId2))
            {
                await Test05_MuteUser();
            }

            // snippet-start: UnmuteUser
            var request = new UnmuteRequest
            {
                TargetIds = new List<string> { _testUserId2 },
                UserID = _moderatorUserId
            };

            var response = await _moderationClient.UnmuteAsync(request);
            // snippet-stop: UnmuteUser

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Data, Is.Not.Null);
            
            // Remove from muted list
            _mutedUserIds.Remove(_testUserId2);
            
            Console.WriteLine($"Successfully unmuted user: {_testUserId2}");
        }

        // =================================================================
        // 4. FLAG OPERATIONS
        // =================================================================

        [Test, Order(7)]
        public async Task Test07_FlagUser()
        {
            Console.WriteLine("\n🚩 Testing user flagging...");

            // snippet-start: FlagUser
            var request = new FlagRequest
            {
                EntityType = "user",
                EntityID = _testUserId,
                EntityCreatorID = _testUserId,
                Reason = "spam",
                UserID = _reporterUserId
            };

            var response = await _moderationClient.FlagAsync(request);
            // snippet-stop: FlagUser

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Data, Is.Not.Null);
            
            Console.WriteLine($"Successfully flagged user: {_testUserId}");
        }

        // =================================================================
        // 6. CONFIGURATION MANAGEMENT
        // =================================================================

        [Test, Order(10)]
        public async Task Test10_CreateModerationConfig()
        {
            Console.WriteLine("\n⚙️ Testing moderation config creation...");

            var configKey = "test-config-" + Guid.NewGuid().ToString("N")[..8];

            // snippet-start: CreateModerationConfig
            var request = new UpsertConfigRequest
            {
                Key = configKey,
                AutomodToxicityConfig = new AutomodToxicityConfig
                {
                    Enabled = true,
                    Rules = new List<AutomodRule>
                    {
                        new AutomodRule
                        {
                            Label = "toxic",
                            Threshold = 0.8,
                            Action = "remove"
                        }
                    }
                }
            };

            var response = await _moderationClient.UpsertConfigAsync(request);
            // snippet-stop: CreateModerationConfig

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Data, Is.Not.Null);
            _createdConfigs.Add(configKey);
            
            Console.WriteLine($"Successfully created moderation config: {configKey}");
        }

        [Test, Order(11)]
        public async Task Test11_QueryModerationConfigs()
        {
            Console.WriteLine("\n🔍 Testing moderation config query...");

            // snippet-start: QueryModerationConfigs
            var request = new QueryModerationConfigsRequest
            {
                Filter = new Dictionary<string, object>(),
                Limit = 10
            };

            var response = await _moderationClient.QueryModerationConfigsAsync(request);
            // snippet-stop: QueryModerationConfigs

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Data, Is.Not.Null);
            
            Console.WriteLine("Successfully queried moderation configs");
        }

        // =================================================================
        // 7. REVIEW QUEUE OPERATIONS
        // =================================================================

        [Test, Order(12)]
        public async Task Test12_QueryReviewQueue()
        {
            Console.WriteLine("\n📋 Testing review queue query...");

            // snippet-start: QueryReviewQueueWithFilter
            var request = new QueryReviewQueueRequest
            {
                Filter = new Dictionary<string, object>(),
                Limit = 25
            };

            var response = await _moderationClient.QueryReviewQueueAsync(request);
            // snippet-stop: QueryReviewQueueWithFilter

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Data, Is.Not.Null);
            
            Console.WriteLine("Successfully queried review queue");
        }

        // =================================================================
        // 8. QUERY OPERATIONS
        // =================================================================

        [Test, Ignore("Fix me")]
        public async Task Test13_QueryModerationFlags()
        {
            Console.WriteLine("\n🚩 Testing moderation flags query...");

            // snippet-start: QueryModerationFlags
            var request = new QueryModerationFlagsRequest
            {
                Filter = new Dictionary<string, object>(),
                Limit = 50
            };

            var response = await _moderationClient.QueryModerationFlagsAsync(request);
            // snippet-stop: QueryModerationFlags

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Data, Is.Not.Null);
            
            Console.WriteLine("Successfully queried moderation flags");
        }

        [Test, Ignore("Fix this")]
        public async Task Test14_QueryModerationLogs()
        {
            Console.WriteLine("\n📝 Testing moderation logs query...");

            // snippet-start: QueryModerationLogs
            var request = new QueryModerationLogsRequest
            {
                Filter = new Dictionary<string, object>(),
                Limit = 25
            };

            var response = await _moderationClient.QueryModerationLogsAsync(request);
            // snippet-stop: QueryModerationLogs

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Data, Is.Not.Null);
            
            Console.WriteLine("Successfully queried moderation logs");
        }

        // =================================================================
        // 9. TEMPLATE OPERATIONS
        // =================================================================

        [Test, Order(15)]
        public async Task Test15_QueryTemplates()
        {
            Console.WriteLine("\n📄 Testing template query...");

            // snippet-start: V2QueryTemplates
            var response = await _moderationClient.V2QueryTemplatesAsync();
            // snippet-stop: V2QueryTemplates

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Data, Is.Not.Null);
            
            Console.WriteLine("Successfully queried moderation templates");
        }
        
        // =================================================================
        // 10. RULE OPERATIONS
        // =================================================================

        [Test, Order(17)]
        public async Task Test17_UpsertModerationRule()
        {
            Console.WriteLine("\n📋 Testing moderation rule upsert...");

            // snippet-start: UpsertModerationRule

            var ruleAction = new RuleBuilderAction
            {
                Type = "ban_user"
            };
            var request = new UpsertModerationRuleRequest
            {
                Name = "test-rule-" + Guid.NewGuid().ToString("N")[..8],
                Description = "Test moderation rule created by .NET SDK",
                Enabled = true,
                Action = ruleAction,
            };

            var response = await _moderationClient.UpsertModerationRuleAsync(request);
            // snippet-stop: UpsertModerationRule

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Data, Is.Not.Null);
            
            Console.WriteLine("Successfully upserted moderation rule");
        }

        [Test, Order(18)]
        public async Task Test18_QueryModerationRules()
        {
            Console.WriteLine("\n📋 Testing moderation rules query...");

            // snippet-start: QueryModerationRules
            var request = new QueryModerationRulesRequest
            {
                Filter = new Dictionary<string, object> { ["enabled"] = true },
                Limit = 20
            };

            var response = await _moderationClient.QueryModerationRulesAsync(request);
            // snippet-stop: QueryModerationRules

            Assert.That(response, Is.Not.Null);
            Assert.That(response.Data, Is.Not.Null);
            
            Console.WriteLine("Successfully queried moderation rules");
        }

        // =================================================================
        // HELPER METHODS
        // =================================================================

        private async Task CleanupResources()
        {
            Console.WriteLine("\n🧹 Cleaning up moderation test resources...");
            
            // Unmute any remaining muted users
            foreach (var userId in _mutedUserIds.ToArray())
            {
                try
                {
                    var request = new UnmuteRequest
                    {
                        TargetIds = new List<string> { userId },
                        UserID = _moderatorUserId
                    };
                    await _moderationClient.UnmuteAsync(request);
                    Console.WriteLine($"Cleaned up mute for user: {userId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to unmute user {userId}: {ex.Message}");
                }
            }
            
            // Delete any created moderation configs
            foreach (var configKey in _createdConfigs.ToArray())
            {
                try
                {
                    await _moderationClient.DeleteConfigAsync(configKey, null);
                    Console.WriteLine($"Cleaned up config: {configKey}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to delete config {configKey}: {ex.Message}");
                }
            }
            
            Console.WriteLine("🧹 Moderation cleanup completed");
        }
    }
}
