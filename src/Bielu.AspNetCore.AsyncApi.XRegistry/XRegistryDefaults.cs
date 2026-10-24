namespace Bielu.AspNetCore.AsyncApi.XRegistry;

/// <summary>Default values for the xRegistry.</summary>
public static class XRegistryDefaults
{
    /// <summary>The version of the xRegistry specification the registry implements.</summary>
    public const string SpecVersion = "1.0-rc4";

    /// <summary>The route the registry is served from unless <c>MapXRegistry()</c> is given another one.</summary>
    public const string DefaultRoute = "/registry";

    /// <summary>The well-known URI xRegistry defines for host-based discovery.</summary>
    public const string WellKnownRoute = "/.well-known/xregistry";
}
