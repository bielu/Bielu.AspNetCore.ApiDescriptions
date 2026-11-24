---
"@bielu/scalar-arazzo": patch
"bielu-aspnetcore-asyncapi": patch
---

The Sequence view labels an `alt` operand with just its status code (`[409]`) only when the action's criteria are a single `$statusCode == 409`. An action that also checks something else, such as a retry on `409` **and** `$response.body#/retryable == true`, now shows every condition it checks, joined with `&&`, rather than `[409]`, which claimed every conflict was retried. Failure notes read `if:` for such actions, and a guard too long for the diagram shows in full on hover.
