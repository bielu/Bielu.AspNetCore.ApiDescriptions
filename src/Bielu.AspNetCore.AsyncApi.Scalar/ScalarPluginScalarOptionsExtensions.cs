using System.Text.Json;
using Scalar.AspNetCore;

namespace Bielu.AspNetCore.AsyncApi.Scalar;

/// <summary>
/// Injects a console plugin script (and its optional document configuration) into a Scalar API
/// Reference page — the shared half of each protocol package's <c>With*Client</c> method.
/// </summary>
public static class ScalarPluginScalarOptionsExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Adds the plugin bundle script (served from <c>{assetsPath}/plugin.js</c>) to the page via
    /// <see cref="ScalarOptions.HeadContent" />. When <paramref name="documents" /> is non-empty, a
    /// preceding inline script assigns them to <c>window.{globalVariableName}</c> so the bundle can
    /// pick them up as an explicit override of its document auto-discovery.
    /// </summary>
    /// <remarks>
    /// The console script must run before Scalar's bundle so it can hook <c>window.Scalar</c>;
    /// placing it in <see cref="ScalarOptions.HeadContent" /> (the document head) guarantees that
    /// ordering.
    /// </remarks>
    /// <param name="options">The Scalar options being configured.</param>
    /// <param name="assetsPath">The base path the plugin script is served from.</param>
    /// <param name="globalVariableName">The window global the document override is assigned to (e.g. <c>__BIELU_SCALAR_SIGNALR__</c>).</param>
    /// <param name="documents">Optional explicit AsyncAPI documents overriding the bundle's auto-discovery.</param>
    /// <returns>The same <see cref="ScalarOptions" /> instance for chaining.</returns>
    public static ScalarOptions WithAsyncApiPluginScript(
        this ScalarOptions options,
        string assetsPath,
        string globalVariableName,
        IEnumerable<ScalarAsyncApiDocument>? documents = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(assetsPath);
        ArgumentException.ThrowIfNullOrEmpty(globalVariableName);

        var scriptTag = $"<script src=\"{assetsPath.TrimEnd('/')}/plugin.js\"></script>";

        var configScript = string.Empty;
        var documentList = documents?.ToArray() ?? [];
        if (documentList.Length > 0)
        {
            var payload = new
            {
                documents = documentList.Select(static document => new
                {
                    name = document.Name,
                    url = document.Url,
                }),
            };
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            configScript = $"<script>window.{globalVariableName} = {json};</script>";
        }

        options.HeadContent = (options.HeadContent ?? string.Empty) + configScript + scriptTag;

        return options;
    }
}
