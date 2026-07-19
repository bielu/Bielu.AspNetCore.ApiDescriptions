namespace Bielu.AspNetCore.AsyncApi.Scalar.Grpc;

/// <summary>
/// Shared defaults for the Scalar gRPC console integration.
/// </summary>
public static class ScalarGrpcDefaults
{
    /// <summary>
    /// The default base path under which the gRPC-enabled Scalar bundle (and the protobuf
    /// descriptor endpoint) is served.
    /// </summary>
    public const string AssetsPath = "/bielu/scalar/grpc";
}
