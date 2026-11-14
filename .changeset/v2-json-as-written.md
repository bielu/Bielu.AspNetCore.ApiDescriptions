---
"bielu-aspnetcore-asyncapi": patch
---

Fix: AsyncAPI 2.6 documents served as JSON are now written the same way as 3.1 ones: indented, with non-ASCII characters
and `+`, `<`, `>` written as they are. The 2.x JSON used to be parsed and written again, which put it on one line and
escaped those characters (`Café` became `Caf\u00E9`). The content of the document is unchanged; the ETag changes once.
