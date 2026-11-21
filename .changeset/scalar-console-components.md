---
"@bielu/scalar-broker": patch
"@bielu/scalar-grpc": patch
"@bielu/scalar-signalr": patch
"bielu-aspnetcore-asyncapi": patch
---

The broker, gRPC and SignalR consoles now use Scalar's own `ScalarButton`, `ScalarTextInput` and `ScalarTextArea` (from `@scalar/components` 0.35.1, the version `@scalar/api-reference` 1.80.0 ships), so their buttons, text fields and JSON editors look and behave like the ones in Scalar's API client. The editors grow with their content instead of showing a resize handle. Selects stay native, drawn to match the text fields. The broker console's controls, unstyled until now, get the same look. Each console bundle grows by about 45–60 KB (minified) for the components and their styles.
