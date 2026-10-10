---
"bielu-aspnetcore-asyncapi": patch
---

Fix `MessageAttribute.Tags` being silently ignored. Tags passed to `[Message(typeof(T), "tag", ...)]` are now written to the generated message's `tags` and registered in `components/tags`, the same way operation tags already are.
