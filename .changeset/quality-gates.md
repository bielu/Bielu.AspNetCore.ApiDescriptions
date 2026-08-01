---
"bielu-aspnetcore-asyncapi": patch
---

close the quality gates a stable release should hold.

**A single-file bug.** `AsyncApiOptions.IncludeXmlComments(Assembly)` derived the XML path from `Assembly.Location`, which is an empty string for an assembly embedded in a single-file app — reducing the path to a bare `{Name}.xml` resolved against the current working directory, so XML descriptions silently went missing. It now falls back to `AppContext.BaseDirectory`.

**The Native AOT example never actually published.** `PublishAot` flows into referenced projects as a global MSBuild property, so publishing `src/examples/AotStreetlights` failed with NETSDK1207 on the netstandard2.0 analyzer and source generator, which cannot be AOT-compiled. Both now opt out explicitly, and the new `scripts/verify-aot.sh` avoids passing `PublishAot` on the command line (a global property cannot be overridden by the projects it flows into). The example publishes through ILC now.

**AOT verification in CI — and what it found.** Roadmap PR 9 scoped a step that publishes the AOT example and compares its document against the reflection-based build; it was never wired up. `scripts/verify-aot.sh` does exactly that, as a new `aot-verification` PR job.

It found that **AsyncAPI generation did not work under Native AOT at all** — the document endpoint threw on every request. Three separate defects, all now fixed:

1. `MapAsyncApi()` mapped its handler as a parameter-bound lambda, so `RequestDelegateFactory` tried to resolve `JsonTypeInfo` for the handler's own return type and threw. Now mapped as a `RequestDelegate` — which also clears the two IL warnings that were sitting on that call site.
2. `AddAsyncApiGeneratedMetadata(...)` registered the generated provider under the **caller's** casing while `AddAsyncApi` registers keyed services **lowercased**. The `Replace` matched nothing, so the reflection provider stayed live and threw `PlatformNotSupportedException`. The source generator's entire reason for existing silently did not take effect for any document name containing an uppercase character — it worked by accident only for an all-lowercase name.
3. `ByteBard.AsyncAPI` resolves enums from display names through `Enum.GetValues()`, which calls `Array.CreateInstance(typeof(TEnum), n)`. Constructing an array type at runtime needs metadata ILC does not emit unless told the type is used dynamically, so the endpoint threw `'ByteBard.AsyncAPI.Models.ReferenceType[]' is missing native code or metadata`. A scoped `rd.xml` roots the assembly **and each enum's array type** — the array types must be named individually, because an array is a constructed type ILC only emits when it can see it used, and `Array.CreateInstance` is invisible to that analysis. Neither `TrimmerRootAssembly` nor rooting the assembly alone is sufficient; both were tried and the failure survived unchanged.

**Native AOT now works**, and the `aot-verification` job is **blocking**: it publishes the example both ways and asserts the served documents are identical, which is what guards against the source generator and the reflection scan silently diverging.

`README.md`'s "✅ Native AOT support" is now a claim the build actually enforces.

**Analyzer release tracking.** The Analyzers project had no `AnalyzerReleases.{Shipped,Unshipped}.md`, producing 9 × RS2008 and leaving the `BASYNC001`–`BASYNC009` rules undeclared — which is how consumers find out a rule was added or changed between versions. All nine are now recorded as unshipped; they move to a `Release 1.0.0` section when the stable version tags.

**XML documentation.** Fixed every broken `<see cref>` in the core package (CS1574) plus the surrounding CS1572/CS1573. Several were wrong rather than merely unresolvable: `AsyncApiOptions.AsyncApiVersion` documented a default of `AsyncApiSpecVersion.AsyncApi3_1` — a type that does not exist, naming a value that is not the default — and `IAsyncApiOperationTransformer`'s `<param>` tags described the wrong parameters. These feed the published API reference.

**Trim annotations.** Annotated the reflection-based helpers (`HasBindAsyncMethod`, `HasTryParseMethod`, the authorization scanner) so their trim behaviour is declared rather than inferred, and replaced a `MakeGenericType` call — which native AOT cannot do — with an equivalent structural check. That also removed a dead comparison against an open generic type, which a return type can never equal.

Solution warnings drop from 278 to 249; all 656 tests still pass.
