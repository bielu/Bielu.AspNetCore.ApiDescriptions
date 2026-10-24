---
"bielu-aspnetcore-asyncapi": major
---

Operation attributes are now named from the application's point of view, as in AsyncAPI 3: `[SendOperation]` (`action: send`) and `[ReceiveOperation]` (`action: receive`). `OperationType` is now `Send`/`Receive` instead of `Publish`/`Subscribe`.

`[PublishOperation]` and `[SubscribeOperation]` still work, produce the same operations as before, and are marked `[Obsolete]`. They used the AsyncAPI 2.x names, which describe the operation from the client's side, so the mapping is inverted: replace `[PublishOperation]` with **`[ReceiveOperation]`** and `[SubscribeOperation]` with **`[SendOperation]`**. A plain find-and-replace of Publish→Send would reverse your operations.

Operations without an explicit `OperationId` get a default ID ending in the operation type, so those IDs change from `{Type}_{Method}_Publish`/`_Subscribe` to `{Type}_{Method}_Receive`/`_Send`. Set `OperationId` explicitly if something (an Arazzo workflow, a client generator) depends on the old ID.
