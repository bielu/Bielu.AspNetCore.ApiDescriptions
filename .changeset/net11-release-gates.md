---
"bielu-aspnetcore-asyncapi": patch
---

Fix: the `Bielu.AspNetCore.AsyncApi` package again ships its `buildTransitive/` Native AOT directives
(`Bielu.AspNetCore.AsyncApi.targets`, `.rd.xml`). They dropped out when the package became multi-targeted, because a
multi-targeted project packs in its outer build, where the `<None Update>` items that included them match nothing.
Native AOT apps that got the core package from NuGet were missing the runtime directives as a result.

Fix: the source-generated `GeneratedAsyncApiMetadataProvider` no longer raises CS8669 in apps with nullable
reference types enabled. It now declares `#nullable enable` and is nullable-clean.

`scripts/verify-packages.sh` is a new release gate. It packs the solution and checks every package (`lib/` and a
dependency group per framework, no 11.x Microsoft package in a `net10.0` group, both tool frameworks, analyzer and
`buildTransitive/` assets). Then it builds and runs a consumer on each framework, on a CI runner that has only that
framework's SDK. The support matrix now lists .NET 11 as supported, with end of support on 14 November 2028, the
same day as .NET 10.
