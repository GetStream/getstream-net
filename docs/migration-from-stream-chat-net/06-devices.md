# Devices

This guide shows how to migrate device management operations from `stream-chat-net` to `getstream-net`.

## Add a Device (Firebase)

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;
using StreamChat.Models;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var deviceClient = clientFactory.GetDeviceClient();

await deviceClient.AddDeviceAsync(new Device
{
    Id = "firebase-device-token",
    PushProvider = PushProvider.Firebase,
    UserId = "bob-1"
});
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");

await client.CreateDeviceAsync(new CreateDeviceRequest
{
    ID = "firebase-device-token",
    PushProvider = "firebase",
    UserID = "bob-1"
});
```

**Key changes:**
- `AddDeviceAsync()` on `IDeviceClient` becomes `CreateDeviceAsync()` on `StreamClient`
- `Device` model becomes `CreateDeviceRequest`
- `PushProvider` is a plain string (`"firebase"`, `"apn"`, `"huawei"`, `"xiaomi"`) instead of an enum

## Add a Device (Apple Push Notification)

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;
using StreamChat.Models;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var deviceClient = clientFactory.GetDeviceClient();

await deviceClient.AddDeviceAsync(new Device
{
    Id = "apn-device-token",
    PushProvider = PushProvider.APN,
    UserId = "jane"
});
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");

await client.CreateDeviceAsync(new CreateDeviceRequest
{
    ID = "apn-device-token",
    PushProvider = "apn",
    UserID = "jane"
});

// For VoIP push tokens
await client.CreateDeviceAsync(new CreateDeviceRequest
{
    ID = "voip-device-token",
    PushProvider = "apn",
    UserID = "jane",
    VoipToken = true
});
```

**Key changes:**
- New SDK adds `VoipToken` boolean for Apple VoIP push tokens

## List Devices

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var deviceClient = clientFactory.GetDeviceClient();

var devices = await deviceClient.GetDevicesAsync("bob-1");
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");

var response = await client.ListDevicesAsync(new { user_id = "bob-1" });
```

**Key changes:**
- `GetDevicesAsync()` on `IDeviceClient` becomes `ListDevicesAsync()` on `StreamClient`
- User ID is passed as a query parameter object instead of a positional argument

## Delete a Device

**Before (stream-chat-net):**

```csharp
using StreamChat.Clients;

var clientFactory = new StreamClientFactory("your-api-key", "your-api-secret");
var deviceClient = clientFactory.GetDeviceClient();

await deviceClient.RemoveDeviceAsync("firebase-device-token", "bob-1");
```

**After (getstream-net):**

```csharp
using GetStream;
using GetStream.Models;

var client = new StreamClient(apiKey: "your-api-key", apiSecret: "your-api-secret");

await client.DeleteDeviceAsync(new { id = "firebase-device-token", user_id = "bob-1" });
```

**Key changes:**
- `RemoveDeviceAsync()` on `IDeviceClient` becomes `DeleteDeviceAsync()` on `StreamClient`
- Parameters are passed as a query parameter object instead of positional arguments

## Method Mapping Summary

| Operation | stream-chat-net | getstream-net |
|-----------|----------------|---------------|
| Add device | `deviceClient.AddDeviceAsync(Device)` | `client.CreateDeviceAsync(CreateDeviceRequest)` |
| List devices | `deviceClient.GetDevicesAsync(userId)` | `client.ListDevicesAsync(new { user_id })` |
| Delete device | `deviceClient.RemoveDeviceAsync(id, userId)` | `client.DeleteDeviceAsync(new { id, user_id })` |
