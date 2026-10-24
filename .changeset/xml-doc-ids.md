---
"bielu-aspnetcore-asyncapi": patch
---

Fix XML documentation lookup for members whose ID the compiler writes differently than the library expected: multi-dimensional array parameters (`System.Int32[0:,0:]`) and generic type definitions (`MyType`1`) now resolve, so their summaries appear in the generated document.
