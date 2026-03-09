# Channels

This guide shows how to migrate channel operations from `stream-chat-net` to `getstream-net`.

## Create or Get a Channel

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;
using StreamChat.Models;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var channelClient = clientFactory.GetChannelClient();

// With channel ID and members
var channel = await channelClient.GetOrCreateAsync("messaging", "general",
    createdBy: "admin-user", "user-1", "user-2");

// With detailed request
var channel = await channelClient.GetOrCreateAsync("messaging", "general",
    new ChannelGetRequest
    {
        Data = new ChannelRequest
        {
            CreatedBy = new UserRequest { Id = "admin-user" },
            Members = new[] { new ChannelMember { UserId = "user-1" } }
        }
    });
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");
var chatClient = new ChatClient(client);

// With channel ID
var response = await chatClient.GetOrCreateChannelAsync("messaging", "general",
    new ChannelGetOrCreateRequest
    {
        Data = new ChannelInput
        {
            CreatedById = "admin-user",
            Members = new List<ChannelMemberRequest>
            {
                new ChannelMemberRequest { UserId = "user-1" },
                new ChannelMemberRequest { UserId = "user-2" }
            }
        }
    });
```

**Key changes:**
- `GetOrCreateAsync()` on `IChannelClient` becomes `GetOrCreateChannelAsync()` on `ChatClient`
- `ChannelGetRequest` becomes `ChannelGetOrCreateRequest`; `ChannelRequest` becomes `ChannelInput`
- `CreatedBy` (a `UserRequest` object) becomes `CreatedById` (a string)
- Members use `ChannelMemberRequest` objects instead of `ChannelMember`

## Create a Distinct (Member-based) Channel

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;
using StreamChat.Models;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var channelClient = clientFactory.GetChannelClient();

var channel = await channelClient.GetOrCreateAsync("messaging",
    new ChannelGetRequest
    {
        Data = new ChannelRequest
        {
            CreatedBy = new UserRequest { Id = "user-1" },
            Members = new[]
            {
                new ChannelMember { UserId = "user-1" },
                new ChannelMember { UserId = "user-2" }
            }
        }
    });
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");
var chatClient = new ChatClient(client);

var response = await chatClient.GetOrCreateDistinctChannelAsync("messaging",
    new ChannelGetOrCreateRequest
    {
        Data = new ChannelInput
        {
            CreatedById = "user-1",
            Members = new List<ChannelMemberRequest>
            {
                new ChannelMemberRequest { UserId = "user-1" },
                new ChannelMemberRequest { UserId = "user-2" }
            }
        }
    });
```

**Key changes:**
- No-ID channel creation uses `GetOrCreateDistinctChannelAsync()` instead of `GetOrCreateAsync()` without an ID

## Add and Remove Members

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var channelClient = clientFactory.GetChannelClient();

// Add members
await channelClient.AddMembersAsync("messaging", "general", "user-3", "user-4");

// Remove members
await channelClient.RemoveMembersAsync("messaging", "general",
    new[] { "user-3", "user-4" }, msg: null);
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");
var chatClient = new ChatClient(client);

// Add members
await chatClient.UpdateChannelAsync("messaging", "general", new UpdateChannelRequest
{
    AddMembers = new List<ChannelMemberRequest>
    {
        new ChannelMemberRequest { UserId = "user-3" },
        new ChannelMemberRequest { UserId = "user-4" }
    }
});

// Remove members
await chatClient.UpdateChannelAsync("messaging", "general", new UpdateChannelRequest
{
    RemoveMembers = new List<string> { "user-3", "user-4" }
});
```

**Key changes:**
- Dedicated `AddMembersAsync()`/`RemoveMembersAsync()` methods become part of `UpdateChannelAsync()` with `AddMembers`/`RemoveMembers` properties
- Members to add use `ChannelMemberRequest` objects; members to remove use string user IDs

## Query Channels

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;
using StreamChat.Models;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var channelClient = clientFactory.GetChannelClient();

var response = await channelClient.QueryChannelsAsync(new QueryChannelsOptions
{
    Filter = new Dictionary<string, object>
    {
        { "type", "messaging" },
        { "members", new Dictionary<string, object> { { "$in", new[] { "user-1" } } } }
    },
    Sort = new[] { new SortParameter { Field = "last_message_at", Direction = SortDirection.Descending } },
    Limit = 10
});
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");
var chatClient = new ChatClient(client);

var response = await chatClient.QueryChannelsAsync(new QueryChannelsRequest
{
    FilterConditions = new Dictionary<string, object>
    {
        { "type", "messaging" },
        { "members", new Dictionary<string, object> { { "$in", new[] { "user-1" } } } }
    },
    Sort = new List<SortParamRequest>
    {
        new SortParamRequest { Field = "last_message_at", Direction = -1 }
    },
    Limit = 10
});
```

**Key changes:**
- `QueryChannelsAsync()` moves from `IChannelClient` to `ChatClient`
- `Filter` becomes `FilterConditions`
- Sort uses `SortParamRequest` with `Direction = -1` (descending) or `1` (ascending) instead of `SortDirection` enum

## Update a Channel

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;
using StreamChat.Models;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var channelClient = clientFactory.GetChannelClient();

await channelClient.UpdateAsync("messaging", "general", new ChannelUpdateRequest
{
    Data = new ChannelRequest { Name = "General Chat" },
    Message = new MessageRequest { Text = "Channel name updated", UserId = "admin-user" }
});
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");
var chatClient = new ChatClient(client);

await chatClient.UpdateChannelAsync("messaging", "general", new UpdateChannelRequest
{
    Data = new ChannelInputRequest
    {
        Custom = new Dictionary<string, object> { { "name", "General Chat" } }
    },
    Message = new MessageRequest { Text = "Channel name updated", UserId = "admin-user" }
});
```

**Key changes:**
- `UpdateAsync()` becomes `UpdateChannelAsync()` on `ChatClient`
- `ChannelRequest` becomes `ChannelInputRequest`; standard fields like `Name` go in the `Custom` dictionary

## Partial Update a Channel

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;
using StreamChat.Models;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var channelClient = clientFactory.GetChannelClient();

await channelClient.PartialUpdateAsync("messaging", "general",
    new PartialUpdateChannelRequest
    {
        Set = new Dictionary<string, object> { { "color", "blue" } },
        Unset = new[] { "old_field" }
    });
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");
var chatClient = new ChatClient(client);

await chatClient.UpdateChannelPartialAsync("messaging", "general",
    new UpdateChannelPartialRequest
    {
        Set = new Dictionary<string, object> { { "color", "blue" } },
        Unset = new List<string> { "old_field" },
        UserId = "admin-user"
    });
```

**Key changes:**
- `PartialUpdateAsync()` becomes `UpdateChannelPartialAsync()`
- `PartialUpdateChannelRequest` becomes `UpdateChannelPartialRequest`
- New SDK requires `UserId` on the partial update request

## Delete a Channel

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var channelClient = clientFactory.GetChannelClient();

// Soft delete
await channelClient.DeleteAsync("messaging", "general");

// Batch delete
await channelClient.DeleteChannelsAsync(
    new[] { "messaging:general", "messaging:support" }, hardDelete: true);
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");
var chatClient = new ChatClient(client);

// Single delete
await chatClient.DeleteChannelAsync("messaging", "general");

// Batch delete
await chatClient.DeleteChannelsAsync(new DeleteChannelsRequest
{
    Cids = new List<string> { "messaging:general", "messaging:support" },
    HardDelete = true
});
```

**Key changes:**
- `DeleteAsync()` becomes `DeleteChannelAsync()`
- Batch delete uses `DeleteChannelsRequest` with a `Cids` list instead of positional parameters

## Truncate a Channel

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;
using StreamChat.Models;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var channelClient = clientFactory.GetChannelClient();

await channelClient.TruncateAsync("messaging", "general",
    new TruncateOptions { HardDelete = true });
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");
var chatClient = new ChatClient(client);

await chatClient.TruncateChannelAsync("messaging", "general",
    new TruncateChannelRequest { HardDelete = true });
```

**Key changes:**
- `TruncateAsync()` becomes `TruncateChannelAsync()`
- `TruncateOptions` becomes `TruncateChannelRequest`
