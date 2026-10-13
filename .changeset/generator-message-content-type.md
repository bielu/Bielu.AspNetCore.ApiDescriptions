---
"bielu-aspnetcore-asyncapi": patch
---

Fix the source generator dropping `MessageAttribute.ContentType`, so source-generated (Native AOT) metadata now sets the same message content type as the reflection path.
