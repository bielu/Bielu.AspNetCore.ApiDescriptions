---
"bielu-aspnetcore-asyncapi": patch
---

Fix: `[AsyncApi]` types in a referenced assembly that has not been loaded yet are now found. Only the assemblies already
loaded into the app domain were scanned, so channels kept in a contracts library could be missing from the document
until some code happened to touch one of that library's types. The references of every loaded application assembly
are now followed and loaded (framework assemblies are skipped).

The scanned assemblies are now ordered by name, so channels and operations come out in the same order regardless of the
order assemblies were loaded in. A document that declares channels in more than one assembly may list them in a
different order than before; the content is unchanged.
