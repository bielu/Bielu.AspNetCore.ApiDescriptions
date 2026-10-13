---
"bielu-aspnetcore-asyncapi": minor
---

Allow `[Message]` on a payload class. The class is the payload, so the type argument is optional (`new MessageAttribute()`), and its values become defaults wherever the class is used as a payload; a `[Message]` on a method still overrides them. `MessageAttribute.PayloadType` is now nullable. Class-level metadata is read by reflection and is not yet part of the source-generated (Native AOT) metadata.
