namespace Bielu.Overlay.Tests;

/// <summary>Locates the vendored OAI conformance fixtures copied next to the test assembly.</summary>
internal static class ConformancePaths
{
    public static string Root { get; } = Path.Combine(AppContext.BaseDirectory, "Conformance");

    public static string CompliantSets { get; } = Path.Combine(Root, "compliant-sets");

    public static string Documents { get; } = Path.Combine(Root, "documents");
}
