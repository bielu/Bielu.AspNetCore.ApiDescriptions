---
"@bielu/scalar-arazzo": minor
"bielu-aspnetcore-asyncapi": minor
---

The Arazzo workflow overview in Scalar is easier to read. An end node (Success, Failed or a named end) is drawn once per step that leads to it, right under that step, so steps side by side that share Success and Failed no longer cross each other's arrows; the graph keeps one node per outcome, only the drawing changes. The full-window view takes almost the whole window, and a graph too tall to fit at a readable size is drawn a little larger than its natural size from the top, to be panned, instead of being shrunk until its text can't be read. Page titles in the Pages view are drawn over the arrows, so an arrow into a page passes behind its title rather than through the text, and the arrow on to the next step after a step succeeds no longer repeats its success criteria (`$statusCode == 201`) in the Pages view.
