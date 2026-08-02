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

`src/examples/AotStreetlights` now imports that same targets file instead of keeping its own copy of
the directives, so `scripts/verify-aot.sh` — and with it the blocking `aot-verification` CI job —
fails if their *content* regresses, rather than passing against a copy that has drifted from what
ships.

The packaging itself is verified separately, by packing and reading the `.nupkg`: a `ProjectReference`
does not flow `buildTransitive` assets, so the example imports the file directly and a wrong
`PackagePath` would not fail that job. Consuming the packed package from a local feed in the fixture
would close that last gap, and is worth doing if the packaging layout ever changes again.

The [Native AOT](https://apidescriptions.bielu.pl/articles/native-aot.html) article documents what is
supplied, why it is necessary, and how to override it.
