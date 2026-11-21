---
"@bielu/scalar-broker": patch
"bielu-aspnetcore-asyncapi": patch
---

The broker console in Scalar is styled with Tailwind CSS on Scalar's own theme (`@scalar/themes`), the stack Scalar's own UI uses. It now takes its colours, type scale, code font and spacing from the active Scalar theme. Its log entries show their direction marker again (green for received, blue for published, red for system notes); a more specific rule had made every marker transparent.
