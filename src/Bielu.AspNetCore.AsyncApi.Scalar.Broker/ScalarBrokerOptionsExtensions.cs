using Scalar.AspNetCore;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Broker;

/// <summary>
/// Extension methods that enable the interactive broker console on a Scalar API Reference.
/// </summary>
public static class ScalarBrokerOptionsExtensions
{
    /// <summary>
    /// The window global the explicit document configuration is assigned to for the broker bundle.
    /// </summary>
    internal const string GlobalVariableName = "__BIELU_SCALAR_BROKER__";

    /// <summary>
    /// Enables the broker console on this Scalar API Reference.
    /// </summary>
    /// <remarks>
    /// This adds a small script (served by <c>MapScalarBrokerAssets</c>) to the page via
    /// <see cref="ScalarOptions.HeadContent" />. The script registers the broker console plugin with
    /// Scalar's own bundle — it does not replace it, so Scalar styles itself normally. The console
    /// discovers its AsyncAPI documents automatically from the Scalar configuration's sources, so no
    /// documents need to be declared here; provide <paramref name="configure" /> only to override
    /// that discovery with an explicit document list. Call <c>MapScalarBrokerAssets</c> on the
    /// endpoint route builder so the script and the proxy endpoints it publishes and tails through
    /// are reachable at <paramref name="assetsPath" />.
    /// </remarks>
    /// <param name="options">The Scalar options being configured.</param>
    /// <param name="configure">Optional callback to override the auto-discovered AsyncAPI documents.</param>
    /// <param name="assetsPath">The base path the broker script is served from. Defaults to <see cref="ScalarBrokerDefaults.AssetsPath" />.</param>
    /// <returns>The same <see cref="ScalarOptions" /> instance for chaining.</returns>
    public static ScalarOptions WithBrokerClient(
        this ScalarOptions options,
        Action<ScalarBrokerOptions>? configure = null,
        string assetsPath = ScalarBrokerDefaults.AssetsPath)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(assetsPath);

        ScalarBrokerOptions? broker = null;
        if (configure is not null)
        {
            broker = new ScalarBrokerOptions();
            configure(broker);
        }

        return options.WithAsyncApiPluginScript(assetsPath, GlobalVariableName, broker?.Documents);
    }
}
