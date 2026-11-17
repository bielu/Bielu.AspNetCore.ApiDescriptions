---
"bielu-aspnetcore-asyncapi": patch
---

The Arazzo view in Scalar no longer checks for newly loaded sibling documents every second for as long as the page is open: the checks back off from 1 to 10 seconds while nothing changes, pause while the page is hidden, and stop once every source description is loaded.
