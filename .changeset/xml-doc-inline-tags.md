---
"bielu-aspnetcore-asyncapi": patch
---

Render XML documentation comments as readable text. `<see cref>`, `<see langword>`, `<see href>`, `<paramref>` and `<typeparamref>` were dropped from summaries, remarks and parameter descriptions (a summary ending "arrives as <see cref="CaseUpdated"/>." became "arrives as ."); they now render as the referenced name, keyword, link or parameter. The line breaks and indentation of multi-line comments no longer leak into descriptions: lines join with single spaces, a blank line or `<para>` starts a new paragraph, `<br/>` a new line, each list `<item>` a "- " line, and `<code>` is kept verbatim.
