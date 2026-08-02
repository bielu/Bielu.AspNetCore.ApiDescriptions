---
"bielu-aspnetcore-asyncapi": patch
---

Ship the Native AOT runtime directives to consumers.

The AOT work that made `README.md`'s "Native AOT support" claim true fixed the failure **in the
example project**: `rd.xml` lived in `src/examples/AotStreetlights/` and was referenced by that one
csproj. Every other application publishing Native AOT would have published, started, and then thrown
on its first document request:

```text
System.NotSupportedException: 'ByteBard.AsyncAPI.Models.ReferenceType[]' is missing native code or metadata.
```

— with nothing in the documentation to explain why, because the cause lives in our dependency graph
(`ByteBard.AsyncAPI` resolving enums from display names through `Array.CreateInstance`, which ILC
cannot see) rather than in the consumer's code.

The directives now ship inside the package as `buildTransitive/Bielu.AspNetCore.AsyncApi.rd.xml`,
applied by a `.targets` alongside them whenever `PublishAot` is `true`. `buildTransitive` rather than
`build`, because the core package is very often an indirect reference — through `.Merger`,
`.Versioning`, a protocol extension or a Scalar console — and the directives are needed however it
was acquired. Opt out with
`<BieluAsyncApiIncludeRuntimeDirectives>false</BieluAsyncApiIncludeRuntimeDirectives>` to supply your
own set.

`scripts/verify-aot.sh` — and with it the blocking `aot-verification` CI job — now proves this from
the consumer's side rather than ours. It packs the library into a local feed and AOT-publishes
`src/examples/AotStreetlights` against it as an ordinary `PackageReference` consumer, outside the
repository and inheriting none of its MSBuild conventions. The runtime directives reach that build
only if they are packed to the right path and imported by NuGet: `RdXmlFile` resolves to
`~/.nuget/packages/bielu.aspnetcore.asyncapi/<version>/buildTransitive/`, and the source generator
arrives through the package's `analyzers/` folder the same way.

The example keeps its project references by default, so `dotnet build` and the IDE work with no packing
step; `-p:UseAsyncApiPackage=true` selects the package path. The script now compares **three**
documents — AOT via project reference, AOT via package, and the reflection-based build — and requires
all three to be equal once parsed as JSON. A wrong `PackagePath` fails the job instead of reaching
consumers, which is what the previous arrangement could not catch.

The [Native AOT](https://apidescriptions.bielu.pl/articles/native-aot.html) article documents what is
supplied, why it is necessary, and how to override it.
