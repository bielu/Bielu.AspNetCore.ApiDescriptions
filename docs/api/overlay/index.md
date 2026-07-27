# API Documentation

Welcome to the Bielu.Overlay.NET API documentation. This section contains the technical reference for
the OpenAPI Overlay spec library.

## Main Components

- **Bielu.Overlay.NET**: The object model, validator, and apply engine for the
  [Overlay Specification](https://spec.openapis.org/overlay/latest.html) (1.0.0 and 1.1.0). Operates on
  `System.Text.Json.Nodes.JsonNode`, so overlays apply to any JSON/YAML API description. Framework-free.
- **Bielu.Overlay.NET.Readers**: JSON and YAML readers that parse Overlay documents into the model above,
  with diagnostics rather than exceptions.
- **Bielu.Spec.Shared**: Document-parsing primitives shared by the spec libraries — currently the
  YAML-to-`JsonNode` conversion that lets one deserializer serve both source formats.

Use the left navigation to explore the namespaces and classes.
