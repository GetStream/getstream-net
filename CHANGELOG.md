# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Breaking Changes: OpenAPI regen (schema name-collision fixes, CHA-3559)

Regenerated from chat/ after upstream schema name-collision fixes. The JSON wire contract is unchanged for all of these; the breaks are in the generated C# type surface, so consumer code fails to compile until updated.

- `ModerationClient.FlagAsync` return type changed from `FlagResponse` to `FlagItemResponse`. The `ItemId` field is preserved on the new type.
- `ModerationClient.BanAsync` return type changed from `BanResponse` to `ModerationBanResponse`. The endpoint only ever returned `duration`; the other `BanResponse` fields (`Channel`, `User`, `Expires`, `Reason`, `Shadow`, `BannedBy`, `CreatedAt`) were never populated by the server, so reads already returned defaults.
- `ChannelInput.ConfigOverrides` and `ChannelDataUpdate.ConfigOverrides` retyped from `ChannelConfig?` to `ChannelConfigOverrides?` (config overrides now have their own schema, distinct from the full `ChannelConfig`).
- Value-type nullability changes (require `.Value` / `?? default` or `?`-aware access):
  - `DeliveryReceiptsResponse.Enabled`, `ReadReceiptsResponse.Enabled`, `TypingIndicatorsResponse.Enabled`: `bool?` to `bool`.
  - `TranslationSettings.Enabled`: `bool` to `bool?`.
  - `TargetResolution.Bitrate`: `int` to `int?`.

### Added: OpenAPI regen

- New endpoints: `ChatClient.CreateSegmentAsync`, `UpdateSegmentAsync`, `AddSegmentTargetsAsync`; `CommonClient.CancelImportV2TaskAsync`; `FeedsV3Client.GetOrCreateFollowAsync`, `GetOrCreateUnfollowAsync`, `GetUserInterestsAsync`; `ModerationClient.AnalyzeAsync`, `BulkActionAppealsAsync`, `GetSetupSessionAsync`, `UpsertSetupSessionAsync`; `VideoClient.ReportClientCallEventAsync`.
- New webhook events: `moderation.image_analysis.complete`, `moderation.text_analysis.complete`.
- New models and fields backing the above (segments, moderation analyze, flood config, setup sessions, etc.).

### Error Handling (CHA-2958)

Structured exception hierarchy replaces the raw-body-only `GetStreamApiException`. Backend `APIError` envelopes are now parsed into typed fields, transport failures are wrapped, 429s expose a parsed `Retry-After`, and `BaseClient.WaitForTaskAsync` lets callers surface async task failures.

**New exception types (`GetStream` namespace):**

- `GetStreamException`: abstract base.
- `GetStreamApiException`: 4xx/5xx with parsed envelope. Fields: `StatusCode`, `Code`, `Message`, `ExceptionFields` (`IReadOnlyDictionary<string,string>`), `Unrecoverable`, `RawResponseBody`, `MoreInfo`, `Details`.
- `GetStreamRateLimitException : GetStreamApiException`: thrown on HTTP 429. Adds `RetryAfter` (`TimeSpan?`) parsed from the `Retry-After` header (integer seconds and HTTP-date, RFC 7231 §7.1.3).
- `GetStreamTransportException`: wraps network-layer failures (connection reset, timeout, DNS, TLS). Adds `ErrorType` (`connection_reset` | `timeout` | `dns_failure` | `tls_handshake_failed` | `unknown`). The underlying exception is preserved as `InnerException`.
- `GetStreamTaskException`: thrown by `WaitForTaskAsync` when an async task completes with `status = "failed"`. Fields: `TaskId`, `ErrorType`, `Description`, `StackTrace`, `Version`.

**Removed (dead code):**

- `GetStreamAuthenticationException`
- `GetStreamValidationException`
- `GetStreamFeedException`

None of these were ever thrown by the SDK and none were referenced in customer documentation. The audit (2026-05-28) found zero throw sites in `src/` and zero matches in `/docs/data/docs/`. Migration for users who wrote defensive catches against any of them:

```csharp
// before
catch (GetStreamAuthenticationException ex) { ... }

// after
catch (GetStreamApiException ex) when (ex.StatusCode is 401 or 403) { ... }
```

**Behavior changes (no breaking API changes outside the deletions above):**

- HTTP responses with 4xx/5xx are deserialized into the existing `APIError` model and the typed fields flow through to `GetStreamApiException`. If the body cannot be parsed as `APIError`, the SDK still throws `GetStreamApiException` with `Code = 0`, `Message = "failed to parse error response"`, and `RawResponseBody` set to the original payload (spec §6.3).
- Transport-layer failures (`HttpRequestException`, `TaskCanceledException` from the framework `HttpClient.Timeout`, `SocketException`, `TimeoutException`, `IOException`) at the request boundary are caught and re-thrown as `GetStreamTransportException`. The original exception is preserved as `InnerException`. Caller-supplied `CancellationToken` cancellation propagates natively (it is not a transport error).
- `BaseClient.WaitForTaskAsync(taskId, pollInterval?, timeout?, cancellationToken?)` polls `/api/v2/tasks/{id}` until terminal state. Defaults: `pollInterval = 1s`, `timeout = 60s`. Returns the `GetTaskResponse` on completion, throws `GetStreamTaskException` on failure, throws `GetStreamTransportException` with `ErrorType = "timeout"` on timeout.

The unit suite for the above is in `tests/ErrorHandlingTests.cs` and runs without credentials.

### Connection Pooling (CHA-2956)

New `StreamOptions` class for explicit HTTP connection-pool tuning. Five knobs:

- `MaxConnsPerHost` (int, default `5`) → `SocketsHttpHandler.MaxConnectionsPerServer`
- `IdleTimeout` (TimeSpan, default `55s`) → `SocketsHttpHandler.PooledConnectionIdleTimeout`
- `ConnectTimeout` (TimeSpan, default `10s`) → `SocketsHttpHandler.ConnectTimeout`
- `RequestTimeout` (TimeSpan, default `30s`) → `HttpClient.Timeout`
- `KeepAlive`: always on (invariant; no toggle)

Two usage paths:

```csharp
// 1) Direct StreamOptions via the hand-written BaseClient
var client = new BaseClient(new StreamOptions
{
    ApiKey = apiKey,
    ApiSecret = secret,
    MaxConnsPerHost = 10,
    IdleTimeout = TimeSpan.FromSeconds(45),
    ConnectTimeout = TimeSpan.FromSeconds(5),
    RequestTimeout = TimeSpan.FromSeconds(20),
});

// 2) ClientBuilder (knobs apply to the product clients)
var chat = new ClientBuilder()
    .ApiKey(apiKey).ApiSecret(secret)
    .MaxConnsPerHost(10)
    .IdleTimeout(TimeSpan.FromSeconds(45))
    .ConnectTimeout(TimeSpan.FromSeconds(5))
    .RequestTimeout(TimeSpan.FromSeconds(20))
    .BuildChatClient();   // also BuildVideoClient / BuildFeedsClient / BuildModerationClient
```

`ClientBuilder` applies the pool knobs by building the configured transport through the hand-written `BaseClient` and backing each product client (`BuildChatClient`, `BuildVideoClient`, `BuildFeedsClient`, `BuildModerationClient`) with it via their existing `IClient` constructors — no generated code is hand-edited.

Known limitation: `ClientBuilder.Build()` returns the concrete generated `StreamClient`, which has no `StreamOptions`-aware constructor. It routes through the positional ctor and therefore always uses the spec-default pool config; custom knobs set on the builder are NOT applied via `Build()`. Wiring them in requires the chat/ dotnet `common.tpl` template to emit a `StreamClient(StreamOptions)` constructor (tracked separately). Use a product-specific `Build*Client()` to apply custom knobs, or pass `StreamOptions` directly to `new StreamClient(opts)` once the template change lands.

Escape hatch: pass an `HttpClient` via `StreamOptions.HttpClient` or `ClientBuilder.HttpClient(...)` to bypass all 5 knobs. The SDK uses the supplied instance as-is and applies none of its own handler configuration. That includes gzip: the default-built handler enables `AutomaticDecompression = GZip`, but on the escape-hatch path the SDK adds nothing, so the caller owns gzip/decompression (configure it on the handler you pass in if you need it).

Connection age: `PooledConnectionLifetime` is intentionally left unset (framework default `InfiniteTimeSpan`). Pooling relies solely on `PooledConnectionIdleTimeout` for eviction; there is deliberately no hard max-connection-age cap.

Pass an `ILogger` via `StreamOptions.Logger` or `ClientBuilder.Logger(...)` to receive one INFO line on construction.

Per-call `RequestTimeout` override: pass a `CancellationToken` derived from `CancellationTokenSource.CancelAfter(...)` to any `*Async` method.

The `StreamOptions` constructor lives on the hand-written `BaseClient`. The generated clients (`StreamClient`, `FeedsV3Client`, `ModerationClient`) are not hand-edited; they are configured through `BaseClient` / their existing `IClient` constructors, so a future regeneration cannot wipe the pooling wiring.

Backward compatibility: the existing positional constructors (`BaseClient`, `StreamClient`, `FeedsV3Client`, `ModerationClient`) continue to work unchanged and now produce clients wired with the new defaults.

### Added

- Webhook handling spec helpers (CHA-2961): `UnknownEvent` class for forward-compat;
  `GunzipPayload`, `DecodeSqsPayload`, `DecodeSnsPayload` primitives;
  `ParseEvent` (returns typed event or `UnknownEvent`);
  `VerifyAndParseWebhook` HTTP composite; `ParseSqs` / `ParseSns`
  queue composites (no signature: backend emits no HMAC for queue messages today;
  queue transports are secured via AWS IAM access controls).
  Transparent gzip via magic-byte detection.
- New exception class: `Webhook.StreamInvalidWebhookException`, a unified failure type
  for signature mismatch, invalid JSON, missing/non-string `type` field, gzip
  decompression failure, base64 decode failure, or malformed SNS envelope.
- New instance methods on `StreamClient` (via `BaseClient`):
  `VerifySignature(body, signature)` and `VerifyAndParseWebhook(body, signature)` that
  drop the `secret` parameter in favor of the client's stored API secret. Dual API:
  static `Webhook.*` methods that take an explicit secret remain available.
- New instance methods on `StreamClient` (via `BaseClient`): `ParseSqs(string)`,
  `ParseSns(string)` (no signature; AWS IAM).
- Conformance fixture suite under `tests/fixtures/webhooks/`.

### Changed

- No breaking changes.

## [6.0.0] - 2026-03-05

### Breaking Changes

- Type names across all products now follow the OpenAPI spec naming convention: response types are suffixed with `Response`, input types with `Request`. See [MIGRATION_v5_to_v6.md](./MIGRATION_v5_to_v6.md) for the complete rename mapping.
- `Event` (WebSocket envelope type) renamed to `WSEvent`. Base event type renamed from `BaseEvent` to `Event` (with field `type` instead of `T`).
- Event composition changed from monolithic `*Preset` embeds to modular `Has*` types.
- `Pager` renamed to `PagerResponse` and migrated from offset-based to cursor-based pagination (`next`/`prev` tokens).

### Added

- Full product coverage: Chat, Video, Moderation, and Feeds APIs are all supported in a single SDK.
- **Feeds**: activities, feeds, feed groups, follows, comments, reactions, collections, bookmarks, membership levels, feed views, and more.
- **Video**: calls, recordings, transcription, closed captions, SFU, call statistics, user feedback analytics, and more.
- **Moderation**: flags, review queue, moderation rules, config, appeals, moderation logs, and more.
- Push notification types, preferences, and templates.
- Webhook support: `WHEvent` envelope class for receiving webhook payloads, utility methods for decoding and verifying webhook signatures, and a full set of individual typed event classes for every event across all products (Chat, Video, Moderation, Feeds) usable as discriminated event types.
- Cursor-based pagination across all list endpoints.

## [5.1.0] - 2026-02-18

## [4.0.0] - 2025-09-30

## [3.1.0] - 2025-07-15

## [3.0.0] - 2025-06-01

## [2.1.0] - 2025-03-15
