using Bielu.Arazzo.Validation;

namespace Bielu.AspNetCore.Arazzo.Services;

/// <summary>Thrown when an Arazzo document fails <see cref="ArazzoValidator"/>'s structural validation while being built.</summary>
public sealed class ArazzoDocumentValidationException : Exception
{
    internal ArazzoDocumentValidationException(string documentName, IReadOnlyList<ArazzoError> errors)
        : base(BuildMessage(documentName, errors))
    {
        DocumentName = documentName;
        Errors = errors;
    }

    /// <summary>The name of the document that failed validation.</summary>
    public string DocumentName { get; }

    /// <summary>The structural validation errors found in the document.</summary>
    public IReadOnlyList<ArazzoError> Errors { get; }

    private static string BuildMessage(string documentName, IReadOnlyList<ArazzoError> errors) =>
        $"Arazzo document '{documentName}' failed validation:{Environment.NewLine}" +
        string.Join(Environment.NewLine, errors.Select(e => $"  - {e}"));
}
