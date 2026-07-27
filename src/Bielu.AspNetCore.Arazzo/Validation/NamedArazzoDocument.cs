namespace Bielu.AspNetCore.Arazzo.Validation;

/// <summary>
/// A plain marker registered once per <c>AddArazzo</c> call so the document names known to the app can be
/// enumerated later — keyed services themselves can't be enumerated
/// (see https://github.com/dotnet/runtime/issues/100105).
/// </summary>
internal sealed record NamedArazzoDocument(string DocumentName);
