---
"bielu-aspnetcore-asyncapi": patch
---

give every package its own nuget.org landing page. `src/Directory.Build.props` packed the **root README** into all 27 packages, so `Bielu.Overlay.NET`, `Bielu.Arazzo.NET`, `Bielu.Cli.Shared` and everything else displayed the `Bielu.AspNetCore.AsyncApi` pitch and quickstart — the wrong page for two thirds of the suite. Each package now ships a `PACKAGE.md` describing that package: what it is, how to install it, a usage example, and links to the relevant documentation article.

A `PACKAGE.md` in the project directory is picked up automatically; projects without one still fall back to the root README, so adding a package does not break `dotnet pack` before its readme is written.

The core package's `PACKAGE.md` already existed but was never referenced by any `PackageReadmeFile`, so it had never actually shipped. It is now wired up, and its stale reference to the removed `Bielu.AspNetCore.AsyncApi.UI` package is replaced with the current package list.
