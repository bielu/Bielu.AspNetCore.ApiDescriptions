---
"bielu-aspnetcore-asyncapi": patch
---

fix: bump `vite` from `^5.4.0` (EOL, no security backports) to `^6.4.3` in the `@bielu/scalar-signalr` and `@bielu/scalar-grpc` asset packages, clearing all open Dependabot alerts: the `server.fs.deny` bypass on Windows alternate paths (GHSA-fx2h-pf6j-xcff, high), the optimized-deps `.map` path traversal (GHSA-4w7w-66w2-5vf9), the launch-editor NTLMv2 hash disclosure via UNC paths (GHSA-v6wh-96g9-6wx3) and — via the bundled esbuild 0.25 — the esbuild dev-server open CORS issue (GHSA-67mh-4wv8-2f99). All were development-scope (vite dev server only); shipped bundles were never affected. Bundles rebuilt and verified with vite 6.
