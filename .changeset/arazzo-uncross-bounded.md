---
"@bielu/scalar-arazzo": patch
"bielu-aspnetcore-asyncapi": patch
---

Ordering a dense diagram's rows to uncross its arrows no longer stalls the page. Each attempted swap of two neighbouring boxes recounted every pair of arrows in the diagram; it now scores only the arrows of the two boxes it swaps, and the sweeps stop once a pass down and back up ends where it started. Two fully connected rows of 35 boxes took about 6 to 10 seconds and now take a few milliseconds, with the same result as before for every diagram.
