namespace Bielu.AspNetCore.Arazzo;

/// <summary>Default values shared across the Arazzo ASP.NET Core integration.</summary>
public static class ArazzoDefaults
{
    /// <summary>The document name used when none is specified to <c>AddArazzo</c>.</summary>
    public const string DefaultDocumentName = "v1";

    /// <summary>The default route pattern used by <c>MapArazzo</c>.</summary>
    public const string DefaultArazzoRoute = "/arazzo/{documentName}.json";

    /// <summary>The <c>info.version</c> of a document whose options set no version, as for AsyncAPI documents.</summary>
    public const string DefaultInfoVersion = "1.0.0";
}
