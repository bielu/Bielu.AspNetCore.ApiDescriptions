---
"@bielu/scalar-arazzo": minor
"bielu-aspnetcore-asyncapi": minor
---

The Arazzo workflow overview in Scalar has a Pages view for readers who follow a journey screen by screen. With the new `pagesExtension`, `pageExtension` and `labelExtension` options (`ScalarArazzoOptions.PagesExtension`, `PageExtension` and `LabelExtension` in `WithArazzoOptions`), a document that describes its pages and labels its steps and actions gets a "Technical | Pages" toggle: steps are grouped into page bands, boxes and arrows read in plain language, and end nodes are named after the page they show when every arrow into them lands there; otherwise each arrow names its own page, so no outcome borrows another's destination. Page bands are centered on their steps and the layout keeps room for their titles, so a long route stays inside its workflow's box and clear of neighbouring branches; titles past 64 characters are cut, in full in the tooltip. Technical stays the default, and the choice is remembered in the browser.
