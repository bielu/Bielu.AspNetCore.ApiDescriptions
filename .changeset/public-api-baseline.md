---
"bielu-aspnetcore-asyncapi": patch
---

Baseline the public API surface before the stable tag, and narrow three pieces of it that were public
by accident.

`Microsoft.CodeAnalysis.PublicApiAnalyzers` was enabled on `Bielu.AspNetCore.AsyncApi` but not
maintained: `PublicAPI.Shipped.txt` was empty, `PublicAPI.Unshipped.txt` had 18 entries, and the
analyzer reported **169 further symbols** as undeclared. The whole public surface of the flagship
package was effectively untracked, which is exactly the gate that is supposed to catch an accidental
break after 1.0.0. All 169 are now declared, and the analyzer is enabled on
`Bielu.AspNetCore.AsyncApi.Attributes` too (81 entries) — that is the assembly consumers' own
contract types compile against, so a break there breaks their build rather than their document.

Reviewing the surface rather than just recording it found three things worth changing while changing
them is still free:

- **`AddServer` was ambiguous at the call site.** `AddServer(name, url, protocol, string? pathName = null)`
  and `AddServer(name, url, protocol, Action<AsyncApiServer> configure)` take the same number of
  parameters, so `AddServer(name, url, protocol, null)` did not compile (CS0121) — and RS0027 flags
  the same shape as a backcompat hazard, since a parameter added to either overload later would
  collide. Split into explicit three- and four-parameter overloads; every existing call site compiles
  unchanged.
- **`AsyncApiOptions.ChannelBindings` / `OperationBindings` are now get-only.** Nothing assigned
  either dictionary; callers go through `AddChannelBinding`/`AddOperationBinding`. The dictionaries
  stay mutable, so nothing the setter allowed is out of reach — but a setter cannot be removed after
  the baseline freezes.
- **`ParameterInfoExtensions` is now internal.** A reflection helper with one call site, published as
  an extension method on `System.Reflection.ParameterInfo`, where it surfaced in IntelliSense on every
  `ParameterInfo` in any file importing the namespace.

The entries stay in `PublicAPI.Unshipped.txt` and move to `PublicAPI.Shipped.txt` when 1.0.0 tags,
alongside the `BASYNC001`–`BASYNC009` analyzer rules moving to their `Release 1.0.0` section. That
move is what turns RS0017 on: from then on, removing a shipped API is a build error rather than a
discovery made by a consumer.

The remaining 26 packable projects are deliberately not baselined yet — the spec libraries in
particular are young enough that freezing them now would cost more than it protects.
