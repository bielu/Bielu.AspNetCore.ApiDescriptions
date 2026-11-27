# @bielu/scalar-likec4

## 0.1.0

### Minor Changes

- [#370](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/370) [`ee1d162`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/ee1d1625146aa7872266e1ee71ddfaa673f0d422) Thanks [@bielu](https://github.com/bielu)! - New npm package `@bielu/scalar-likec4`: a LikeC4 document type for the Scalar API Reference (1.80.0 or later). Load the JSON `likec4 export json` writes as a Scalar source and each view of the architecture model gets a sidebar entry and a section, drawn in Scalar's theme from the layout LikeC4 already computed: element shapes and colours, containers around their children, relationships solid, dashed or dotted with their labels. Dynamic views number their steps; with `variant sequence` they are drawn as sequence diagrams (lanes, lifelines, activation bars, self-steps as loops), and their steps are listed with their notes. An element or step with `navigateTo` links to the view it opens. A model that is not laid out gets a note on how to export it. Ships the package root (`createLikeC4Plugin`), a `<script>` bundle (`dist/plugin.js`) and a `pluginUrls` module (`dist/scalar-plugin.mjs`).
