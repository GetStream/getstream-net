# GetStream .NET SDK

This is the official .NET SDK for GetStream's Feeds API.

For detailed setup instructions, see [Example](samples/ConsoleApp/Program.cs).

## Migrating from stream-chat-net?

If you are coming from [`stream-chat-net`](https://github.com/GetStream/stream-chat-net), we have a detailed migration guide with
side-by-side code examples for every common Chat use case.

See the [Migration Guide](docs/migration-from-stream-chat-net/README.md).

## Environment Setup

### For Development
```bash
export STREAM_API_KEY=your-api-key-here
export STREAM_API_SECRET=your-api-secret-here
```

### For CI/CD
This repository uses the API key `zta48ppyvwet` for continuous integration testing.

## Makefile Commands

The project includes a Makefile for common development tasks:

```bash
# Run the sample application
make sample

# Run unit tests (no credentials needed)
make test

# Run the tests that talk to a live Stream app
make test-integration

# Additional commands available in Makefile
make build     # Build the project
make clean     # Clean build artifacts
```

A fixture in the `Integration` category talks to a live Stream app and needs credentials. `TestBase` carries the category and NUnit inherits it, so deriving from it is enough. `make test` excludes the category; `make test-integration` runs only it.

CI follows the same split:

| When | What runs | Gates |
| --- | --- | --- |
| Pull request | format check, both builds, `make test`, package build | yes, `🧪 Tests` |
| Daily at 13:00 UTC | `make test-integration` | no, a red run opens an issue |
| Release PR merged | nothing on the default branch, the unit lane on `N.x` | `N.x` only |

## Structure

The SDK is organized into several key components:

- `src/` - Core SDK implementation
  - `Client.cs` - Main HTTP client with authentication and request handling
  - `CommonClient.cs` - Shared client functionality and utilities
  - `CustomCode.cs` - Custom implementations and extensions
  - `Feed.cs` - Feed entity and core functionality
  - Several OpenAPI-generated files:
    - `FeedsV3Client.cs` - Generated client with all feeds API methods
    - `models.cs` - Generated response models
    - `requests.cs` - Generated request models
- `tests/` - Comprehensive test suite
  - `FeedClientTests.cs` - Unit tests for feed client
  - `FeedIntegrationTests.cs` - Integration tests
  - `FeedTests.cs` - General feed functionality tests
- `samples/` - Example applications and usage demos
  - `ConsoleApp/` - Console application demonstrating basic usage

## Development

The SDK development workflow:

1. **Core Components**
   - Manual implementation of core client functionality
   - Custom extensions and utilities for .NET-specific features
   - Comprehensive test coverage for all components
   - OpenAPI-generated code for complete API coverage

2. **Testing**
   - Unit tests for individual components
   - Integration tests for end-to-end functionality
   - Sample applications for usage demonstration

3. **Build and Run**
   - Use Makefile commands for common tasks
   - Regular testing and validation
   - Continuous integration checks

The SDK follows .NET best practices and conventions while providing a clean, maintainable codebase for GetStream's Feeds API integration.

## Structured Logging

Pass `Logger` on `StreamOptions` (or `ClientBuilder.Logger(...)`) to receive structured events via `Microsoft.Extensions.Logging.ILogger`. No logger set means no output; the SDK never changes the logger's configured level. Four canonical events, matching the cross-SDK logging spec:

| Event (dotted name, message prefix) | Level | Emitted |
|---|---|---|
| `client.initialized` | INFO | Once, at `BaseClient` construction |
| `http.request.sent` | DEBUG | Before every request is sent |
| `http.response.received` | DEBUG | After any HTTP response, including 4xx/5xx |
| `http.request.failed` | ERROR or DEBUG | ERROR on a final transport failure (no HTTP response received); DEBUG instead, with a `{RetryAttempt}` field, on any attempt that [Retry](#retry-policy) will retry. Never emitted for a final/non-retried 429 (already covered by `http.response.received`). |

.NET's `ILogger` uses PascalCase message-template placeholders (`{Method}`, `{StatusCode}`, ...). Each maps to a canonical snake_case field name used identically across all GetStream SDKs:

| Event | Placeholder | Canonical field |
|---|---|---|
| `client.initialized` | `{SdkName}` | `stream.sdk.name` |
| | `{SdkVersion}` | `stream.sdk.version` |
| | `{MaxConnsPerHost}` | `stream.client.max_conns_per_host` |
| | `{IdleTimeoutSeconds}` | `stream.client.idle_timeout_seconds` |
| | `{ConnectTimeoutSeconds}` | `stream.client.connect_timeout_seconds` |
| | `{RequestTimeoutSeconds}` | `stream.client.request_timeout_seconds` |
| | `{GzipEnabled}` | `stream.client.gzip_enabled` |
| | `{UserHttpClient}` | `stream.client.user_http_client` |
| | `{LogBodies}` | `stream.client.log_bodies` |
| `http.request.sent` | `{Method}` | `method` |
| | `{Path}` | `path` |
| | `{Query}` | `query` (redacted) |
| | `{Body}` | `body` (redacted; only present when `LogBodies=true`) |
| `http.response.received` | `{Method}` | `method` |
| | `{Path}` | `path` |
| | `{StatusCode}` | `status_code` |
| | `{BodySize}` | `body_size` (bytes) |
| | `{DurationMs}` | `duration_ms` |
| | `{Body}` | `body` (redacted; only present when `LogBodies=true`) |
| `http.request.failed` | `{Method}` | `method` |
| | `{Path}` | `path` |
| | `{ErrorType}` | `error.type` (transport failures only; never present on a retried 429 — see below) |
| | `{DurationMs}` | `duration_ms` (transport failures only) |
| | `{Message}` | `error.message` (redacted; transport failures only) |
| | `{RetryAttempt}` | `retry.attempt` (1-indexed; present only when this attempt will be retried) |

`error.type` is one of `connection_reset`, `timeout`, `dns_failure`, `tls_handshake_failed`, `unknown` (see `GetStreamTransportException.ErrorType`) — a closed transport-only enum. A retried HTTP 429 has no transport error at all, so its `http.request.failed` DEBUG line carries only `{Method}`, `{Path}`, `{RetryAttempt}`: never `{ErrorType}`.

**Redaction (always on, no opt-out):** query values for `api_key`/`api_secret`/`token` (case-insensitive) become `<redacted>`; top-level JSON body keys `api_secret`/`token`/`password` become `<redacted>` (shallow, key names are preserved). No header values are ever logged. `error.message` is additionally scrubbed for any `api_key=`/`api_secret=`/`token=` value appearing anywhere in the free-form transport-exception text.

**Bodies are not logged by default.** Set `StreamOptions.LogBodies = true` (or `ClientBuilder.LogBodies(true)`) to opt in; body content is still key-redacted as above. Enabling it emits exactly one WARN line at construction.

## Retry Policy

Auto-retry is opt-in and off by default: with no `Retry` configured, the client performs exactly one attempt and errors surface unchanged.

```csharp
var client = new StreamClient(new StreamOptions
{
    ApiKey = "...",
    ApiSecret = "...",
    Retry = new RetryConfig { Enabled = true, MaxAttempts = 3, MaxBackoff = TimeSpan.FromSeconds(30) },
});
```

When enabled:
- Only `GET`/`HEAD` requests are retried. Writes (`POST`/`PUT`/`PATCH`/`DELETE`) never are.
- Only HTTP 429 (rate limit) and transport-layer failures (connection reset, timeout, DNS, TLS) are retried; any other 4xx/5xx is never retried.
- A 429 marked `unrecoverable` by the backend is never retried.
- `MaxAttempts` is the total attempt budget including the initial request (default `3`: 1 initial + 2 retries).
- The delay before each retry honors a `Retry-After` response header when present, clamped to `MaxBackoff`; otherwise it's full jitter over `[0, min(MaxBackoff, 2^attempt seconds)]`.

## Release Process

Releases are driven by [release-please](https://github.com/googleapis/release-please).

- Merge PRs to `master` with conventional-commit titles, using **Squash and merge**. The
  title becomes the commit subject and decides the next version: `feat:` is a minor,
  `fix:` and `perf:` are a patch, `feat!:` or `<type>(scope)!:` is a major. Other types
  (`chore`, `ci`, `docs`, `test`, `refactor`) ship nothing.
- release-please keeps a Release PR open with the version bump in
  `src/stream-feed-net.csproj` and `CHANGELOG.md`. Never edit `<Version>` by hand.
- Its runs are created held at `action_required` until someone clicks **Approve and
  run**, because release-please opens the PR with `GITHUB_TOKEN`. The unit lane then
  reports `skipped` and `🧪 Tests` goes green without running a test. The skip only applies while the diff is nothing but what release-please writes, down to the version line in each version file, so a code or dependency change pushed onto a Release PR by hand runs the unit lane like any other PR.
- Merging the Release PR creates the tag and the GitHub Release on that merge commit and pushes the package to NuGet, with no further test run: the Release PR adds only the version bump and changelog to an already-tested `master`. A hotfix release from `N.x` runs the unit lane first, since its commits were pushed without a PR. A tag, a GitHub Release and a NuGet push cannot be withdrawn. The publish step builds and packs, so a build that does not compile fails there after the tag exists; the fix ships under the next version, since `publish_tag` rebuilds the same tag.

To retry a NuGet push that failed after the release was tagged, use "Re-run failed jobs"
on that workflow run. Once GitHub has retired the run, dispatch `Release` from `master`
with `publish_tag` set to the tag (for example `v16.1.1`), which packs and pushes that
tag without touching release-please. If the release job fails after the Release PR merged,
the release stays pending and every later push logs a warning naming the commit to go
back to, rather than failing.

To force a specific version, type `Release-As: X.Y.Z` in the commit message box of the
squash dialog when merging a PR; the PR description is not copied there. To hotfix while
`master` carries unreleased work, branch `N.x` from the last tag, cherry-pick the fix,
and merge the Release PR that release-please opens against that branch.
