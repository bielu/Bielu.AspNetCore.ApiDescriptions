# Bielu.AspNetCore.Scalar.Plugins.Shared

Shared internals for the Bielu Scalar document-type plugin packages (`Bielu.AspNetCore.Arazzo.Scalar`,
`Bielu.AspNetCore.Arazzo.Scalar.Aspire`) and the ones that follow them: checking that a plugin option names a
specification extension, and passing the options to the plugin's bundle, as a page global for Scalar.AspNetCore
or on the module URL for Scalar.Aspire.

It references neither Scalar.AspNetCore nor Scalar.Aspire, so both sides share one implementation. This is an
implementation detail of those packages, published because they depend on it: it has no public API.

## Documentation

- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Copyright (c) 2025 Arkadiusz Biel. Released under the [MIT license](https://licenses.nuget.org/MIT).
Bug reports and contributions are welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
