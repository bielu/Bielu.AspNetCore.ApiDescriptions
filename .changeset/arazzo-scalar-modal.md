---
"@bielu/scalar-arazzo": patch
"bielu-aspnetcore-asyncapi": patch
---

The expanded Arazzo workflow overview opens in Scalar's own `ScalarModal` (from `@scalar/components` 0.35.1) instead of a native `<dialog>`, so it uses Scalar's modal frame, backdrop and fade-in. Escape, the close button and a click on the backdrop close it, and focus returns to the Expand button. The modal and Headless UI add about 175 KB to the minified plugin bundle (416 KB → 592 KB). The Data flow view's "What flows" dialog, opened from the expanded overview, moves into the modal while it is open, so clicking in it or pressing Escape closes only that dialog.
