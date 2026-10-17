# @bielu/scalar-broker

## 1.0.1

### Patch Changes

- [#120](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/120) [`ce37962`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/ce37962c2a516d93ac10c5944f0b92cadc90b587) Thanks [@bielu](https://github.com/bielu)! - The broker console no longer offers a connection whose protocol doesn't match the selected channel. It used to fall back to every registered connection, so with only the Kafka driver installed, an `mqtt` or `amqp` channel was offered the Kafka connection, and publishing would write to a Kafka topic named after the MQTT topic or AMQP queue. Such a channel now shows that the server has no connection for its protocol. The README, docs and package readmes now say Kafka is the only driver that ships today.
