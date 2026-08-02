---
---

Chore: retire the stale `[Unreleased]` changelog block left behind by the 1.0.0 release and fix
`scripts/apply-nuget-version.mjs` to drain it on future releases. Touches only `CHANGELOG.md` and the
release script, so no package content changes and no version bump is needed.
