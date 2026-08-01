# Bielu.Overlay.Cli

`dotnet overlay` — apply and validate
[OpenAPI Overlays](https://spec.openapis.org/overlay/latest.html) against **any** JSON or YAML API
description.

## Installation

```sh
dotnet tool install --global Bielu.Overlay.Cli
```

## Commands

```sh
# Apply overlays in declaration order
dotnet overlay apply --file asyncapi.json --overlay public.yaml --output public.json --strict

# Validate the overlay documents themselves
dotnet overlay validate --file "overlays/*.yaml" --format json
```

The tool is deliberately document-type-agnostic: the target can be an OpenAPI description, an
AsyncAPI document, an Arazzo workflow, or any other JSON/YAML file. Output format follows
`--format`, else the `--output` extension, else JSON so the command pipes. Nothing is written when
application reports errors, so a partially transformed document is never left behind for a later
build step.

## Documentation

- [Overlay CLI](https://apidescriptions.bielu.pl/articles/overlay/cli.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Released under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are
welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
