# Setup and Authentication

This guide shows how to migrate setup and authentication code from `stream-chat-net` to `getstream-net`.

## Installation

**Before (stream-chat-net):**

```bash
dotnet add package stream-chat-net
```

**After (getstream-net):**

```bash
dotnet add package getstream-net
```

**Key changes:**
- The NuGet package name changes from `stream-chat-net` to `getstream-net`

## Client Initialization

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");

// Get individual clients
var userClient = clientFactory.GetUserClient();
var channelClient = clientFactory.GetChannelClient();
var messageClient = clientFactory.GetMessageClient();
var reactionClient = clientFactory.GetReactionClient();
var deviceClient = clientFactory.GetDeviceClient();
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

// Option 1: Provide credentials directly
var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");

// Option 2: Use environment variables (STREAM_API_KEY, STREAM_API_SECRET)
var clientFromEnv = new ClientBuilder().Build();

// Access sub-clients
var chatClient = new ChatClient(client);
var moderationClient = new ModerationClient(client);
```

**Key changes:**
- Namespace changes from `StreamChat.Clients` to `GetStream` and `GetStream.Models`
- Single `StreamClient` replaces the factory pattern; sub-clients wrap it for domain-specific methods
- Environment variables are `STREAM_API_KEY` and `STREAM_API_SECRET` instead of `STREAM_KEY` and `STREAM_SECRET`

**Available sub-clients and builder methods:**

| Sub-client | Direct | Builder |
|------------|--------|---------|
| Chat | `new ChatClient(client)` | `builder.BuildChatClient()` |
| Video | `new VideoClient(client)` | `builder.BuildVideoClient()` |
| Moderation | `new ModerationClient(client)` | `builder.BuildModerationClient()` |
| Feeds | `new FeedsV3Client(client)` | `builder.BuildFeedsClient()` |

## Client Initialization with Options

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret",
    opts => opts.Timeout = TimeSpan.FromSeconds(5));
```

**After (getstream-net):**

```csharp
using GetStream;

var client = new ClientBuilder()
    .ApiKey("your-api-key")
    .ApiSecret("your-api-secret")
    .BaseUrl("https://chat.stream-io-api.com")
    .Build();
```

**Key changes:**
- `ClientBuilder` provides a fluent API for configuration instead of an options callback

## Token Generation

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var userClient = clientFactory.GetUserClient();

// Without expiration
var token = userClient.CreateToken("user-id");

// With expiration
var token = userClient.CreateToken("user-id", expiration: DateTimeOffset.UtcNow.AddHours(1));
```

**After (getstream-net):**

```csharp
using GetStream;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");

// Without expiration
var token = client.CreateUserToken("user-id");

// With expiration
var token = client.CreateUserToken("user-id", expiration: DateTimeOffset.UtcNow.AddHours(1));
```

**Key changes:**
- `CreateToken()` moves from `IUserClient` to the main `StreamClient` as `CreateUserToken()`
- No need to get a separate user client just for token generation
