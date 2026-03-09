# Migrating from stream-chat-net to getstream-net

## Why Migrate?

- `getstream-net` is the actively developed, long-term-supported SDK
- Covers Chat, Video, Moderation, and Feeds in a single package
- Strongly typed models generated from the official OpenAPI spec
- `stream-chat-net` will enter maintenance mode (critical fixes only)

## Key Differences

| Aspect | stream-chat-net | getstream-net |
|--------|----------------|---------------|
| Package | `stream-chat-net` (NuGet) | `getstream-net` (NuGet) |
| Namespace | `StreamChat.Clients`, `StreamChat.Models` | `GetStream`, `GetStream.Models` |
| Client init | `new StreamClientFactory(key, secret)` | `new StreamClient(apiKey, apiSecret)` or `new ClientBuilder().Build()` |
| Architecture | Factory + per-domain clients (`GetUserClient()`, `GetChannelClient()`) | Single `StreamClient` + sub-clients (`ChatClient`, `ModerationClient`) |
| Models | `SetData()` for custom fields | `Custom` dictionary property |
| Responses | `ApiResponse` with `GetRateLimit()` | `StreamResponse<T>` with `Data`, `Duration`, `Error` |
| Token generation | `userClient.CreateToken(userId)` | `client.CreateUserToken(userId)` |

## Quick Example

**Before:**

```csharp
using StreamChat.Clients;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var messageClient = clientFactory.GetMessageClient();

var message = await messageClient.SendMessageAsync("messaging", "general",
    "bob-1", "Hello from the old SDK!");
```

**After:**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");
var chatClient = new ChatClient(client);

var response = await chatClient.SendMessageAsync("messaging", "general",
    new SendMessageRequest
    {
        Message = new MessageRequest
        {
            Text = "Hello from the new SDK!",
            UserId = "bob-1"
        }
    });
```

## Migration Guides by Topic

| # | Topic | File |
|---|-------|------|
| 1 | [Setup and Authentication](01-setup-and-auth.md) | Client init, tokens |
| 2 | [Users](02-users.md) | Upsert, query, update, delete |
| 3 | [Channels](03-channels.md) | Create, query, members, update |
| 4 | [Messages and Reactions](04-messages-and-reactions.md) | Send, reply, react |
| 5 | [Moderation](05-moderation.md) | Ban, mute, moderators |
| 6 | [Devices](06-devices.md) | Push device management |

## Notes

- `stream-chat-net` is not going away. Your existing integration will keep working.
- The new SDK uses typed request/response classes generated from the OpenAPI spec.
- If you find a use case missing from this guide, please open an issue.
