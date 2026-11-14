---
"bielu-aspnetcore-asyncapi": patch
---

Fix: a channel listing several servers in `[Channel(Servers = ...)]` now references every one of them. A server whose
name ends another listed server's name (`prod` after `eu-prod`) was treated as already referenced and left out.
