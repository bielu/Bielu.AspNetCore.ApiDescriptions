namespace Bielu.Arazzo.Validation;

/// <summary>A validation finding, located by a JSON-Pointer-style path from the document root.</summary>
public sealed record ArazzoError(string Path, string Message, bool IsWarning = false)
{
    public override string ToString() => $"{(IsWarning ? "warning" : "error")} at {Path}: {Message}";
}
