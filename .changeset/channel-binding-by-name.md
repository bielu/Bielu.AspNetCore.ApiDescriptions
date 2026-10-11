---
"bielu-aspnetcore-asyncapi": patch
---

Fix `AsyncApiOptions.AddChannelBinding` not attaching to the channel of the same name. The binding was registered in `components/channelBindings` but only reached a channel that named it through `[Channel(BindingsRef = "...")]`. A channel without a `BindingsRef` now gets the bindings registered under its own name; an explicit `BindingsRef` still takes precedence.
