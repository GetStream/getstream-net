# getstream-net

Official .NET server SDK for Stream Chat, Video, Feeds, and Moderation.

- Default branch: `master` (CI also watches `main`)
- NuGet: `getstream-net`
- Project: `src/stream-feed-net.csproj` (`TargetFramework` `net8.0`)
- Solution: `stream-feed-net.sln`
- Version: `<Version>` in `src/stream-feed-net.csproj` and `VersionName` in `src/Client.cs`
- Clone sibling of the chat monorepo as `../chat` (required for OpenAPI regen)

## Layout

Generated (from OpenAPI, plus `generate.sh` post-patches): `src/models.cs`, `src/requests.cs`, `src/Feed.cs`, `src/FeedsV3Client.cs`, `src/ChatClient.cs`, `src/CommonClient.cs`, `src/VideoClient.cs`, `src/ModerationClient.cs`, `src/Webhook.cs`.

Handwritten: `src/Client.cs`, `ClientBuilder.cs`, `CustomCode.cs`, `Exceptions.cs`, `IClient.cs`, `LogRedaction.cs`, `RetryConfig.cs`, `StreamOptions.cs`, `stream-feed-net.csproj`.

- Tests: `tests/` (`stream-feed-net-test.csproj`)
- Samples: `samples/ConsoleApp/`
- Webhook fixtures: `tests/fixtures/webhooks/`

`generate.sh` comments out colliding `DeleteActivity`/`DeleteMessage`/`DeleteReaction` properties in `requests.cs`, drops duplicate `[JsonPropertyName("Role")]` in `models.cs`, and renames `UploadFile`/`UploadImage` to `FileUpload`/`ImageUpload`.

## Local commands

```bash
# STREAM_API_KEY + STREAM_API_SECRET (optional STREAM_BASE_URL)
make restore
make build
make test
make test-one TEST_NAME=TestName
make test-endpoints     # FullyQualifiedName~FeedEndpointTests
make test-integration    # FullyQualifiedName~FeedIntegrationTests
dotnet format --verify-no-changes
./generate.sh           # ends with dotnet format; uses macOS sed -i ''
```

`make test` requires `.env` or `STREAM_API_KEY`. CI Debug+Release build; warnings fail the job.

## OpenAPI regen

`./generate.sh`:

1. `make openapi` in `../chat`, then `./build/chat-manager openapi generate-client --language dotnet --spec ./releases/v2/serverside-api.yaml --output <this repo>`.
2. Webhook fixtures: `generate-webhook-fixtures --output ../getstream-net/tests/fixtures/webhooks` (expects this repo named `getstream-net` next to chat).
3. Patches above; `dotnet format`.

Uses chat’s local spec. Generator is internal.

Additive regen = **minor**. PR title `feat: …`, not `feat!:`, unless the public C# API actually breaks.

## CI

`.github/workflows/ci.yml` (`.NET CI`): push/PR to `main`/`master`; ignores markdown/docs-only paths; skip with `[skip ci]` in the head commit message.

Steps: restore, `dotnet format --verify-no-changes`, Debug+Release build, tests via `make test`, warnings check, `dotnet pack` smoke test.

CI tests use **repository secrets** `STREAM_API_KEY` and `STREAM_API_SECRET` (not `vars`). Release tests use `vars.STREAM_API_KEY` + `secrets.STREAM_API_SECRET`.

`.github/workflows/release.yml` (`Release`): merged PR to `master`, or `workflow_dispatch`. Release job uses .NET `9.0.x`.

Secrets: `STREAM_API_KEY` (CI), `STREAM_API_SECRET`, `NUGET_USER` (nuget.org profile name for Trusted Publishing / `NuGet/login@v1`).
Vars (release tests): `STREAM_API_KEY`.

Known flakes: live Chat API 503 if the CI app is on a bad shard.

Do not copy API keys from README into docs or commits.

## Release

Tags: `vX.Y.Z`. Publishes `getstream-net` to nuget.org (OIDC via `NuGet/login@v1`), then GitHub Release with `.nupkg` artifacts.

- Merge to `master` with title `feat:` (minor), `fix:`/`bug:` (patch), or `!:` (major). Other prefixes do not release.
- Fallback: Actions → **Release** with `version_bump` / `use_current_version`.
- `scripts/bump_version.sh` updates `src/stream-feed-net.csproj` and `src/Client.cs`, commits and pushes to `master`, tags, packs, pushes nupkg (`--skip-duplicate --no-symbols`).

## PR conventions

Conventional titles drive the bump. Markdown-only PRs skip CI (`paths-ignore`). Example regen: `feat: regenerate from OpenAPI`.
