---
"@bielu/scalar-arazzo": patch
"bielu-aspnetcore-asyncapi": patch
---

The Arazzo plugin trims a sentence-ending `.` or `#` from a Runtime Expression in linear time. It used `/[.#]+$/`, which took quadratic time on an expression holding a long run of them (about 4 s for 100,000), as CodeQL's `js/polynomial-redos` flagged.
