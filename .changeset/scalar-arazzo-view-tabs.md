---
"@bielu/scalar-arazzo": patch
"bielu-aspnetcore-asyncapi": patch
---

The Arazzo workflow overview in Scalar offers its views (Technical, Data flow, Sequence and Pages) as tabs on a row of their own, under the overview's title and its export and expand buttons, sharing the overview's width. With four views, the buttons squeezed between the title and the actions had wrapped both the title and "Data flow" onto two lines. In the full-window view the tabs sit on a bar under the title with the legend on its right, rather than among the actions. A tab's focus ring is drawn inside it, so the strip, which scrolls sideways when too narrow, no longer clips it. The tabs follow the WAI-ARIA tabs pattern: screen readers announce them as tabs of the panel they show, only the selected tab is in the Tab order, and the Left and Right arrow keys, Home and End move between them.
