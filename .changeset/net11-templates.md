---
"bielu-aspnetcore-asyncapi": minor
---

Templates: `--Framework` is now a real choice (`net10.0`, the default, or `net11.0`) that reaches every generated
project. It used to be advertised but substituted nowhere. Console and worker projects get the
`Microsoft.Extensions.Hosting` version that matches the framework.

Template fixes found by the new end-to-end check, which generates and builds every template for every framework:
- generated projects enable `ImplicitUsings` and `Nullable`. Without them, `asyncapi-webapi` did not compile
  (`DateTime` not found).
- the templates reference `Scalar.AspNetCore` 2.16.17. 2.16.15 is older than what the Scalar console packages
  require, so `asyncapi-signalr` failed to restore with a package-downgrade error (NU1605).

`scripts/verify-aot.sh` takes the target framework as a second argument, and CI runs the AOT comparison for
`net10.0` and `net11.0`.
