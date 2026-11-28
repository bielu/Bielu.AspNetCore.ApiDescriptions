---
"bielu-aspnetcore-asyncapi": patch
---

`Bielu.AspNetCore.Arazzo.Scalar` and `Bielu.AspNetCore.Arazzo.Scalar.Aspire` now validate their view options and pass them to the plugin through a new internal package, `Bielu.AspNetCore.Scalar.Plugins.Shared`, which the LikeC4 Scalar packages will share. No change to their public API or behaviour.
