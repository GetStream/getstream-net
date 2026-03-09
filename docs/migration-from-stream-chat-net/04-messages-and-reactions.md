# Messages and Reactions

This guide shows how to migrate message and reaction operations from `stream-chat-net` to `getstream-net`.

## Send a Message

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var messageClient = clientFactory.GetMessageClient();

// Simple text message
var message = await messageClient.SendMessageAsync("messaging", "general",
    "bob-1", "Hello, world!");
```

**After (getstream-net):**

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
            Text = "Hello, world!",
            UserId = "bob-1"
        }
    });
```

**Key changes:**
- `SendMessageAsync()` moves from `IMessageClient` to `ChatClient`
- User ID and text are no longer positional parameters; they go inside a `SendMessageRequest` wrapping a `MessageRequest`

## Send a Message with Custom Data

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;
using StreamChat.Models;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var messageClient = clientFactory.GetMessageClient();

var msgReq = new MessageRequest { Text = "Check this out!" };
msgReq.SetData("location", "amsterdam");

var message = await messageClient.SendMessageAsync("messaging", "general", msgReq, "bob-1");
```

**After (getstream-net):**

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
            Text = "Check this out!",
            UserId = "bob-1",
            Custom = new Dictionary<string, object> { { "location", "amsterdam" } }
        }
    });
```

**Key changes:**
- Custom fields use the `Custom` dictionary instead of `SetData()`

## Send a Message with Options

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;
using StreamChat.Models;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var messageClient = clientFactory.GetMessageClient();

var msgReq = new MessageRequest { Text = "Silent notification" };
var message = await messageClient.SendMessageAsync("messaging", "general",
    msgReq, "bob-1", new SendMessageOptions { SkipPush = true });
```

**After (getstream-net):**

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
            Text = "Silent notification",
            UserId = "bob-1"
        },
        SkipPush = true
    });
```

**Key changes:**
- Send options like `SkipPush` go directly on `SendMessageRequest` instead of a separate `SendMessageOptions` object

## Send a Thread Reply

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;
using StreamChat.Models;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var messageClient = clientFactory.GetMessageClient();

var reply = new MessageRequest { Text = "Replying to thread!" };
var threadReply = await messageClient.SendMessageToThreadAsync("messaging", "general",
    reply, "jane", parentMessageId, skipPush: false);
```

**After (getstream-net):**

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
            Text = "Replying to thread!",
            UserId = "jane",
            ParentId = parentMessageId
        }
    });
```

**Key changes:**
- No separate `SendMessageToThreadAsync()` method; use `SendMessageAsync()` with `ParentId` on the `MessageRequest`

## Get a Message

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var messageClient = clientFactory.GetMessageClient();

var message = await messageClient.GetMessageAsync(messageId);
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");
var chatClient = new ChatClient(client);

var response = await chatClient.GetMessageAsync(messageId);
```

**Key changes:**
- Method name stays `GetMessageAsync()` but moves from `IMessageClient` to `ChatClient`

## Update a Message

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;
using StreamChat.Models;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var messageClient = clientFactory.GetMessageClient();

var updatedMsg = new MessageRequest
{
    Id = messageId,
    Text = "Updated text",
    UserId = "bob-1"
};
await messageClient.UpdateMessageAsync(updatedMsg);
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");
var chatClient = new ChatClient(client);

await chatClient.UpdateMessageAsync(messageId, new UpdateMessageRequest
{
    Message = new MessageRequest
    {
        Text = "Updated text",
        UserId = "bob-1"
    }
});
```

**Key changes:**
- Message ID is a path parameter instead of a property on the request
- The message body is wrapped in `UpdateMessageRequest`

## Partial Update a Message

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;
using StreamChat.Models;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var messageClient = clientFactory.GetMessageClient();

await messageClient.UpdateMessagePartialAsync(messageId, new MessagePartialUpdateRequest
{
    Set = new Dictionary<string, object> { { "text", "Partially updated" } },
    Unset = new[] { "custom_field" },
    UserId = "bob-1"
});
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");
var chatClient = new ChatClient(client);

await chatClient.UpdateMessagePartialAsync(messageId, new UpdateMessagePartialRequest
{
    Set = new Dictionary<string, object> { { "text", "Partially updated" } },
    Unset = new List<string> { "custom_field" },
    UserId = "bob-1"
});
```

**Key changes:**
- `MessagePartialUpdateRequest` becomes `UpdateMessagePartialRequest`
- `Unset` uses `List<string>` instead of `string[]`

## Delete a Message

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var messageClient = clientFactory.GetMessageClient();

// Soft delete
await messageClient.DeleteMessageAsync(messageId);

// Hard delete
await messageClient.DeleteMessageAsync(messageId, hardDelete: true);
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");
var chatClient = new ChatClient(client);

// Soft delete
await chatClient.DeleteMessageAsync(messageId);

// Hard delete (pass as query parameter)
await chatClient.DeleteMessageAsync(messageId);
```

**Key changes:**
- Method moves from `IMessageClient` to `ChatClient`

## Send a Reaction

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var reactionClient = clientFactory.GetReactionClient();

await reactionClient.SendReactionAsync(messageId, "like", "bob-1");
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");
var chatClient = new ChatClient(client);

await chatClient.SendReactionAsync(messageId, new SendReactionRequest
{
    Reaction = new ReactionRequest
    {
        Type = "like",
        UserId = "bob-1"
    }
});
```

**Key changes:**
- Positional parameters become a nested `SendReactionRequest` > `ReactionRequest`
- `SendReactionAsync()` moves from `IReactionClient` to `ChatClient`

## Send a Reaction with Score

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;
using StreamChat.Models;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var reactionClient = clientFactory.GetReactionClient();

await reactionClient.SendReactionAsync(messageId, new ReactionRequest
{
    Type = "like",
    UserId = "bob-1",
    Score = 5
}, skipPush: false);
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");
var chatClient = new ChatClient(client);

await chatClient.SendReactionAsync(messageId, new SendReactionRequest
{
    Reaction = new ReactionRequest
    {
        Type = "like",
        UserId = "bob-1",
        Score = 5
    },
    SkipPush = false,
    EnforceUnique = false
});
```

**Key changes:**
- `skipPush` moves from a method parameter to `SkipPush` on `SendReactionRequest`
- `EnforceUnique` option is available to replace existing reactions from the same user

## List Reactions

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var reactionClient = clientFactory.GetReactionClient();

var reactions = await reactionClient.GetReactionsAsync(messageId, offset: 0, limit: 10);
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");
var chatClient = new ChatClient(client);

var response = await chatClient.GetReactionsAsync(messageId);
```

**Key changes:**
- `GetReactionsAsync()` moves from `IReactionClient` to `ChatClient`

## Delete a Reaction

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var reactionClient = clientFactory.GetReactionClient();

await reactionClient.DeleteReactionAsync(messageId, "like", "bob-1");
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");
var chatClient = new ChatClient(client);

await chatClient.DeleteReactionAsync(messageId, "like");
```

**Key changes:**
- `DeleteReactionAsync()` moves from `IReactionClient` to `ChatClient`
- User ID is passed as a query parameter rather than a positional argument
