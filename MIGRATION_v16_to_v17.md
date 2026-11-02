# Migrating from v16 to v17

v17 regenerates the client from the current v2 serverside spec (FLU-1105). **HTTP JSON for API fields is unchanged.** Enum fields are string-backed structs, and a string still assigns. The compile breaks are removed request types and two campaign return types.

This release also includes the DateTime serialization fix that was going to ship as 16.2.2.

Full diff: [v16.2.1...v17.0.0](https://github.com/GetStream/getstream-net/compare/v16.2.1...v17.0.0).

## Installation

```xml
<PackageReference Include="getstream-net" Version="17.0.0" />
```

## DateTime values

The SDK sends `DateTime` as RFC 3339 UTC strings instead of nanosecond numbers, including `DateTime` values inside custom data. A `DateTime` with `DateTimeKind.Unspecified` is treated as UTC.

## Campaigns

`UpdateCampaignAsync` returns `UpdateCampaignResponse`. `StopCampaignAsync` returns `StopCampaignResponse` and no longer takes `StopCampaignRequest`. The campaign is nested.

```csharp
// v16
CampaignResponse updated = (await chat.UpdateCampaignAsync(id, request)).Data;
await chat.StopCampaignAsync(id, new StopCampaignRequest());

// v17
CampaignResponse updated = (await chat.UpdateCampaignAsync(id, request)).Data.Campaign;
await chat.StopCampaignAsync(id);
CampaignResponse stopped = (await chat.StopCampaignAsync(id)).Data.Campaign;
```

`StopCampaignRequest` is removed.

## Commit message

`CommitMessageRequest` is removed. `CommitMessageAsync` takes an optional `object` body, which you can omit.

```csharp
await chat.CommitMessageAsync(id);
```

## Other removed request types

These endpoints no longer have a body type. Drop the argument, or pass `null`.

- `EndCallRequest`
- `CancelImportV2TaskRequest`
- `ExportFeedUserDataRequest`
- `RestoreFeedGroupRequest`
- `StartHLSBroadcastingRequest`
- `StartPolicyTestRunRequest`
- `StopAllRTMPBroadcastsRequest`
- `StopFrameRecordingRequest`
- `StopHLSBroadcastingRequest`
- `VideoEndCallRequestPayload`
- `VideoKickUserRequestPayload`

`IntentBufferedItem` and `IntentDetectedEvent` are removed. `moderation.intent_detected` is no longer a known webhook type.

## Named enums

New types such as `PushLevel`, `ChannelModerationBehavior`, and `ActivityVisibility` are `readonly struct` values with an implicit conversion from `string`. Existing string assignments still compile. Prefer the constants:

```csharp
request.AutomodBehavior = ChannelModerationBehavior.FLAG;
request.PushLevel = PushLevel.ALL;
```

`ChannelOwnCapability`, `FeedOwnCapability`, and `OwnCapability` changed from empty classes to these structs. `new ChannelOwnCapability()` no longer compiles. Use a constant or `new ChannelOwnCapability("send-message")`.

## When to stay on v16

Stay on **16.2.1** until you can fix the compile errors above. The server does not require v17. Staying there also keeps the old nanosecond `DateTime` JSON.
