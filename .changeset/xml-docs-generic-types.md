---
"bielu-aspnetcore-asyncapi": patch
---

XML documentation comments now describe generic payload types and their members. A payload such as `Envelope<Order>` used to get no description, nor did its properties, because the XML documentation id was built from the constructed type (`Envelope{Order}`), while the compiler documents a generic type once, under its definition (`` Envelope`1 ``). An open generic type also had its arity written twice, and a multi-dimensional array parameter was written as `[,]` instead of `[0:,0:]`.
