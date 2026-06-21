namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Grpc;

/// <summary>
/// Well-known constants for the custom <c>grpc</c> AsyncAPI protocol.
/// </summary>
/// <remarks>
/// gRPC is not part of the official AsyncAPI bindings catalogue, so these values define a
/// project-specific binding shape. They are intentionally aligned with the concepts exposed by
/// Protocol Buffers and gRPC (services, RPC methods, the four streaming method kinds and protobuf
/// message framing).
/// </remarks>
public static class GrpcProtocol
{
    /// <summary>The protocol identifier used in server and binding keys.</summary>
    public const string ProtocolName = "grpc";

    /// <summary>The default binding version emitted when none is set explicitly.</summary>
    public const string DefaultBindingVersion = "0.1.0";

    /// <summary>The kinds of RPC a gRPC method can be (see <see cref="GrpcMethodType"/>).</summary>
    public static class MethodTypes
    {
        public const string Unary = "unary";
        public const string ServerStreaming = "serverStreaming";
        public const string ClientStreaming = "clientStreaming";
        public const string BidirectionalStreaming = "bidirectionalStreaming";
    }

    /// <summary>The idempotency levels declared on a gRPC method (mirrors protobuf <c>idempotency_level</c>).</summary>
    public static class IdempotencyLevels
    {
        public const string IdempotencyUnknown = "idempotencyUnknown";
        public const string NoSideEffects = "noSideEffects";
        public const string Idempotent = "idempotent";
    }

    /// <summary>How a gRPC message payload is encoded on the wire.</summary>
    public static class MessageEncodings
    {
        public const string Protobuf = "protobuf";
        public const string Json = "json";
    }

    /// <summary>Compression algorithms a gRPC endpoint can negotiate.</summary>
    public static class Compressions
    {
        public const string Gzip = "gzip";
        public const string Deflate = "deflate";
        public const string Identity = "identity";
    }
}

/// <summary>
/// The four kinds of RPC defined by gRPC, distinguished by which side(s) stream messages.
/// See https://grpc.io/docs/what-is-grpc/core-concepts/.
/// </summary>
public enum GrpcMethodType
{
    /// <summary>A single request and a single response.</summary>
    Unary,

    /// <summary>A single request and a stream of responses.</summary>
    ServerStreaming,

    /// <summary>A stream of requests and a single response.</summary>
    ClientStreaming,

    /// <summary>A stream of requests and a stream of responses.</summary>
    BidirectionalStreaming,
}

/// <summary>Serialization helpers mapping <see cref="GrpcMethodType"/> to/from its wire token.</summary>
public static class GrpcMethodTypeExtensions
{
    /// <summary>
    /// Returns the camelCase token used in the binding document, for example
    /// <see cref="GrpcMethodType.ServerStreaming"/> becomes <c>"serverStreaming"</c>.
    /// </summary>
    public static string ToWireName(this GrpcMethodType type)
    {
        var name = type.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>
    /// Parses a method type from its camelCase token (for example <c>"unary"</c>). The match is
    /// case-insensitive. Returns <see langword="null"/> when the value is missing or unrecognized.
    /// </summary>
    public static GrpcMethodType? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<GrpcMethodType>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : null;
    }
}
