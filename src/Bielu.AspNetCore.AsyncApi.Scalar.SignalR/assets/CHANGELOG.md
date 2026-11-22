# @bielu/scalar-signalr

## 1.0.2

### Patch Changes

- [#318](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/318) [`1fdeb93`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/1fdeb933bff6de64c1ed74fd573973e79fc2b9cf) Thanks [@bielu](https://github.com/bielu)! - The plugins' Vue components are formatted with Scalar's own Prettier setup, including `prettier-plugin-tailwindcss`'s class order. Nothing changes on the page.

- [#306](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/306) [`1de811a`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/1de811aa3f19c62c3b5d2f95e2dc65a6de9e3f44) Thanks [@bielu](https://github.com/bielu)! - The broker console's log entries show their direction marker again: a green bar for received messages, blue for published and red for system notes. A more specific rule had made every marker transparent. The markers, the broker console's error text and the SignalR console's "connected" pill now use Scalar's `--scalar-color-*` variables instead of fixed hex colours, so they follow the active Scalar theme and dark mode.

- [#317](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/317) [`34d4a41`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/34d4a41d4d343590ae048777e2fa8e73c33f7496) Thanks [@bielu](https://github.com/bielu)! - The broker, gRPC and SignalR consoles now use Scalar's own `ScalarButton`, `ScalarTextInput` and `ScalarTextArea` (from `@scalar/components` 0.35.1, the version `@scalar/api-reference` 1.80.0 ships), so their buttons, text fields and JSON editors look and behave like the ones in Scalar's API client. The editors grow with their content instead of showing a resize handle. Selects stay native, drawn to match the text fields. The broker console's controls, unstyled until now, get the same look. The components and their styles add about 60 KB to each minified console bundle (the broker's grows from 96 KB to 158 KB).

- [#310](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/310) [`62605d2`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/62605d2c5acf7c9fd84df87674e62a6716588000) Thanks [@bielu](https://github.com/bielu)! - The SignalR console in Scalar is styled with Tailwind CSS on Scalar's own theme, like the broker console, so its colours, type scale and spacing come from the active Scalar theme. Sample payloads under "Events" get the grey left border they were meant to have; a later rule had made them green, like a successful call's result. The cards, inputs, buttons, method tags, pills, results and message log it shares with the gRPC console are now `bielu-*` utilities in `@bielu/scalar-core`.

## 1.0.1

### Patch Changes

- [#179](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/179) [`280cd7e`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/280cd7e4db64c6d6da0b1382f89703378e232fb0) Thanks [@bielu](https://github.com/bielu)! - Write custom protocol bindings as `x-` extensions, so generated AsyncAPI 3 documents validate against the AsyncAPI JSON Schema. AsyncAPI 3 bindings objects only allow the specification's binding names (`amqp`, `http`, `kafka`, `ws`, ...) and `x-` extensions, so the `signalr`, `grpc`, `webrtc` and `sse` bindings are now written under `x-signalr`, `x-grpc`, `x-webrtc` and `x-sse`. The new `SignalRProtocol.BindingKey`, `GrpcProtocol.BindingKey`, `WebRtcProtocol.BindingKey` and `SseProtocol.BindingKey` constants hold the key; `ProtocolName` is unchanged and is still a server's `protocol`.

  **Changes the generated document:** a consumer that reads `bindings.signalr` (or `grpc`, `webrtc`, `sse`) needs to read `bindings["x-signalr"]` instead. The Scalar SignalR and gRPC consoles read either key, the bare name first, so they work with documents from before and after this change, and the MCP operation search reports `x-signalr` as the `signalr` protocol.

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
