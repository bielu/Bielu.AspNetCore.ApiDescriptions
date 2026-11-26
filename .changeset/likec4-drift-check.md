---
"bielu-aspnetcore-asyncapi": minor
---

Check a hand-written LikeC4 model against the AsyncAPI documents it describes. `LikeC4ModelChecker.Check(model, sources)` (in `Bielu.AspNetCore.AsyncApi.LikeC4`) reads the model as `likec4 export json` writes it, maps its elements to documents and channels through `asyncapi` and `asyncapiChannel` metadata (an element nested in a mapped one counts as it), and reports relationships with no operation behind them, operations on modelled channels and flows between modelled services that the model does not draw, and metadata naming nothing; a document no element stands for is a warning. The CLI gets `dotnet asyncapi likec4 check --model <likec4.json> --source [name=]<path|url> [--strict] [--format text|json]`.
