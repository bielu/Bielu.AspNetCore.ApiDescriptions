namespace Bielu.AspNetCore.XRegistry;

/// <summary>Options for the xRegistry served by <c>MapXRegistry()</c>.</summary>
/// <example>
/// <code>
/// builder.Services.AddXRegistry(options => options
///     .WithRegistry("orders", "Orders registry")
///     .IncludeDocuments("v1"));
/// </code>
/// </example>
public sealed class XRegistryOptions
{
    private readonly HashSet<string> _documents = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The <c>registryid</c> of the registry. Defaults to <c>asyncapi</c>.</summary>
    public string RegistryId { get; set; } = "asyncapi";

    /// <summary>The human-readable <c>name</c> of the registry, if any.</summary>
    public string? Name { get; set; }

    /// <summary>The <c>description</c> of the registry, if any.</summary>
    public string? Description { get; set; }

    /// <summary>Sets <see cref="RegistryId"/> and, optionally, <see cref="Name"/>.</summary>
    /// <param name="registryId">The <c>registryid</c>.</param>
    /// <param name="name">The human-readable name.</param>
    /// <returns>The options, for chaining.</returns>
    public XRegistryOptions WithRegistry(string registryId, string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registryId);

        RegistryId = registryId;
        Name = name ?? Name;
        return this;
    }

    /// <summary>
    /// Limits the registry to these AsyncAPI documents. Every AsyncAPI document of the app is included when none is named.
    /// </summary>
    /// <param name="documentNames">The AsyncAPI document names, as given to <c>AddAsyncApi</c>.</param>
    /// <returns>The options, for chaining.</returns>
    public XRegistryOptions IncludeDocuments(params string[] documentNames)
    {
        ArgumentNullException.ThrowIfNull(documentNames);
        foreach (var documentName in documentNames)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(documentName);
            _documents.Add(documentName);
        }

        return this;
    }

    internal bool IsIncluded(string documentName) => _documents.Count == 0 || _documents.Contains(documentName);
}
