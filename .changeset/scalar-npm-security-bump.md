---
"bielu-aspnetcore-asyncapi": patch
---

The standalone SignalR and gRPC Scalar console bundles now include `@scalar/api-reference` 1.76.0 (was 1.62.9). The newer Scalar no longer pulls in the `ai` / `@ai-sdk/*` versions affected by [GHSA-866g-f22w-33x8](https://github.com/advisories/GHSA-866g-f22w-33x8) or the zod affected by [GHSA-mrhx-c7xc-m3w5](https://github.com/advisories/GHSA-mrhx-c7xc-m3w5). The console build tooling also moves to vite 6.4.5, which fixes the vite and launch-editor advisories flagged by Dependabot.
