---
"bielu-aspnetcore-asyncapi": minor
---

Add attribute-declared security requirements. `SecuritySchemes` on `[PublishOperation]`/`[SubscribeOperation]` and the new `AsyncApiOptions.AddServerSecurity(...)` reference schemes registered with the new `AsyncApiOptions.AddSecurityScheme(...)`, writing `security` on the generated operation or server.
