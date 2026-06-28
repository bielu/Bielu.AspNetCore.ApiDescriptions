using System.Text.Json;
using Scalar.AspNetCore;

namespace Bielu.AspNetCore.AsyncApi.Scalar.SignalR;

/// <summary>
/// Extension methods that enable the interactive SignalR console on a Scalar API Reference.
/// </summary>
public static class ScalarSignalROptionsExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Enables the SignalR console on this Scalar API Reference.
    /// </summary>
    /// <remarks>
    /// This adds a small script (served by <c>MapScalarSignalRAssets</c>) to the page via
    /// <see cref="ScalarOptions.HeadContent" />. The script registers the SignalR console plugin with
    /// Scalar's own bundle — it does not replace it, so Scalar styles itself normally. The console
    /// discovers its AsyncAPI documents automatically from the Scalar configuration's sources, so no
    /// documents need to be declared here; provide <paramref name="configure" /> only to override
    /// that discovery with an explicit document list. Call <c>MapScalarSignalRAssets</c> on the
    /// endpoint route builder so the script is reachable at <paramref name="assetsPath" />.
    /// </remarks>
    /// <param name="options">The Scalar options being configured.</param>
    /// <param name="configure">Optional callback to override the auto-discovered AsyncAPI documents.</param>
    /// <param name="assetsPath">The base path the SignalR script is served from. Defaults to <see cref="ScalarSignalRDefaults.AssetsPath" />.</param>
    /// <returns>The same <see cref="ScalarOptions" /> instance for chaining.</returns>
    public static ScalarOptions WithSignalRClient(
        this ScalarOptions options,
        Action<ScalarSignalROptions>? configure = null,
        string assetsPath = ScalarSignalRDefaults.AssetsPath)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(assetsPath);

        var scriptTag = $"<script src=\"{assetsPath.TrimEnd('/')}/plugin.js\"></script>";

        var configScript = string.Empty;
        if (configure is not null)
        {
            var signalR = new ScalarSignalROptions();
            configure(signalR);

            if (signalR.Documents.Count > 0)
            {
                var payload = new
                {
                    documents = signalR.Documents.Select(static document => new
                    {
                        name = document.Name,
                        url = document.Url,
                    }),
                };
                var json = JsonSerializer.Serialize(payload, JsonOptions);
                configScript = $"<script>window.__BIELU_SCALAR_SIGNALR__ = {json};</script>";
            }
        }

        // The console script must run before Scalar's bundle so it can hook `window.Scalar`; placing
        // it in HeadContent (the document head) guarantees that ordering.
        options.HeadContent = (options.HeadContent ?? string.Empty) + configScript + scriptTag;

        return options;
    }
}
