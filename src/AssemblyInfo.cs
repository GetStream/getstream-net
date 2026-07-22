// Grants the test assembly access to internal members (e.g. LogRedaction) without
// widening any public API surface. Test assembly name matches tests/stream-feed-net-test.csproj.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("stream-feed-net-test")]
