---
"bielu-aspnetcore-asyncapi": patch
---

The Scalar console and Arazzo bundles (`plugin.js`) are now served with an `ETag` derived from their content. Browsers still revalidate on every page load, but get a `304 Not Modified` instead of downloading the bundle again until it changes.
