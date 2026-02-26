using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GetStream;
using GetStream.Models;
using NUnit.Framework;

namespace GetStream.Tests
{
    [TestFixture]
    public class VideoIntegrationTests : TestBase
    {
        [Test]
        public async Task CRUDCallTypeOperations()
        {
            var callTypeName = "calltype-" + Guid.NewGuid().ToString("N")[..16];

            // List call types; if > 10, delete custom ones to avoid hitting limits
            var listResp = await StreamClient.MakeRequestAsync<object, ListCallTypeResponse>(
                "GET", "/api/v2/video/calltypes", null, null, null);
            Assert.That(listResp.Data, Is.Not.Null);

            if (listResp.Data!.CallTypes.Count > 10)
            {
                var builtinTypes = new HashSet<string> { "default", "livestream", "audio_room", "development" };
                foreach (var ct in listResp.Data.CallTypes)
                {
                    if (!builtinTypes.Contains(ct.Key))
                    {
                        try
                        {
                            await StreamClient.MakeRequestAsync<object, Response>(
                                "DELETE", "/api/v2/video/calltypes/{name}", null, null,
                                new Dictionary<string, string> { ["name"] = ct.Key });
                        }
                        catch { /* ignore cleanup errors */ }
                    }
                }
            }

            try
            {
                // Create call type
                var createResp = await StreamClient.MakeRequestAsync<CreateCallTypeRequest, CreateCallTypeResponse>(
                    "POST",
                    "/api/v2/video/calltypes",
                    null,
                    new CreateCallTypeRequest
                    {
                        Name = callTypeName,
                        Settings = new CallSettingsRequest
                        {
                            Audio = new AudioSettingsRequest
                            {
                                DefaultDevice = "speaker",
                                MicDefaultOn = true
                            },
                            Screensharing = new ScreensharingSettingsRequest
                            {
                                AccessRequestEnabled = false,
                                Enabled = true
                            }
                        },
                        NotificationSettings = new NotificationSettingsRequest
                        {
                            Enabled = true,
                            CallNotification = new EventNotificationSettingsRequest
                            {
                                APNS = new APNSPayload { Title = "{{ user.display_name }} invites you to a call", Body = "" },
                                Enabled = true
                            },
                            SessionStarted = new EventNotificationSettingsRequest
                            {
                                APNS = new APNSPayload { Title = "{{ user.display_name }} invites you to a call", Body = "" },
                                Enabled = false
                            },
                            CallLiveStarted = new EventNotificationSettingsRequest
                            {
                                APNS = new APNSPayload { Title = "{{ user.display_name }} invites you to a call", Body = "" },
                                Enabled = false
                            },
                            CallRing = new EventNotificationSettingsRequest
                            {
                                APNS = new APNSPayload { Title = "{{ user.display_name }} invites you to a call", Body = "" },
                                Enabled = false
                            }
                        },
                        Grants = new Dictionary<string, List<string>>
                        {
                            ["admin"] = new List<string> { "send-audio", "send-video", "mute-users" },
                            ["user"] = new List<string> { "send-audio", "send-video" }
                        }
                    },
                    null);

                Assert.That(createResp.Data, Is.Not.Null);
                Assert.That(createResp.Data!.Name, Is.EqualTo(callTypeName));
                Assert.That(createResp.Data.Settings.Audio.MicDefaultOn, Is.True);
                Assert.That(createResp.Data.Settings.Audio.DefaultDevice, Is.EqualTo("speaker"));
                Assert.That(createResp.Data.Settings.Screensharing.AccessRequestEnabled, Is.False);

                // Video call types have eventual consistency; wait before update/read
                await Task.Delay(20000);

                // Update call type (with retry for eventual consistency)
                UpdateCallTypeResponse? updateData = null;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        var updateResp = await StreamClient.MakeRequestAsync<UpdateCallTypeRequest, UpdateCallTypeResponse>(
                            "PUT",
                            "/api/v2/video/calltypes/{name}",
                            null,
                            new UpdateCallTypeRequest
                            {
                                Settings = new CallSettingsRequest
                                {
                                    Audio = new AudioSettingsRequest
                                    {
                                        DefaultDevice = "earpiece",
                                        MicDefaultOn = false
                                    },
                                    Recording = new RecordSettingsRequest
                                    {
                                        Mode = "disabled"
                                    },
                                    Backstage = new BackstageSettingsRequest
                                    {
                                        Enabled = true
                                    }
                                },
                                Grants = new Dictionary<string, List<string>>
                                {
                                    ["host"] = new List<string> { "join-backstage" }
                                }
                            },
                            new Dictionary<string, string> { ["name"] = callTypeName });

                        updateData = updateResp.Data;
                        break;
                    }
                    catch
                    {
                        if (attempt < 2)
                            await Task.Delay(5000);
                    }
                }

                Assert.That(updateData, Is.Not.Null);
                Assert.That(updateData!.Settings.Audio.MicDefaultOn, Is.False);
                Assert.That(updateData.Settings.Audio.DefaultDevice, Is.EqualTo("earpiece"));
                Assert.That(updateData.Settings.Recording.Mode, Is.EqualTo("disabled"));
                Assert.That(updateData.Settings.Backstage.Enabled, Is.True);
                Assert.That(updateData.Grants.ContainsKey("host"), Is.True);
                Assert.That(updateData.Grants["host"], Does.Contain("join-backstage"));

                // Read call type (with retry)
                GetCallTypeResponse? getCallTypeData = null;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        var getResp = await StreamClient.MakeRequestAsync<object, GetCallTypeResponse>(
                            "GET",
                            "/api/v2/video/calltypes/{name}",
                            null,
                            null,
                            new Dictionary<string, string> { ["name"] = callTypeName });

                        getCallTypeData = getResp.Data;
                        break;
                    }
                    catch
                    {
                        if (attempt < 2)
                            await Task.Delay(5000);
                    }
                }

                Assert.That(getCallTypeData, Is.Not.Null);
                Assert.That(getCallTypeData!.Name, Is.EqualTo(callTypeName));
            }
            finally
            {
                // Wait before delete for eventual consistency, then retry delete
                await Task.Delay(6000);
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    try
                    {
                        await StreamClient.MakeRequestAsync<object, Response>(
                            "DELETE",
                            "/api/v2/video/calltypes/{name}",
                            null,
                            null,
                            new Dictionary<string, string> { ["name"] = callTypeName });
                        break;
                    }
                    catch
                    {
                        if (attempt < 4)
                            await Task.Delay(3000);
                    }
                }
            }
        }
    }
}
