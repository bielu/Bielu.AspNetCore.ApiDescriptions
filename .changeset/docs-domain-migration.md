---
"bielu-aspnetcore-asyncapi": patch
---

Migrated the documentation site from `asyncapi.bielu.pl` to `apidescriptions.bielu.pl` (`docs/CNAME`, `PackageProjectUrl` in `src/Directory.Build.props`, README links) ahead of the stable 1.0.0 tag, since package metadata is immutable once published. Split the docfx site into separate **AsyncAPI** and **Arazzo** sections — articles and API reference are now generated independently (`docs/api/asyncapi/`, `docs/api/arazzo/`, `docs/articles/arazzo/`) with their own top-level navigation — and replaced the landing page with a two-column overview introducing both specs.
