# Users

This guide shows how to migrate user operations from `stream-chat-net` to `getstream-net`.

## Upsert a Single User

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;
using StreamChat.Models;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var userClient = clientFactory.GetUserClient();

var user = new UserRequest
{
    Id = "bob-1",
    Role = Role.Admin,
    Teams = new[] { "red", "blue" }
};
user.SetData("age", 27);

var response = await userClient.UpsertAsync(user);
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");

var response = await client.UpdateUsersAsync(new UpdateUsersRequest
{
    Users = new Dictionary<string, UserRequest>
    {
        ["bob-1"] = new UserRequest
        {
            Id = "bob-1",
            Role = "admin",
            Teams = new List<string> { "red", "blue" },
            Custom = new Dictionary<string, object> { { "age", 27 } }
        }
    }
});
```

**Key changes:**
- `UpsertAsync()` on `IUserClient` becomes `UpdateUsersAsync()` on `StreamClient` with a dictionary-based request
- Custom data uses the `Custom` dictionary instead of `SetData()`
- Roles are plain strings instead of `Role.*` constants

## Batch Upsert Users

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;
using StreamChat.Models;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var userClient = clientFactory.GetUserClient();

var jane = new UserRequest { Id = "jane" };
var june = new UserRequest { Id = "june" };
var users = await userClient.UpsertManyAsync(new[] { jane, june });
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");

var response = await client.UpdateUsersAsync(new UpdateUsersRequest
{
    Users = new Dictionary<string, UserRequest>
    {
        ["jane"] = new UserRequest { Id = "jane" },
        ["june"] = new UserRequest { Id = "june" }
    }
});
```

**Key changes:**
- `UpsertManyAsync()` with an array becomes `UpdateUsersAsync()` with a dictionary keyed by user ID
- The same method handles both single and batch upserts

## Query Users

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;
using StreamChat.Models;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var userClient = clientFactory.GetUserClient();

var response = await userClient.QueryAsync(new QueryUserOptions
{
    Filter = new Dictionary<string, object>
    {
        { "role", new Dictionary<string, object> { { "$eq", "admin" } } }
    },
    Sort = new[] { new SortParameter { Field = "created_at", Direction = SortDirection.Descending } },
    Offset = 0,
    Limit = 10
});
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");

var response = await client.QueryUsersAsync(new
{
    filter_conditions = new Dictionary<string, object>
    {
        { "role", new Dictionary<string, object> { { "$eq", "admin" } } }
    },
    sort = new[] { new { field = "created_at", direction = -1 } },
    offset = 0,
    limit = 10
});
```

**Key changes:**
- `QueryAsync()` on `IUserClient` becomes `QueryUsersAsync()` on `StreamClient`
- Uses anonymous objects or dictionaries for filter/sort parameters

## Partial Update User

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;
using StreamChat.Models;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var userClient = clientFactory.GetUserClient();

await userClient.UpdatePartialAsync(new UserPartialRequest
{
    Id = "bob-1",
    Set = new Dictionary<string, object> { { "age", 28 }, { "city", "Amsterdam" } },
    Unset = new[] { "nickname" }
});
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");

await client.UpdateUsersPartialAsync(new UpdateUsersPartialRequest
{
    Users = new List<UpdateUserPartialRequest>
    {
        new UpdateUserPartialRequest
        {
            Id = "bob-1",
            Set = new Dictionary<string, object> { { "age", 28 }, { "city", "Amsterdam" } },
            Unset = new List<string> { "nickname" }
        }
    }
});
```

**Key changes:**
- `UpdatePartialAsync()` on `IUserClient` becomes `UpdateUsersPartialAsync()` on `StreamClient`
- Request wraps partials in a `Users` list, enabling batch partial updates in one call

## Deactivate User

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var userClient = clientFactory.GetUserClient();

await userClient.DeactivateAsync("bob-1", markMessagesDeleted: true, createdById: "admin-user");
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");

await client.DeactivateUserAsync("bob-1", new DeactivateUserRequest
{
    MarkMessagesDeleted = true,
    CreatedById = "admin-user"
});
```

**Key changes:**
- Positional parameters become a `DeactivateUserRequest` object
- User ID is a separate path parameter

## Reactivate User

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var userClient = clientFactory.GetUserClient();

await userClient.ReactivateAsync("bob-1", restoreMessages: true, name: "Bob", createdById: "admin-user");
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");

await client.ReactivateUserAsync("bob-1", new ReactivateUserRequest
{
    RestoreMessages = true,
    Name = "Bob",
    CreatedById = "admin-user"
});
```

**Key changes:**
- Positional parameters become a `ReactivateUserRequest` object

## Delete Users

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;
using StreamChat.Models;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var userClient = clientFactory.GetUserClient();

// Single user delete
await userClient.DeleteAsync("bob-1", markMessagesDeleted: true, hardDelete: false);

// Batch delete
await userClient.DeleteManyAsync(new DeleteUsersRequest
{
    UserIds = new[] { "bob-1", "jane" },
    User = DeleteStrategy.SoftDelete,
    Messages = DeleteStrategy.HardDelete
});
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");

// Batch delete (all deletions use the batch endpoint)
await client.DeleteUsersAsync(new DeleteUsersRequest
{
    UserIds = new List<string> { "bob-1", "jane" },
    User = "soft",       // "soft" or "hard"
    Messages = "hard"    // "soft" or "hard"
});
```

**Key changes:**
- Single and batch deletes both use `DeleteUsersAsync()` with a `DeleteUsersRequest`
- Delete strategies are plain strings (`"soft"`, `"hard"`) instead of enum values
