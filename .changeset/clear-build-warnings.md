---
"bielu-aspnetcore-asyncapi": patch
---

Clear the build warnings, and pin the package sources the build restores from.

The solution built with 154 warnings. It now builds with 20, all of which are the trim/AOT warnings
covered by the open annotation decision (`IL2026`, `IL3050`, `IL2072` on the reflection-based
generation path, which is still the default).

**A new `NuGet.config` is the largest single part of that.** There was none, so the build inherited
whatever feeds the machine happened to have configured — 72 of the warnings were `NU1507` ("there are
N package sources defined... please map your package sources"), one per project, on any machine with
more than one feed. That is a reproducibility problem before it is a warning: every extra feed is
another source that can answer for any package ID in `Directory.Packages.props`. The config clears
inherited sources, declares nuget.org, and maps it explicitly, so adding a second feed later cannot
silently start serving an existing package ID. A forced full restore confirms everything this
repository consumes comes from nuget.org.

Two of the fixes are behavioural rather than cosmetic:

- **`AsyncApiOptions.DocumentRoutePattern` was never initialised** (`CS8618`), so it was null until
  `MapAsyncApi(pattern)` assigned it — a null anything reading it earlier would see, build-time
  generation included. It now defaults to `AsyncApiGeneratorConstants.DefaultAsyncApiRoute`.
- **The merger swallowed cancellation.** `LoadContentAsync`'s catch-all (`CS0168`, on the discarded
  exception variable) reported *any* failure as an unavailable source, including the
  `OperationCanceledException` from cancelling the merge itself. Cancellation now propagates; a
  per-source HTTP timeout still reports the source as unavailable, which is what the catch-all is for.

The rest are local: nullable annotations in tests and examples, three internal `async` methods gaining
the `Async` suffix (`VSTHRD200`), an unread primary-constructor parameter, and an `IRouteConstraint`
cref that cannot be written unambiguously because two shared-framework assemblies declare that type.

`AsyncApiMessageExampleTests` also moves off `WebHostBuilder`/`TestServer(IWebHostBuilder)`
(`ASPDEPR004`/`ASPDEPR008`, deprecations scheduled to become errors) and onto the
`Host.CreateDefaultBuilder().ConfigureWebHostDefaults(... UseTestServer())` pattern the sibling
integration tests already use.
