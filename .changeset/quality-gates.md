---
"bielu-aspnetcore-asyncapi": patch
---

close the quality gates a stable release should hold.

**A single-file bug.** `AsyncApiOptions.IncludeXmlComments(Assembly)` derived the XML path from `Assembly.Location`, which is an empty string for an assembly embedded in a single-file app — reducing the path to a bare `{Name}.xml` resolved against the current working directory, so XML descriptions silently went missing. It now falls back to `AppContext.BaseDirectory`.

**The Native AOT example never actually published.** `PublishAot` flows into referenced projects as a global MSBuild property, so publishing `src/examples/AotStreetlights` failed with NETSDK1207 on the netstandard2.0 analyzer and source generator, which cannot be AOT-compiled. Both now opt out explicitly, and the new `scripts/verify-aot.sh` avoids passing `PublishAot` on the command line (a global property cannot be overridden by the projects it flows into). The example publishes through ILC now.

**AOT verification in CI.** Roadmap PR 9 scoped a step that publishes the AOT example and compares its document against the reflection-based build; it was never wired up. `scripts/verify-aot.sh` does exactly that — publishing both, serving each on the *same* port (the document records its bound address as the server host, so differing ports would make the comparison fail for the wrong reason) and comparing the parsed JSON. Runs as a new `aot-verification` PR job.

**Analyzer release tracking.** The Analyzers project had no `AnalyzerReleases.{Shipped,Unshipped}.md`, producing 9 × RS2008 and leaving the `BASYNC001`–`BASYNC009` rules undeclared — which is how consumers find out a rule was added or changed between versions. All nine are now recorded as unshipped; they move to a `Release 1.0.0` section when the stable version tags.

**XML documentation.** Fixed every broken `<see cref>` in the core package (CS1574) plus the surrounding CS1572/CS1573. Several were wrong rather than merely unresolvable: `AsyncApiOptions.AsyncApiVersion` documented a default of `AsyncApiSpecVersion.AsyncApi3_1` — a type that does not exist, naming a value that is not the default — and `IAsyncApiOperationTransformer`'s `<param>` tags described the wrong parameters. These feed the published API reference.

**Trim annotations.** Annotated the reflection-based helpers (`HasBindAsyncMethod`, `HasTryParseMethod`, the authorization scanner) so their trim behaviour is declared rather than inferred, and replaced a `MakeGenericType` call — which native AOT cannot do — with an equivalent structural check. That also removed a dead comparison against an open generic type, which a return type can never equal.

Solution warnings drop from 278 to 249; all 656 tests still pass.
