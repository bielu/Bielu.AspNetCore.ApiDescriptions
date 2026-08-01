# Bielu.Spec.Shared

Shared document-parsing primitives for the Bielu API-description spec libraries: conversion between
YAML and `System.Text.Json.Nodes.JsonNode` in both directions.

Extracted so `Bielu.Arazzo.NET.Readers` and `Bielu.Overlay.NET.Readers` share one implementation
rather than two that drift. The subtleties are real — plain-scalar type inference, empty scalars
meaning null, and quoting on the way back out so a value like an Arazzo `channelPath` does not
re-parse as a YAML flow mapping.

This is an implementation detail of those packages, published because they depend on it.

## Documentation

- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Released under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are
welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
