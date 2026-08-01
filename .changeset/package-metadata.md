---
"bielu-aspnetcore-asyncapi": patch
---

complete the NuGet package metadata before the first stable tag, since it cannot be changed once a version is published.

- **License.** No package carried any license metadata — a `LICENSE` file sat at the repo root but no `PackageLicenseExpression` was ever set, so every package would have published as "License: not specified". Now `MIT` (matching `LICENSE`) via `src/Directory.Build.props`, along with a `Copyright` field. Both are inherited by all 27 packages.
- **Descriptions.** `Bielu.AspNetCore.AsyncApi` itself had none — NuGet substitutes the package id, so the flagship package would have shipped with "Bielu.AspNetCore.AsyncApi" as its entire description. Same for `.Attributes` and `.Templates`. `.Versioning`'s one-line description was expanded to match the detail level of its siblings.
- **Tags.** 19 of 27 packages had no `PackageTags`, including the core package — which matters because staying discoverable on the `asyncapi` search term was the stated reason for keeping `AsyncApi` in the package ids in the first place.

Also fixes a packaging bug found while auditing: `Bielu.AspNetCore.AsyncApi.SourceGenerators` was packable, so it produced a **standalone package with no lib and no content** alongside the copy correctly bundled into `Bielu.AspNetCore.AsyncApi`'s `analyzers/dotnet/cs` folder. It is now `IsPackable=false`, matching how `Bielu.AspNetCore.AsyncApi.Analyzers` is already handled inside the Attributes package. The bundled analyzer DLLs are unaffected — both were verified present in the packed output.
