---
"bielu-aspnetcore-asyncapi": patch
---

Take the stable `ByteBard.AsyncAPI.NET` 3.0.1 instead of `3.0.1-beta.17`.

This was the last thing standing between the packages and a defensible stable tag. Packing a stable
1.0.0 while depending on a prerelease produced `NU5104` ("a stable release of a package should not
have a prerelease dependency") — invisible for as long as our own version was `1.0.0-beta.*`, and a
real problem once it is not: a stable release resting on a beta that can be unlisted or replaced.

Same version number, prerelease suffix dropped, across all three packages (`ByteBard.AsyncAPI.NET`,
`.Bindings`, `.Readers`). Verified by packing the full solution: **27 packages, zero NuGet warnings**,
and the core package's nuspec now records `ByteBard.AsyncAPI.NET 3.0.1`. Full build and all 656 tests
pass unchanged, which is the evidence that the stable release is the same code the betas were.
