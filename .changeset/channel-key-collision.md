---
"bielu-aspnetcore-asyncapi": patch
---

Fix: two different channel names that produce the same channel key once characters not allowed in a component key are
removed (for example `orders/created` and `orderscreated`) now fail generation with an error naming both channels.
They used to be merged into one channel that kept the first channel's address and carried both channels' messages and
operations. Message, operation and schema ids already failed the same way.
