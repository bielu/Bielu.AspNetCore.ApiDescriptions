# @bielu/scalar-arazzo

## 1.0.0

### Major Changes

- [#233](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/233) [`c87003f`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/c87003f3c81188a8844b714f5922a102fd699f07) Thanks [@bielu](https://github.com/bielu)! - Release the Arazzo plugin for Scalar (`@bielu/scalar-arazzo`) as stable `1.0.0`. It shows every field of Arazzo 1.0 and 1.1, which spec each step targets, links from each step to its operation or channel, AsyncAPI message payloads, collapsible steps, a workflow and step filter, and works from the keyboard and at phone width. From 1.0.0 it follows semantic versioning; the rendered markup and CSS classes are not part of its public surface.

### Patch Changes

- [#238](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/238) [`31d3554`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/31d3554b3488deb500586c7aa17ae91dfb6389d7) Thanks [@bielu](https://github.com/bielu)! - In the Arazzo view in Scalar, an AsyncAPI step now shows the message it sends or receives: the payload as a property list (references followed into the AsyncAPI document), its content type and its first example payload. Payloads in other schema formats, such as Avro, are shown as written.

- [#236](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/236) [`1a6eacd`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/1a6eacda5eb4bcae806ebcffc310cbceb44e8dd2) Thanks [@bielu](https://github.com/bielu)! - Steps in the Arazzo view in Scalar can be collapsed, one at a time with the chevron beside each heading or all at once with the workflow's "Collapse steps" button. A collapsed step keeps a one-line summary of its target, and clicking a step in the overview opens it.

- [#246](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/246) [`fa06814`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/fa068148930a7028715808b297bc71a589a7d548) Thanks [@bielu](https://github.com/bielu)! - Polish the Arazzo view in Scalar with prominent operation and channel targets, compact spec labels, payload properties on the page surface, and consistent disclosure controls. Source versions remain visible in Sources and target tooltips.
