---
"bielu-aspnetcore-asyncapi": patch
---

Fix: concurrent requests for a document whose servers have security requirements (`AddServerSecurity`) no longer
fail with "Collection was modified", and no longer grow the server's security list with duplicate requirements. Each
document now gets its own copy of the servers configured with `AddServer`, instead of sharing (and adding to) the
options' instances. For the same reason, a document transformer's edits to a server or to `info` (including its
license and contact) now apply to that document only, rather than carrying into every later request.

Fix: `info.termsOfService`, `info.tags`, `info.externalDocs` and `info` extensions set through `WithInfo` now reach the
document. Only the title, version, description, license and contact were copied before.
