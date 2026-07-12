# Changesets

This folder is managed by [changesets](https://github.com/changesets/changesets). Every pull
request that changes a shipped package should include a **changeset** — a small markdown file that
records what changed and how the version should bump. CI aggregates these into an automated
"Version Packages" PR that bumps versions and updates the changelogs.

## Add a changeset

```bash
npx changeset
```

Pick the affected package(s) and a bump level, then write a short, user-facing summary. For a
change to **any** NuGet package, bump the single `bielu-aspnetcore-asyncapi` entry — all NuGet
packages in this repo share one version (`version.props`), so one bump covers the whole suite.

Bump levels follow semver:

- **patch** — bug fixes, docs, internal changes with no API impact.
- **minor** — new, backward-compatible features.
- **major** — breaking changes.

Chore-only PRs (CI, formatting, tests) can record an empty changeset:

```bash
npx changeset add --empty
```

## How the NuGet version is applied

Changesets is npm-native, so the NuGet suite is represented by a private placeholder package,
`bielu-aspnetcore-asyncapi` (in `build/changeset/nuget-suite`). After `changeset version` runs,
`scripts/apply-nuget-version.mjs` copies that package's new version into `version.props`
`<VersionPrefix>` and splices its generated changelog section into the root `CHANGELOG.md`.
