---
"bielu-aspnetcore-asyncapi": patch
---

Each `AddArazzo` document build now works on its own copy of the configured model. Before, a document transformer that changed the document and then threw, or was cancelled, left its change in the options. The next `GetArazzoDocumentAsync` then started from that changed model, so a step a transformer appended showed up twice.
