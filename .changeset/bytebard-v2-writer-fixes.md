---
"bielu-aspnetcore-asyncapi": patch
---

Drop the two AsyncAPI 2.x writer workarounds, now fixed in ByteBard (ByteBardOrg/AsyncAPI.NET#57, #58). Channel servers are referenced as `#/servers/{name}` for both versions and the 2.x writer outputs the server name. A message trait without headers is no longer given an empty `headers` schema in 2.x documents. Requires the ByteBard release that contains those fixes and ByteBardOrg/AsyncAPI.NET#56 (the JsonPointer.Net 7 conflict).
