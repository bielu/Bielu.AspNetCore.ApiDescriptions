---
"bielu-aspnetcore-asyncapi": patch
---

fix: pin `@asyncapi/react-component` to exactly 3.1.3 and override `@asyncapi/specs` to the safe 6.11.1 in Bielu.AspNetCore.AsyncApi.UI, so a lockfile-less `npm install` can never float to the malicious `@asyncapi/specs@6.11.2` published in the 14 July 2026 supply-chain attack (Miasma RAT). The committed lockfile already resolved only safe versions; none of the compromised `@asyncapi/generator*` packages are in the dependency graph.
