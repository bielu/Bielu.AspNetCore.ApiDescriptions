using Scalar.AspNetCore;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Grpc;

/// <summary>
/// Extension methods that enable the interactive gRPC console on a Scalar API Reference.
/// </summary>
public static class ScalarGrpcOptionsExtensions
{
    /// <summary>
    /// The window global the explicit document configuration is assigned to for the gRPC bundle.
    /// </summary>
    internal const string GlobalVariableName = "__BIELU_SCALAR_GRPC__";

    /// <summary>
    /// Enables the gRPC console on this Scalar API Reference.
    /// </summary>
    /// <remarks>
    /// This adds a small script (served by <c>MapScalarGrpcAssets</c>) to the page via
    /// <see cref="ScalarOptions.HeadContent" />. The script registers the gRPC console plugin with
    /// Scalar's own bundle — it does not replace it, so Scalar styles itself normally. The console
    /// discovers its AsyncAPI documents automatically from the Scalar configuration's sources, so no
    /// documents need to be declared here; provide <paramref name="configure" /> only to override
    /// that discovery with an explicit document list. Call <c>MapScalarGrpcAssets</c> on the
    /// endpoint route builder so the script (and the protobuf descriptor endpoint the console needs
    /// to encode wire messages) is reachable at <paramref name="assetsPath" />. The console invokes
    /// methods over gRPC-Web, so the target app must also enable <c>UseGrpcWeb</c>.
    /// </remarks>
    /// <param name="options">The Scalar options being configured.</param>
    /// <param name="configure">Optional callback to override the auto-discovered AsyncAPI documents.</param>
    /// <param name="assetsPath">The base path the gRPC script is served from. Defaults to <see cref="ScalarGrpcDefaults.AssetsPath" />.</param>
    /// <returns>The same <see cref="ScalarOptions" /> instance for chaining.</returns>
    public static ScalarOptions WithGrpcClient(
        this ScalarOptions options,
        Action<ScalarGrpcOptions>? configure = null,
        string assetsPath = ScalarGrpcDefaults.AssetsPath)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(assetsPath);

        ScalarGrpcOptions? grpc = null;
        if (configure is not null)
        {
            grpc = new ScalarGrpcOptions();
            configure(grpc);
        }

        return options.WithAsyncApiPluginScript(assetsPath, GlobalVariableName, grpc?.Documents);
    }
}
