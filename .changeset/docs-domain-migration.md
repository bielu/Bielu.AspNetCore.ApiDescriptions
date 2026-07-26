---
"bielu-aspnetcore-asyncapi": patch
---

Migrated the documentation site from `asyncapi.bielu.pl` to `apidescriptions.bielu.pl` (`docs/CNAME`, `PackageProjectUrl` in `src/Directory.Build.props`, README links) ahead of the stable 1.0.0 tag, since package metadata is immutable once published. Split the docfx site into separate **AsyncAPI** and **Arazzo** sections — articles and API reference are now generated independently (`docs/api/asyncapi/`, `docs/api/arazzo/`, `docs/articles/arazzo/`) with their own top-level navigation — and replaced the landing page with a two-column overview introducing both specs.

Also completed the repo-rename follow-through: the GitHub repo has actually been renamed to `bielu/Bielu.AspNetCore.ApiDescriptions` (confirmed via `gh repo view`), so `RepositoryUrl` in `src/Directory.Build.props`, the README CI badge, `globalMetadata.repository`/`docurl` in `docs/docfx.json`, `CONTRIBUTING.md`'s upstream remote, `PACKAGE.md`, and the Scalar gRPC/SignalR console asset READMEs now all point at the new repo.
