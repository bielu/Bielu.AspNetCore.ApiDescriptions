---
"@bielu/scalar-arazzo": minor
"bielu-aspnetcore-asyncapi": minor
---

The Arazzo Sequence view can draw the calls services make to each other. Arazzo has no field for who makes a step's call (its runner makes them all), so a new opt-in option, `ActorExtension` (`actorExtension` in npm and on the plugin URL), names a specification extension such as `x-actor` on a step or a workflow. Its value is a source description's name, which puts the call on that source's lane, or any other name, which gets a lane of its own. The step's request, message or workflow call then starts from that lane, and the response or delivered message goes back to it. A lane calling itself is drawn as a loop, and the client's lane is left out when no step is the client's. `ScalarArazzoOptions.ActorExtension` and `ScalarArazzoAspireOptions.ActorExtension` are new.

A second option, `ActorsExtension` (`actorsExtension`), names a document-level actor registry such as `x-actors`: actor name → `{ title, description, client }`. The lanes it lists are titled after it, with the description in their tooltip, and come first in its order; the entry with `client: true` renames the client's lane, and its steps are the client's. `ScalarArazzoOptions.ActorsExtension` and `ScalarArazzoAspireOptions.ActorsExtension` are new.
