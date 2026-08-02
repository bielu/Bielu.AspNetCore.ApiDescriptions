# @bielu/scalar-signalr

## 1.0.0

### Major Changes

- [#61](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/61) [`7bd7638`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/7bd763882527e7ed7d7d297217e90e75230bde5a) Thanks [@bielu](https://github.com/bielu)! - release the SignalR and gRPC console bundles as stable `1.0.0`, aligned with the NuGet suite version.

  This is a release-sequencing fix, not a code change. The Aspire packages embed an exact, immutable
  jsDelivr pin built from the npm `package.json` version at compile time
  (`ScalarPluginBundleVersion.targets`), and since the pluginUrls migration that pin points at
  `dist/scalar-plugin.mjs`:

  ```text
  https://cdn.jsdelivr.net/npm/@bielu/scalar-signalr@<version>/dist/scalar-plugin.mjs
  ```

  No changeset targeted either npm package, so exiting prerelease mode would have left both at `0.1.0`
  — a version published _before_ the pluginUrls migration, whose tarball contains no
  `scalar-plugin.mjs` at all (`@bielu/scalar-signalr@0.1.0` has no `standalone.js` either). The stable
  NuGet packages would have shipped a CDN URL that 404s, and both Aspire consoles would have rendered
  blank. Because `DefaultPluginUrl` is a `public const string`, consumers inline it at their own
  compile time, so this could not have been corrected by a later patch release — only by a new pin in a
  new version.

  `1.0.0` rather than a `0.x` bump so the console bundle version matches the suite it ships with: the
  CDN URL reads `@bielu/scalar-signalr@1.0.0` next to `Bielu.AspNetCore.AsyncApi.Scalar.SignalR`
  `1.0.0`. The npm packages keep their own version line afterwards — an npm-only fix goes to `1.0.1`
  without moving NuGet.
