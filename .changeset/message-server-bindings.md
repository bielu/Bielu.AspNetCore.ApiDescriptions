---
"bielu-aspnetcore-asyncapi": minor
---

Add `AsyncApiOptions.AddMessageBinding` and `AsyncApiOptions.AddServerBinding`. Message bindings are registered in `components/messageBindings` and attached to messages declared with a matching `[Message(BindingsRef = "...")]`, which was previously ignored. Server bindings are registered in `components/serverBindings` and attached to the server of the same name.
