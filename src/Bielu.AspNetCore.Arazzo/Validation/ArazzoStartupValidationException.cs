namespace Bielu.AspNetCore.Arazzo.Validation;

/// <summary>
/// Thrown at app startup when one or more workflow steps' <c>operationId</c>/<c>operationPath</c>/<c>channelPath</c>
/// could not be resolved against the live, self-wired AsyncAPI/OpenAPI documents — a renamed channel or
/// operation fails startup instead of failing in production. Disable via
/// <see cref="Bielu.AspNetCore.Arazzo.Services.ArazzoOptions.ValidateSourceReferencesOnStartup"/>.
/// </summary>
public sealed class ArazzoStartupValidationException : Exception
{
    internal ArazzoStartupValidationException(IReadOnlyList<string> errors)
        : base(BuildMessage(errors))
    {
        Errors = errors;
    }

    /// <summary>The unresolved step references found across every self-wired Arazzo document.</summary>
    public IReadOnlyList<string> Errors { get; }

    private static string BuildMessage(IReadOnlyList<string> errors) =>
        $"Arazzo cross-spec reference validation failed:{Environment.NewLine}" +
        string.Join(Environment.NewLine, errors.Select(e => $"  - {e}"));
}
