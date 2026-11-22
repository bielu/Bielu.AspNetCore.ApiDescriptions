# @bielu/scalar-broker

## 1.0.2

### Patch Changes

- [#318](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/318) [`1fdeb93`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/1fdeb933bff6de64c1ed74fd573973e79fc2b9cf) Thanks [@bielu](https://github.com/bielu)! - The plugins' Vue components are formatted with Scalar's own Prettier setup, including `prettier-plugin-tailwindcss`'s class order. Nothing changes on the page.

- [#307](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/307) [`9dad1f1`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/9dad1f13ebb73ff36770f996dd4e41f7a7b7e27c) Thanks [@bielu](https://github.com/bielu)! - The broker console in Scalar is styled with Tailwind CSS on Scalar's own theme (`@scalar/themes`), the stack Scalar's own UI uses. It now takes its colours, type scale, code font and spacing from the active Scalar theme.

- [#306](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/306) [`1de811a`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/1de811aa3f19c62c3b5d2f95e2dc65a6de9e3f44) Thanks [@bielu](https://github.com/bielu)! - The broker console's log entries show their direction marker again: a green bar for received messages, blue for published and red for system notes. A more specific rule had made every marker transparent. The markers, the broker console's error text and the SignalR console's "connected" pill now use Scalar's `--scalar-color-*` variables instead of fixed hex colours, so they follow the active Scalar theme and dark mode.

- [#317](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/317) [`34d4a41`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/34d4a41d4d343590ae048777e2fa8e73c33f7496) Thanks [@bielu](https://github.com/bielu)! - The broker, gRPC and SignalR consoles now use Scalar's own `ScalarButton`, `ScalarTextInput` and `ScalarTextArea` (from `@scalar/components` 0.35.1, the version `@scalar/api-reference` 1.80.0 ships), so their buttons, text fields and JSON editors look and behave like the ones in Scalar's API client. The editors grow with their content instead of showing a resize handle. Selects stay native, drawn to match the text fields. The broker console's controls, unstyled until now, get the same look. The components and their styles add about 60 KB to each minified console bundle (the broker's grows from 96 KB to 158 KB).

## 1.0.1

### Patch Changes

- [#120](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/120) [`ce37962`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/ce37962c2a516d93ac10c5944f0b92cadc90b587) Thanks [@bielu](https://github.com/bielu)! - The broker console no longer offers a connection whose protocol doesn't match the selected channel. It used to fall back to every registered connection, so with only the Kafka driver installed, an `mqtt` or `amqp` channel was offered the Kafka connection, and publishing would write to a Kafka topic named after the MQTT topic or AMQP queue. Such a channel now shows that the server has no connection for its protocol. The README, docs and package readmes now say Kafka is the only driver that ships today.
