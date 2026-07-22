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

# Run tests
make test

# Additional commands available in Makefile
make build     # Build the project
make clean     # Clean build artifacts
```

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
| `http.request.failed` | ERROR | Only on a transport failure (no HTTP response received) |

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
| | `{ErrorType}` | `error.type` |
| | `{DurationMs}` | `duration_ms` |
| | `{Message}` | `error.message` (redacted) |

`error.type` is one of `connection_reset`, `timeout`, `dns_failure`, `tls_handshake_failed`, `unknown` (see `GetStreamTransportException.ErrorType`).

**Redaction (always on, no opt-out):** query values for `api_key`/`api_secret`/`token` (case-insensitive) become `<redacted>`; top-level JSON body keys `api_secret`/`token`/`password` become `<redacted>` (shallow, key names are preserved). No header values are ever logged. `error.message` is additionally scrubbed for any `api_key=`/`api_secret=`/`token=` value appearing anywhere in the free-form transport-exception text.

**Bodies are not logged by default.** Set `StreamOptions.LogBodies = true` (or `ClientBuilder.LogBodies(true)`) to opt in; body content is still key-redacted as above. Enabling it emits exactly one WARN line at construction.

## Release Process

Releases use two paths, both handled by `.github/workflows/release.yml`:

- **Default**: automatic release when a PR is merged to `master`. The PR title drives the semver bump.
- **Fallback**: manual release via the `Release` workflow's `workflow_dispatch` (admin use). Select a `version_bump` (`patch`/`minor`/`major`). `use_current_version=true` skips the bump and publishes whatever is already in `src/stream-feed-net.csproj`.

Automatic semver bump rules:

- `feat:` -> minor
- `fix:` (or `bug:`) -> patch
- `feat!:` or `<type>(scope)!:` (the `!` marker) -> major

PRs with any other prefix do not trigger a release.

The release pipeline runs `dotnet build`, `make test`, and a warnings-as-errors check on the merged commit before publishing to NuGet. Each step is idempotent; a failed run can be re-dispatched from the Actions UI.
