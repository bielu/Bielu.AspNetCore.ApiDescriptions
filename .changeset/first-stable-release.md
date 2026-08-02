---
"bielu-aspnetcore-asyncapi": major
---

First stable release.

Everything in this changelog shipped only to the `1.0.0-beta.*` pre-release channel until now. The
public API of `Bielu.AspNetCore.AsyncApi` is baselined at this version
(`PublicAPI.Shipped.txt`), and the `BASYNC001`–`BASYNC009` analyzer rules move from unshipped to
`Release 1.0.0`, so from here on an accidental break in either surface is caught at build time rather
than discovered by a consumer.

This entry also exists to pin the version arithmetic. The shared-version placeholder
(`build/changeset/nuget-suite`) is held at `0.0.0` and this major bump lands it exactly on `1.0.0` —
without it the accumulated minor changesets would have produced `1.1.0` as the first stable version,
which the `dotnet new` templates (which pin `Version="1.0.0"`), the documentation and the existing
`1.0.0-beta.*` channel all contradict.
