---
"bielu-aspnetcore-asyncapi": patch
---

Internal: the document service no longer keeps the last operation transformer context for each operation id. Nothing
read it, and it kept the last request's scoped service provider and document alive for the lifetime of the app.
