# Moderation

This guide shows how to migrate moderation operations from `stream-chat-net` to `getstream-net`.

## Add Moderators

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var channelClient = clientFactory.GetChannelClient();

await channelClient.AddModeratorsAsync("messaging", "general", new[] { "jane", "june" });
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");
var chatClient = new ChatClient(client);

await chatClient.UpdateChannelAsync("messaging", "general", new UpdateChannelRequest
{
    AddModerators = new List<string> { "jane", "june" }
});
```

**Key changes:**
- Dedicated `AddModeratorsAsync()` is replaced by `UpdateChannelAsync()` with the `AddModerators` property
- Same pattern applies for `DemoteModeratorsAsync()` using the `DemoteModerators` property

## Demote Moderators

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var channelClient = clientFactory.GetChannelClient();

await channelClient.DemoteModeratorsAsync("messaging", "general", new[] { "jane" });
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");
var chatClient = new ChatClient(client);

await chatClient.UpdateChannelAsync("messaging", "general", new UpdateChannelRequest
{
    DemoteModerators = new List<string> { "jane" }
});
```

**Key changes:**
- Uses `UpdateChannelAsync()` with `DemoteModerators` list instead of a dedicated method

## Ban a User (App-level)

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;
using StreamChat.Models;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var userClient = clientFactory.GetUserClient();

await userClient.BanAsync(new BanRequest
{
    TargetUserId = "bad-user",
    UserId = "admin-user",
    Reason = "spamming",
    Timeout = 60  // minutes
});
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");
var moderationClient = new ModerationClient(client);

await moderationClient.BanAsync(new BanRequest
{
    TargetUserID = "bad-user",
    BannedByID = "admin-user",
    Reason = "spamming",
    Timeout = 60
});
```

**Key changes:**
- `BanAsync()` moves from `IUserClient` to `ModerationClient`
- `UserId` becomes `BannedByID`; `TargetUserId` becomes `TargetUserID` (note the uppercase `ID` suffix)

## Ban a User (Channel-level)

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;
using StreamChat.Models;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var userClient = clientFactory.GetUserClient();

await userClient.BanAsync(new BanRequest
{
    TargetUserId = "bad-user",
    UserId = "admin-user",
    Type = "messaging",
    Id = "general",
    Reason = "off-topic"
});
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");
var moderationClient = new ModerationClient(client);

await moderationClient.BanAsync(new BanRequest
{
    TargetUserID = "bad-user",
    BannedByID = "admin-user",
    ChannelCid = "messaging:general",
    Reason = "off-topic"
});
```

**Key changes:**
- Separate `Type` and `Id` fields become a single `ChannelCid` in `type:id` format

## Unban a User

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;
using StreamChat.Models;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var userClient = clientFactory.GetUserClient();

await userClient.UnbanAsync(new BanRequest
{
    TargetUserId = "bad-user",
    Type = "messaging",
    Id = "general"
});
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");
var moderationClient = new ModerationClient(client);

await moderationClient.UnbanAsync(new UnbanRequest
{
    TargetUserID = "bad-user",
    ChannelCid = "messaging:general"
});
```

**Key changes:**
- `UnbanAsync()` uses its own `UnbanRequest` type instead of reusing `BanRequest`
- Moves from `IUserClient` to `ModerationClient`

## Shadow Ban

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;
using StreamChat.Models;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var userClient = clientFactory.GetUserClient();

await userClient.ShadowBanAsync(new ShadowBanRequest
{
    TargetUserId = "bad-user",
    UserId = "admin-user"
});
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");
var moderationClient = new ModerationClient(client);

await moderationClient.BanAsync(new BanRequest
{
    TargetUserID = "bad-user",
    BannedByID = "admin-user",
    Shadow = true
});
```

**Key changes:**
- No separate `ShadowBanAsync()` method; use `BanAsync()` with `Shadow = true`
- Same applies for removing shadow bans: use `UnbanAsync()` instead of `RemoveShadowBanAsync()`

## Mute a User

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var userClient = clientFactory.GetUserClient();

await userClient.MuteAsync("noisy-user", "admin-user");
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");
var moderationClient = new ModerationClient(client);

await moderationClient.MuteAsync(new MuteRequest
{
    TargetIds = new List<string> { "noisy-user" },
    UserID = "admin-user"
});
```

**Key changes:**
- `MuteAsync()` moves from `IUserClient` to `ModerationClient`
- Takes a `MuteRequest` with `TargetIds` list (supports batch muting) instead of two string arguments

## Unmute a User

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var userClient = clientFactory.GetUserClient();

await userClient.UnmuteAsync("noisy-user", "admin-user");
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");
var moderationClient = new ModerationClient(client);

await moderationClient.UnmuteAsync(new UnmuteRequest
{
    TargetIds = new List<string> { "noisy-user" },
    UserID = "admin-user"
});
```

**Key changes:**
- `UnmuteAsync()` moves from `IUserClient` to `ModerationClient`
- Uses `UnmuteRequest` with `TargetIds` list

## Method Mapping Summary

| Operation | stream-chat-net | getstream-net |
|-----------|----------------|---------------|
| Add moderators | `channelClient.AddModeratorsAsync()` | `chatClient.UpdateChannelAsync()` with `AddModerators` |
| Demote moderators | `channelClient.DemoteModeratorsAsync()` | `chatClient.UpdateChannelAsync()` with `DemoteModerators` |
| Ban user | `userClient.BanAsync()` | `moderationClient.BanAsync()` |
| Unban user | `userClient.UnbanAsync()` | `moderationClient.UnbanAsync()` |
| Shadow ban | `userClient.ShadowBanAsync()` | `moderationClient.BanAsync()` with `Shadow = true` |
| Remove shadow ban | `userClient.RemoveShadowBanAsync()` | `moderationClient.UnbanAsync()` |
| Mute user | `userClient.MuteAsync()` | `moderationClient.MuteAsync()` |
| Unmute user | `userClient.UnmuteAsync()` | `moderationClient.UnmuteAsync()` |
